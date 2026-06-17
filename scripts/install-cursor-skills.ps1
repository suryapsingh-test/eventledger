# Install Event Ledger skills into Cursor

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$skillsSrc = Join-Path $repoRoot "ai-agent-skills"
$skillsDst = Join-Path $repoRoot ".cursor\skills"

if (-not (Test-Path $skillsSrc)) {
    Write-Error "ai-agent-skills not found at: $skillsSrc"
}

New-Item -ItemType Directory -Force -Path $skillsDst | Out-Null

Get-ChildItem $skillsSrc -Directory | ForEach-Object {
    $dest = Join-Path $skillsDst $_.Name
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Copy-Item -Recurse $_.FullName $dest
    Write-Host "Installed: $($_.Name)"
}

$count = (Get-ChildItem $skillsDst -Directory).Count
Write-Host "Done. $count skills installed to .cursor/skills"
