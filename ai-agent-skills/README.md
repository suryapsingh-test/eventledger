# Event Ledger — AI Agent Skills

**Skills** = capabilities (how to work). **Agents** = workers who load skills — see [ai-agents/](../ai-agents/).

## Skills

| Skill | Used by agents | Invoke when |
|-------|----------------|-------------|
| [orchestrator](orchestrator/SKILL.md) | ORCH-01 | Run full workflow or parallel dispatch |
| [pm-planning](pm-planning/SKILL.md) | PM-01 | Sprint kickoff, tracker |
| [business-analyst](business-analyst/SKILL.md) | BA-01 | Requirements |
| [solution-architect](solution-architect/SKILL.md) | ARCH-01 | Design (no code) |
| [developer](developer/SKILL.md) | DEV-01, DEV-02, DEV-03 | Implementation (parallel lanes) |
| [code-reviewer](code-reviewer/SKILL.md) | REV-01 | Code quality review after dev |
| [security-reviewer](security-reviewer/SKILL.md) | REV-02 | OWASP Top 10 security review |
| [qa-engineer](qa-engineer/SKILL.md) | QA-01..QA-04 | Tests (parallel lanes) |

## Multi-agent workflow

```
ORCH → PM → BA → ARCH → [USER] → DEV-02 ∥ DEV-01 → DEV-03 → REV-01 ∥ REV-02 → [DEV fixes] → QA waves
```

1. **PM** — `artifacts/01-sprint-plan/sprint-plan.md`
2. **BA** — `artifacts/02-requirements/requirements.md`
3. **Architect** — `artifacts/03-architecture/design.md`
4. **User gate** — approve architecture before any code in `src/`
5. **Developer** — `src/`, logging, auditing, resiliency
6. **Review** — `artifacts/05-code-review/` (code + OWASP security)
7. **QA** — `tests/`, coverage reports in `coverage/`

Log each phase in [docs/ai-sdlc.md](../docs/ai-sdlc.md).

## Using in Cursor

**Option A — project skills (recommended while working):**

```powershell
# From EventLedger repo root
New-Item -ItemType Directory -Force -Path .cursor/skills
Copy-Item -Recurse ai-agent-skills/pm-planning .cursor/skills/
# ... repeat for each skill, or symlink
```

**Option B — explicit invocation in chat:**

```
Use event-ledger solution-architect skill. Produce design.md only — no code.
```

## Stack constraints (all skills)

- C# / .NET 8 only
- ASP.NET Core Web API
- EF Core + SQLite (one DB per service; no shared database)
- Synchronous REST between Gateway and Account Service
- Polly, Serilog, OpenTelemetry, xUnit

## Submission evidence

Reviewers should find:

- This folder (`ai-agent-skills/`) — the agent definitions
- `artifacts/` — outputs per phase
- `docs/ai-sdlc.md` — which skill ran, what changed after AI output
- Meaningful git commit history per phase
