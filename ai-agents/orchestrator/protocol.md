# Inter-Agent Communication Protocol

All agents communicate **through the PM hub** (message log). Agents do not hand off directly without PM/Orchestrator awareness.

## Message types

| Type | When | Blocks workflow? |
|------|------|------------------|
| `ASSIGNMENT` | Orchestrator → Agent | No |
| `STATUS UPDATE` | Agent → PM (via Orchestrator) | No — triggers approval flow |
| `APPROVAL REQUEST` | Orchestrator → **Developer** | **Yes** — after every task |
| `CLARIFICATION REQUEST` | Agent → Developer (via PM) | **Yes** |
| `IMPEDIMENT` | Agent → PM | **Yes** until resolved |

## Developer approval (mandatory after every task)

No task is marked `Done` until the **developer** explicitly approves.

Flow:

```
Agent completes work
  → status: Awaiting Developer Approval
  → update session-context.md + active-task.json + task-tracker.json
  → APPROVAL REQUEST to developer
  → STOP (orchestrator does not dispatch next agent)
Developer: Approved | Changes needed | Rejected
  → log approval-log.md
  → if Approved: Done + next task
```

See [resume-protocol.md](resume-protocol.md).

## Message template

```markdown
### [TIMESTAMP] [AGENT-ID] → [TO] | TYPE

**Task:** T-XX
**Stage:** Sx — [name]
**Skill used:** ai-agent-skills/.../SKILL.md

**Summary:** One paragraph.

**Deliverables:**
- path/to/file

**Context updated:** artifacts/context/session-context.md

**Next (after developer approval):** T-YY — [AGENT-ID]

**Questions:** (if CLARIFICATION REQUEST)
1. ...
```

## Logging

| File | Purpose |
|------|---------|
| `artifacts/agent-comms/message-log.md` | All agent messages |
| `artifacts/context/session-context.md` | Resume summary |
| `artifacts/context/approval-log.md` | Developer decisions |
| `artifacts/01-sprint-plan/task-tracker.json` | Task + stage state |

Orchestrator runs `scripts/generate-dashboard.ps1` after tracker changes (full task breakdown from JSON).

## Parallel agents

When DEV-01 and DEV-02 run in parallel:

- Each posts STATUS UPDATE with **only their file paths**
- Orchestrator waits for **both** before **one** APPROVAL REQUEST for the wave
- If conflict → IMPEDIMENT → developer or DEV-03 resolves

## Agent on-completion (all agents)

Every agent MUST before finishing:

1. List deliverables
2. **Not** mark task `Done` — orchestrator sets `Awaiting Developer Approval`
3. **Not** start next task
