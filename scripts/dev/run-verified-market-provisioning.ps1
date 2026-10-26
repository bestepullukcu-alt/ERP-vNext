<#
.SYNOPSIS
    WP-VW-W4-INFRA — runs Platform's ONE-SHOT verified market provisioning in local Development
    (Program.cs: --run-verified-market-provisioning; refused outside Development).

.DESCRIPTION
    Loads the locked market catalog (Seed/business-reference-data/mod-0290-market-reference.json, version
    UNSD-M49-2026-08-08) under the configured reference tenant and verifies the publication. It writes only the
    catalog provisioning's own reference rows; it is idempotent (a second run replays the same operation).
    The verified GSKU catalog needs no command: Platform's hosted runner reconciles it at every start.

    WP-PLT-MARKET-REFTENANT-01 (BL-645, MOD-0048-FU01 amendment 2026-10-15): the command runs only when the PROCESS itself
    names its values, equal to the configuration. This script reads them from the API's appsettings.Development.json
    (never the .local.json, never typed by hand), sets them as environment variables for the child process only, and
    restores the previous values when it ends. The reference tenant is BusinessReferenceData:Provider:ReferenceTenantId.
    Nothing here is a secret.

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
$settingsPath = Join-Path $platformDir 'appsettings.Development.json'

$settings = Get-Content -Raw -Path $settingsPath | ConvertFrom-Json
$referenceData = $settings.BusinessReferenceData
$market = $referenceData.VerifiedMarketOperationalProvisioning

# The process values the eligibility matches against the configuration (exact, ordinal). Enabled / CatalogLoad are fixed.
$processValues = [ordered]@{
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__Enabled'                    = 'true'
    'BusinessReferenceData__CatalogLoad__Enabled'                                              = 'false'
    'BusinessReferenceData__Provider__ReferenceTenantId'                                       = [string]$referenceData.Provider.ReferenceTenantId
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__CatalogPath'                = [string]$market.CatalogPath
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__ExpectedCatalogVersion'     = [string]$market.ExpectedCatalogVersion
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__ExpectedCatalogFingerprint' = [string]$market.ExpectedCatalogFingerprint
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__ActorId'                    = [string]$market.ActorId
    'BusinessReferenceData__VerifiedMarketOperationalProvisioning__IdempotencyNamespace'       = [string]$market.IdempotencyNamespace
}

foreach ($name in $processValues.Keys) {
    if ([string]::IsNullOrWhiteSpace($processValues[$name])) {
        throw "appsettings.Development.json has no value for $name; nothing was run."
    }
}

$previous = @{}
foreach ($name in $processValues.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location $platformDir
try {
    foreach ($name in $processValues.Keys) {
        Set-Item -Path "env:$name" -Value $processValues[$name]
    }

    & dotnet run --no-build --launch-profile http -- --run-verified-market-provisioning
    if ($LASTEXITCODE -ne 0) { throw "Verified market provisioning failed (exit $LASTEXITCODE); see the log above." }
    Write-Host 'Verified market catalog provisioned (or already verified).' -ForegroundColor Green
}
finally {
    foreach ($name in $processValues.Keys) {
        if ($null -eq $previous[$name]) {
            Remove-Item -Path "env:$name" -ErrorAction SilentlyContinue
        }
        else {
            Set-Item -Path "env:$name" -Value $previous[$name]
        }
    }

    Pop-Location
}
