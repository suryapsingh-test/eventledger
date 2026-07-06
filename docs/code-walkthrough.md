# Event Ledger — Code Walkthrough

This guide walks through the Event Ledger codebase step by step. It assumes you are comfortable reading C# and basic web concepts, but may be new to microservices, Entity Framework, or distributed tracing.

If you want to run the app first, see the [README](../README.md). If you want design decisions and diagrams, see [artifacts/03-architecture/design.md](../artifacts/03-architecture/design.md).

---

## What this system does (in one paragraph)

Clients send **transaction events** (credits and debits) to the **Event Gateway**. The Gateway validates each event, checks whether it was already processed, asks the **Account Service** to update the account balance, and then stores a copy of the event in its own database. The Account Service keeps the real financial state; the Gateway keeps an audit log that clients can query even when the Account Service is temporarily down.

---

## The big picture

```
  Client
    │
    │  POST /events, GET /events/...
    ▼
┌─────────────────────────────────────────────────────────┐
│  Event Gateway (port 8080)                              │
│  Inbound: concurrency bulkhead + per-client rate limit  │
│  Endpoints → EventService → AccountServiceClient        │
│                    │                                    │
│                    ▼                                    │
│              GatewayDbContext → gateway.db (SQLite)     │
└──────────────────────────┬──────────────────────────────┘
                           │ HTTP POST (Polly: CB→retry→timeout)
                           ▼
┌─────────────────────────────────────────────────────────┐
│  Account Service (port 8081)                              │
│                                                         │
│  Endpoints → TransactionService / AccountQueryService   │
│                    │                                    │
│                    ▼                                    │
│              AccountDbContext → account.db (SQLite)     │
└─────────────────────────────────────────────────────────┘
```

**Important rule:** each service has its **own database file**. They never share a database. That is what makes them independent microservices.

---

## Folder map — where to look first

| Path | What lives here |
|------|-----------------|
| `src/EventLedger.Contracts/` | Shared request/response types used by both services |
| `src/EventGateway/` | Public API — the service clients talk to |
| `src/AccountService/` | Internal API — balance and transaction logic |
| `tests/EventGateway.Tests/` | Unit tests for the Gateway only |
| `tests/AccountService.Tests/` | Tests for the Account Service only |
| `tests/EventLedger.IntegrationTests/` | Tests that exercise Gateway + Account together |

Inside each service, code is grouped by responsibility:

| Folder | Role |
|--------|------|
| `Program.cs` | Application startup — wiring, dependencies, middleware |
| `Endpoints/` | HTTP routes — thin layer that receives requests and returns responses |
| `Services/` | Business logic — where the real work happens |
| `Data/` | Database models and Entity Framework context |
| `Clients/` | (Gateway only) HTTP calls to another service |
| `Resilience/` | (Gateway only) Inbound rate limits + outbound Polly policies |
| `Middleware/` | Cross-cutting HTTP behavior (error handling) |
| `Logging/`, `Metrics/` | Observability helpers |

---

## How a request enters the app

Every ASP.NET Core app starts in `Program.cs`. Think of it as the **bootstrap file**: it registers services, configures logging, and maps URL paths to handler code.

### Event Gateway — `src/EventGateway/Program.cs`

Read this file top to bottom. Here is what each block does:

1. **Serilog** — configures JSON logging to the console, including a `TraceId` on every log line.
2. **Database** — registers `GatewayDbContext` with SQLite.
3. **OpenTelemetry** — enables distributed tracing and custom metrics.
4. **Inbound resilience** — `AddGatewayInboundResilience()`: global concurrency bulkhead + per-client write rate limit (`UseRateLimiter`).
5. **Outbound Polly** — `AddAccountServiceResiliencePolicies()` on the `AccountService` HttpClient: circuit breaker → retry (jitter) → timeout.
6. **Dependency injection** — registers `EventService`, `AccountServiceClient`, etc.
7. **Startup** — creates database tables if they do not exist (`EnsureCreated`).
8. **Middleware pipeline** — global error handler, rate limiter, request logging.
9. **Endpoints** — `MapEventEndpoints()` and `MapHealthEndpoints()`.

