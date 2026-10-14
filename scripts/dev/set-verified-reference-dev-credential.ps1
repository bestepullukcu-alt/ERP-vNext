<#
.SYNOPSIS
    WP-VW-W4-INFRA — sets the LOCAL DEVELOPMENT credential that MDM uses to call Platform's verified reference-data
    resolver (verified GSKU UOM + verified market), in dotnet user-secrets on BOTH services.

.DESCRIPTION
    Generates one random secret and writes the SAME value to:
      * Platform  (Diten.Platform.API)     VerifiedGskuResolverCredential:Mdm:ActiveSecret
      * MDM       (Diten.MdmService.Api)   VerifiedGskuResolver:CredentialSecret
    The non-secret parts (identifier, consumer service, audience, Platform address, timeout) are in each service's
    appsettings.Development.json. The secret is never printed, never written to the repository and never read back.

    Run it once (again only to rotate), then restart Platform and MDM (the fleet). Development only — the live
    credential is issued by the operator (docs/guides/operations/verified-reference-data-provisioning.md).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/dev/set-verified-reference-dev-credential.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$platformProject = Join-Path $repoRoot 'services\Diten.Platform\src\Diten.Platform.API\Diten.Platform.API.csproj'
$mdmProject = Join-Path $repoRoot 'services\Diten.MdmService\src\Diten.MdmService.Api\Diten.MdmService.Api.csproj'

foreach ($project in @($platformProject, $mdmProject)) {
    if (-not (Test-Path $project)) { throw "Project not found: $project" }
    if (-not (Select-String -Path $project -Pattern '<UserSecretsId>' -Quiet)) {
        throw "No <UserSecretsId> in $project — the secret would not be loaded. Update the branch first."
    }
}

# 48 random bytes → base64url (64 characters, no padding): no shell-special characters.
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$secret = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

function Set-Secret([string] $project, [string] $key, [string] $value) {
    $output = & dotnet user-secrets set $key $value --project $project 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet user-secrets failed for $key ($project): $($output -replace [regex]::Escape($value), '***')"
    }
}

Set-Secret $platformProject 'VerifiedGskuResolverCredential:Mdm:ActiveSecret' $secret
Set-Secret $mdmProject 'VerifiedGskuResolver:CredentialSecret' $secret
$secret = $null

Write-Host 'Verified reference-data dev credential written to Platform and MDM user-secrets (value not shown).' -ForegroundColor Green
Write-Host 'Next: restart the fleet (Platform + MDM), then run scripts/dev/run-verified-market-provisioning.ps1 once.' -ForegroundColor Yellow
