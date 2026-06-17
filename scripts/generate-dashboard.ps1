# Generate project-dashboard.html from task-tracker.json

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$trackerPath = Join-Path $repoRoot "artifacts\01-sprint-plan\task-tracker.json"
$outPath = Join-Path $repoRoot "dashboard\project-dashboard.html"

$tracker = Get-Content $trackerPath -Raw | ConvertFrom-Json
$tasks = @($tracker.tasks)
$stages = @($tracker.stages)

$total = $tasks.Count
$done = @($tasks | Where-Object { $_.status -eq "Done" }).Count
$skipped = @($tasks | Where-Object { $_.status -eq "Skipped" }).Count
$pct = if ($total -gt 0) { [math]::Round((($done + $skipped) / $total) * 100) } else { 0 }

if ($tracker.sprintStatus -eq "Done") {
    $badgeClass = "badge-done"
    $badgeText = "Sprint Complete"
} else {
    $badgeClass = "badge-progress"
    $badgeText = $tracker.sprintStatus
}

function Esc([string]$s) {
    if ($null -eq $s) { return "" }
    return $s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
}

function Status-Class([string]$status) {
    switch ($status) {
        "Done" { return "status-done" }
        "Ready" { return "status-ready" }
        "In Progress" { return "status-progress" }
        "Awaiting Developer Approval" { return "status-approval" }
        "Blocked" { return "status-blocked" }
        default { return "status-pending" }
    }
}

function Phase-Class([string]$stageId, [string]$currentStage, [bool]$sprintDone) {
    if ($sprintDone) { return "done" }
    $order = @("S1","S2","S3","S4","S5","S6","S7")
    $ci = [array]::IndexOf($order, $currentStage)
    $si = [array]::IndexOf($order, $stageId)
    if ($stageId -eq $currentStage) { return "active" }
    if ($si -lt $ci) { return "done" }
    return "pending"
}

$sprintDone = ($tracker.sprintStatus -eq "Done")
$lastUpdated = if ($tracker.lastUpdated) { $tracker.lastUpdated.Substring(0, 10) } else { Get-Date -Format "yyyy-MM-dd" }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("<!DOCTYPE html>")
[void]$sb.AppendLine('<html lang="en">')
[void]$sb.AppendLine("<head>")
[void]$sb.AppendLine('  <meta charset="UTF-8" />')
[void]$sb.AppendLine('  <meta name="viewport" content="width=device-width, initial-scale=1.0" />')
[void]$sb.AppendLine('  <meta http-equiv="refresh" content="5" />')
[void]$sb.AppendLine("  <title>Event Ledger - Project Dashboard</title>")
[void]$sb.AppendLine('  <link rel="stylesheet" href="dashboard-theme.css" />')
[void]$sb.AppendLine("</head>")
[void]$sb.AppendLine("<body>")

