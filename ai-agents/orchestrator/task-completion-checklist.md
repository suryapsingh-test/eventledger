# Task Completion Checklist (all agents)

When you finish your assigned task, do **only** this — orchestrator handles approval and next steps.

## Do

1. List all deliverable file paths
2. Post `STATUS UPDATE` to `artifacts/agent-comms/message-log.md` (include task ID, stage, deliverables)
3. Stop — return control to ORCH-01

## Do not

- Mark task `Done` in tracker (orchestrator sets `Awaiting Developer Approval` first)
- Dispatch or start the next agent
- Skip context update (orchestrator updates `session-context.md`, `active-task.json`, `task-tracker.json`)

## Orchestrator then

1. Sets status → `Awaiting Developer Approval`
2. Updates context files
3. Asks **developer** for approval
4. On **Approved** only → `Done` + next task

See `ai-agents/orchestrator/resume-protocol.md`.
