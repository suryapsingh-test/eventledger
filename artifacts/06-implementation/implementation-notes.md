# Implementation Notes

**Stage:** S4 · **Updated:** 2026-06-16

## dev-wave-1 (DEV-01 + DEV-02)

| Topic | Decision |
|-------|----------|
| Shared contracts | Local DTOs per service until DEV-03 creates `EventLedger.Contracts` |
| Gateway persist | After Account Service success (per architecture DEC-05) |
| Duplicate events | HTTP 200 + `Idempotency-Replay: true` |
| Account balance | Computed from transactions; negative allowed |
| Resiliency / tracing | Stub HttpClient — DEV-03 adds Polly + OpenTelemetry |

## dev-wave-2 (DEV-03)

| Topic | Decision |
|-------|----------|
| Shared contracts | `src/EventLedger.Contracts/` — Events, Accounts, Health, Headers namespaces; both services reference it |
| Solution | `src/EventLedger.sln` includes Contracts, both services, both test projects |
| Polly | `Microsoft.Extensions.Http.Polly` on Gateway `AccountService` HttpClient: 5s timeout (inner), circuit breaker 5 failures / 30s break (outer); no retry on POST |
| Downstream failure | `BrokenCircuitException`, `TimeoutRejectedException`, HTTP errors → 503 ProblemDetails; event not persisted |
| OpenTelemetry | Gateway: ASP.NET Core + HttpClient tracing + metrics console export; Account: ASP.NET Core tracing |
| Trace propagation | OTel HttpClient instrumentation + explicit `traceparent` header in `AccountServiceClient` |
| Serilog TraceId | `TraceIdEnricher` on both services from `Activity.Current` |
| Custom metrics | `eventledger.events.processed` (201 only, not replay), `eventledger.events.failed` (validation + downstream) |
| Docker | `docker-compose.yml` with build context `./src`; multi-stage Dockerfiles (SDK 8 → aspnet 8); SQLite volumes; Account healthcheck gates Gateway |
| Docker ports | Gateway 8080 (host), Account 8081 (internal network) |

## Build verification

```powershell
dotnet build src/EventLedger.sln
dotnet test src/EventLedger.sln
```

All passing as of 2026-06-16 (12 smoke tests: 7 Gateway + 5 Account).
