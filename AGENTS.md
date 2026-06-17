# Event Ledger — Cursor Agents

**Orchestrator-controlled** multi-agent AI SDLC with **task tracker**, **context files**, and **developer approval after every task**.

**Resume anytime:** `Resume Event Ledger`  
**Live dashboard:** [dashboard/project-dashboard.html](dashboard/project-dashboard.html)  
**Context (read first):** [artifacts/context/session-context.md](artifacts/context/session-context.md)

## Start or resume

```
Resume Event Ledger
```

ORCH-01 reads `session-context.md` + `task-tracker.json` and continues — **never restarts from scratch**.

## After each task

Orchestrator **stops** and asks you:

> Task T-XX complete. Review deliverables. **Approved** | **Changes needed** | **Rejected**

Logged in [approval-log.md](artifacts/context/approval-log.md).

## State files

| File | Role |
|------|------|
| `artifacts/context/session-context.md` | Human resume summary |
| `artifacts/context/active-task.json` | Current task pointer |
| `artifacts/01-sprint-plan/task-tracker.json` | Stages S1–S7 + task status |
| `artifacts/context/approval-log.md` | Your decisions |

## Agents

| ID | Role |
|----|------|
| ORCH-01 | Orchestrator — workflow, context, approvals |
| PM-01 … QA-04 | See [roster.md](ai-agents/roster.md) |
| REV-01, REV-02 | Code + OWASP review |

## Install skills

```powershell
.\scripts\install-cursor-skills.ps1
```
