<#
.SYNOPSIS
  WP-0173-UI2 runtime chain (E2/E3): browser → Diten.Web (this branch, Release, :5091) → a SCRATCH COPY of the
  gateway (:5090) → Diten.InventoryService (this branch, Release, :5067, its own database) + a probe Prism
  PRODUCT-MASTER (:4011) + a probe Model A SKU mock (:4012). The user's Auth 5056, Platform 5057, MDM 5059 are REUSED
  (read / called only) and never stopped, restarted or rebuilt; the user's Prism 4010 is not used. Copy of the LOC2
  script (run-loc2-env.ps1) with the UI-2 fixtures (ui2-fixtures.js).

.DESCRIPTION
  -Action Start
    Refuses to start when a port it needs is taken or a reused user service is missing. Records every PID it started.
      * Prism :4011 — a COPY of the frozen PRODUCT-MASTER-BUNDLE spec whose examples name one real MDM product
        (-PrismVariant lot | serial; ui2-fixtures.js spec). Committed spec and the user's Prism are untouched.
      * SKU mock :4012 — the Model A /api/gskus, /api/lskus lists (dev MDM has 0 GSKU / 0 LSKU, measured).
      * InventoryService :5067 — Development, database -MongoDatabase, PRODUCT-MASTER → Prism :4011, legal-entity master /
        org-unit directory → the probe gateway. Pushes the INVENTORY-LEDGER manifest (8 pages) to the user's Platform.
      * Gateway :5090 — Release output copied to a scratch folder; ocelot.json proven byte-identical to the committed
        file, then ui2-fixtures.js gateway: the four 5066 ports → 5067, /api/gskus + /api/lskus → :4012, and ONE added
        route /api/product-master/{everything} → :4011 (FU-PM-01 stand-in). Every change is written to the evidence.
      * Diten.Web :5091 — GatewayUrl = the probe gateway. -WebWithoutTenantLiveness: Platform:InternalApiKey blank
        (FIX-4 tenant-liveness guard fails open for the seed tenant that is still "Provisioning", UI1-11).
      * persona-helper.js :5097 — localhost-only test JWT cookie for the view-only / no-permission personas.
  -Action SwapPrism
    Stops only the probe Prism it started and starts it again with -PrismVariant (lot ↔ serial).
  -Action Stop
    Stops exactly the PIDs recorded at Start (and their child trees), deletes the gateway copy and lists any listener
    left on its own ports.

  No secret is printed or written: values go from config files to child-process environments only.
