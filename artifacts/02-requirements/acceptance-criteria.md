# Event Ledger — Acceptance Criteria

**Task:** T-03 · **Agent:** BA-01  
**Format:** Given / When / Then  
**QA ownership:** QA-01 (Gateway unit), QA-02 (Account unit), QA-03 (integration), QA-04 (resiliency + trace)

---

## AC-01 — Submit valid CREDIT event (happy path)

**Maps to:** FR-01, FR-06, FR-15, FR-17  
**QA:** QA-03 (integration), QA-01 (Gateway unit)

| Step | Criterion |
|------|-----------|
| **Given** | Both services are running and Account `acct-123` exists or will be created on first transaction |
| **When** | Client sends `POST /events` with valid CREDIT payload (`eventId: evt-001`, `amount: 150.00`, `type: CREDIT`, valid ISO `eventTimestamp`) |
| **Then** | Response is 2xx success |
| **And** | Event is retrievable via `GET /events/evt-001` (or equivalent ID) |
| **And** | Account balance increases by 150.00 |
| **And** | Gateway audit record shows successful processing |

---

## AC-02 — Submit valid DEBIT event

**Maps to:** FR-01, FR-06, FR-17  
**QA:** QA-03, QA-02

| Step | Criterion |
|------|-----------|
| **Given** | Account `acct-123` has balance ≥ debit amount |
| **When** | Client sends valid DEBIT event |
| **Then** | Response is 2xx success |
| **And** | Account balance decreases by debit amount |

---

## AC-03 — Idempotent duplicate eventId

**Maps to:** FR-07, FR-16  
**QA:** QA-01, QA-02, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Event `evt-dup` was successfully submitted once |
| **When** | Client submits the same `eventId` again (identical or equivalent payload) |
| **Then** | Response returns the original event (same stored data) |
| **And** | No second transaction is applied in Account Service |
| **And** | Account balance is unchanged from after the first submission |
| **And** | Only one event record exists in Gateway for `evt-dup` |

---

## AC-04 — Validation: missing required field

**Maps to:** FR-02  
**QA:** QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Gateway is running |
| **When** | Client sends `POST /events` without `eventId` (or another required field) |
| **Then** | Response is 400 (or documented 4xx) |
| **And** | Response body contains a meaningful error message identifying the problem |
| **And** | No event is persisted |
| **And** | Account Service is not called |

---

## AC-05 — Validation: invalid event type

**Maps to:** FR-03  
**QA:** QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Gateway is running |
| **When** | Client sends `type: "TRANSFER"` or other non-CREDIT/DEBIT value |
| **Then** | Response is 400 with meaningful error |
| **And** | No side effects (no persist, no Account call) |

---

## AC-06 — Validation: zero or negative amount

**Maps to:** FR-04  
**QA:** QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Gateway is running |
| **When** | Client sends `amount: 0` or `amount: -10` |
| **Then** | Response is 400 with meaningful error |
| **And** | No side effects |

---

## AC-07 — Out-of-order event listing

**Maps to:** FR-13  
**QA:** QA-01, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Events for `acct-123` submitted in arrival order: T3 (latest timestamp), T1 (earliest), T2 (middle) |
| **When** | Client calls `GET /events?account=acct-123` |
| **Then** | Events are returned ordered by `eventTimestamp` ascending: T1, T2, T3 |
| **And** | Order is independent of submission order |

---

## AC-08 — Out-of-order balance correctness

**Maps to:** FR-18  
**QA:** QA-02, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | CREDIT 100 at T1, DEBIT 30 at T2, CREDIT 50 at T3 (by `eventTimestamp`) |
| **When** | Events are submitted in reverse chronological arrival order (T3, T2, T1) |
| **Then** | Final balance equals 100 − 30 + 50 = **120** |
| **And** | Balance matches result if submitted in chronological order |

---

## AC-09 — Get single event by ID

**Maps to:** FR-10, FR-11  
**QA:** QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Event `evt-xyz` exists |
| **When** | Client calls `GET /events/evt-xyz` |
| **Then** | Response is 200 with full event payload |
| **When** | Client calls `GET /events/nonexistent` |
| **Then** | Response is 404 (or documented not-found status) |

---

## AC-10 — Account balance endpoint

**Maps to:** FR-19, FR-17  
**QA:** QA-02, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Known set of applied CREDIT/DEBIT transactions |
| **When** | Client calls `GET /accounts/{accountId}/balance` on Account Service |
| **Then** | Balance equals sum(CREDIT) − sum(DEBIT) |

---

## AC-11 — Account details with recent transactions

**Maps to:** FR-20  
**QA:** QA-02

| Step | Criterion |
|------|-----------|
| **Given** | Account with transaction history |
| **When** | Client calls `GET /accounts/{accountId}` |
| **Then** | Response includes account identifier and recent transactions |
| **And** | Transaction data is consistent with balance endpoint |

---

## AC-12 — Health checks (both services)

