# Event Ledger — Test Results

**Last updated:** 2026-06-17 · **Stage:** S6 QA

---

## Summary

| Project | Tests | Passed | Failed | Duration |
|---------|-------|--------|--------|----------|
| `EventGateway.Tests` | 18 | 18 | 0 | ~5s |
| `AccountService.Tests` | 9 | 9 | 0 | ~10s |
| `EventLedger.IntegrationTests` | 13 | 13 | 0 | ~9s |
| **Total** | **40** | **40** | **0** | ~30s |

**Verify:** `dotnet test src/EventLedger.sln`

---

## T-20b — Resiliency + trace (QA-04)

**Agent:** QA-04  
**Project:** `tests/EventLedger.IntegrationTests/`  
**Tests added:** 6 (3 resiliency + 3 trace propagation)

### Test run

```
dotnet test tests/EventLedger.IntegrationTests/EventLedger.IntegrationTests.csproj
Passed: 13 | Failed: 0 | Skipped: 0 | Duration: ~9s
```

(QA-04 owns 6 of 13 integration tests; remaining 7 are QA-03 E2E.)

### Resiliency (`ResiliencyTests.cs`)

| Test | AC | Result |
|------|-----|--------|
| `PostEvent_AccountReturns500_Returns503AndDoesNotPersist` | AC-13 | Pass |
| `PostEvent_AccountUnreachable_Returns503WithinTimeout` | AC-13, AC-18 | Pass |
| `PostEvent_RepeatedAccountFailures_OpensCircuitBreakerAndFailsFast` | AC-18 | Pass |

**Observations:**

- Circuit breaker threshold matches production config (5 failures / 30s break).
- Open circuit returns 503 with detail containing "Circuit breaker"; handler call count stays at 5 on 6th request.
- Account 500 responses surface as 503 with status code in problem detail; event not persisted.

### Trace propagation (`TracePropagationTests.cs`)

| Test | AC | Result |
|------|-----|--------|
| `PostEvent_ForwardsTraceParentHeaderToAccountService` | AC-17 | Pass |
| `PostEvent_DownstreamTraceParentMatchesGatewayTraceIdOnFailure` | AC-16, AC-17 | Pass |
| `PostEvent_SuccessfulApply_UsesConsistentTraceAcrossDownstreamCall` | AC-17 | Pass |

**Observations:**

- Downstream requests include W3C-format `traceparent` header (`00-{traceId}-{spanId}-{flags}`).
- Problem JSON `traceId` on failure matches trace ID extracted from downstream `traceparent`.

---

## Coverage (T-21)

See [coverage-summary.md](coverage-summary.md).

| Module | Line coverage |
|--------|---------------|
| EventGateway (unit) | **81.7%** |
| AccountService (unit) | **77.1%** |
| Integration (combined) | **71.9%** |

Reports: `coverage/unit-gateway/`, `coverage/unit-account/`, `coverage/integration/`

---

## T-19 / T-19b — Unit tests (QA-01, QA-02)

See [message-log.md](../agent-comms/message-log.md) for QA-01 and QA-02 status updates.

| Project | Line coverage |
|---------|---------------|
| Account Service | 77.12% |
| Gateway | collected via `coverage/unit-gateway/` |

---

## Defects

None filed from QA-04 resiliency/trace lane.