The last line `app.Run()` starts the web server and waits for HTTP requests.

### Account Service — `src/AccountService/Program.cs`

Same idea, but simpler: no HttpClient, no Polly, no custom metrics. It registers `TransactionService`, `AccountQueryService`, and maps account + health endpoints.

On startup it calls **`AccountDbInitializer.InitializeAsync`** instead of bare `EnsureCreated`: creates tables if needed, then patches **legacy** databases that predate the `Balance` column (adds the column and backfills from existing transactions).

---

## Shared contracts — `EventLedger.Contracts`

Microservices should agree on the shape of JSON messages. Instead of duplicating classes, both services reference one project: `EventLedger.Contracts`.

Example — what a client sends to `POST /events`:

```csharp
// src/EventLedger.Contracts/Events/EventRequest.cs
public sealed class EventRequest
{
    public required string EventId { get; init; }
    public required string AccountId { get; init; }
    public required string Type { get; init; }      // "CREDIT" or "DEBIT"
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string EventTimestamp { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

ASP.NET Core automatically deserializes incoming JSON into these classes. Property names in JSON use camelCase (`eventId`); C# properties use PascalCase (`EventId`). The framework maps between them.

Other contract files:

| File | Purpose |
|------|---------|
| `Events/EventResponse.cs` | What the Gateway returns after processing an event |
| `Accounts/TransactionRequest.cs` | What the Gateway sends to the Account Service |
| `Accounts/TransactionResponse.cs` | What the Account Service returns after applying a transaction |
| `Accounts/BalanceResponse.cs` | Balance query result |
| `Headers/EventLedgerHeaders.cs` | Custom header name: `Idempotency-Replay` |

---

## Event Gateway — layer by layer

### 1. Endpoints — the front door

File: `src/EventGateway/Endpoints/EventEndpoints.cs`

Endpoints are **thin**. They should not contain business rules; they parse HTTP, call a service, and format the HTTP response.

```csharp
app.MapPost("/events", SubmitEventAsync)
    .RequireRateLimiting(GatewayInboundPolicies.PerClientWrites);
app.MapGet("/events/{eventId}", GetEventByIdAsync);
app.MapGet("/events", GetEventsByAccountAsync);
```

`POST /events` is subject to **inbound** limits: a global concurrency bulkhead (all routes except `/health`) and a per-client fixed-window rate limit on writes. Excess traffic returns **429 Too Many Requests**.

When a POST arrives at `/events`:

1. ASP.NET binds the JSON body to `EventRequest`.
2. `SubmitEventAsync` calls `EventService.SubmitEventAsync`.
3. If the event was a duplicate, it sets the response header `Idempotency-Replay: true`.
4. If it was new, it increments the `eventledger.events.processed` metric.
5. Returns JSON with status `201` (new) or `200` (replay).

GET handlers follow the same pattern: call the service, return `200 OK` or a `404` ProblemDetails if not found.

**Dependency injection note:** parameters like `EventService eventService` are not passed by you — the framework creates them based on registrations in `Program.cs`.

### 2. EventService — the brain of the Gateway

File: `src/EventGateway/Services/EventService.cs`

This is the most important file to understand on the Gateway side. Walk through `SubmitEventAsync` in order:

#### Step A — Validate

```csharp
var validationErrors = EventRequestValidator.Validate(request);
if (validationErrors.Count > 0)
    throw new EventValidationException(validationErrors);
```

Validation rules live in `EventRequestValidator.cs` (required fields, allowed types, positive amount, valid ISO timestamp). Throwing an exception keeps the endpoint code clean; `ExceptionHandlingMiddleware` converts it to HTTP 400.

#### Step B — Idempotency check

```csharp
var existing = await db.Events.AsNoTracking()
    .FirstOrDefaultAsync(e => e.EventId == request.EventId, cancellationToken);

if (existing is not null)
    return (ToResponse(existing), StatusCodes.Status200OK, IsReplay: true);
```

If this `eventId` was already stored, return the stored record immediately. **No second call to the Account Service.** That prevents duplicate balance changes.

The code also compares a **payload hash**. If someone reuses an `eventId` but sends different data, we still return the original event and log a warning — we do not overwrite.

#### Step C — Call Account Service

```csharp
var accountResult = await accountServiceClient.ApplyTransactionAsync(...);

