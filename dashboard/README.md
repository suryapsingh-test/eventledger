# Dashboard — Event Ledger

Live sprint progress generated from **`task-tracker.json`**.

## Open dashboard

```powershell
.\scripts\open-dashboard.ps1
```

Or open `dashboard/project-dashboard.html` in a browser.

## Regenerate (after tracker changes)

```powershell
.\scripts\generate-dashboard.ps1
```

This produces the **full breakdown**:

- Sprint progress %
- **Stage breakdown** (S1–S7)
- **All 27 tasks** with ID, stage, agent, status, dependencies, approval group
- Workflow pipeline + parallel lanes
- Agent roster chips
- Artifact links

Bootstrap runs this automatically: `.\scripts\bootstrap.ps1`

## Ownership

**ORCH-01** must run `generate-dashboard.ps1` after every task status change — **never** replace with a summary-only HTML.

## Configuration

`dashboard-config.json` — meta-refresh interval (default 5s).
