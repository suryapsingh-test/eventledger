# Backlog — Event Ledger

Ordered work items for the take-home. IDs map to task tracker.

## Epic E1 — AI SDLC setup

| ID | Story | Skill | Priority |
|----|-------|-------|----------|
| T-01 | Create repo structure + ai-agent-skills | PM | P0 |
| T-02 | Sprint plan + backlog | PM | P0 |
| T-03 | Requirements + acceptance criteria | BA | P0 |
| T-04 | Architecture design + diagrams | Architect | P0 |
| T-05 | User architecture approval | User | P0 |

## Epic E2 — Account Service

| ID | Story | Skill | Priority |
|----|-------|-------|----------|
| T-06 | Scaffold Account Service + EF Core SQLite | Dev | P0 |
| T-07 | POST /accounts/{id}/transactions (idempotent) | Dev | P0 |
| T-08 | GET balance + GET account details | Dev | P0 |
| T-09 | Health + structured logging | Dev | P0 |

## Epic E3 — Event Gateway

| ID | Story | Skill | Priority |
|----|-------|-------|----------|
| T-10 | Scaffold Gateway + EF Core SQLite | Dev | P0 |
| T-11 | POST /events (validate, idempotency, forward) | Dev | P0 |
| T-12 | GET /events endpoints | Dev | P0 |
| T-13 | Polly circuit breaker + timeout | Dev | P0 |
| T-14 | OpenTelemetry trace propagation | Dev | P0 |
| T-15 | Auditing + custom metric | Dev | P0 |
| T-16 | Health + structured logging | Dev | P0 |

## Epic E4 — Code & security review

| ID | Story | Agent | Priority |
|----|-------|-------|----------|
| T-18a | Code quality review | REV-01 | P0 |
| T-18b | OWASP Top 10 security review | REV-02 | P0 |
| T-18c | Fix review defects | DEV-01..03 | P0 (if needed) |

## Epic E5 — Ops & delivery

| ID | Story | Skill | Priority |
|----|-------|-------|----------|
| T-17 | Docker Compose | Dev | P0 |
| T-18 | README (run, test, resiliency) | Dev | P0 |
| T-19 | Unit tests | QA | P0 |
| T-20 | Integration + resiliency + trace tests | QA | P0 |
| T-21 | Coverage reports | QA | P0 |
| T-22 | Complete ai-sdlc.md + git history | PM/Dev | P0 |

## Deferred (bonus)

- Jaeger/Zipkin visualization
- Prometheus metrics endpoint
- Rate limiting
- Pact contract tests
- Async queue fallback
