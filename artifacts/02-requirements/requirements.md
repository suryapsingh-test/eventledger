# Event Ledger — Requirements

**Task:** T-03 · **Agent:** BA-01 · **Stage:** S2 — Requirements  
**Source:** [docs/event-ledger-handout.md](../../docs/event-ledger-handout.md)  
**Date:** 2026-06-16

---

## 1. Scope

This document defines functional and non-functional requirements for the Event Ledger take-home: two microservices (Event Gateway API and Account Service) that process financial transaction events with idempotency, out-of-order tolerance, observability, resiliency, and graceful degradation.

Out of scope for requirements (deferred to architecture/implementation): technology choices, database schema, HTTP client library, specific Polly policy parameters, and deployment topology beyond “two independent processes.”

---

## 2. Actors and systems

| Actor / system | Role |
|----------------|------|
| **Client** | Upstream system or browser submitting and querying events |
| **Event Gateway API** | Public-facing entry point; validates, stores events, forwards to Account Service |
| **Account Service** | Internal service; applies transactions, maintains balances and account state |
| **Developer / operator** | Runs services, observes logs/metrics, runs health checks |

---

## 3. Functional requirements

### 3.1 Event Gateway — POST /events

| ID | Requirement |
|----|-------------|
| **FR-01** | The Gateway SHALL accept `POST /events` with a JSON body containing: `eventId`, `accountId`, `type`, `amount`, `currency`, `eventTimestamp`, and optional `metadata`. |
| **FR-02** | The Gateway SHALL validate required fields are present and non-empty where applicable. |
| **FR-03** | The Gateway SHALL reject `type` values other than `CREDIT` or `DEBIT` with a meaningful error and appropriate HTTP status (e.g., 400 Bad Request). |
| **FR-04** | The Gateway SHALL reject `amount` that is zero or negative with a meaningful error and appropriate HTTP status. |
| **FR-05** | The Gateway SHALL validate `eventTimestamp` is a valid ISO 8601 datetime. |
| **FR-06** | On first submission of a unique `eventId`, the Gateway SHALL persist the event locally, call the Account Service to apply the transaction, and return success only when the Account Service call succeeds. |
| **FR-07** | On duplicate submission of the same `eventId`, the Gateway SHALL NOT create a duplicate event record, SHALL NOT call the Account Service again, SHALL NOT alter the account balance, and SHALL return the original event with an appropriate status code (e.g., 200 OK or 409 Conflict — architect to choose; behavior must be idempotent). |
| **FR-08** | The Gateway SHALL record audit metadata for each received event (at minimum: when received, processing status, and payload or reference sufficient for troubleshooting). |
| **FR-09** | When the Account Service is unavailable or the resiliency policy blocks the call, `POST /events` SHALL return an appropriate client error (e.g., 503 Service Unavailable) within a bounded time — not hang indefinitely and not return an unhandled 500. |

### 3.2 Event Gateway — GET /events

| ID | Requirement |
|----|-------------|
| **FR-10** | The Gateway SHALL provide `GET /events/{id}` to retrieve a single event by its `eventId` (or internal ID — architect to align with storage). |
| **FR-11** | The Gateway SHALL return 404 (or equivalent) when the requested event does not exist. |
| **FR-12** | The Gateway SHALL provide `GET /events?account={accountId}` to list all events for the given account. |
| **FR-13** | Event listings SHALL be ordered chronologically by `eventTimestamp` ascending (earliest first), regardless of the order events arrived at the Gateway. |
| **FR-14** | `GET /events/{id}` and `GET /events?account=` SHALL function using only Gateway-local data and SHALL NOT depend on Account Service availability. |

### 3.3 Account Service — transactions and balance

| ID | Requirement |
|----|-------------|
| **FR-15** | The Account Service SHALL expose `POST /accounts/{accountId}/transactions` callable only by the Gateway (not public clients). |
| **FR-16** | Applying a transaction SHALL be idempotent by `eventId` — duplicate application for the same `eventId` SHALL NOT change balance twice. |
| **FR-17** | The Account Service SHALL compute net balance as: **sum(CREDIT amounts) − sum(DEBIT amounts)** for all applied transactions on the account. |
| **FR-18** | Balance SHALL be correct regardless of the order transactions are applied (out-of-order arrival by `eventTimestamp` must not produce incorrect balance). |
| **FR-19** | The Account Service SHALL expose `GET /accounts/{accountId}/balance` returning the current balance for the account. |
| **FR-20** | The Account Service SHALL expose `GET /accounts/{accountId}` returning account details and recent transaction history. |
| **FR-21** | Balance and account detail queries SHALL return a clear error when the service itself cannot satisfy the request; when invoked via Gateway proxy patterns, unreachable Account Service SHALL be surfaced clearly to the client (see FR-25). |

### 3.4 Health

| ID | Requirement |
|----|-------------|
| **FR-22** | Both services SHALL expose `GET /health` returning service status and basic diagnostics (e.g., database connectivity). |
| **FR-23** | Health endpoints SHALL return appropriate HTTP status reflecting health (e.g., 200 when healthy, 503 when unhealthy). |

### 3.5 Graceful degradation

| ID | Requirement |
|----|-------------|
| **FR-24** | When Account Service is unavailable, `POST /events` SHALL fail fast with a meaningful error (FR-09). |
| **FR-25** | When Account Service is unavailable, balance-related queries (direct or via any Gateway-exposed balance path) SHALL return a clear error indicating Account Service is unreachable. |
| **FR-26** | When Account Service is unavailable, Gateway read APIs for stored events (FR-14) SHALL continue to work. |