if (!accountResult.Success)
    throw new AccountServiceUnavailableException(...);
```

The Gateway applies the financial change **before** saving its own record. If the Account Service fails, we throw → middleware returns **503** → **nothing is saved in the Gateway database**. That avoids orphan events with no matching transaction.

#### Step D — Persist the event

```csharp
db.Events.Add(record);
await db.SaveChangesAsync(cancellationToken);
return (ToResponse(record), StatusCodes.Status201Created, IsReplay: false);
```

Only after a successful Account Service call do we write to `gateway.db`.

#### Concurrent duplicate protection

Two requests with the same `eventId` might arrive at the same time. Both pass the initial idempotency check, both call Account Service (Account Service idempotency handles that safely), but only one insert into Gateway DB succeeds. The loser catches `DbUpdateException` for a unique-key violation and returns the winner's record as a replay.

#### Read methods

- `GetEventByIdAsync` — simple lookup by primary key.
- `GetEventsByAccountAsync` — filters by `accountId`, sorts by `EventTimestamp` then `EventId`. This is how **out-of-order delivery** is handled for listings: sort by when the event *happened*, not when it *arrived*.

### 3. AccountServiceClient — talking to the other service

File: `src/EventGateway/Clients/AccountServiceClient.cs`

This class wraps `HttpClient`. It:

1. Builds the URL: `{baseUrl}/accounts/{accountId}/transactions`
2. Sends a JSON `TransactionRequest`
3. Forwards the W3C `traceparent` header so traces continue across services
4. Returns `AccountTransactionResult` — success/failure, not raw HTTP

Errors are **caught and converted** to a result object instead of crashing the Gateway:

| Situation | What the client returns |
|-----------|-------------------------|
| HTTP 5xx from Account Service | `Success: false` with status in message |
| Network failure / timeout | `Success: false`, "Account Service is unavailable" |
| Circuit breaker open | `Success: false`, "Circuit breaker is open" |

Polly (configured in `Program.cs`) applies timeout and circuit breaker **before** this code runs. That is why you see `BrokenCircuitException` and `TimeoutRejectedException` in the catch blocks.

### 4. Database — Gateway side

Files:

- `src/EventGateway/Data/EventRecord.cs` — C# class representing one row
- `src/EventGateway/Data/GatewayDbContext.cs` — Entity Framework configuration

`EventRecord` stores everything needed for audit and replay: payload JSON, hash, timestamps, trace ID, status.

`GatewayDbContext` configures:

- Primary key on `EventId` (also enforces uniqueness)
- Index on `(AccountId, EventTimestamp, EventId)` for fast listing queries
- `Amount` mapped as `decimal` with precision `(19, 4)` (native EF mapping, not string conversion)

Entity Framework (EF Core) is an **ORM**: you work with C# objects; EF generates SQL for SQLite.

### 5. Error handling middleware

File: `src/EventGateway/Middleware/ExceptionHandlingMiddleware.cs`

Middleware sits in the HTTP pipeline **around** your endpoints. Every request passes through it.

```
Request → ExceptionHandlingMiddleware → Endpoints → Response
              ↑ catches exceptions here
