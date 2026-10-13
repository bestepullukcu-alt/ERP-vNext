<#
.SYNOPSIS
  WP-0173-UI1 Phase 4.5 runtime chain: Diten.Web (this branch) → Gateway 5000 → Diten.InventoryService 5066 + Prism.

.DESCRIPTION
  -Action Start
    Starts ONLY what this WP needs and records every PID it started in -StateFile:
      * Prism mocks of the frozen PRODUCT-MASTER (4010) and LOCATION (4011) contracts (node on the cached CLI entry);
      * Diten.InventoryService on 5066, environment Mock, database -MongoDatabase, the JWT secret / issuer / audience
        of the local AuthService (read from its appsettings.Development.json, handed over by process environment only)
        and PlatformRegistration (Platform 5057 + the local internal API key, same source) so the FU01 manifest is
        pushed to the user's Platform at startup;
      * Diten.Web built from this branch (Release) on -WebPort, GatewayUrl = http://localhost:5000.
    It REUSES the Gateway / Auth / Platform / MDM already listening (5000 / 5056 / 5057 / 5059) and never stops them.
    Refuses to start when a port it needs is taken.
  -Action Stop
    Stops exactly the PIDs recorded at Start (and their child trees) and lists any listener left on its ports.

  No secret is printed or written: values go from the config file to the child's environment, nowhere else.
#>
param(
    [ValidateSet('Start', 'Stop')][string]$Action = 'Start',
    [Parameter(Mandatory = $true)][string]$StateFile,
    [Parameter(Mandatory = $true)][string]$LogDir,
    [int]$WebPort = 5091,
    [int]$ServicePort = 5066,
    [string]$MongoDatabase = 'diten_inventory_fu01_smoke',
    # The seed test user's tenant ("Default Tenant") is in Provisioning in the local Platform, so the Web's FIX-4
    # tenant-liveness guard signs it out on every page. With this switch the SMOKE Web instance runs without
    # Platform:InternalApiKey: liveness is unverifiable and the guard fails open, exactly as documented for "no key".
    # Nothing else changes; no data, no code, no other process. (WP-0173-UI1 ASSUMPTION — reported, not hidden.)
    [switch]$WebWithoutTenantLiveness
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path

function Read-Json([string]$path) { Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json }
function Assert-PortFree([int]$port) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is already in use." }
}
function Start-Logged([string]$name, [string]$file, [string]$arguments, [string]$workDir, [hashtable]$environment) {
    # The child outlives this script, so its output goes straight to files (an in-session drain would die with the
    # session and block the child). The environment is set only for the instant of the start, then removed again.
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
    foreach ($p in $state.processes) {
        & taskkill /PID $p.pid /T /F 2>$null | Out-Null
    }
    Start-Sleep -Seconds 2
    $ports = @(4010, 4011, $ServicePort, $WebPort)
    $left = foreach ($port in $ports) { Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object LocalPort, OwningProcess }
    $alive = foreach ($p in $state.processes) { if (Get-Process -Id $p.pid -ErrorAction SilentlyContinue) { $p.pid } }
    [ordered]@{ stopped = $state.processes; listenersLeftOnOwnPorts = @($left); startedPidsStillAlive = @($alive) } | ConvertTo-Json -Depth 5
    return
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
foreach ($port in @(4010, 4011, $ServicePort, $WebPort)) { Assert-PortFree $port }
foreach ($port in @(5000, 5056, 5057)) {
    if (-not (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) { throw "Expected the user's service on $port; it is not listening." }
}

$auth = Read-Json (Join-Path $repo 'services\Diten.AuthService\src\Diten.AuthService.Api\appsettings.Development.json')
$platform = Read-Json (Join-Path $repo 'services\Diten.Platform\src\Diten.Platform.API\appsettings.Development.json')

$started = New-Object System.Collections.ArrayList

# Prism (frozen consumed contracts) — direct node entry, no cmd/npx chain.
$prismPackage = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\package.json" -ErrorAction SilentlyContinue |
    Where-Object { (Get-Content -Raw $_.FullName | ConvertFrom-Json).version -eq '4.10.5' } | Select-Object -First 1
if (-not $prismPackage) { throw 'Prism 4.10.5 not found in the npx cache.' }
$prismJs = Join-Path $prismPackage.Directory.FullName 'dist\index.js'
# The frozen contracts are found BY FILE NAME under the docs tree (no docs folder path is pinned — DocsPathGuardTests).
function Find-Contract([string]$fileName) {
    $found = @(Get-ChildItem -Path (Join-Path $repo 'docs') -Recurse -Filter $fileName -File)
    if ($found.Count -ne 1) { throw "Expected exactly one $fileName under docs, found $($found.Count)." }
    return $found[0].FullName
}
foreach ($mock in @(@{ name = 'prism-product-master'; spec = 'product-master-bundle.openapi.yaml'; port = 4010 }, @{ name = 'prism-location'; spec = 'location.openapi.yaml'; port = 4011 })) {
    [void]$started.Add((Start-Logged $mock.name 'node' "`"$prismJs`" mock `"$(Find-Contract $mock.spec)`" --port $($mock.port) --host 127.0.0.1" $repo @{}))
}

$serviceBin = Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\bin\Debug\net8.0'
[void]$started.Add((Start-Logged 'inventory-service' 'dotnet' "`"$(Join-Path $serviceBin 'Diten.InventoryService.Api.dll')`"" $serviceBin @{
    ASPNETCORE_ENVIRONMENT = 'Mock'
    Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
    Mongo__DatabaseName = $MongoDatabase
    JwtSettings__Secret = $auth.JwtSettings.Secret
    JwtSettings__Issuer = $auth.JwtSettings.Issuer
    JwtSettings__Audience = $auth.JwtSettings.Audience
    Inventory__Upstream__ProductMasterBaseUrl = 'http://127.0.0.1:4010/'
    Inventory__Upstream__LocationMasterBaseUrl = 'http://127.0.0.1:4011/'
    PlatformRegistration__BaseUrl = 'http://localhost:5057'
    PlatformRegistration__InternalApiKey = $platform.AuthService.InternalApiKey
}))

$webDir = Join-Path $repo 'frontend\Diten.Web'
$webEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS = "http://localhost:$WebPort"
}
# A whitespace value: an empty one would delete the variable and leave the appsettings key in force.
if ($WebWithoutTenantLiveness) { $webEnvironment['Platform__InternalApiKey'] = ' ' }
[void]$started.Add((Start-Logged 'diten-web' 'dotnet' "`"$(Join-Path $webDir 'bin\Release\net8.0\Diten.Web.dll')`"" $webDir $webEnvironment))

$ready = [ordered]@{
    prismProductMaster = Wait-Port 4010 60
    prismLocation = Wait-Port 4011 60
    inventoryService = Wait-Port $ServicePort 90
    web = Wait-Port $WebPort 120
}
[ordered]@{ processes = $started; ready = $ready; database = $MongoDatabase; webPort = $WebPort } | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $StateFile
Get-Content -Raw $StateFile
