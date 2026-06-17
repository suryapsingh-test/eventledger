# Context & Resume — Event Ledger

Persistent state so the **developer can resume** without restarting from scratch.

## Files

| File | Updated by | Purpose |
|------|------------|---------|
| [session-context.md](session-context.md) | ORCH-01 after every task | **Read first** — human resume summary |
| [active-task.json](active-task.json) | ORCH-01 | Current task pointer + approval state |
| [approval-log.md](approval-log.md) | ORCH-01 on developer decision | Audit trail of your approvals |
| [../01-sprint-plan/task-tracker.json](../01-sprint-plan/task-tracker.json) | ORCH-01 | Full task list by stage |

## Developer approval gate (mandatory)

After **every** task completes:

1. Agent sets status → `Awaiting Developer Approval`
2. Context files updated with deliverables to review
3. Orchestrator **stops** and asks you explicitly
4. You respond: **Approved** | **Changes needed** | **Rejected**
5. Only on **Approved** → task marked `Done`, next task assigned

Parallel waves (e.g. DEV-01 ∥ DEV-02): approval requested **once** when the whole wave is complete.

## Resume

```
Resume Event Ledger
```

or

```
Use event-ledger orchestrator skill. Read artifacts/context/session-context.md and continue.
```

Orchestrator reads context → does not redo completed tasks → continues from `currentTaskId`.

## Developer notes section

Edit the **Developer notes** section at the bottom of `session-context.md` — agents preserve it.
