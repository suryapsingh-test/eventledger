# Event Ledger
An Event Ledger system composed of two microservices.Two .NET microservices that process financial transaction events with idempotency, out-of-order tolerance, observability, and resiliency.

---

## How to read this repo

Use this guide depending on your goal:

| If you want to… | Start here | Then read |
|-----------------|------------|-----------|
| **Run and test the app** | [Setup / run / test](#setup--run--test) below | Run `dotnet test`, try the [API examples](#how-to-use-the-api) |
| **Understand the design** | [Architecture overview](#architecture-overview) | [artifacts/03-architecture/design.md](artifacts/03-architecture/design.md) |
| **Walk through the code** | [docs/code-walkthrough.md](docs/code-walkthrough.md) | Follow the suggested reading order at the end of the guide |
| **Review the code** | [Project layout](#project-layout) | `src/EventGateway/` → `src/AccountService/` → `src/EventLedger.Contracts/` |
| **Review test coverage** | [Tests](#tests) | `tests/EventGateway.Tests/`, `tests/AccountService.Tests/`, `tests/EventLedger.IntegrationTests/` |
| **Review AI-assisted process** | [Agentic workflow](#agentic-workflow-how-this-project-is-built) | [docs/ai-sdlc.md](docs/ai-sdlc.md), `artifacts/`, [dashboard/project-dashboard.html](dashboard/project-dashboard.html) |

**Suggested 10-minute review path**

1. Read [Architecture overview](#architecture-overview) (services, endpoints, core behaviors).
2. Run `dotnet test src/EventLedger.sln` — all 40 tests should pass.
3. Skim `src/EventGateway/Services/EventService.cs` (write path) and `src/AccountService/Services/TransactionService.cs` (balance + idempotency).
4. Optional: read [docs/code-walkthrough.md](docs/code-walkthrough.md) for a full guided tour.
5. Optional: `docker compose up --build` and exercise the [API examples](#how-to-use-the-api).

**Key files at a glance**

| Area | Where to look |
|------|---------------|
| Gateway API | `src/EventGateway/Endpoints/EventEndpoints.cs` |
| Idempotency + persist order | `src/EventGateway/Services/EventService.cs` |
| Resiliency (Polly) | `src/EventGateway/Program.cs`, `src/EventGateway/Clients/AccountServiceClient.cs` |
| Balance computation | `src/AccountService/Services/BalanceCalculator.cs` |
| Shared contracts | `src/EventLedger.Contracts/` |
| Architecture decisions | `artifacts/03-architecture/design.md` |

---

## Architecture overview

Event Ledger receives financial transaction events from upstream systems that may deliver **duplicate** or **out-of-order** events. Two independently deployable services handle ingestion and account state:

```
                          ┌──────────────────────┐
Browser / Client ──────→  │  Event Gateway API    │
                          │  (public-facing)      │
                          └──────┬───────────────┘
                                 │ REST (sync)
                                 ▼
                          ┌──────────────────────┐
                          │  Account Service      │
                          │  (internal)           │
                          └──────────────────────┘
```

Each service runs as its own process with a **separate SQLite database** (`gateway.db` and `account.db`). They do not share storage or in-process state. Shared request/response types live in `EventLedger.Contracts`.

### Event Gateway (public)

Entry point for all client traffic. Validates events, enforces idempotency, stores an audit record, and forwards transactions to the Account Service.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/events` | Submit a transaction event |
| `GET` | `/events/{eventId}` | Retrieve a single event by ID |
| `GET` | `/events?account={accountId}` | List events for an account, ordered by `eventTimestamp` |
| `GET` | `/health` | Health check (includes database connectivity) |

### Account Service (internal)

Manages account balances and transaction history. Called only by the Gateway — not exposed to external clients in the Docker Compose setup.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/accounts/{accountId}/transactions` | Apply a transaction to an account |
| `GET` | `/accounts/{accountId}/balance` | Get the current balance for an account |
| `GET` | `/accounts/{accountId}` | Get account details and recent transactions |
| `GET` | `/health` | Health check (includes database connectivity) |

### Request flow — `POST /events`

1. **Validate** the payload (required fields, `CREDIT`/`DEBIT`, amount > 0, ISO 8601 timestamp).
2. **Idempotency check** — if `eventId` already exists, return the original event (`200 OK`, `Idempotency-Replay: true`).
3. **Apply transaction** — call Account Service with Polly timeout + circuit breaker. On failure, return `503` and **do not** persist the event.
4. **Persist** the event record in the Gateway database and return `201 Created`.

Read paths (`GET /events/...`) query the Gateway database only, so they continue to work when the Account Service is unavailable.

### Core behaviors

| Concern | Behavior |
|---------|----------|
| **Idempotency** | Same `eventId` never creates a duplicate or changes balance; replays return the original response |
| **Out-of-order events** | Event listings sorted by `eventTimestamp`; balance = sum(CREDITs) − sum(DEBITs) regardless of arrival order |
| **Graceful degradation** | `POST /events` → `503` when Account Service is down; `GET /events` still serves local data |
| **Tracing** | OpenTelemetry generates a trace at the Gateway and propagates W3C `traceparent` to Account Service; both services log trace ID in JSON output |

Full design details: [artifacts/03-architecture/design.md](artifacts/03-architecture/design.md)

---

## Application stack

- C# / .NET 8
- ASP.NET Core Web API (Event Gateway + Account Service)
- EF Core + SQLite (separate DB per service)
- Polly (circuit breaker + timeout)
- Serilog (structured JSON logging)
- OpenTelemetry (distributed tracing)
- xUnit + WebApplicationFactory (tests)

## Setup / run / test

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (optional, for containerized run)

### Local development

```powershell
# Restore and build the solution
dotnet build src/EventLedger.sln

# Run Account Service (terminal 1)
dotnet run --project src/AccountService/AccountService.csproj --urls http://localhost:8081

# Run Event Gateway (terminal 2)
dotnet run --project src/EventGateway/EventGateway.csproj --urls http://localhost:8080
```

Gateway listens on **8080**; Account Service on **8081**. Each service uses a local SQLite file in its working directory.

### Docker Compose

Build and start both services with persistent SQLite volumes:

```powershell
docker compose up --build
```

| Service | Host port | Internal URL |
|---------|-----------|--------------|
| Event Gateway | 8080 | `http://localhost:8080` |
| Account Service | (internal only) | `http://account-service:8081` |

Gateway waits for Account Service health before starting (`depends_on: service_healthy`).

### How to use the API

All client traffic goes through the **Event Gateway** on port **8080**. The Account Service on **8081** is internal — use it only when running both services locally for debugging.

**1. Check health**

```powershell
curl http://localhost:8080/health
curl http://localhost:8081/health   # local dev only
```

**2. Submit a transaction event**

```powershell
curl -X POST http://localhost:8080/events `
  -H "Content-Type: application/json" `
  -d '{"eventId":"evt-001","accountId":"acct-123","type":"CREDIT","amount":150.00,"currency":"USD","eventTimestamp":"2026-05-15T14:02:11Z","metadata":{"source":"demo"}}'
```

- **201 Created** — new event applied and stored.
- **200 OK** — duplicate `eventId`; response includes original event. Response header: `Idempotency-Replay: true`.
- **400 Bad Request** — validation error (missing field, bad type, amount ≤ 0, invalid timestamp).
- **503 Service Unavailable** — Account Service unreachable; event is **not** stored.

**3. Retrieve a single event**

```powershell
curl http://localhost:8080/events/evt-001
```

**4. List events for an account** (ordered by `eventTimestamp`)

```powershell
curl "http://localhost:8080/events?account=acct-123"
```

**5. Check balance** (Account Service — local dev only)

```powershell
curl http://localhost:8081/accounts/acct-123/balance
curl http://localhost:8081/accounts/acct-123
```

**6. Try idempotency** — submit the same `eventId` twice; second call returns `200` with `Idempotency-Replay: true` and the same body.

**7. Try out-of-order events** — submit events with different `eventTimestamp` values in reverse order; listing and balance remain correct.

**Event payload reference**

| Field | Required | Notes |
|-------|----------|-------|
| `eventId` | Yes | Unique idempotency key |
| `accountId` | Yes | Target account |
| `type` | Yes | `CREDIT` or `DEBIT` |
| `amount` | Yes | Must be > 0, max 2 decimal places |
| `currency` | Yes | e.g. `USD` |
| `eventTimestamp` | Yes | ISO 8601 (when the event originally occurred) |
| `metadata` | No | Optional key/value context |

### Tests

```powershell
dotnet test src/EventLedger.sln
```

**40 tests** across three projects:

| Project | Coverage |
|---------|----------|
| `EventGateway.Tests` | Idempotency, validation, event queries, health, graceful degradation |
| `AccountService.Tests` | Idempotency, out-of-order balance, validation, balance/account endpoints, health |
| `EventLedger.IntegrationTests` | End-to-end Gateway → Account flow, trace propagation, resiliency (503, circuit breaker) |

### Resiliency pattern (Gateway → Account Service)

The Gateway uses **Polly** on the `AccountService` `HttpClient`:

| Policy | Setting | Rationale |
|--------|---------|-----------|
| **Timeout** | 5 seconds per attempt | Fail fast when Account Service is slow; return 503 to clients |
| **Circuit breaker** | Open after 5 consecutive failures; 30s break | Stop hammering a failing dependency; protect thread pool |
| **Retry** | None on POST | Avoid duplicate side effects; client retries + idempotency handle recovery |

When the circuit is open or a timeout occurs, `POST /events` returns **503 ProblemDetails** and the event is **not** persisted. Read paths (`GET /events`) continue to serve from the Gateway's local SQLite database.

### Observability

- **Serilog** JSON logs with `TraceId`, `ServiceName`, and structured properties
- **OpenTelemetry** ASP.NET Core + HttpClient instrumentation (W3C `traceparent` propagation)
- **Custom metrics** on Gateway: `eventledger.events.processed`, `eventledger.events.failed` (OpenTelemetry console exporter in dev)

---

## Project layout

```
EventLedger/
├── src/
│   ├── EventLedger.sln
│   ├── EventLedger.Contracts/   # Shared API contracts
│   ├── EventGateway/            # Public API
│   └── AccountService/          # Internal account state
├── tests/
│   ├── EventGateway.Tests/
│   ├── AccountService.Tests/
│   └── EventLedger.IntegrationTests/
├── artifacts/03-architecture/   # Design docs, API contracts, diagrams
├── docker-compose.yml
└── README.md
```

---

## Agentic workflow (how this project is built)

This submission is built using a **multi-agent AI SDLC**. Multiple specialized agents collaborate under a single **Orchestrator** that controls sequencing, parallel work, gates, and the progress dashboard.

**Live dashboard:** [`dashboard/project-dashboard.html`](dashboard/project-dashboard.html)  
**Resume:** [`artifacts/context/session-context.md`](artifacts/context/session-context.md)

### The three layers

| Layer | Location | What it is |
|-------|----------|------------|
| **Orchestrator** | `ai-agents/orchestrator/` + [orchestrator skill](ai-agent-skills/orchestrator/SKILL.md) | **Controls the workflow** — assigns agents, enforces gates, launches parallel workers, updates dashboard |
| **Agents** | [`ai-agents/agents/`](ai-agents/agents/) | **Who** does the work — PM, BA, Architect, 3 Developers, 4 QAs |
| **Skills** | [`ai-agent-skills/`](ai-agent-skills/) | **How** they work — procedures each agent loads and follows |

Agents are not separate applications. In Cursor, each agent is a **scoped AI session** (or Task subagent) that reads its agent definition, applies its skill, writes artifacts, and reports back to the Orchestrator.

### Who controls what?

```
                         ┌──────────────────────────────┐
                         │   ORCH-01 Orchestrator        │
                         │   • Reads task-tracker        │
                         │   • Dispatches next agent(s)  │
                         │   • Enforces approval gates   │
                         │   • Updates HTML dashboard    │
                         └──────────────┬───────────────┘
                                        │
              ┌─────────────────────────┼─────────────────────────┐
              ▼                         ▼                         ▼
         ┌─────────┐              ┌─────────┐              ┌─────────┐
         │ PM-01   │              │ BA-01   │              │ ARCH-01 │
         │ planning│              │ reqs    │              │ design  │
         └────┬────┘              └─────────┘              └────┬────┘
              │ hub                                            │
              │         ┌──────────────── USER APPROVAL ─────────┘
              │         ▼
              │    ┌─────────┐     ┌─────────┐
              │    │ DEV-01  │ ∥   │ DEV-02  │   parallel wave 1
              │    │ Gateway │     │ Account │
              │    └────┬────┘     └────┬────┘
              │         └────────┬──────┘
              │                  ▼
              │            ┌─────────┐
              │            │ DEV-03  │   platform (Polly, Docker, contracts)
              │            └────┬────┘
              │                  ▼
              │    ┌─────────┐     ┌─────────┐
              │    │ REV-01  │ ∥   │ REV-02  │   code + OWASP review
              │    └────┬────┘     └────┬────┘
              │         └────────┬──────┘
              │                  ▼ (DEV fixes if needed)
              │    ┌─────────┐     ┌─────────┐
              │    │ QA-01   │ ∥   │ QA-02   │   parallel QA wave 1
              │    └────┬────┘     └────┬────┘
              │         └────────┬──────┘
              │    ┌─────────┐     ┌─────────┐
              │    │ QA-03   │ ∥   │ QA-04   │   parallel QA wave 2
              │    └─────────┘     └─────────┘
              ▼
    artifacts/agent-comms/message-log.md
    dashboard/project-dashboard.html
```

**ORCH-01** is the workflow controller. **PM-01** is the sprint hub (tracker, backlog). All agents report via the [message log](artifacts/agent-comms/message-log.md); the Orchestrator updates the [HTML dashboard](dashboard/project-dashboard.html) after every status change.

### End-to-end flow

| Step | Controller action | Agent(s) | Output |
|------|-------------------|----------|--------|
| 1 | ORCH dispatches | PM-01 | Sprint plan, backlog, tracker |
| 2 | ORCH dispatches | BA-01 | Requirements, acceptance criteria |
| 3 | ORCH dispatches | ARCH-01 | design.md, API contracts, diagrams |
| 4 | **Gate** — ORCH stops | User | Approve architecture |
| 5 | ORCH launches **parallel** Task agents | DEV-01 ∥ DEV-02 | Gateway + Account Service |
| 6 | ORCH dispatches | DEV-03 | Contracts, Polly, OTel, Docker |
| 7 | ORCH launches **parallel** review agents | REV-01 ∥ REV-02 | Code review + OWASP Top 10 report |
| 8 | ORCH dispatches (if needed) | DEV-01..03 | Fix Critical/Major defects |
| 9 | ORCH launches **parallel** QA agents | QA-01 ∥ QA-02, then QA-03 ∥ QA-04 | Tests + coverage |
| 10 | ORCH dispatches | PM-01 | Sprint review |

Parallel file ownership is defined in [`ai-agents/parallel-workplan.md`](ai-agents/parallel-workplan.md) so agents do not conflict.

### Task tracker, context & resume

Work is **never lost between sessions**. The orchestrator maintains:

| File | Purpose |
|------|---------|
| [`artifacts/01-sprint-plan/task-tracker.json`](artifacts/01-sprint-plan/task-tracker.json) | Tasks by **stage** (S1–S7), machine-readable |
| [`artifacts/context/session-context.md`](artifacts/context/session-context.md) | **Read first to resume** — what's done, what's next |
| [`artifacts/context/active-task.json`](artifacts/context/active-task.json) | Current task + approval state |
| [`artifacts/context/approval-log.md`](artifacts/context/approval-log.md) | Your approve / changes decisions |

**After every task completes**, the orchestrator **stops and asks you to approve** before the next task runs. Reply:

- **Approved** — continue
- **Changes needed** — agent re-runs with your feedback
- **Rejected** — stop and discuss

**To resume** (any time, any session):

```
Resume Event Ledger
```

Orchestrator reads context files and continues from the current task — **does not start from the beginning**.

### How to run the agentic workflow

**1. Install skills into Cursor (one time):**

```powershell
cd C:\Users\surya\source\repos\EventLedger
.\scripts\install-cursor-skills.ps1
```

**2. Start or resume:**

```
Resume Event Ledger
```

or

```
Use event-ledger orchestrator skill. Continue from session-context.md.
```

The Orchestrator reads **context + tracker**, assigns the next agent, and after each completion **asks for your approval** before proceeding.

**3. Approve each task** when prompted (see deliverables in chat or `session-context.md`).

**4. Watch progress:**

```powershell
Start-Process dashboard\project-dashboard.html
```

Dashboard auto-refreshes every 5 seconds (see [`dashboard/dashboard-config.json`](dashboard/dashboard-config.json)).

**5. Run a single agent manually (orchestrator still handles approval):**

```
You are BA-01 Business Analyst Agent. Read ai-agents/agents/ba-agent.md and apply ai-agent-skills/business-analyst/SKILL.md.
```

### Submission evidence (AI-augmented SDLC)

| Schwab deliverable | Where reviewers find it |
|--------------------|-------------------------|
| Design Agent | `artifacts/03-architecture/` (from ARCH-01) |
| Development Agent | `src/`, logging, auditing (from DEV-01..03) |
| Code / Security review | `artifacts/05-code-review/` (REV-01, REV-02 OWASP) |
| QA Agent | `tests/`, `coverage/` (from QA-01..04) |
| AI process proof | `ai-agents/`, `ai-agent-skills/`, `docs/ai-sdlc.md`, `dashboard/`, message log, git history |

More detail: [AGENTS.md](AGENTS.md) · [docs/ai-sdlc.md](docs/ai-sdlc.md) · [ai-agents/README.md](ai-agents/README.md)

### Application status

| Phase | Agent | Status |
|-------|-------|--------|
| Sprint planning | PM-01 | Done |
| Requirements | BA-01 | Done |
| Architecture | ARCH-01 | Done |
| Implementation (wave 1) | DEV-01, DEV-02 | Done |
| Implementation (wave 2) | DEV-03 | Done |
| Code & security review | REV-01, REV-02 | Done |
| Review defect fixes | DEV-01..03 | Done |
| QA unit tests (wave 1) | QA-01, QA-02 | Done |
| QA integration + resiliency | QA-03, QA-04 | Done |
| QA coverage | QA-01 | Done |
| Sprint closeout | PM-01 | Done |

See [dashboard/project-dashboard.html](dashboard/project-dashboard.html) for live tracker.