```

| Exception | HTTP status | When |
|-----------|-------------|------|
| `EventValidationException` | 400 | Bad input |
| `AccountServiceUnavailableException` | 503 | Downstream failure |
| Anything else | 500 | Unexpected bug |

Responses use **ProblemDetails** (`application/problem+json`) — a standard error format that includes `title`, `detail`, `status`, and a `traceId` extension for debugging.

---

## Account Service — layer by layer

### 1. Endpoints

File: `src/AccountService/Endpoints/AccountEndpoints.cs`

| Route | Handler |
|-------|---------|
| `POST /accounts/{accountId}/transactions` | Apply a transaction |
| `GET /accounts/{accountId}/balance` | Current balance |
| `GET /accounts/{accountId}` | Account info + recent transactions |

The POST handler mirrors the Gateway pattern: call service, set `Idempotency-Replay: true` on replay, return `201 Created` on first apply.

### 2. TransactionService — applying money movement

File: `src/AccountService/Services/TransactionService.cs`

Flow parallels `EventService`:

1. **Validate** input (`TransactionRequestValidator.cs`)
2. **Check idempotency** — if `eventId` exists in `Transactions` table, return existing row + current balance
3. **Begin database transaction** — `BeginTransactionAsync` groups writes so they succeed or fail together
4. **Auto-create account** if `accountId` is new (first event for that account)
5. **Insert** a new `Transaction` row and **update** `Account.Balance` in the same DB transaction
6. **Commit** and return `account.Balance` as `balanceAfter`

Balance is **maintained incrementally** (O(1)) instead of scanning all transactions:

```csharp
ApplyBalanceDelta(account, request.Type, request.Amount);
// CREDIT → Balance += amount; DEBIT → Balance -= amount
```

On idempotent replay, balance is read from `Account.Balance` via `GetAccountBalanceAsync`.

**Why this still handles out-of-order arrival:** each transaction is applied exactly once; CREDIT/DEBIT addition is commutative, so the final `Account.Balance` equals sum(CREDITs) − sum(DEBITs) regardless of arrival order.

Event **listing** order is handled separately in the Gateway (`OrderBy EventTimestamp`). Balance and listing solve different problems.

### 3. AccountQueryService — read-only queries

File: `src/AccountService/Services/AccountQueryService.cs`

Used by GET endpoints. Reads `account.Balance` directly (no full transaction scan) and for account detail returns the 20 most recent transactions ordered by `EventTimestamp` descending.

### 4. Database — Account side

Files:

- `src/AccountService/Data/Entities/Account.cs` — includes `Balance` column
- `src/AccountService/Data/Entities/Transaction.cs`
- `src/AccountService/Data/AccountDbContext.cs`

Key design points:

- `Transaction.EventId` has a **unique index** — database-level idempotency enforcement
- `Transaction.Id` is an auto-increment surrogate key (internal row ID)
- `Account` ↔ `Transaction` is a one-to-many relationship with foreign key on `AccountId`

### 5. Legacy database migration — `AccountDbInitializer`

File: `src/AccountService/Data/AccountDbInitializer.cs`

If you already have an `account.db` from an earlier version **without** a `Balance` column on `Accounts`, startup runs a one-time patch:

1. `EnsureCreatedAsync` — create tables for fresh installs
2. `ALTER TABLE Accounts ADD COLUMN Balance` if missing
3. Backfill `Balance` from existing `Transactions` (sum of CREDITs minus DEBITs)

New installs get `Balance` from EF model creation; legacy installs are upgraded in place. Tests: `LegacySchemaMigrationTests.cs`.

---

## Cross-cutting concerns

### Structured logging

Both services use **Serilog** with a custom `TraceIdEnricher`:

```csharp
// src/EventGateway/Logging/TraceIdEnricher.cs
var traceId = Activity.Current?.TraceId.ToString();
logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", traceId));
```

Every log line in JSON includes `"TraceId": "..."` so you can grep logs across both services for one request.

### Distributed tracing

**OpenTelemetry** creates an `Activity` for each incoming HTTP request. `Activity.Current` holds the trace ID and trace parent string.

The Gateway forwards `Activity.Current?.Id` as the `traceparent` HTTP header when calling the Account Service. OpenTelemetry on the Account Service continues the same trace.

### Custom metrics

File: `src/EventGateway/Metrics/EventMetrics.cs`

Three counters:

- `eventledger.events.processed` — incremented on successful new events
- `eventledger.events.failed` — incremented on validation or downstream failures
- `eventledger.inbound.throttled` — incremented when inbound rate/concurrency limits reject a request

Registered with OpenTelemetry and exported to the console in development.

### Resiliency — two layers

#### Inbound (client → Gateway)

File: `src/EventGateway/Resilience/GatewayInboundResilienceExtensions.cs`

| Control | Purpose |
|---------|---------|
| Global concurrency bulkhead | Caps in-flight Gateway requests (`Gateway:Inbound:ConcurrencyLimit`) |
| Per-client write rate limit | Fixed window on `POST /events` per IP |
| `/health` exempt | Probes bypass the bulkhead |

Returns **429** with ProblemDetails when limits are exceeded. Retry and circuit breaker are **not** used on inbound HTTP — clients resubmit.

#### Outbound (Gateway → Account Service)

File: `src/EventGateway/Resilience/AccountServiceResilienceExtensions.cs`

Policies are wrapped as a **singleton** `Policy.WrapAsync(circuitBreaker, retry, timeout)`:

```csharp
builder.Services.AddHttpClient("AccountService")
    .AddAccountServiceResiliencePolicies();
