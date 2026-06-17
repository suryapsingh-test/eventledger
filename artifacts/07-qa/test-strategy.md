# Event Ledger — Test Strategy

**Task:** T-19 · **Agent:** QA-01  
**Last updated:** 2026-06-17

---

## Gateway unit tests (`tests/EventGateway.Tests/`)

**Framework:** xUnit + `WebApplicationFactory<Program>`  
**Coverage tool:** Coverlet (`coverlet.collector` + `coverlet.runsettings`)  
**Output:** `coverage/unit-gateway/` (via `--results-directory`)

### Acceptance criteria mapping

| AC | Scenario | Test file |
|----|----------|-----------|
| AC-03 | Duplicate `eventId` → 200 replay, single account call, one stored record | `IdempotencyTests.cs` |
| AC-04 | Missing/empty `eventId` → 400, no persist, no account call | `ValidationTests.cs` |
| AC-05 | Invalid `type` → 400, no side effects | `ValidationTests.cs` |
| AC-06 | Zero/negative `amount` → 400, no side effects | `ValidationTests.cs` |
| AC-07 | `GET /events?account=` ordered by `eventTimestamp` asc (tie-break: `eventId`) | `EventQueryTests.cs` |
| AC-09 | `GET /events/{id}` → 200 when exists, 404 when missing | `EventQueryTests.cs` |
| AC-12 | `GET /health` → 200 Healthy with DB check | `HealthTests.cs` |
| AC-13 | Account down → `POST /events` 503, event not persisted | `DegradationTests.cs` |
| AC-14 | Account down → `GET` endpoints still return persisted Gateway data | `DegradationTests.cs` |

### Test fixtures

| Fixture | Purpose |
|---------|---------|
| `EventGatewayWebApplicationFactory` | Stub account client, isolated SQLite DB |
| `CountingAccountWebApplicationFactory` | Tracks downstream account apply calls |
| `FailingAccountWebApplicationFactory` | Simulates Account Service unavailable (503 on POST) |

Shared DB path constructor supports seeding events with stub client, then reading via failing client (AC-14).

### Commands

```powershell
# Gateway unit tests only
dotnet test tests/EventGateway.Tests/EventGateway.Tests.csproj

# With coverage
dotnet test tests/EventGateway.Tests/EventGateway.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory coverage/unit-gateway
```

Coverage includes `[EventGateway]*` assemblies per `coverlet.runsettings`.

---

## Account unit tests (`tests/AccountService.Tests/`)

**Framework:** xUnit + `WebApplicationFactory<Program>`  
**Coverage tool:** Coverlet (`coverlet.collector` + `coverlet.msbuild`)  
**Output:** `coverage/unit-account/`

### Commands

```powershell
dotnet test tests/AccountService.Tests/AccountService.Tests.csproj
```

---

## Integration tests (`tests/EventLedger.IntegrationTests/`)

**Task:** T-20 · **Agent:** QA-03 (E2E) + QA-04 (resiliency/trace)  
**Framework:** xUnit + dual `WebApplicationFactory` hosts  
**Coverage tool:** Coverlet (`coverlet.collector` + `coverlet.runsettings`)  
**Output:** `coverage/integration/`

### Architecture

| Fixture | Purpose |
|---------|---------|
| `EventLedgerIntegrationFixture` | Starts Account + Gateway; Gateway `HttpClient` uses Account `TestServer.CreateHandler()` for real HTTP pipeline |
| `AccountIntegrationWebApplicationFactory` | In-memory SQLite Account Service |
| `E2EGatewayWebApplicationFactory` | Real `AccountServiceClient` + Polly; routes to Account test host |
| `CapturingGatewayWebApplicationFactory` | Capturing handler for resiliency/trace (QA-04) |

Both service assemblies are referenced; `WebApplicationFactory<T>` uses assembly marker types (`EventService`, `TransactionService`) to avoid `Program` ambiguity.

### E2E acceptance criteria mapping (QA-03)

| AC | Scenario | Test file |
|----|----------|-----------|
| AC-01 | `POST /events` CREDIT → balance updated, `GET /events/{id}` | `EndToEndEventFlowTests.cs` |
| AC-02 | DEBIT after CREDIT → correct balance | `EndToEndEventFlowTests.cs` |
| AC-03 | Duplicate `eventId` → balance unchanged | `IdempotencyIntegrationTests.cs` |
| AC-08 | Out-of-order arrival via Gateway → balance 120 | `OutOfOrderGatewayIntegrationTests.cs` |
| AC-12 | `GET /health` healthy on both services | `HealthIntegrationTests.cs` |
| — | Multiple events same account → list + balance | `EndToEndEventFlowTests.cs` |

### Resiliency + trace mapping (QA-04)

| AC | Scenario | Test file |
|----|----------|-----------|
| AC-13 | Account unreachable or returns 500 → `POST /events` 503, no persist | `ResiliencyTests.cs` |
| AC-16 | Problem response `traceId` matches downstream `traceparent` trace ID | `TracePropagationTests.cs` |
| AC-17 | Outbound Account call includes W3C `traceparent` header | `TracePropagationTests.cs` |
| AC-18 | After 5 consecutive Account failures, circuit opens and fails fast | `ResiliencyTests.cs` |

Uses `CapturingAccountServiceHandler` to intercept outbound HTTP without stubbing `IAccountServiceClient`, so Polly circuit breaker and timeout policies execute in-process.

### Commands

```powershell
# Integration tests only
dotnet test tests/EventLedger.IntegrationTests/EventLedger.IntegrationTests.csproj

# With coverage
dotnet test tests/EventLedger.IntegrationTests/EventLedger.IntegrationTests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory coverage/integration
```

Coverage includes `[EventGateway]*` and `[AccountService]*` per `coverlet.runsettings`.

---

## Other test projects

| Project | Owner | Focus |
|---------|-------|-------|
| `AccountService.Tests` | QA-02 | Account validation, balance, health |
| `EventLedger.IntegrationTests` (E2E) | QA-03 | Gateway → Account full flow |
| `EventLedger.IntegrationTests` (resiliency/trace) | QA-04 | Circuit breaker, trace propagation |

See `artifacts/02-requirements/acceptance-criteria.md` for full AC matrix.