#>
param(
    [ValidateSet('Start', 'SwapPrism', 'Stop')][string]$Action = 'Start',
    [Parameter(Mandatory = $true)][string]$StateFile,
    [Parameter(Mandatory = $true)][string]$LogDir,
    [Parameter(Mandatory = $true)][string]$EvidenceDir,
    [ValidateSet('lot', 'serial')][string]$PrismVariant = 'lot',
    # The frozen PRODUCT-MASTER-BUNDLE spec, passed by the caller (no path into docs/ is written here — DocsPathGuardTests).
    [string]$ProductMasterSpec,
    [int]$WebPort = 5091,
    [int]$GatewayPort = 5090,
    [int]$ServicePort = 5067,
    [int]$HelperPort = 5097,
    [int]$PrismPort = 4011,
    [int]$SkuMockPort = 4012,
    [string]$MongoDatabase = 'diten_inventory_ui2_smoke',
    [switch]$WebWithoutTenantLiveness
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$fixtures = Join-Path $PSScriptRoot 'ui2-fixtures.js'

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
function Start-Prism([string]$variant) {
    $prismJs = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\dist\index.js" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $prismJs) { throw 'prism-cli is not in the npm cache.' }
    if (-not $ProductMasterSpec -or -not (Test-Path $ProductMasterSpec)) { throw 'Pass -ProductMasterSpec <the committed product-master-bundle.openapi.yaml>.' }
    $committed = $ProductMasterSpec
    $copy = Join-Path $LogDir "product-master-ui2-$variant.openapi.yaml"
    & node $fixtures spec $variant $committed $copy | Out-Null
    Copy-Item $copy (Join-Path $EvidenceDir "d6-prism-spec-$variant.openapi.yaml") -Force
    return Start-Logged "prism-$variant" 'node' "--max-http-header-size=65536 `"$prismJs`" mock `"$copy`" --port $PrismPort --host 127.0.0.1" $LogDir @{}
}

if ($Action -eq 'Stop') {
    $state = Read-Json $StateFile
    foreach ($p in $state.processes) { cmd /c "taskkill /PID $($p.pid) /T /F >nul 2>&1" }
    Start-Sleep -Seconds 2
    if ($state.gatewayCopy -and (Test-Path $state.gatewayCopy)) { Remove-Item -Recurse -Force $state.gatewayCopy }
    $ports = @($ServicePort, $GatewayPort, $WebPort, $HelperPort, $PrismPort, $SkuMockPort)
    $left = foreach ($port in $ports) { Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object LocalPort, OwningProcess }
    $alive = foreach ($p in $state.processes) { if (Get-Process -Id $p.pid -ErrorAction SilentlyContinue) { $p.pid } }
    [ordered]@{ stopped = $state.processes; gatewayCopyRemoved = -not (Test-Path $state.gatewayCopy); listenersLeftOnOwnPorts = @($left); startedPidsStillAlive = @($alive) } | ConvertTo-Json -Depth 5
    return
}

if ($Action -eq 'SwapPrism') {
    $state = Read-Json $StateFile
    $prism = $state.processes | Where-Object { $_.name -like 'prism-*' }
    foreach ($p in $prism) { cmd /c "taskkill /PID $($p.pid) /T /F >nul 2>&1" }
    Start-Sleep -Seconds 2
    Assert-PortFree $PrismPort
    $started = Start-Prism $PrismVariant
    $state.processes = @($state.processes | Where-Object { $_.name -notlike 'prism-*' }) + $started
    $state | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $StateFile
    [ordered]@{ prism = $started; ready = (Wait-Port $PrismPort 60) } | ConvertTo-Json
    return
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
foreach ($port in @($ServicePort, $GatewayPort, $WebPort, $HelperPort, $PrismPort, $SkuMockPort)) { Assert-PortFree $port }
foreach ($port in @(5056, 5057, 5059)) {
    if (-not (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) { throw "Expected the user's service on $port; it is not listening." }
}

$web = Read-Json (Join-Path $repo 'frontend\Diten.Web\appsettings.Development.json')
$started = New-Object System.Collections.ArrayList

# ── probe Prism PRODUCT-MASTER and probe Model A SKU mock ─────────────────────────────────────────────────────────
[void]$started.Add((Start-Prism $PrismVariant))
[void]$started.Add((Start-Logged 'sku-mock' 'node' "--max-http-header-size=65536 `"$fixtures`" skumock $SkuMockPort" $LogDir @{}))

# ── InventoryService (this branch, Release) ───────────────────────────────────────────────────────────────────
$serviceBin = Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\bin\Release\net8.0'
[void]$started.Add((Start-Logged 'inventory-service' 'dotnet' "`"$(Join-Path $serviceBin 'Diten.InventoryService.Api.dll')`"" $serviceBin @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
    Mongo__DatabaseName = $MongoDatabase
    Inventory__Upstream__ProductMasterBaseUrl = "http://127.0.0.1:$PrismPort/"
    Inventory__Upstream__LegalEntityMasterBaseUrl = "http://localhost:$GatewayPort/api/legal-entities/"
    Inventory__Upstream__OrgUnitDirectoryBaseUrl = "http://localhost:$GatewayPort/api/platform/organization-units/"
    Inventory__Upstream__LocationMasterBaseUrl = 'http://127.0.0.1:9/'
}))

# ── Gateway: a scratch copy of this branch's Release output, routes adjusted by ui2-fixtures.js ─────────────────
$builtGateway = Join-Path $repo 'gateway\Diten.ApiGateway\bin\Release\net8.0'
$gatewayCopy = Join-Path $env:TEMP ('ui2-gateway-' + [guid]::NewGuid().ToString('N'))
Copy-Item -Path $builtGateway -Destination $gatewayCopy -Recurse
$committed = Join-Path $repo 'gateway\Diten.ApiGateway\ocelot.json'
$copied = Join-Path $gatewayCopy 'ocelot.json'
$override = [ordered]@{
    committedSha256 = (Get-FileHash $committed -Algorithm SHA256).Hash
    copiedBeforeOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
}
if ($override.committedSha256 -ne $override.copiedBeforeOverrideSha256) { throw 'Built ocelot.json differs from the committed file.' }
& node $fixtures gateway $copied (Join-Path $EvidenceDir 'd6-gateway-copy-changes.json') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'gateway override failed' }
$override.copiedAfterOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
$override | ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd6-gateway-copy-override.json') -Encoding utf8
[void]$started.Add((Start-Logged 'gateway' 'dotnet' "`"$(Join-Path $gatewayCopy 'Diten.ApiGateway.dll')`"" $gatewayCopy @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = "http://localhost:$GatewayPort"
}))

# ── Diten.Web (this branch, Release) ─────────────────────────────────────────────────────────────────────────
$webDir = Join-Path $repo 'frontend\Diten.Web'
$webEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = "http://localhost:$WebPort"
    GatewayUrl = "http://localhost:$GatewayPort"
}
if ($WebWithoutTenantLiveness) { $webEnvironment['Platform__InternalApiKey'] = ' ' }
[void]$started.Add((Start-Logged 'diten-web' 'dotnet' "`"$(Join-Path $webDir 'bin\Release\net8.0\Diten.Web.dll')`"" $webDir $webEnvironment))

# ── persona helper ────────────────────────────────────────────────────────────────────────────────────────────
[void]$started.Add((Start-Logged 'persona-helper' 'node' "--max-http-header-size=65536 `"$(Join-Path $PSScriptRoot 'persona-helper.js')`"" $PSScriptRoot @{
    UI2_HELPER_PORT = $HelperPort
    UI2_WEB_ORIGIN = "http://localhost:$WebPort"
    UI2_JWT_SECRET = $web.JwtSettings.Secret
    UI2_JWT_ISSUER = $web.JwtSettings.Issuer
    UI2_JWT_AUDIENCE = $web.JwtSettings.Audience
}))

$ready = [ordered]@{
    prism = Wait-Port $PrismPort 60
    skuMock = Wait-Port $SkuMockPort 30
    inventoryService = Wait-Port $ServicePort 90
    gateway = Wait-Port $GatewayPort 90
    web = Wait-Port $WebPort 120
    helper = Wait-Port $HelperPort 30
}
[ordered]@{ processes = $started; ready = $ready; database = $MongoDatabase; gatewayCopy = $gatewayCopy; webPort = $WebPort; prismVariant = $PrismVariant } |
    ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $StateFile
Get-Content -Raw $StateFile