### 3.6 Event payload contract

| Field | Type | Required | Constraints |
|-------|------|----------|-------------|
| `eventId` | string | Yes | Unique per logical event |
| `accountId` | string | Yes | — |
| `type` | string | Yes | `CREDIT` or `DEBIT` only |
| `amount` | number | Yes | > 0 |
| `currency` | string | Yes | e.g., `USD` |
| `eventTimestamp` | string (ISO 8601) | Yes | Valid datetime |
| `metadata` | object | No | Opaque key-value context |

### 3.7 Delivery and documentation (handout)

| ID | Requirement |
|----|-------------|
| **FR-27** | A `docker-compose.yml` (preferred) or equivalent documented steps SHALL allow starting both services locally. |
| **FR-28** | Automated tests SHALL be runnable via a standard command (e.g., `dotnet test`). |
| **FR-29** | `README.md` SHALL document architecture, setup, run instructions, test instructions, and resiliency pattern rationale. |

---

## 4. Non-functional requirements

### 4.1 Service separation

| ID | Requirement |
|----|-------------|
| **NFR-01** | Event Gateway and Account Service SHALL run as **independently deployable processes**. |
| **NFR-02** | Each service SHALL use its **own embedded/in-memory database** (e.g., SQLite). |
| **NFR-03** | Services SHALL NOT share a database or in-process state. |
| **NFR-04** | Inter-service communication SHALL use synchronous REST with a documented API contract. |

### 4.2 Distributed tracing

| ID | Requirement |
|----|-------------|
| **NFR-05** | The Gateway SHALL generate a **trace ID** for each incoming client request. |
| **NFR-06** | The Gateway SHALL propagate the trace ID to the Account Service via HTTP headers on downstream calls. |
| **NFR-07** | Both services SHALL include the trace ID in structured log output for a single client request path. |
| **NFR-08** | OpenTelemetry is preferred but not mandatory; minimum bar is trace ID generation, propagation, and logging. |

### 4.3 Observability

| ID | Requirement |
|----|-------------|
| **NFR-09** | Both services SHALL emit **JSON structured logs** including at minimum: trace ID, timestamp, log level, and service name. |
| **NFR-10** | At least **one custom metric** SHALL be exposed (e.g., request count by endpoint, error rate, or latency) via logs, endpoint, or observability library. |
| **NFR-11** | Health endpoints SHALL support operational readiness checks (FR-22, FR-23). |

### 4.4 Resiliency

| ID | Requirement |
|----|-------------|
| **NFR-12** | The Gateway SHALL implement **at least one** resiliency pattern on calls to the Account Service: circuit breaker, bulkhead, or timeout + retry with backoff. |
| **NFR-13** | Resiliency behavior SHALL prevent indefinite blocking and SHALL produce predictable error responses to clients when downstream is failing or slow. |
| **NFR-14** | Implementation SHALL be prepared to explain the chosen pattern and trade-offs (documented in README). |

### 4.5 Quality and testing (NFR for QA phase)

| ID | Requirement |
|----|-------------|
| **NFR-15** | Automated tests SHALL cover core functionality: idempotency, out-of-order events, balance correctness, validation. |
| **NFR-16** | Automated tests SHALL cover resiliency: simulated Account Service failure, circuit breaker / timeout behavior, proper error responses. |
| **NFR-17** | Automated tests SHALL verify trace ID propagation Gateway → Account Service. |
| **NFR-18** | At least one integration test SHALL exercise the full Gateway → Account Service flow. |

### 4.6 AI-SDLC (submission evidence — from sprint plan)

| ID | Requirement |
|----|-------------|
| **NFR-19** | Multi-agent workflow artifacts (`ai-agents/`, `ai-agent-skills/`, `artifacts/`, dashboard) SHALL be maintained as evidence of AI-augmented SDLC. |
| **NFR-20** | Git commit history SHALL reflect incremental working process (not a single squashed commit). |

---

## 5. Requirement traceability (implementation tasks)

| Requirement group | Primary implementation tasks |
|-------------------|------------------------------|
| FR-01 … FR-14, FR-24, FR-26 | T-10, T-11, T-12, T-15, T-16 (DEV-01) |
| FR-15 … FR-21 | T-06, T-07, T-08, T-09 (DEV-02) |
| NFR-12, NFR-13 | T-13 (DEV-03) |
| NFR-05 … NFR-08 | T-14 (DEV-03) |
| NFR-09, NFR-10, FR-22 | T-09, T-15, T-16 |
| FR-27 | T-17 (DEV-03) |
| FR-28, NFR-15 … NFR-18 | T-19 … T-21 (QA-01..04) |
| FR-29 | T-18 (DEV-03) |

---

## 6. Assumptions and open items (for architect)

| # | Item | BA recommendation |
|---|------|-------------------|
| A-01 | Duplicate `eventId` HTTP status | Return 200 with original body OR 409 — either acceptable if idempotent; document choice |
| A-02 | Out-of-order balance strategy | Account Service must apply by `eventTimestamp` or equivalent ordering — architect to specify |
| A-03 | Gateway persistence timing | Persist event only after Account Service success (per sprint plan risk mitigation) |
| A-04 | Balance query exposure | Handout implies balance via Account Service; Gateway may proxy or clients call internal URL only in compose — architect to define |
| A-05 | Currency validation | Accept any non-empty string; no FX conversion required |
