# AI-Assisted SDLC Log — Event Ledger

This document records how each AI agent skill was used, what it produced, and what was reviewed or changed by the developer.

## Workflow

```
ORCH → PM → BA → ARCH → [USER APPROVAL]
     → DEV-02 ∥ DEV-01 → DEV-03
     → REV-01 ∥ REV-02 → [DEV fixes if needed]
     → QA-01 ∥ QA-02 → QA-03 ∥ QA-04 → PM review
```

Agent roster: [ai-agents/roster.md](../ai-agents/roster.md)  
Message log: [artifacts/agent-comms/message-log.md](../artifacts/agent-comms/message-log.md)  
**Resume:** [artifacts/context/session-context.md](../artifacts/context/session-context.md)  
**Live dashboard:** [dashboard/project-dashboard.html](../dashboard/project-dashboard.html)

## Phase log

| Agent | Skill | Date | Outputs | AI contribution | Human review |
|-------|-------|------|---------|-----------------|--------------|
| PM-01 | [pm-planning](../ai-agent-skills/pm-planning/SKILL.md) | 2026-06-15 | sprint-plan, backlog | Initial plan | — |
| ORCH-01 | [orchestrator](../ai-agent-skills/orchestrator/SKILL.md) | 2026-06-15 | ai-agents/, parallel workplan | Multi-agent system | — |
| BA-01 | [business-analyst](../ai-agent-skills/business-analyst/SKILL.md) | 2026-06-16 | requirements/, acceptance-criteria, edge-cases | Full requirements phase | Approved |
| ARCH-01 | [solution-architect](../ai-agent-skills/solution-architect/SKILL.md) | 2026-06-16 | design.md, api-contracts, data-model, diagrams | Full architecture | Approved |
| DEV-01 | [developer](../ai-agent-skills/developer/SKILL.md) | 2026-06-16 | src/EventGateway/ | Gateway dev-wave-1 | Approved |
| DEV-02 | [developer](../ai-agent-skills/developer/SKILL.md) | 2026-06-16 | src/AccountService/ | Account dev-wave-1 | Approved |
| DEV-03 | [developer](../ai-agent-skills/developer/SKILL.md) | 2026-06-16 | Contracts, Polly, OTel, Docker | Platform dev-wave-2 | Approved |
| REV-01 | [code-reviewer](../ai-agent-skills/code-reviewer/SKILL.md) | 2026-06-16 | code-review-report, defects | Code review | Approved |
| REV-02 | [security-reviewer](../ai-agent-skills/security-reviewer/SKILL.md) | 2026-06-16 | security-report, OWASP | Security review | Approved |
| QA-01 | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | 2026-06-16 | EventGateway.Tests (18 tests) | Gateway unit QA | Approved |
| QA-02 | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | 2026-06-16 | AccountService.Tests (9 tests) | Account unit QA | Approved |
| QA-03 | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | 2026-06-17 | IntegrationTests (7 E2E) | Integration QA | Approved |
| QA-04 | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | 2026-06-17 | Resiliency + trace (6 tests) | Resiliency QA | Approved |
| QA-01 | [qa-engineer](../ai-agent-skills/qa-engineer/SKILL.md) | 2026-06-17 | coverage-summary.md | Coverage aggregation | Approved |
| PM-01 | [pm-planning](../ai-agent-skills/pm-planning/SKILL.md) | 2026-06-17 | sprint-closeout.md | Sprint closeout | Approved |

> **Post-sprint enhancements** (inbound resilience, O(1) balance, legacy DB migration, expanded tests): **48 tests** total as of final check-in — see [artifacts/07-qa/coverage-summary.md](../artifacts/07-qa/coverage-summary.md) and [README.md](../README.md). QA row counts above reflect deliverables at original sprint close (2026-06-16/17).

## Example prompt snippets

### PM
```
Use event-ledger pm-planning skill. Read docs/event-ledger-handout.md and create sprint plan + backlog.
```

### Architect
```
Use event-ledger solution-architect skill. Produce artifacts/03-architecture/design.md only — no code.
```

### Developer
```
Use event-ledger developer skill. Architecture approved. Implement from artifacts/03-architecture/.
```

### Resume (any session)
```
Resume Event Ledger
```

### Review
```
You are REV-02. Apply ai-agent-skills/security-reviewer/SKILL.md — OWASP Top 10.
```

## Definition of Done (from PM)

### Application
- [x] Event Gateway + Account Service run via Docker Compose
- [x] Idempotency, out-of-order, validation, balance rules correct
- [x] Circuit breaker + timeout on Gateway → Account Service
- [x] Trace ID propagated and logged
- [x] Structured JSON logging + health + custom metric
- [x] Auditing on Gateway
- [x] `dotnet test` passes with coverage reports (48/48 as of post-sprint enhancements)

### AI-SDLC process
- [x] 9 skills, 13 agents, orchestrator workflow
- [x] `task-tracker.json` + `session-context.md` maintained
- [x] Developer approval logged in `approval-log.md`
- [x] Code review + OWASP security reports
- [x] Dashboard + message log updated per task
- [ ] Meaningful git commit history (developer action)

---

## Post-sprint enhancements (after initial closeout)

| Enhancement | Summary |
|-------------|---------|
| O(1) balance | `Account.Balance` maintained in `TransactionService` |
| Native decimals | EF `HasPrecision(19,4)` on money columns |
| Outbound retry | Polly retry + jitter on Account Service HttpClient |
| Inbound limits | Concurrency bulkhead + per-client write rate limit (429) |
| Tests | 46 total — see `InboundResilienceTests`, `LegacySchemaMigrationTests`, updated `ResiliencyTests` |
