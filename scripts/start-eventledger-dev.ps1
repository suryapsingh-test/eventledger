# Stops any running dev instances, then starts Account Service and Event Gateway.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$stopScript = Join-Path $PSScriptRoot 'stop-eventledger-dev.ps1'
& $stopScript

$accountProject = Join-Path $RepoRoot 'src\AccountService\AccountService.csproj'
$gatewayProject = Join-Path $RepoRoot 'src\EventGateway\EventGateway.csproj'

Write-Host 'Starting Account Service on http://localhost:8081 ...'
Start-Process -FilePath 'dotnet' -ArgumentList @(
    'run',
    '--project', $accountProject,
    '--urls', 'http://localhost:8081',
    '--no-launch-profile'
) -WorkingDirectory $RepoRoot

Start-Sleep -Seconds 3

Write-Host 'Starting Event Gateway on http://localhost:8080 ...'
Start-Process -FilePath 'dotnet' -ArgumentList @(
    'run',
    '--project', $gatewayProject,
    '--urls', 'http://localhost:8080',
    '--no-launch-profile'
) -WorkingDirectory $RepoRoot

Write-Host 'Both services starting in separate windows. Gateway: http://localhost:8080'
