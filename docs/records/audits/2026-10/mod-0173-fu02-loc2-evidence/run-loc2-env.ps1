<#
.SYNOPSIS
  WP-0173-LOC2 runtime chain (E2/E3): browser → Diten.Web (this branch, Release, :5091) → a SCRATCH COPY of the
  gateway (:5090, only its four "Port": 5066 lines rewritten to the probe service) → Diten.InventoryService (this
  branch, Release, :5067, its own database). The user's Auth 5056, Platform 5057, MDM 5059 and Prism 4010 are REUSED
  (read / called only) and never stopped, restarted or rebuilt. Copy of the UI1 / LE1 / INT-2 scripts.

.DESCRIPTION
  -Action Start
    Refuses to start when a port it needs is taken or a reused user service is missing. Records every PID it started.
      * InventoryService :5067 — Development (the local AuthService signing settings and the Development
        PlatformRegistration from its own appsettings), database -MongoDatabase, PRODUCT-MASTER → the user's Prism 4010,
        legal-entity master / org-unit directory → the probe gateway (so they reach the user's MDM / Platform).
        At startup it pushes the INVENTORY-LEDGER manifest (this branch: 4 pages) to the user's Platform (AC-20).
      * Gateway :5090 — Release output copied to a scratch folder; ocelot.json proven byte-identical to the committed
        file, then ONLY the four "Port": 5066 lines (/api/inventory, /api/locations + {everything}) → 5067; the diff
        is written to the evidence. Everything else routes to the user's services exactly as committed.
      * Diten.Web :5091 — GatewayUrl = the probe gateway. -WebWithoutTenantLiveness: Platform:InternalApiKey blank,
        so the FIX-4 tenant-liveness guard fails open for the seed tenant that is still "Provisioning" (UI1-11).
      * persona-helper.js :5097 — localhost-only; mints a test JWT with the Web's local signing settings for a named
        persona (read-only / no permission) and sets it as the access_token cookie; the token is never printed.
  -Action Stop
    Stops exactly the PIDs recorded at Start (and their child trees), deletes the gateway copy and lists any listener
    left on its own ports.

  No secret is printed or written: values go from config files to child-process environments only.
#>
param(
    [ValidateSet('Start', 'Stop')][string]$Action = 'Start',
    [Parameter(Mandatory = $true)][string]$StateFile,
    [Parameter(Mandatory = $true)][string]$LogDir,
    [Parameter(Mandatory = $true)][string]$EvidenceDir,
    [int]$WebPort = 5091,
    [int]$GatewayPort = 5090,
    [int]$ServicePort = 5067,
    [int]$HelperPort = 5097,
    [string]$MongoDatabase = 'diten_inventory_loc2_smoke',
    [switch]$WebWithoutTenantLiveness
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path

function Read-Json([string]$path) { Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json }
function Assert-PortFree([int]$port) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
function Start-Logged([string]$name, [string]$file, [string]$arguments, [string]$workDir, [hashtable]$environment) {
    foreach ($key in $environment.Keys) { Set-Item -Path "Env:$key" -Value ([string]$environment[$key]) }
    try {
        $process = Start-Process -FilePath $file -ArgumentList $arguments -WorkingDirectory $workDir -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $LogDir "$name.log") -RedirectStandardError (Join-Path $LogDir "$name.err.log")
    }
    finally {
        foreach ($key in $environment.Keys) { Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue }
    }
    return [pscustomobject]@{ name = $name; pid = $process.Id; started = (Get-Date).ToString('o') }
}
function Wait-Port([int]$port, [int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

if ($Action -eq 'Stop') {
    $state = Read-Json $StateFile
    foreach ($p in $state.processes) { & taskkill /PID $p.pid /T /F 2>$null | Out-Null }
    Start-Sleep -Seconds 2
    if ($state.gatewayCopy -and (Test-Path $state.gatewayCopy)) { Remove-Item -Recurse -Force $state.gatewayCopy }
    $ports = @($ServicePort, $GatewayPort, $WebPort, $HelperPort)
    $left = foreach ($port in $ports) { Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object LocalPort, OwningProcess }
    $alive = foreach ($p in $state.processes) { if (Get-Process -Id $p.pid -ErrorAction SilentlyContinue) { $p.pid } }
    [ordered]@{ stopped = $state.processes; gatewayCopyRemoved = -not (Test-Path $state.gatewayCopy); listenersLeftOnOwnPorts = @($left); startedPidsStillAlive = @($alive) } | ConvertTo-Json -Depth 5
    return
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
foreach ($port in @($ServicePort, $GatewayPort, $WebPort, $HelperPort)) { Assert-PortFree $port }
foreach ($port in @(4010, 5056, 5057, 5059)) {
    if (-not (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) { throw "Expected the user's service on $port; it is not listening." }
}

$web = Read-Json (Join-Path $repo 'frontend\Diten.Web\appsettings.Development.json')
$started = New-Object System.Collections.ArrayList

# ── InventoryService (this branch, Release) ─────────────────────────────────────────────────────────────────
$serviceBin = Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\bin\Release\net8.0'
[void]$started.Add((Start-Logged 'inventory-service' 'dotnet' "`"$(Join-Path $serviceBin 'Diten.InventoryService.Api.dll')`"" $serviceBin @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
    Mongo__DatabaseName = $MongoDatabase
    Inventory__Upstream__ProductMasterBaseUrl = 'http://127.0.0.1:4010/'
    Inventory__Upstream__LegalEntityMasterBaseUrl = "http://localhost:$GatewayPort/api/legal-entities/"
    Inventory__Upstream__OrgUnitDirectoryBaseUrl = "http://localhost:$GatewayPort/api/platform/organization-units/"
    Inventory__Upstream__LocationMasterBaseUrl = 'http://127.0.0.1:9/'
}))

# ── Gateway: a scratch copy of this branch's Release output, four ports rewritten ──────────────────────────
$builtGateway = Join-Path $repo 'gateway\Diten.ApiGateway\bin\Release\net8.0'
$gatewayCopy = Join-Path $env:TEMP ('loc2-gateway-' + [guid]::NewGuid().ToString('N'))
Copy-Item -Path $builtGateway -Destination $gatewayCopy -Recurse
$committed = Join-Path $repo 'gateway\Diten.ApiGateway\ocelot.json'
$copied = Join-Path $gatewayCopy 'ocelot.json'
$override = [ordered]@{
    committedSha256 = (Get-FileHash $committed -Algorithm SHA256).Hash
    copiedBeforeOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
    servicePort = $ServicePort
    changedLines = @()
}
if ($override.committedSha256 -ne $override.copiedBeforeOverrideSha256) { throw 'Built ocelot.json differs from the committed file.' }
$lines = [IO.File]::ReadAllText($copied) -split "`r`n"
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\s*"Port": 5066$') {
        $new = $lines[$i] -replace '5066', [string]$ServicePort
        $override.changedLines += "line $($i + 1): '$($lines[$i].Trim())' -> '$($new.Trim())'"
        $lines[$i] = $new
    }
}
[IO.File]::WriteAllText($copied, ($lines -join "`r`n"))
if ($override.changedLines.Count -ne 4) { throw "Expected to rewrite exactly 4 ports, rewrote $($override.changedLines.Count)." }
$override.copiedAfterOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
$override | ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd6-gateway-copy-override.json') -Encoding utf8
[void]$started.Add((Start-Logged 'gateway' 'dotnet' "`"$(Join-Path $gatewayCopy 'Diten.ApiGateway.dll')`"" $gatewayCopy @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = "http://localhost:$GatewayPort"
}))

# ── Diten.Web (this branch, Release) ────────────────────────────────────────────────────────────────────────
$webDir = Join-Path $repo 'frontend\Diten.Web'
$webEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = "http://localhost:$WebPort"
    GatewayUrl = "http://localhost:$GatewayPort"
}
# A whitespace value: an empty one would delete the variable and leave the appsettings key in force.
if ($WebWithoutTenantLiveness) { $webEnvironment['Platform__InternalApiKey'] = ' ' }
[void]$started.Add((Start-Logged 'diten-web' 'dotnet' "`"$(Join-Path $webDir 'bin\Release\net8.0\Diten.Web.dll')`"" $webDir $webEnvironment))

# ── persona helper (localhost only; the signing values reach it through its environment) ───────────────────
# --max-http-header-size: the browser sends a real login's chunked token cookies (~35 KB) to every localhost port.
[void]$started.Add((Start-Logged 'persona-helper' 'node' "--max-http-header-size=65536 `"$(Join-Path $PSScriptRoot 'persona-helper.js')`"" $PSScriptRoot @{
    LOC2_HELPER_PORT = $HelperPort
    LOC2_WEB_ORIGIN = "http://localhost:$WebPort"
    LOC2_JWT_SECRET = $web.JwtSettings.Secret
    LOC2_JWT_ISSUER = $web.JwtSettings.Issuer
    LOC2_JWT_AUDIENCE = $web.JwtSettings.Audience
}))

$ready = [ordered]@{
    inventoryService = Wait-Port $ServicePort 90
    gateway = Wait-Port $GatewayPort 90
    web = Wait-Port $WebPort 120
    helper = Wait-Port $HelperPort 30
}
[ordered]@{ processes = $started; ready = $ready; database = $MongoDatabase; gatewayCopy = $gatewayCopy; webPort = $WebPort } |
    ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $StateFile
Get-Content -Raw $StateFile
