# Code Review Report — Event Ledger

**Agent:** REV-01 · **Date:** 2026-06-16  
**Scope:** `src/EventGateway/`, `src/AccountService/`, `src/EventLedger.Contracts/`, `tests/`, `docker-compose.yml`  
**References:** [design.md](../03-architecture/design.md), [requirements.md](../02-requirements/requirements.md)

---

## Executive summary

| Severity | Count |
|----------|------:|
| Critical | 0 |
| Major | 4 |
| Minor | 5 |
| Info | 6 |

The implementation aligns with the approved architecture on primary flows: separate SQLite databases, persist-after-Account-success, commutative balance, Polly timeout + circuit breaker, OpenTelemetry tracing, Serilog JSON logging, custom metrics, and Docker Compose with health-gated startup.

**Recommendation:** Fix Major defects **REV-01-M01** and **REV-01-M02** before QA. No Critical blockers.

---

## Checklist results

| Area | Result | Notes |
|------|--------|-------|
| Idempotency | ⚠️ Partial | Happy path OK; Gateway concurrent race (M01) |
| Balance correctness | ✅ Pass | CREDIT − DEBIT, order-independent |
| Event listing order | ✅ Pass | `eventTimestamp ASC`, `eventId ASC` |
| 503 degradation | ✅ Pass | POST fails fast; GET uses local DB only |
| Polly resiliency | ✅ Pass | 5s timeout + circuit breaker, no POST retry |
| OpenTelemetry | ✅ Pass | Both services instrumented |
| Structured logging | ✅ Pass | JSON, TraceId, ServiceName |
| Service separation | ✅ Pass | No shared DB or in-process state |
| EF indexes | ✅ Pass | Unique `eventId` on both services |
| Test coverage | ⚠️ Gap | Smoke tests only (M03) |

---

## Major findings

### REV-01-M01 — Gateway concurrent idempotency race

**Location:** `EventService.SubmitEventAsync`  
**Issue:** Read-then-insert without handling `DbUpdateException` on unique `EventId` constraint. Concurrent POSTs with the same new `eventId` can throw and return **500** instead of **200** + `Idempotency-Replay: true`.  
**Owner:** DEV-01  
**Requirement:** FR-07, AC-03

### REV-01-M02 — Gateway lacks global exception middleware

**Location:** `EventGateway/Program.cs`  
**Issue:** Account Service has `ExceptionHandlingMiddleware` returning ProblemDetails. Gateway relies on local catches and default `UseExceptionHandler()`, so unhandled exceptions (including M01) may return generic **500** without trace-enriched ProblemDetails.  
**Owner:** DEV-01  
**Requirement:** FR-09

### REV-01-M03 — Test coverage gaps

**Location:** `tests/`  
**Issue:** 12 smoke tests pass but NFR-15–18 require integration, resiliency, and trace tests. Gateway tests stub `IAccountServiceClient`, so HttpClient + Polly path is untested. No integration test project yet.  
**Owner:** QA-01..04 (planned); note for sprint  
**Requirement:** NFR-15–18

### REV-01-M04 — EF Core package version mismatch

**Location:** `EventGateway.csproj` (8.0.17) vs `AccountService.csproj` (8.0.11)  
**Issue:** Inconsistent EF Core patch versions across services.  
**Owner:** DEV-03

---

## Minor findings

| ID | Finding | Owner |
|----|---------|-------|
| REV-01-m01 | No explicit request body size limit on POST /events | DEV-01 |
| REV-01-m02 | `EventResponse.Metadata` deserialization uses `Dictionary<string, object>` (flexible but loose typing) | DEV-01 |
| REV-01-m03 | Account `TransactionRequest` uses record; Gateway uses class — unified in Contracts but style differs | Info |
| REV-01-m04 | No `EventLedger.sln` at repo root (under `src/`) — document in README | DEV-03 |
| REV-01-m05 | Health check does not verify downstream on Gateway (by design — acceptable) | Info |

---

## Informational

- Write-path ordering (Account then Gateway persist) supports recovery on client retry.
- `Idempotency-Replay` header propagated via `EventLedgerHeaders`.
- Payload hash conflict detection logs warning and returns original (DEC-01).
- Smoke tests cover health, CRUD, duplicates, ordering, 503 no-persist.
- OpenTelemetry export is console-only; OTLP optional via env (acceptable for take-home).

---

## Positive observations

- Account Service handles `DbUpdateException` for idempotent transaction replay.
- Polly policies match architecture (5s timeout, 5 failures / 30s break).
- Docker Compose correctly keeps Account Service off host ports.
- Custom metrics `eventledger.events.processed` / `eventledger.events.failed` present on Gateway.

---

## Next steps

1. **T-18c:** DEV-01 fixes M01, M02; DEV-03 fixes M04  
2. **QA wave:** Address M03 with integration and resiliency tests  
3. Developer approval of this review → proceed per orchestrator
