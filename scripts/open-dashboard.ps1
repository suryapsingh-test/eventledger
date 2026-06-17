# Open Event Ledger project dashboard in default browser

$repoRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot "generate-dashboard.ps1")

$dashboard = Join-Path $repoRoot "dashboard\project-dashboard.html"
Start-Process (Resolve-Path $dashboard)
