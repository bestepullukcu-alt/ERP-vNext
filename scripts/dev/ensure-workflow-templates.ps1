<#
.SYNOPSIS
    WP-PLT-WORKFLOW-TEMPLATES-01 — installs the product-identity approval definitions in the tenants you name, through
    Platform's one-shot --ensure-workflow-templates command.

.DESCRIPTION
    The catalog is Platform's Seed/workflow-templates/product-identity-templates.json. A DRY RUN unless you pass -Apply
    yourself: this script never adds it. Uses the Platform build the fleet already produced (--no-build); the command exits
    before the web host starts. Exit codes are the command's: 0 done, 1 failure, 2 drift / needs-owner (left as is),
    3 refused, 4 stopped part-way. Guide: docs/guides/operations/service-trust-chain-runbook.md section 4.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/dev/ensure-workflow-templates.ps1 -Tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/dev/ensure-workflow-templates.ps1 -Tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93 -Apply
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Tenant,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$platformDir = Join-Path $repoRoot 'services\Diten.Platform\src\Diten.Platform.API'

$commandArguments = @('--ensure-workflow-templates')
foreach ($id in $Tenant) {
    $commandArguments += @('--tenant', $id)
}
if ($Apply) {
    $commandArguments += '--apply'
}

Push-Location $platformDir
try {
    & dotnet run --no-build --launch-profile http -- @commandArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
