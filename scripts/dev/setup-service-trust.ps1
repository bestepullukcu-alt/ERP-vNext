# MOD-0033-FU02 §F.3 — Development bootstrap of the MDM -> Auth -> Platform service trust chain (Windows wrapper).
# Same behaviour as setup-service-trust.sh: writes one git-ignored appsettings.Development.local.json into each of the
# Auth, Platform and MDM API directories, prints nothing secret, takes no secret as an argument, never overwrites.
#
#   scripts/dev/setup-service-trust.ps1 -TenantId <guid> -ApproverUserId <guid>[,<guid>...]
#
# Guide: docs/guides/operations/service-trust-chain-runbook.md
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$TenantId,
    [Parameter(Mandatory = $true)][string[]]$ApproverUserId,
    [string]$AuthDir,
    [string]$PlatformDir,
    [string]$MdmDir,
    [string]$ClientCode,
    [string]$Issuer,
    [string]$AuthBaseUrl
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $AuthDir) { $AuthDir = Join-Path $repoRoot 'services\Diten.AuthService\src\Diten.AuthService.Api' }
if (-not $PlatformDir) { $PlatformDir = Join-Path $repoRoot 'services\Diten.Platform\src\Diten.Platform.API' }
if (-not $MdmDir) { $MdmDir = Join-Path $repoRoot 'services\Diten.MdmService\src\Diten.MdmService.Api' }

$toolArgs = @('--auth-dir', $AuthDir, '--platform-dir', $PlatformDir, '--mdm-dir', $MdmDir)
foreach ($id in $TenantId) { $toolArgs += @('--tenant-id', $id) }
foreach ($id in $ApproverUserId) { $toolArgs += @('--approver-user-id', $id) }
if ($ClientCode) { $toolArgs += @('--client-code', $ClientCode) }
if ($Issuer) { $toolArgs += @('--issuer', $Issuer) }
if ($AuthBaseUrl) { $toolArgs += @('--auth-base-url', $AuthBaseUrl) }

$project = Join-Path $repoRoot 'services\Diten.AuthService\tools\Diten.AuthService.DevTrustBootstrap\Diten.AuthService.DevTrustBootstrap.csproj'
& dotnet run --nologo --project $project -- @toolArgs
exit $LASTEXITCODE