[void]$sb.AppendLine("<header>")
[void]$sb.AppendLine("  <div>")
[void]$sb.AppendLine("    <h1>Event Ledger - Project Dashboard</h1>")
[void]$sb.AppendLine('    <p class="subtitle">Multi-agent AI SDLC - full task breakdown</p>')
[void]$sb.AppendLine("    <p style=`"margin-top:0.5rem`"><span class=`"badge $badgeClass`">$badgeText</span></p>")
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine('  <div class="meta">')
[void]$sb.AppendLine("    <div><strong>Owner:</strong> ORCH-01</div>")
[void]$sb.AppendLine("    <div><strong>Stage:</strong> $($tracker.currentStage) - $(Esc $tracker.currentStageName)</div>")
[void]$sb.AppendLine("    <div><strong>Updated:</strong> $lastUpdated</div>")
[void]$sb.AppendLine("    <div><strong>Auto-refresh:</strong> 5s</div>")
[void]$sb.AppendLine('    <div><a href="../artifacts/context/session-context.md">session-context.md</a></div>')
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine("</header>")

$skipLabel = if ($skipped -gt 0) { " ($skipped skipped)" } else { "" }
$currentTask = if ($tracker.currentTaskId) { $tracker.currentTaskId } else { "None (sprint complete)" }
if ($tracker.awaitingDeveloperApproval) {
    $approvalNote = "Awaiting developer approval"
} else {
    $approvalNote = "No pending approvals"
}

[void]$sb.AppendLine('<div class="grid">')
[void]$sb.AppendLine('  <div class="card"><h2>Sprint progress</h2>')
[void]$sb.AppendLine("    <div class=`"metric`">$pct%</div>")
[void]$sb.AppendLine("    <div class=`"metric-label`">$done of $total tasks done$skipLabel</div>")
[void]$sb.AppendLine("    <div class=`"progress-bar`"><div class=`"progress-fill`" style=`"width:${pct}%`"></div></div>")
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine('  <div class="card"><h2>Current focus</h2>')
[void]$sb.AppendLine("    <div style=`"font-size:1.1rem;font-weight:600`">$(Esc $currentTask)</div>")
[void]$sb.AppendLine("    <div class=`"metric-label`">$approvalNote</div>")
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine('  <div class="card"><h2>Source</h2>')
[void]$sb.AppendLine('    <p class="metric-label"><a href="../artifacts/01-sprint-plan/task-tracker.json">task-tracker.json</a></p>')
[void]$sb.AppendLine('    <p class="metric-label"><a href="../artifacts/context/approval-log.md">approval-log.md</a></p>')
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine('  <div class="card"><h2>Regenerate</h2>')
[void]$sb.AppendLine('    <p style="font-size:0.85rem"><code>.\scripts\generate-dashboard.ps1</code></p>')
[void]$sb.AppendLine("  </div>")
[void]$sb.AppendLine("</div>")

# Stage breakdown
[void]$sb.AppendLine('<div class="section card"><h2>Stage breakdown</h2><table>')
[void]$sb.AppendLine("<thead><tr><th>Stage</th><th>Name</th><th>Tasks done</th><th>Status</th></tr></thead><tbody>")
foreach ($stage in $stages) {
    $stageTasks = @($tasks | Where-Object { $_.stage -eq $stage.id })
    $sd = @($stageTasks | Where-Object { $_.status -eq "Done" }).Count
    $st = $stageTasks.Count
    if ($sd -eq $st) { $ss = "status-done"; $sl = "Complete" }
    elseif ($stage.id -eq $tracker.currentStage -and -not $sprintDone) { $ss = "status-progress"; $sl = "Active" }
    else { $ss = "status-pending"; $sl = if ($sd -gt 0) { "In progress" } else { "Pending" } }
    [void]$sb.AppendLine("<tr><td><strong>$($stage.id)</strong></td><td>$(Esc $stage.name)</td><td>$sd / $st</td><td><span class=`"status $ss`">$sl</span></td></tr>")
}
[void]$sb.AppendLine("</tbody></table></div>")

# Pipeline
[void]$sb.AppendLine('<div class="section card"><h2>Workflow pipeline</h2><div class="pipeline">')
$labels = @(
    @{ id = "S1"; label = "PM" },
    @{ id = "S2"; label = "BA" },
    @{ id = "S3"; label = "ARCH" },
    @{ id = "S4"; label = "DEV" },
    @{ id = "S5"; label = "REV" },
    @{ id = "S6"; label = "QA" },
    @{ id = "S7"; label = "Close" }
)
for ($i = 0; $i -lt $labels.Count; $i++) {
    $pc = Phase-Class $labels[$i].id $tracker.currentStage $sprintDone
    [void]$sb.AppendLine("<span class=`"phase $pc`">$($labels[$i].label)</span>")
    if ($i -lt $labels.Count - 1) { [void]$sb.AppendLine('<span class="arrow">-&gt;</span>') }
}
[void]$sb.AppendLine('</div></div>')

# All tasks
[void]$sb.AppendLine('<div class="section card"><h2>All tasks (detailed)</h2><table>')
[void]$sb.AppendLine("<thead><tr><th>ID</th><th>Stage</th><th>Task</th><th>Agent</th><th>Status</th><th>Depends on</th><th>Group</th></tr></thead><tbody>")
foreach ($t in $tasks) {
    $sc = Status-Class $t.status
    $deps = if ($t.dependsOn -and @($t.dependsOn).Count -gt 0) { ($t.dependsOn -join ", ") } else { "-" }
    $grp = if ($t.approvalGroup) { $t.approvalGroup } elseif ($t.parallelGroup) { $t.parallelGroup } else { "-" }
    [void]$sb.AppendLine("<tr><td>$($t.id)</td><td>$($t.stage)</td><td>$(Esc $t.title)</td><td>$(Esc $t.agent)</td><td><span class=`"status $sc`">$(Esc $t.status)</span></td><td>$deps</td><td>$grp</td></tr>")
}
[void]$sb.AppendLine("</tbody></table></div>")

# Agent roster
[void]$sb.AppendLine('<div class="section card"><h2>Agent roster</h2><div class="agent-grid">')
$knownAgents = @("ORCH-01","PM-01","BA-01","ARCH-01","DEV-01","DEV-02","DEV-03","REV-01","REV-02","QA-01","QA-02","QA-03","QA-04")
foreach ($a in $knownAgents) {
    $agentTasks = @($tasks | Where-Object { $_.agent -eq $a -or ($a -eq "ORCH-01") })
    if ($a -eq "ORCH-01") { $cls = "done"; $lbl = "Done" }
    elseif (@($tasks | Where-Object { $_.agent -eq $a -and $_.status -ne "Done" -and $_.status -ne "Skipped" }).Count -eq 0 -and @($tasks | Where-Object { $_.agent -eq $a }).Count -gt 0) {
        $cls = "done"; $lbl = "Done"
    }
    elseif (@($tasks | Where-Object { $_.agent -eq $a -and ($_.status -eq "In Progress" -or $_.status -eq "Ready") }).Count -gt 0) {
        $cls = "active"; $lbl = "Active"
    }
    else { $cls = "idle"; $lbl = "-" }
    [void]$sb.AppendLine("<div class=`"agent-chip $cls`">$a<br><small>$lbl</small></div>")
}
[void]$sb.AppendLine("</div></div>")

[void]$sb.AppendLine("<footer>")
[void]$sb.AppendLine("<p>Generated from task-tracker.json. Run scripts/generate-dashboard.ps1 after tracker updates.</p>")
[void]$sb.AppendLine("</footer>")
[void]$sb.AppendLine("</body></html>")

[System.IO.File]::WriteAllText($outPath, $sb.ToString())
Write-Host "Dashboard generated: $outPath"
Write-Host "Progress: $done/$total ($pct%)"
