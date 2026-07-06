# Event Ledger — Architecture Design

**Task:** T-04 · **Agent:** ARCH-01 · **Stage:** S3 — Architecture  
**Date:** 2026-06-16  
**Requirements:** [requirements.md](../02-requirements/requirements.md)

---

## 1. Executive summary

Event Ledger is a two-service .NET 8 system:

| Service | Role | Port (local/Docker) | Database |
|---------|------|---------------------|----------|
| **Event Gateway** | Public API — ingest, validate, idempotency, audit, forward | `8080` | SQLite `gateway.db` |
| **Account Service** | Internal — apply transactions, balance, account reads | `8081` | SQLite `account.db` |

Communication is synchronous REST. The Gateway calls Account Service via `IHttpClientFactory` with **Polly** (timeout + circuit breaker). **OpenTelemetry** propagates trace context; **Serilog** emits JSON logs with trace ID.

---

## 2. Context

```
┌─────────────┐     HTTPS/HTTP      ┌──────────────────┐    REST (internal)    ┌──────────────────┐
│   Client    │ ──────────────────► │  Event Gateway   │ ────────────────────► │ Account Service  │
│ (upstream)  │                     │  (public)        │                       │ (internal)       │
└─────────────┘                     └────────┬─────────┘                       └────────┬─────────┘
                                             │ SQLite                                      │ SQLite
                                             ▼                                             ▼
                                      gateway.db                                      account.db
```

**Trust boundary:** Only the Gateway is exposed on the host (`8080`). Account Service listens on the Docker internal network (`8081`). External clients do not call Account Service directly in production-like compose setup; integration tests may target both URLs.

---

## 3. Architecture decisions (BA DEC-01..05)

| ID | Decision | Choice | Rationale |
|----|----------|--------|-----------|
| DEC-01 | Same `eventId`, conflicting payload | Return **original** stored event; log warning | Idempotency key is `eventId`; safe replay semantics |
| DEC-02 | DEBIT exceeding balance | **Allow negative balance** | Handout silent; commutative sum avoids ordering bugs; simpler than holds |
| DEC-03 | New `accountId` | **Auto-create** account on first transaction | Reduces client coupling; matches event-driven ingestion |
| DEC-04 | Duplicate HTTP status | **200 OK** + `Idempotency-Replay: true` header | Idempotent POST pattern; body matches first success |
| DEC-05 | Gateway persist timing | **After** Account Service success | Avoids orphan events when downstream fails (sprint risk mitigation) |

---

## 4. Write path — POST /events

```
Client POST /events
    │
    ▼
┌─────────────────┐
│ 1. Validate     │──► 400 ProblemDetails (missing fields, bad type, amount ≤ 0, bad timestamp)
└────────┬────────┘
         ▼
┌─────────────────┐
│ 2. Idempotency  │──► eventId exists? → 200 OK + original EventDto + Idempotency-Replay: true
│    check (DB)   │     (if payload hash differs, log warning, still return original)
└────────┬────────┘
         ▼
┌─────────────────┐
│ 3. Call Account │──► Polly: timeout 5s + circuit breaker
│    POST txn     │     Failure → 503 ProblemDetails (no Gateway persist)
└────────┬────────┘
         ▼
┌─────────────────┐
│ 4. Persist      │──► EventRecord + audit (receivedAt, status=Applied, traceId)
│    Gateway event│
└────────┬────────┘
         ▼
    201 Created + EventDto
```

**Ordering note:** Account transaction is applied **before** Gateway persistence. If step 4 fails after step 3 succeeds, Account remains source of truth; Gateway retry with same `eventId` hits Account idempotency and then completes persist (recovery path).

---

## 5. Read paths

### Gateway (local DB only)

| Endpoint | Behavior |
|----------|----------|
| `GET /events/{eventId}` | Lookup by `eventId` (string PK) |
| `GET /events?account={accountId}` | Filter by account; `ORDER BY eventTimestamp ASC, eventId ASC` |

No Account Service call on read paths — satisfies graceful degradation (FR-14, FR-26).

### Account Service (internal)

| Endpoint | Behavior |
|----------|----------|
| `GET /accounts/{id}/balance` | `SUM(CREDIT) - SUM(DEBIT)` from `Transactions` table |
| `GET /accounts/{id}` | Account metadata + recent transactions (last 20, by `eventTimestamp DESC`) |

Balance is **commutative** — application order does not affect final balance. Listing order is explicit in SQL.

---

## 6. Idempotency

| Layer | Mechanism |
|-------|-----------|
| **Gateway** | Unique index on `Events.EventId`; lookup before downstream call |
| **Account** | Unique index on `Transactions.EventId` (global per event, not per account) |
| **Duplicate response** | HTTP 200, same body, header `Idempotency-Replay: true` |
| **Conflict** | Same `eventId` with different canonical payload → still return original; log `IdempotencyConflict` |

Canonical payload hash: SHA-256 of normalized JSON (`eventId`, `accountId`, `type`, `amount`, `currency`, `eventTimestamp`).

---

## 7. Out-of-order events

- **Balance:** `net = Σ(CREDIT) − Σ(DEBIT)` — order-independent.
- **Event listing:** Sort by `eventTimestamp ASC`, tie-break `eventId ASC`.
- **Account transaction history:** Stored with `eventTimestamp`; display sorted chronologically.

No reordering or replay engine required.

---

## 8. Failure and graceful degradation

| Scenario | Gateway POST /events | Gateway GET /events | Account balance GET |
|----------|---------------------|---------------------|---------------------|
| Account down / circuit open | **503** within timeout | **200** (local data) | **503** (service down) |
| Account slow | Timeout → **503** | Unaffected | Timeout → **503** |
| Gateway down | N/A | N/A | Account still serves internal reads |