```

| Policy | Setting | Notes |
|--------|---------|-------|
| Circuit breaker | 5 failures / 30s break | Outermost — fail fast while open |
| Retry | 3 retries (up to 4 HTTP attempts), exponential backoff + jitter | Safe because Account Service is idempotent on `eventId` |
| Timeout | 5 seconds per attempt | Innermost — per HTTP attempt |

`AccountServiceClient` catches `BrokenCircuitException`, `TimeoutRejectedException`, and network errors and returns `Success: false` so `EventService` can return **503** without persisting.

---

## End-to-end story: POST /events

Follow this path through the code with a debugger or by reading in order:

| # | Location | What happens |
|---|----------|--------------|
| 1 | `EventEndpoints.SubmitEventAsync` | HTTP POST arrives, body → `EventRequest` |
| 2 | `EventService.SubmitEventAsync` | Validate → idempotency check |
| 3 | `AccountServiceClient.ApplyTransactionAsync` | HTTP POST to Account Service (with trace header) |
| 4 | `AccountEndpoints.ApplyTransactionAsync` | Account Service receives transaction |
| 5 | `TransactionService.ApplyTransactionAsync` | Validate → idempotency → insert → balance |
| 6 | Back in `EventService` | Account succeeded → insert `EventRecord` |
| 7 | `EventEndpoints` | Return 201 + JSON body |

If step 3–5 fails, step 6 never runs. Client gets 503.

If the same `eventId` is POSTed again, step 2 returns early with 200 — steps 3–6 are skipped.

---

## Tests — how to read them

Tests live under `tests/` and use **xUnit** with `[Fact]` for individual test methods.

### Gateway tests (`EventGateway.Tests`)

Use `WebApplicationFactory` to spin up the Gateway in memory with a test database and a **fake** Account Service client.

Example — `IdempotencyTests.cs`:

- POST the same event twice
- Assert first = 201, second = 200 with `Idempotency-Replay: true`
- Assert Account Service was called **once**, not twice

Other files:

| Test file | What it proves |
|-----------|----------------|
| `ValidationTests.cs` | Bad payloads → 400 |
| `EventQueryTests.cs` | GET by id and by account |
| `DegradationTests.cs` | GET still works when Account Service fails |
| `InboundResilienceTests.cs` | Inbound bulkhead and per-client rate limit → 429 |
| `HealthTests.cs` | `/health` returns database status |

### Account Service tests (`AccountService.Tests`)

| Test file | What it proves |
|-----------|----------------|
| `IdempotencyTests.cs` | Duplicate `eventId` on transactions |
| `OutOfOrderBalanceTests.cs` | Submit events in reverse time order → correct balance |
| `BalanceEndpointTests.cs` | GET balance |
| `DebitBalanceTests.cs` | Debits reduce balance |
| `AccountDetailEndpointTests.cs` | GET account + recent transactions |
| `AccountValidationTests.cs` | Bad transaction payloads → 400 |
| `HealthEndpointTests.cs` | `/health` returns database status |
| `LegacySchemaMigrationTests.cs` | Legacy DB without `Balance` column patched on startup |

### Integration tests (`EventLedger.IntegrationTests`)

These test the **real HTTP path** through the Gateway with a stub or capturing handler for the Account Service:

| Test file | What it proves |
|-----------|----------------|
| `EndToEndEventFlowTests.cs` | Full happy path |
| `IdempotencyIntegrationTests.cs` | Duplicate `eventId` end-to-end through Gateway |
| `TracePropagationTests.cs` | `traceparent` header forwarded |
| `ResiliencyTests.cs` | Outbound retry (up to 4 attempts), 503, circuit breaker opens |
| `OutOfOrderGatewayIntegrationTests.cs` | Listing order across services |
| `HealthIntegrationTests.cs` | Health endpoints for Gateway and Account |
| `AccountServiceUnavailableTests.cs` | Balance/account reads fail when Account Service is stopped or unreachable |

Run all tests (**48 total** — 21 Gateway + 11 Account + 16 Integration):

```powershell
dotnet test src/EventLedger.sln
```

When reading a test, start with the **method name** — it describes the scenario in plain English (e.g. `PostEvent_DuplicateEventId_ReturnsOriginalEventWithoutSecondAccountCall`).

---

## Glossary

| Term | Meaning in this project |
|------|-------------------------|
| **Endpoint** | A URL path + HTTP method handler (`POST /events`) |
| **Service class** | Business logic (`EventService`, `TransactionService`) |
| **DbContext** | Entity Framework gateway to the database |
| **Idempotency** | Calling the same operation twice has the same effect as once |
| **ProblemDetails** | Standard JSON error response (RFC 7807 style) |
| **Middleware** | Code that runs on every HTTP request before/after endpoints |
| **Polly** | Library for retries, timeouts, circuit breakers (outbound) |
| **Rate limiter** | ASP.NET middleware that rejects excess inbound requests (429) |
| **Bulkhead** | Concurrency cap isolating resource usage (inbound global limit; outbound via Polly isolation patterns) |
| **Dependency injection** | Framework supplies object instances (e.g. `EventService`) automatically |
| **WebApplicationFactory** | Test helper that hosts the app in-process for automated tests |
| **traceparent** | W3C header that links spans across services in one trace |

---

## Suggested reading order

Work through these files in sequence on your first pass:

1. `src/EventLedger.Contracts/Events/EventRequest.cs` — know the input shape
2. `src/EventGateway/Program.cs` — see how the app starts
3. `src/EventGateway/Endpoints/EventEndpoints.cs` — see the HTTP surface
4. `src/EventGateway/Services/EventService.cs` — understand the write path
5. `src/EventGateway/Clients/AccountServiceClient.cs` — see the service call
6. `src/AccountService/Endpoints/AccountEndpoints.cs` — Account Service HTTP surface
7. `src/AccountService/Services/TransactionService.cs` — balance + idempotency
8. `src/AccountService/Data/AccountDbInitializer.cs` — legacy DB migration
9. `src/EventGateway/Resilience/` — inbound limits + outbound Polly
10. `tests/EventGateway.Tests/IdempotencyTests.cs` — see expected behavior as code
11. `tests/EventLedger.IntegrationTests/EndToEndEventFlowTests.cs` — full flow

After that, explore `Middleware/`, `Metrics/`, and `Logging/` when you want to understand observability and error handling.

---

## Common questions

**Why two databases?**  
So each service can start, fail, and scale independently. The Gateway can serve read queries from `gateway.db` even if the Account Service is down.

**Why persist in the Gateway *after* calling Account Service?**  
If Account Service fails, we should not tell the client the event was stored. Persisting last avoids inconsistent state.

**Why store events in the Gateway at all if Account Service has transactions?**  
The Gateway is the client-facing audit log. Clients query `/events`; they never talk to Account Service directly in production Docker setup.

**Where is authentication?**  
Not implemented — out of scope for this take-home. In production you would add it at the Gateway.

**Where do I add a new validation rule?**  
Gateway: `EventRequestValidator.cs`. Account Service: `TransactionRequestValidator.cs`. Add a test in the matching `ValidationTests.cs` file.

---

## Next steps

- Run the app locally and set breakpoints in `EventService.SubmitEventAsync`
- Step through [`demo/EventLedger.http`](../demo/EventLedger.http) (REST Client) or import the Postman collection
- Change a validation rule and watch the matching test fail, then pass again
- Read [design.md](../artifacts/03-architecture/design.md) for architecture decisions (DEC-01 through DEC-05)
- Skim [api-contracts.md](../artifacts/03-architecture/api-contracts.md) for full request/response examples
