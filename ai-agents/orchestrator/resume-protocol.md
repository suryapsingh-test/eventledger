# Resume Protocol — Event Ledger

ORCH-01 **must** follow this on every run (new session, resume, or continue).

## 1. Read context (never skip)

```
artifacts/context/session-context.md      ← human summary (read first)
artifacts/context/active-task.json        ← current pointer
artifacts/01-sprint-plan/task-tracker.json
artifacts/context/approval-log.md
```

Do **not** redo tasks marked `Done`. Do **not** skip tasks marked `Awaiting Developer Approval` without developer decision.

## 2. Resume command

When developer says **Resume Event Ledger** or **Continue**:

1. Load context files above
2. If `awaitingDeveloperApproval: true` → remind developer what to review; **do not dispatch next agent**
3. If developer says **Approved** → log in `approval-log.md`, mark task `Done`, update context, dispatch next
4. If no task in progress and next is `Ready` → dispatch assigned agent
5. Update dashboard

## 3. After every task completes (mandatory)

### Agent / orchestrator updates (in order)

1. Set task status → `Awaiting Developer Approval` in `task-tracker.json` and `task-tracker.md`
2. Set `active-task.json`:
   - `awaitingDeveloperApproval: true`
   - `deliverablesForReview`: list of files
   - `status`: `Awaiting Developer Approval`
3. Rewrite `session-context.md`:
   - "What was completed last" section
   - "Awaiting your approval?" → **Yes**
   - List deliverables to open/review
   - "What to do next (after you approve)"
   - Preserve **Developer notes** section
4. Append `APPROVAL REQUEST` to `message-log.md`
5. Run `scripts/generate-dashboard.ps1`
6. **STOP** — ask developer:

```
Task [T-XX] complete — [title]
Agent: [AGENT-ID]
Deliverables:
  - path/to/file
  ...

Please review. Reply:
  • Approved — continue to next task
  • Changes needed — [your feedback]
  • Rejected — stop and discuss
```

### On developer "Approved"

1. Append row to `approval-log.md`
2. Set task `Done` in tracker JSON + MD
3. Set `awaitingDeveloperApproval: false` in `active-task.json`
4. Update `session-context.md` current stage/task to **next Ready task**
5. Dispatch next agent OR ask approval before parallel wave (see below)

### On "Changes needed"

1. Log decision in `approval-log.md`
2. Re-assign **same** agent with feedback
3. Task status → `In Progress`

## 4. Parallel waves — single approval per approvalGroup

Tasks with the same `approvalGroup` in `task-tracker.json` share **one** developer approval when all tasks in that group are `Done` or `Awaiting Developer Approval`.

| approvalGroup | Tasks | Agents |
|---------------|-------|--------|
| dev-wave-1 | T-06 … T-16 (DEV-01 + DEV-02 items) | DEV-01 ∥ DEV-02 |
| dev-wave-2 | T-13, T-14, T-17, T-18 | DEV-03 |
| review-wave-1 | T-18a, T-18b | REV-01 ∥ REV-02 |
| qa-wave-1 | T-19, T-19b | QA-01 ∥ QA-02 |
| qa-wave-2 | T-20, T-20b | QA-03 ∥ QA-04 |

Tasks **without** `approvalGroup` → individual approval (T-03, T-04, T-05, T-18c, T-21, T-22).

### T-18c (conditional skip)

If `artifacts/05-code-review/defects.md` has **no Critical or Major** items:

1. Mark T-18c → `Skipped`
2. QA tasks use `dependsOnIfSkipped` → proceed from T-18a + T-18b Done

If Critical/Major exist → run T-18c, developer approval, then QA.

## 5. Special gates

| Task | Extra approval prompt |
|------|------------------------|
| T-05 | Architecture sign-off — review `artifacts/03-architecture/design.md` |
| T-18c | Only if review defects exist |

## 6. Context file ownership

| File | Writer |
|------|--------|
| `session-context.md` | ORCH-01 (preserve Developer notes) |
| `active-task.json` | ORCH-01 |
| `task-tracker.json` | ORCH-01 |
| `approval-log.md` | ORCH-01 on developer reply |
