<#
.SYNOPSIS
    WP-0173-PM2 — creates (or rotates) a development service key for InventoryService's internal operations
    (INVENTORY-BUNDLE 1.1.0 getStockPresence) and stores it in InventoryService's USER SECRETS.

.DESCRIPTION
    - Generates a cryptographically random secret (32 bytes, RandomNumberGenerator, base64url = 43 characters).
    - Writes it to the user-secrets file of Diten.InventoryService.Api (its <UserSecretsId>), under
      Inventory:InternalServiceKeys:<n>:{Identifier, Secrets, Endpoints}. The file is written directly (atomic replace) —
      the secret is never passed on a command line (no `dotnet user-secrets set`), never printed, never logged, never
      written to any other file.
    - Copies the secret to the clipboard and says so: paste it into your password manager and give it to the calling
      service (MDM) through its own secure configuration.
    - Rotation: if the identifier already has ONE secret, the new one is ADDED as the second (both valid during the
      overlap; remove the old one later with -RetireOldest). If it already has TWO, the script stops unless -RetireOldest is
      given (then the oldest is dropped and the new one appended — never more than two).
    - Does NOT restart anything. InventoryService reads its configuration on every call; user secrets are re-read when the
      file changes in Development.
    - appsettings.json keeps an EMPTY key list: without this script (or an administrator's setting) the operation answers
      401 to every call.

.PARAMETER Identifier
    Key identifier of the calling service (default: ditenmdmservice). Must not contain ':'.

.PARAMETER Endpoints
    Operation ids the key opens (default: getStockPresence).

.PARAMETER UserSecretsId
    Overrides the id read from the InventoryService Api project (e.g. an isolated store for a probe).

.PARAMETER SecretsRoot
    Overrides the user-secrets root (default: %APPDATA%\Microsoft\UserSecrets). For an isolated probe store only.

.PARAMETER RetireOldest
    When the identifier already has two secrets: drop the oldest, append the new one.

.PARAMETER NoClipboard
    Do not touch the clipboard (automated runs). The secret is then ONLY in the user-secrets file.

.EXAMPLE
    pwsh scripts/new-inventory-internal-key.ps1
#>
[CmdletBinding()]
param(
    [string]$Identifier = 'ditenmdmservice',
    [string[]]$Endpoints = @('getStockPresence'),
    [string]$UserSecretsId,
    [string]$SecretsRoot,
    [switch]$RetireOldest,
    [switch]$NoClipboard
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$section = 'Inventory:InternalServiceKeys'

if ([string]::IsNullOrWhiteSpace($Identifier) -or $Identifier.Contains(':')) {
    throw "Identifier must be non-empty and must not contain ':'."
}
if (-not $Endpoints -or $Endpoints.Count -eq 0) {
    throw 'At least one endpoint (operation id) is required.'
}

if (-not $UserSecretsId) {
    $project = Join-Path $PSScriptRoot '..\services\Diten.InventoryService\src\Diten.InventoryService.Api\Diten.InventoryService.Api.csproj'
    $match = Select-String -Path $project -Pattern '<UserSecretsId>([^<]+)</UserSecretsId>' | Select-Object -First 1
    if (-not $match) { throw "No <UserSecretsId> in $project." }
    $UserSecretsId = $match.Matches[0].Groups[1].Value
}
if (-not $SecretsRoot) {
    $SecretsRoot = Join-Path $env:APPDATA 'Microsoft\UserSecrets'
}

$dir = Join-Path $SecretsRoot $UserSecretsId
$file = Join-Path $dir 'secrets.json'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

# Flat "a:b:c" keys, the user-secrets file format.
$settings = [ordered]@{}
if (Test-Path $file) {
    $existing = Get-Content -Raw -Path $file | ConvertFrom-Json
    if ($existing) {
        foreach ($p in $existing.PSObject.Properties) { $settings[$p.Name] = [string]$p.Value }
    }
}

# Find the entry of this identifier, or the next free index.
$index = $null
$maxIndex = -1
foreach ($name in @($settings.Keys)) {
    $m = [regex]::Match($name, '^' + [regex]::Escape($section) + ':(\d+):Identifier$', 'IgnoreCase')
    if ($m.Success) {
        $i = [int]$m.Groups[1].Value
        if ($i -gt $maxIndex) { $maxIndex = $i }
        if ($settings[$name] -ceq $Identifier) { $index = $i }
    }
}
$mode = 'new'
if ($null -eq $index) { $index = $maxIndex + 1 } else { $mode = 'rotation' }
$prefix = "${section}:${index}"

$secrets = New-Object System.Collections.Generic.List[string]
foreach ($name in @($settings.Keys | Where-Object { $_ -match ('^' + [regex]::Escape($prefix) + ':Secrets:(\d+)$') } | Sort-Object { [int]($_ -replace '.*:', '') })) {
    if ($settings[$name]) { $secrets.Add($settings[$name]) }
    $settings.Remove($name)
}
if ($secrets.Count -ge 2) {
    if (-not $RetireOldest) {
        throw "Identifier '$Identifier' already has two secrets (rotation in progress). Re-run with -RetireOldest to drop the oldest."
    }
    $secrets.RemoveAt(0)
}

# 32 cryptographically random bytes, base64url (no padding): 43 characters.
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$secret = [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
[Array]::Clear($bytes, 0, $bytes.Length)
$secrets.Add($secret)

$settings["${prefix}:Identifier"] = $Identifier
foreach ($name in @($settings.Keys | Where-Object { $_ -match ('^' + [regex]::Escape($prefix) + ':Endpoints:\d+$') })) { $settings.Remove($name) }
for ($i = 0; $i -lt $Endpoints.Count; $i++) { $settings["${prefix}:Endpoints:$i"] = $Endpoints[$i] }
for ($i = 0; $i -lt $secrets.Count; $i++) { $settings["${prefix}:Secrets:$i"] = $secrets[$i] }

# Atomic replace: write a temp file next to it, then move over.
$temp = Join-Path $dir ('secrets.' + [guid]::NewGuid().ToString('N') + '.tmp')
($settings | ConvertTo-Json -Depth 3) | Set-Content -Path $temp -Encoding utf8
Move-Item -Force -Path $temp -Destination $file

if (-not $NoClipboard) {
    Set-Clipboard -Value $secret
}
$secret = $null

$count = $secrets.Count
$secrets.Clear()
if ($mode -eq 'rotation') {
    Write-Host "Key '$Identifier' rotated: $count valid secret(s) now (index $index). Endpoints: $($Endpoints -join ', ')."
} else {
    Write-Host "Key '$Identifier' created (index $index). Endpoints: $($Endpoints -join ', ')."
}
Write-Host "Stored in the InventoryService user secrets ($UserSecretsId)."
if ($NoClipboard) {
    Write-Host 'Clipboard not used (-NoClipboard): the secret is only in the user-secrets file.'
} else {
    Write-Host 'Panoya kopyalandı — parola yöneticisine yapıştırın. (Copied to the clipboard — paste it into your password manager.)'
}
Write-Host 'Nothing was restarted.'