**Resiliency — inbound (Gateway entry):**

| Control | Setting |
|---------|---------|
| Concurrency bulkhead | 100 concurrent, queue 50 (global; `/health` exempt) |
| Per-client write rate limit | 300 POST `/events` per IP per minute |
| Rejection | **429 Too Many Requests** + `eventledger.inbound.throttled` metric |

**Polly policy (Gateway → Account, outbound):**

| Policy | Setting |
|--------|---------|
| Circuit breaker | Outermost — open after 5 consecutive failures; break duration 30s |
| Retry | 3 retries (up to 4 HTTP attempts), exponential backoff + jitter (200ms base); safe with idempotency |
| Timeout | 5 seconds per attempt (innermost) |

Wrapped via `Policy.WrapAsync(circuitBreaker, retry, timeout)` as a singleton policy.

**Rationale:** Inbound limits protect the Gateway from client floods; outbound Polly protects from Account Service failure. Retry on outbound POST is safe because `eventId` idempotency prevents double application.

---

## 9. Observability

### Structured logging (Serilog)

JSON to console. Enrichers: `TraceId`, `SpanId`, `ServiceName`, `@t`, `@l`.

Example log shape:

```json
{
  "@t": "2026-06-16T14:00:00Z",
  "@l": "Information",
  "ServiceName": "EventGateway",
  "TraceId": "abc123",
  "Message": "Event applied",
  "EventId": "evt-001",
  "AccountId": "acct-123"
}
```

### Distributed tracing (OpenTelemetry)

- ASP.NET Core instrumentation on both services
- `HttpClient` instrumentation on Gateway with **W3C `traceparent`** propagation
- Export: console (dev); optional OTLP endpoint via env var

### Custom metric

| Metric | Type | Service | Description |
|--------|------|---------|-------------|
| `eventledger.events.processed` | Counter | Gateway | Incremented on successful POST (not on replay) |
| `eventledger.events.failed` | Counter | Gateway | Validation failures + downstream failures |
| `eventledger.inbound.throttled` | Counter | Gateway | Inbound rate/concurrency limit rejections |

Exposed via OpenTelemetry metrics (console or OTLP). README documents how to observe.

### Health checks

`GET /health` on both services:

```json
{
  "status": "Healthy",
  "service": "EventGateway",
  "checks": {
    "database": "Healthy"
  },
  "timestamp": "2026-06-16T14:00:00Z"
}
```

Returns **503** when SQLite unreachable.

---

## 10. Auditing (Gateway)

Each persisted `EventRecord` includes:

| Field | Purpose |
|-------|---------|
| `EventId` | Business id |
| `PayloadJson` | Full request body |
| `ReceivedAt` | UTC when Gateway accepted request |
| `ProcessedAt` | UTC when Account confirmed |
| `Status` | `Applied` \| `Replayed` (duplicates use existing row) |
| `TraceId` | Correlation |
| `PayloadHash` | Idempotency conflict detection |

---

## 11. Solution structure

```
EventLedger/
├── src/
│   ├── EventLedger.Contracts/          # DEV-03 — shared DTOs, constants
│   │   ├── Events/
│   │   ├── Accounts/
│   │   └── Headers/
│   ├── EventGateway/                   # DEV-01
│   │   ├── Program.cs
│   │   ├── Endpoints/
│   │   ├── Services/
│   │   ├── Data/
│   │   └── Clients/
│   └── AccountService/                 # DEV-02
│       ├── Program.cs
│       ├── Endpoints/
│       ├── Services/
│       └── Data/
├── tests/
│   ├── EventGateway.Tests/             # QA-01
│   ├── AccountService.Tests/           # QA-02
│   └── EventLedger.IntegrationTests/   # QA-03, QA-04
├── docker-compose.yml                  # DEV-03
└── EventLedger.sln
```

**Parallel ownership:** See [parallel-workplan.md](../../ai-agents/parallel-workplan.md).

---

## 12. Docker Compose

```yaml
services:
  account-service:
    build: ./src/AccountService
    environment:
      - ASPNETCORE_URLS=http://+:8081
      - ConnectionStrings__Default=Data Source=/data/account.db
    volumes:
      - account-data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8081/health"]
    networks: [eventledger]

  event-gateway:
    build: ./src/EventGateway
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - AccountService__BaseUrl=http://account-service:8081
      - ConnectionStrings__Default=Data Source=/data/gateway.db
    depends_on:
      account-service:
        condition: service_healthy
    volumes:
      - gateway-data:/data
    networks: [eventledger]

networks:
  eventledger:

volumes:
  account-data:
  gateway-data:
```

---

## 13. API surface summary

See [api-contracts.md](api-contracts.md) for full request/response schemas.

| Service | Endpoints |
|---------|-----------|
| Gateway | `POST /events`, `GET /events/{eventId}`, `GET /events?account=`, `GET /health` |
| Account | `POST /accounts/{id}/transactions`, `GET /accounts/{id}/balance`, `GET /accounts/{id}`, `GET /health` |

---

## 14. Data model summary

See [data-model.md](data-model.md) for entities and indexes.

---

## 15. Diagrams

See [diagrams.md](diagrams.md) for Mermaid context, container, and sequence diagrams.

---

## 16. Implementation notes for developers

| Topic | Guidance |
|-------|----------|
| API style | Minimal APIs with endpoint classes |
| Validation | FluentValidation or built-in data annotations |
| Errors | RFC 7807 `ProblemDetails` |
| Money | `decimal` in C#; SQLite `TEXT` or `REAL` with 2 decimal places |
| EF migrations | Applied at startup in Development/Docker |
| Contracts | `EventLedger.Contracts` referenced by both services; DEV-03 owns, DEV-01/02 consume |
