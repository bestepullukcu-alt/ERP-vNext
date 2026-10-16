<#
.SYNOPSIS
    WP-VW-W4-INFRA — runs Platform's ONE-SHOT verified market provisioning in local Development
    (Program.cs: --run-verified-market-provisioning; refused outside Development).

.DESCRIPTION
    Loads the locked market catalog (Seed/business-reference-data/mod-0290-market-reference.json, version
    UNSD-M49-2026-08-08) under the configured reference tenant and verifies the publication. It writes only the
    catalog provisioning's own reference rows; it is idempotent (a second run replays the same operation).
    The verified GSKU catalog needs no command: Platform's hosted runner reconciles it at every start.

    Uses the Platform build the fleet already produced (--no-build), so it can run while the fleet is up. The command
    exits before the web host starts: no port is opened, no background job runs.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/dev/run-verified-market-provisioning.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$platformDir = Join-Path $repoRoot 'services\Diten.Platform\src\Diten.Platform.API'

Push-Location $platformDir
try {
    & dotnet run --no-build --launch-profile http -- --run-verified-market-provisioning
    if ($LASTEXITCODE -ne 0) { throw "Verified market provisioning failed (exit $LASTEXITCODE); see the log above." }
    Write-Host 'Verified market catalog provisioned (or already verified).' -ForegroundColor Green
}
finally {
    Pop-Location
}
