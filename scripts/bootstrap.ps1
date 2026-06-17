# Bootstrap — Event Ledger
# Run once when opening the project.

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "Event Ledger bootstrap..." -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"

& (Join-Path $PSScriptRoot "install-cursor-skills.ps1")
& (Join-Path $PSScriptRoot "generate-dashboard.ps1")

$dashboard = Join-Path $repoRoot "dashboard\project-dashboard.html"
if (Test-Path $dashboard) {
    Start-Process (Resolve-Path $dashboard)
    Write-Host "Dashboard opened." -ForegroundColor Green
} else {
    Write-Warning "Dashboard not found at: $dashboard"
}

Write-Host ""
Write-Host "Next: In Cursor chat, type:" -ForegroundColor Yellow
Write-Host "  Resume Event Ledger" -ForegroundColor White