**Maps to:** FR-22, FR-23  
**QA:** QA-01, QA-02, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Services started with healthy database |
| **When** | Client calls `GET /health` on Gateway and Account Service |
| **Then** | Both return 200 with status indicating healthy |
| **And** | Response includes basic diagnostics (e.g., DB connectivity) |

---

## AC-13 — Graceful degradation: POST when Account down

**Maps to:** FR-09, FR-24, NFR-13  
**QA:** QA-04, QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Account Service is stopped or unreachable |
| **When** | Client sends `POST /events` with new `eventId` |
| **Then** | Response is 503 (or documented service-unavailable status) within configured timeout |
| **And** | Response body explains downstream unavailability |
| **And** | Request does not hang indefinitely |

---

## AC-14 — Graceful degradation: GET events when Account down

**Maps to:** FR-14, FR-26  
**QA:** QA-04, QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Events previously persisted in Gateway; Account Service stopped |
| **When** | Client calls `GET /events/{id}` or `GET /events?account=` |
| **Then** | Responses succeed with stored Gateway data |
| **And** | Behavior does not depend on Account Service |

---

## AC-15 — Graceful degradation: balance when Account down

**Maps to:** FR-25  
**QA:** QA-04, QA-02

| Step | Criterion |
|------|-----------|
| **Given** | Account Service stopped |
| **When** | Client requests balance (direct Account API or documented Gateway path) |
| **Then** | Response is error status with clear message that Account Service is unreachable |

---

## AC-16 — Structured JSON logging with trace ID

**Maps to:** NFR-09, NFR-07  
**QA:** QA-04

| Step | Criterion |
|------|-----------|
| **Given** | Services running with log capture enabled |
| **When** | Client submits `POST /events` |
| **Then** | Gateway logs contain JSON with trace ID, timestamp, level, service name |
| **And** | Account Service logs for the downstream call contain the **same** trace ID |

---

## AC-17 — Trace propagation via HTTP header

**Maps to:** NFR-06, NFR-08  
**QA:** QA-04

| Step | Criterion |
|------|-----------|
| **Given** | Instrumented test or mock Account Service |
| **When** | Gateway processes an event |
| **Then** | Outbound HTTP request to Account Service includes propagated trace/correlation header |
| **And** | Header value matches Gateway-generated trace ID for that request |

---

## AC-18 — Resiliency pattern under repeated failures

**Maps to:** NFR-12, NFR-13  
**QA:** QA-04

| Step | Criterion |
|------|-----------|
| **Given** | Account Service fails consistently (e.g., 500 or connection refused) |
| **When** | Gateway receives multiple `POST /events` requests |
| **Then** | Circuit breaker opens (or configured pattern activates) after threshold |
| **And** | Subsequent requests fail fast without hammering Account Service |
| **And** | Client receives consistent, meaningful error responses |

---

## AC-19 — Custom metric exposed

**Maps to:** NFR-10  
**QA:** QA-01, QA-04

| Step | Criterion |
|------|-----------|
| **Given** | Services running |
| **When** | Events are submitted (success and failure cases) |
| **Then** | At least one custom metric is observable (endpoint, log, or telemetry export) |
| **And** | Metric reflects activity (e.g., event count, error rate, latency) |

---

## AC-20 — Gateway auditing fields

**Maps to:** FR-08  
**QA:** QA-01

| Step | Criterion |
|------|-----------|
| **Given** | Event processed through Gateway |
| **When** | Audit/store record is inspected |
| **Then** | Record includes received timestamp, processing status, and event payload (or retrievable reference) |

---

## AC-21 — Docker Compose startup

**Maps to:** FR-27  
**QA:** QA-03

| Step | Criterion |
|------|-----------|
| **Given** | Docker available |
| **When** | Developer runs `docker compose up --build` |
| **Then** | Both Gateway and Account Service start and pass health checks |
| **And** | End-to-end `POST /events` succeeds |

---

## AC-22 — Test suite runnable

**Maps to:** FR-28, NFR-15  
**QA:** QA-01..04

| Step | Criterion |
|------|-----------|
| **Given** | Repository cloned and dependencies restored |
| **When** | Developer runs `dotnet test` (or documented equivalent) |
| **Then** | All tests pass |
| **And** | Suite includes unit, integration, resiliency, and trace coverage per NFR-15..18 |

---

## Summary matrix

| AC | Feature area | Primary QA |
|----|--------------|------------|
| AC-01, AC-02 | Happy path | QA-03 |
| AC-03 | Idempotency | QA-01, QA-02, QA-03 |
| AC-04 … AC-06 | Validation | QA-01 |
| AC-07, AC-09 | GET /events | QA-01 |
| AC-08 | Out-of-order balance | QA-02, QA-03 |
| AC-10, AC-11 | Account reads | QA-02 |
| AC-12 | Health | QA-01, QA-02 |
| AC-13 … AC-15 | Degradation | QA-04 |
| AC-16, AC-17 | Tracing | QA-04 |
| AC-18 | Resiliency | QA-04 |
| AC-19 | Metrics | QA-01, QA-04 |
| AC-20 | Auditing | QA-01 |
| AC-21, AC-22 | Delivery | QA-03 |
