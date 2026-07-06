# Stops local Event Ledger dev processes so rebuilds (F5) do not fail with locked DLLs.
# Used by: Visual Studio PreBuild, VS Code/Cursor preLaunchTask, start-eventledger-dev.ps1

param(
    [int[]]$Ports = @(8080, 8081)
)

$ErrorActionPreference = 'SilentlyContinue'

foreach ($processName in @('EventGateway', 'AccountService')) {
    Get-Process -Name $processName | Stop-Process -Force
}

foreach ($port in $Ports) {
    $listenerPids = Get-NetTCPConnection -LocalPort $port -State Listen |
        Select-Object -ExpandProperty OwningProcess -Unique

    foreach ($pid in $listenerPids) {
        Stop-Process -Id $pid -Force
    }
}

# Allow SQLite / DLL file handles to release before MSBuild copies assemblies.
Start-Sleep -Milliseconds 300

Write-Host "Event Ledger dev processes stopped (ports $($Ports -join ', '))."
