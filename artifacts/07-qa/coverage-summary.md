# Event Ledger — Coverage Summary

**Task:** T-21 · **Date:** 2026-06-17 · **Agent:** QA-01 (aggregated)

---

## Commands

```powershell
# Unit — Gateway
dotnet test tests/EventGateway.Tests/ `
  --collect:"XPlat Code Coverage" `
  --results-directory coverage/unit-gateway

# Unit — Account (coverlet.msbuild → coverage/unit-account/)
dotnet test tests/AccountService.Tests/

# Integration
dotnet test tests/EventLedger.IntegrationTests/ `
  --collect:"XPlat Code Coverage" `
  --results-directory coverage/integration
```

---

## Line coverage by module

| Module | Test project | Line | Branch | Method |
|--------|--------------|------|--------|--------|
| **EventGateway** | EventGateway.Tests | **81.7%** | 60.2% | — |
| **AccountService** | AccountService.Tests | **77.1%** | 59.5% | 90.5% |
| **EventLedger.Contracts** | (included in unit) | 69.8% | 100% | 69.8% |
| **Combined (integration run)** | IntegrationTests | **71.9%** | 48.8% | — |

Integration coverage exercises Gateway + Account + Contracts together via real HTTP.

---

## Report locations

| Path | Contents |
|------|----------|
| `coverage/unit-gateway/**/coverage.cobertura.xml` | Gateway unit |
| `coverage/unit-account/coverage.cobertura.xml` | Account unit |
| `coverage/integration/**/coverage.cobertura.xml` | Integration |

---

## Test summary (all passing)

| Suite | Tests |
|-------|------:|
| EventGateway.Tests | 21 |
| AccountService.Tests | 11 |
| EventLedger.IntegrationTests | 14 |
| **Total** | **46** |

---

## Gaps (acceptable for take-home)

- Gateway `Resilience/` (inbound + outbound Polly) — integration and `InboundResilienceTests` cover behavior
- Account `AccountQueryService` detail path — lower coverage in integration-only runs
- REV-01-M03 addressed via integration suite (E2E + resiliency/trace + inbound limits)

---

## Acceptance

Meets handout requirement: automated tests with `dotnet test` and coverage artifacts in `coverage/`.
