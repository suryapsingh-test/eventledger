# Parallel Work Plan — Event Ledger

Defines which agents run **concurrently** and merge rules to avoid conflicts.

## Phase sequence (sequential gates)

```
ORCH → PM → BA → ARCH → [USER APPROVAL] → DEV (parallel) → REV (parallel) → [DEV fixes if needed] → QA (parallel) → PM review
```

## Code & security review — parallel lanes

### Review wave 1 (run together, after T-18)

| Agent | Owns | Skill |
|-------|------|-------|
| **REV-01** Code Reviewer | `artifacts/05-code-review/code-review-report.md`, code section of `defects.md` | code-reviewer |
| **REV-02** Security Reviewer | `artifacts/05-code-review/security-report.md`, OWASP section of `defects.md` | security-reviewer |

**Read-only on `src/`** — no code changes. Merge defects into single `defects.md` (REV-02 appends security items).

**Gate:** If Critical/Major defects → Orchestrator assigns DEV remediation (T-18c) before QA.

### Remediation (conditional)

| Agent | Action |
|-------|--------|
| DEV-01..03 | Fix defects per ownership; re-run REV-01 or REV-02 spot-check if needed |

## Development — parallel lanes

### Wave 1 (run together)

| Agent | Owns | Must NOT touch |
|-------|------|----------------|
| **DEV-02** Account Developer | `src/AccountService/**`, `tests/AccountService.Tests/**` | EventGateway, docker-compose (except env vars doc) |
| **DEV-01** Gateway Developer | `src/EventGateway/**`, `tests/EventGateway.Tests/**` | AccountService internals |

**Shared dependency:** `src/EventLedger.Contracts/` — owned by **DEV-03**; wave 1 agents may *consume* DTOs only. If Contracts missing, DEV-03 runs first (short spike), then wave 1.

**Merge rule:** Each agent commits only their paths. PM resolves conflicts via orchestrator.

### Wave 2 (after wave 1 compiles)

| Agent | Owns |
|-------|------|
| **DEV-03** Platform Developer | `src/EventLedger.Contracts/**`, `docker-compose.yml`, `src/EventGateway` Polly/OTel wiring, cross-cutting README sections |

DEV-03 integrates Gateway → Account HTTP client, trace propagation, Docker Compose.

## QA — parallel lanes

### Wave 1 (run together)

| Agent | Owns |
|-------|------|
| **QA-01** | `tests/EventGateway.Tests/**` — validation, idempotency, GET ordering |
| **QA-02** | `tests/AccountService.Tests/**` — transactions, balance, idempotency |

### Wave 2 (run together)

| Agent | Owns |
|-------|------|
| **QA-03** | `tests/EventLedger.IntegrationTests/**` — E2E POST → balance |
| **QA-04** | Resiliency + trace tests (may extend Gateway or Integration test project) |

**Coverage merge:** QA-01 reports `coverage/unit-gateway/`; QA-02 → `coverage/unit-account/`; QA-03/04 → `coverage/integration/`. PM aggregates in `artifacts/07-qa/test-results.md`.

## Orchestrator commands (Cursor)

**Launch parallel dev:**

```
Task × 2 (generalPurpose):
  - Agent DEV-02: read dev-account-agent.md + developer SKILL.md
  - Agent DEV-01: read dev-gateway-agent.md + developer SKILL.md
Wait for both → DEV-03 Platform Developer
```

**Launch parallel review:**

```
Task × 2 (generalPurpose, readonly: true):
  - Agent REV-01: read rev-code-agent.md + code-reviewer/SKILL.md
  - Agent REV-02: read rev-security-agent.md + security-reviewer/SKILL.md (OWASP Top 10)
Wait for both → if defects Critical/Major → DEV fix cycle → else QA wave 1
```

**Launch parallel QA:**

```
Task × 2: QA-01 and QA-02
Then Task × 2: QA-03 and QA-04
```

## File ownership summary

```
src/EventLedger.Contracts/     → DEV-03
src/AccountService/            → DEV-02
src/EventGateway/              → DEV-01 (API), DEV-03 (Polly/OTel/Docker client)
docker-compose.yml             → DEV-03
tests/EventGateway.Tests/      → QA-01 (+ DEV-01 smoke)
tests/AccountService.Tests/    → QA-02 (+ DEV-02 smoke)
tests/EventLedger.IntegrationTests/ → QA-03, QA-04
artifacts/05-code-review/          → REV-01, REV-02
```
