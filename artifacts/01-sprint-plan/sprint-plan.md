# Sprint Plan — Event Ledger

**Sprint:** Event Ledger Take-Home  
**Date:** 2026-06-15  
**Stack:** C# / .NET 8 / ASP.NET Core / EF Core SQLite / Polly / Serilog / OpenTelemetry  

## Sprint goal

Deliver a working **Event Ledger** (Event Gateway + Account Service) that meets the candidate handout, plus visible **AI-augmented SDLC** evidence via multi-agent orchestration.

## Milestones

| # | Milestone | Agent(s) | Target artifact |
|---|-----------|----------|-----------------|
| M1 | Project kickoff + skills | PM-01, ORCH-01 | `ai-agent-skills/`, `ai-agents/`, `artifacts/01-sprint-plan/` |
| M2 | Requirements | BA-01 | `artifacts/02-requirements/` |
| M3 | Architecture | ARCH-01 | `artifacts/03-architecture/` |
| M4 | Architecture approval | Developer | `approval-log.md` (T-05) |
| M5 | Implementation | DEV-01..03 | `src/`, `docker-compose.yml` |
| M6 | Code + security review | REV-01, REV-02 | `artifacts/05-code-review/` (OWASP) |
| M7 | Tests + coverage | QA-01..04 | `tests/`, `coverage/` |
| M8 | Submission polish | PM-01 | README, ai-sdlc, git history |

## Workflow controls

- **Orchestrator (ORCH-01)** dispatches agents and maintains context files
- **Developer approval** after each task or approval group (see `task-tracker.json`)
- **Resume:** `artifacts/context/session-context.md`

## Definition of Done

### Functional
- [ ] POST /events with validation, idempotency, Account Service call
- [ ] GET /events/{id} and GET /events?account= ordered by eventTimestamp
- [ ] Account Service transactions, balance, account details
- [ ] Correct balance regardless of arrival order
- [ ] Graceful degradation when Account Service unavailable

### Non-functional
- [ ] Two independent processes, separate SQLite DBs
- [ ] Trace ID propagated Gateway → Account Service, logged in JSON
- [ ] GET /health on both services
- [ ] Custom metric exposed
- [ ] Circuit breaker + timeout (Polly) on downstream call
- [ ] Auditing on Gateway (receivedAt, status, payload)

### AI-SDLC / submission
- [ ] `ai-agent-skills/` (9 skills) + `ai-agents/` (13 agents)
- [ ] `artifacts/context/` — resume + approval log
- [ ] `dashboard/project-dashboard.html` maintained by orchestrator
- [ ] Code review + OWASP security review reports
- [ ] `docs/ai-sdlc.md` completed per phase
- [ ] Unit + integration tests, coverage reports
- [ ] Docker Compose runs both services
- [ ] Meaningful git commits (not one squash)

## Risks

| Risk | Mitigation |
|------|------------|
| Scope creep | Handout + AI-SDLC evidence first |
| Lost context between sessions | `session-context.md` + `task-tracker.json` |
| Inconsistent Gateway/Account state | Persist event only after Account Service success |

## Next step

**T-03** — ORCH-01 dispatches **BA-01** → `artifacts/02-requirements/`
