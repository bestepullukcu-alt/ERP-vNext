<#
.SYNOPSIS
  WP-0173-INT2 D3 - end-to-end probe of /api/locations THROUGH THE GATEWAY: client -> Diten.ApiGateway (temporary
  port 5090) -> Diten.InventoryService (5066, Mock environment). Copy of the INT-1 probe (run-int1-probe.ps1, helpers
  unchanged); Prism serves PRODUCT-MASTER (4010) only - the LOCATION master is in the service (MOD-0173-FU02), its
  HTTP client and the org-unit client point at a closed port and are never called.

.DESCRIPTION
  - The user's own gateway on port 5000 is never touched; the probe gateway runs on -GatewayPort.
  - A JWT secret is minted for this run and handed to the service AND the gateway through per-process
    environment variables only (never written to a file); every token in the evidence is masked.
  - Each step records: request (Authorization masked), gateway response (status, X-Correlation-Id, body) and the
    gateway / service log lines for that request.
  - All started processes are stopped at the end; the remaining-process check is written to the evidence.
#>
param(
    [Parameter(Mandatory = $true)][string]$EvidenceDir,
    [int]$GatewayPort = 5090,
    [int]$ServicePort = 5066,
    [int]$ProductMasterPort = 4010,
    [string]$MongoDatabase = 'diten_inventory_mock'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$EvidenceDir = (New-Item -ItemType Directory -Force -Path $EvidenceDir).FullName
$started = New-Object System.Collections.ArrayList
$steps = New-Object System.Collections.ArrayList

function Assert-PortFree([int]$port) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is in use." }
}

function Start-Tracked([string]$name, [string]$file, [string[]]$arguments, [string]$workDir, [hashtable]$environment) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $file
    $psi.Arguments = ($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $psi.WorkingDirectory = $workDir
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    foreach ($key in $environment.Keys) { $psi.EnvironmentVariables[$key] = [string]$environment[$key] }
    $process = [System.Diagnostics.Process]::Start($psi)
    $log = Join-Path $EvidenceDir "$name.log"
    $out = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action { if ($EventArgs.Data) { Add-Content -Path $Event.MessageData -Value $EventArgs.Data } } -MessageData $log
    $err = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action { if ($EventArgs.Data) { Add-Content -Path $Event.MessageData -Value $EventArgs.Data } } -MessageData $log
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
    [void]$started.Add([pscustomobject]@{ Name = $name; Process = $process; Events = @($out, $err) })
    return $process
}

function Wait-Http([string]$url, [int]$seconds) {
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(2)
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        try { $r = $client.GetAsync($url).Result; if ($r) { return [int]$r.StatusCode } } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "No HTTP answer from $url within $seconds s."
}

function B64Url([byte[]]$bytes) { [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') }

function New-Jwt([hashtable]$claims) {
    $header = B64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $payload = B64Url ([Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress -Depth 4)))
    $hmac = New-Object System.Security.Cryptography.HMACSHA256 (, [Text.Encoding]::UTF8.GetBytes($script:secret))
    $signature = B64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$header.$payload")))
    return "$header.$payload.$signature"
}

function Send-Step([string]$label, [string]$method, [string]$path, $body, [hashtable]$headers, [string]$token) {
    $client = New-Object System.Net.Http.HttpClient
    $request = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($method)), ("http://localhost:$GatewayPort" + $path)
    $shownHeaders = [ordered]@{}
    if ($token) { [void]$request.Headers.TryAddWithoutValidation('Authorization', "Bearer $token"); $shownHeaders['Authorization'] = 'Bearer ***(masked)' }
    foreach ($k in $headers.Keys) { [void]$request.Headers.TryAddWithoutValidation($k, [string]$headers[$k]); $shownHeaders[$k] = [string]$headers[$k] }
    if ($null -ne $body) {
        $request.Content = New-Object System.Net.Http.StringContent (($body | ConvertTo-Json -Depth 5 -Compress), [Text.Encoding]::UTF8, 'application/json')
    }
    $response = $client.SendAsync($request).Result
    $text = $response.Content.ReadAsStringAsync().Result
    $correlation = $null
    $values = $null
    if ($response.Headers.TryGetValues('X-Correlation-Id', [ref]$values)) { $correlation = @($values) }
    $parsed = $null
    try { $parsed = $text | ConvertFrom-Json } catch { $parsed = $text }
    $step = [ordered]@{
        step = $label
        request = [ordered]@{ method = $method; url = "http://localhost:$GatewayPort$path"; headers = $shownHeaders; body = $body }
        gatewayResponse = [ordered]@{ status = [int]$response.StatusCode; xCorrelationId = $correlation; etag = $(if ($response.Headers.ETag) { $response.Headers.ETag.ToString() } else { $null }); body = $parsed }
    }
    [void]$script:steps.Add($step)
    return $step
}

$checks = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok) { [void]$script:checks.Add([ordered]@{ check = $name; passed = $ok }) }

try {
    foreach ($p in @($GatewayPort, $ServicePort, $ProductMasterPort)) { Assert-PortFree $p }
    $script:secret = 'int2-' + [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')

    # -- build (no source change in the service; the gateway carries the new routes) --
    # Release, not Debug: a gateway / InventoryService the user runs from bin\Debug keeps its files locked and is
    # never rebuilt or restarted by this probe (INT-2 note).
    & dotnet build (Join-Path $repo 'gateway\Diten.ApiGateway\Diten.ApiGateway.csproj') -c Release -nologo -v q | Out-File (Join-Path $EvidenceDir 'd3-build.log') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Gateway build failed.' }
    & dotnet build (Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\Diten.InventoryService.Api.csproj') -c Release -nologo -v q | Out-File (Join-Path $EvidenceDir 'd3-build.log') -Append -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'InventoryService build failed.' }

    # -- Prism (one node process each, cached CLI) --
    $prismPackage = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\package.json" |
        Where-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).version -eq '4.10.5' } | Select-Object -First 1
    $prismJs = Join-Path $prismPackage.Directory.FullName 'dist\index.js'
    $productSpec = (Get-ChildItem (Join-Path $repo 'docs') -Recurse -Filter 'product-master-bundle.openapi.yaml' | Select-Object -First 1).FullName
    Start-Tracked 'prism-product-master' 'node' @($prismJs, 'mock', $productSpec, '--port', [string]$ProductMasterPort, '--host', '127.0.0.1') $repo @{} | Out-Null
    Wait-Http "http://127.0.0.1:$ProductMasterPort/" 90 | Out-Null

    # -- InventoryService (Mock) --
    $serviceBin = Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\bin\Release\net8.0'
    Start-Tracked 'service' 'dotnet' @((Join-Path $serviceBin 'Diten.InventoryService.Api.dll')) $serviceBin @{
        ASPNETCORE_ENVIRONMENT = 'Mock'
        JwtSettings__Secret = $script:secret
        Mongo__DatabaseName = $MongoDatabase
        Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
        Inventory__Upstream__ProductMasterBaseUrl = "http://127.0.0.1:$ProductMasterPort/"
        Inventory__Upstream__LocationMasterBaseUrl = 'http://127.0.0.1:9/'
        Inventory__Upstream__OrgUnitDirectoryBaseUrl = 'http://127.0.0.1:9/'
        'Logging__LogLevel__Microsoft.AspNetCore' = 'Information'
    } | Out-Null
    Wait-Http "http://localhost:$ServicePort/health" 60 | Out-Null

    # -- Gateway (Development, temporary port) --
    # The gateway runs from a scratch COPY of its Release output. Its ocelot.json is first proven byte-identical to the
    # committed file; when the probe service cannot use 5066 (taken by the user's own InventoryService), ONLY the
    # "Port" of the two /api/locations routes is rewritten in the copy, and the diff is written to the evidence.
    $builtGateway = Join-Path $repo 'gateway\Diten.ApiGateway\bin\Release\net8.0'
    $script:gatewayCopy = Join-Path $env:TEMP ('int2-gateway-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -Path $builtGateway -Destination $script:gatewayCopy -Recurse
    $gatewayBin = $script:gatewayCopy
    $committed = Join-Path $repo 'gateway\Diten.ApiGateway\ocelot.json'
    $copied = Join-Path $gatewayBin 'ocelot.json'
    $override = [ordered]@{
        committedSha256 = (Get-FileHash $committed -Algorithm SHA256).Hash
        copiedBeforeOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
        servicePort = $ServicePort
        changedLines = @()
    }
    if ($override.committedSha256 -ne $override.copiedBeforeOverrideSha256) { throw 'Built ocelot.json differs from the committed file.' }
    if ($ServicePort -ne 5066) {
        $lines = [IO.File]::ReadAllText($copied) -split "`r`n"
        $inLocationRoute = $false
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -match '"DownstreamPathTemplate": "/api/locations') { $inLocationRoute = $true }
            elseif ($lines[$i] -match '"DownstreamPathTemplate"') { $inLocationRoute = $false }
            if ($inLocationRoute -and $lines[$i] -match '^\s*"Port": 5066$') {
                $new = $lines[$i] -replace '5066', [string]$ServicePort
                $override.changedLines += "line $($i + 1): '$($lines[$i].Trim())' -> '$($new.Trim())'"
                $lines[$i] = $new
            }
        }
        [IO.File]::WriteAllText($copied, ($lines -join "`r`n"))
        if ($override.changedLines.Count -ne 2) { throw "Expected to rewrite exactly 2 ports, rewrote $($override.changedLines.Count)." }
    }
    $override.copiedAfterOverrideSha256 = (Get-FileHash $copied -Algorithm SHA256).Hash
    $override | ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd3-gateway-copy-override.json') -Encoding utf8
    Start-Tracked 'gateway' 'dotnet' @((Join-Path $gatewayBin 'Diten.ApiGateway.dll')) $gatewayBin @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = "http://localhost:$GatewayPort"
        JwtSettings__Secret = $script:secret
    } | Out-Null
    Wait-Http "http://localhost:$GatewayPort/health/live" 60 | Out-Null

    # -- tokens --
    $tenant = [guid]::NewGuid().ToString(); $le = [guid]::NewGuid().ToString(); $user = [guid]::NewGuid().ToString()
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $token = New-Jwt @{ sub = $user; tenant_id = $tenant; legal_entity_id = $le; iss = 'diten-auth-service'; aud = 'diten-erp';
        nbf = $now - 60; iat = $now - 60; exp = $now + 900; actor_type = 'tenant_user';
        permission = @('inventory.locations.read', 'inventory.locations.manage') }
    $run = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')

    # a) create a Site through the gateway (Idempotency-Key) + replay; d) X-Correlation-Id on the first call
    $site = [ordered]@{ idempotencyKey = "int2-$run-site"; code = "SITE-INT2-$run"; name = 'INT-2 probe site'; type = 'Site' }
    $correlation = [guid]::NewGuid().ToString()
    $a1 = Send-Step 'a) POST /api/locations Site (+ e: X-Correlation-Id UUID)' 'POST' '/api/locations' $site @{ 'Idempotency-Key' = $site.idempotencyKey; 'X-Correlation-Id' = $correlation } $token
    Check 'a) create through the gateway -> 201 with ETag W/"1"' (($a1.gatewayResponse.status -eq 201) -and ($a1.gatewayResponse.etag -eq 'W/"1"'))
    $siteId = $a1.gatewayResponse.body.locationId
    $a2 = Send-Step 'a) same Idempotency-Key again (replay)' 'POST' '/api/locations' $site @{ 'Idempotency-Key' = $site.idempotencyKey } $token
    Check 'a) replay -> 200 with the same locationId' (($a2.gatewayResponse.status -eq 200) -and ($a2.gatewayResponse.body.locationId -eq $siteId))

    # b) ETag out, If-Match in, through the gateway
    $b1 = Send-Step 'b) GET /api/locations/{id}' 'GET' "/api/locations/$siteId" $null @{} $token
    Check 'b) GET -> 200 with ETag W/"1" (ETag passes the gateway)' (($b1.gatewayResponse.status -eq 200) -and ($b1.gatewayResponse.etag -eq 'W/"1"'))
    $b2 = Send-Step 'b) PATCH name with If-Match W/"1"' 'PATCH' "/api/locations/$siteId" ([ordered]@{ name = 'INT-2 probe site (renamed)' }) @{ 'If-Match' = 'W/"1"' } $token
    Check 'b) PATCH with the current If-Match -> 200, ETag W/"2" (If-Match passes the gateway)' (($b2.gatewayResponse.status -eq 200) -and ($b2.gatewayResponse.etag -eq 'W/"2"') -and ($b2.gatewayResponse.body.version -eq 2))
    $b3 = Send-Step 'b) PATCH again with the now-stale If-Match W/"1"' 'PATCH' "/api/locations/$siteId" ([ordered]@{ name = 'stale' }) @{ 'If-Match' = 'W/"1"' } $token
    Check 'b) stale If-Match -> 409 LOCATION_CONFLICT' (($b3.gatewayResponse.status -eq 409) -and ($b3.gatewayResponse.body.error.code -eq 'LOCATION_CONFLICT'))
    $b4 = Send-Step 'b) PATCH without If-Match' 'PATCH' "/api/locations/$siteId" ([ordered]@{ name = 'none' }) @{} $token
    Check 'b) missing If-Match -> 400 INVALID_REQUEST' (($b4.gatewayResponse.status -eq 400) -and ($b4.gatewayResponse.body.error.code -eq 'INVALID_REQUEST'))

    # c) bulk-bins dry run under a StorageLocation: preview, nothing written
    $wh = Send-Step 'c) POST /api/locations Warehouse' 'POST' '/api/locations' ([ordered]@{ idempotencyKey = "int2-$run-wh"; code = "WH-INT2-$run"; name = 'INT-2 warehouse'; type = 'Warehouse'; parentId = $siteId }) @{ 'Idempotency-Key' = "int2-$run-wh" } $token
    $sl = Send-Step 'c) POST /api/locations StorageLocation' 'POST' '/api/locations' ([ordered]@{ idempotencyKey = "int2-$run-sl"; code = "SL-INT2-$run"; name = 'INT-2 storage'; type = 'StorageLocation'; parentId = $wh.gatewayResponse.body.locationId }) @{ 'Idempotency-Key' = "int2-$run-sl" } $token
    $slId = $sl.gatewayResponse.body.locationId
    $before = Send-Step 'c) GET /api/locations (count before the dry run)' 'GET' '/api/locations' $null @{} $token
    $bulk = [ordered]@{ idempotencyKey = "int2-$run-bulk"; aisleFrom = 'A'; aisleTo = 'B'; rackFrom = 1; rackTo = 2; levelFrom = 1; levelTo = 1; positionsPerLevel = 2; dryRun = $true }
    $c1 = Send-Step 'c) POST /api/locations/{id}/bins/bulk dryRun (2 aisles x 2 racks x 1 level x 2 positions)' 'POST' "/api/locations/$slId/bins/bulk" $bulk @{ 'Idempotency-Key' = $bulk.idempotencyKey } $token
    $after = Send-Step 'c) GET /api/locations (count after the dry run)' 'GET' '/api/locations' $null @{} $token
    Check 'c) dry run -> 200 preview of 8 codes (A-01-01-01 first)' (($c1.gatewayResponse.status -eq 200) -and ($c1.gatewayResponse.body.count -eq 8) -and ($c1.gatewayResponse.body.toCreate[0] -eq 'A-01-01-01'))
    Check 'c) dry run wrote nothing (3 locations before and after)' (($before.gatewayResponse.body.total -eq 3) -and ($after.gatewayResponse.body.total -eq 3))

    # d) path and history (+ the literal /search route)
    $d1 = Send-Step 'd) GET /api/locations/{id}/path' 'GET' "/api/locations/$slId/path" $null @{} $token
    Check 'd) path -> 200 Site > Warehouse > StorageLocation' (($d1.gatewayResponse.status -eq 200) -and ((@($d1.gatewayResponse.body.path | ForEach-Object { $_.type }) -join '>') -eq 'Site>Warehouse>StorageLocation'))
    $d2 = Send-Step 'd) GET /api/locations/{id}/history' 'GET' "/api/locations/$siteId/history" $null @{} $token
    Check 'd) history -> 200 Updated(name) + Created, newest first' (($d2.gatewayResponse.status -eq 200) -and ((@($d2.gatewayResponse.body.items | ForEach-Object { $_.action }) -join ',') -eq 'Updated,Created'))
    $d3 = Send-Step 'd) GET /api/locations/search?type=Site' 'GET' '/api/locations/search?type=Site' $null @{} $token
    Check 'd) search -> 200 (literal segment routed, not taken for {id})' (($d3.gatewayResponse.status -eq 200) -and ($d3.gatewayResponse.body.total -eq 1))

    # e) correlation
    Check 'e) caller X-Correlation-Id echoed in the gateway response' (@($a1.gatewayResponse.xCorrelationId) -contains $correlation)
    $hist = $d2.gatewayResponse.body.items | Where-Object { $_.action -eq 'Created' }
    Check 'e) the same UUID is persisted on the Created history row' ($hist.correlationId -eq $correlation)

    # f) no token; method not routed
    $f1 = Send-Step 'f) no token' 'GET' "/api/locations/$siteId" $null @{} $null
    Check 'f) no token -> 401 from the service through the gateway (contract Error body)' (($f1.gatewayResponse.status -eq 401) -and ($f1.gatewayResponse.body.contractVersion -eq 'v1'))
    $f2 = Send-Step 'f) PUT /api/locations/{id}' 'PUT' "/api/locations/$siteId" ([ordered]@{ name = 'put' }) @{} $token
    Check 'f) PUT -> 404 at the gateway (method not routed)' ($f2.gatewayResponse.status -eq 404)
    $f3 = Send-Step 'f) DELETE /api/locations/{id}' 'DELETE' "/api/locations/$siteId" $null @{} $token
    Check 'f) DELETE -> 404 at the gateway (method not routed)' ($f3.gatewayResponse.status -eq 404)

    $script:probe = [ordered]@{ tenantId = $tenant; legalEntityId = $le; userId = $user; callerCorrelationId = $correlation; siteId = $siteId; mongoDatabase = $MongoDatabase }
}
finally {
    $remaining = @()
    foreach ($s in $started) {
        try { if (-not $s.Process.HasExited) { $s.Process.Kill(); $s.Process.WaitForExit(15000) | Out-Null } } catch { }
        foreach ($ev in $s.Events) { Unregister-Event -SourceIdentifier $ev.Name -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Seconds 1
    $copyRemoved = $null
    if ($script:gatewayCopy -and (Test-Path $script:gatewayCopy)) {
        Remove-Item -Recurse -Force $script:gatewayCopy
        $copyRemoved = -not (Test-Path $script:gatewayCopy)
    }
    $startedIds = @($started | ForEach-Object { $_.Process.Id })
    $matching = @(Get-CimInstance Win32_Process | Where-Object {
        ($_.Name -in @('node.exe', 'dotnet.exe')) -and ($_.CommandLine -match 'prism-cli|Diten\.InventoryService\.Api\.dll|Diten\.ApiGateway\.dll')
    })
    $remaining = @($matching | Where-Object { $startedIds -contains $_.ProcessId } | ForEach-Object { "$($_.ProcessId) $($_.Name)" })
    # Processes of the same kind this probe did NOT start (e.g. someone else's Prism) - listed, never touched.
    $foreign = @($matching | Where-Object { $startedIds -notcontains $_.ProcessId } | ForEach-Object { "$($_.ProcessId) $($_.Name) created $($_.CreationDate)" })
    [ordered]@{ startedProcesses = @($started | ForEach-Object { "$($_.Name) pid=$($_.Process.Id)" }); remainingAfterStop = $remaining; notStartedByThisProbeUntouched = $foreign; gatewayScratchCopyRemoved = $copyRemoved } |
        ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd3-processes.json') -Encoding utf8
    foreach ($log in Get-ChildItem $EvidenceDir -Filter '*.log' -ErrorAction SilentlyContinue) {
        $text = [IO.File]::ReadAllText($log.FullName)
        [IO.File]::WriteAllText($log.FullName, [regex]::Replace($text, 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '***(masked-jwt)'))
    }
}

[ordered]@{ probe = $script:probe; steps = $steps } | ConvertTo-Json -Depth 10 | Out-File (Join-Path $EvidenceDir 'd3-steps.json') -Encoding utf8
$failed = @($checks | Where-Object { -not $_.passed }).Count
[ordered]@{ passed = $checks.Count - $failed; failed = $failed; checks = $checks } | ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd3-checks.json') -Encoding utf8
Write-Host "D3 (INT-2): $($checks.Count - $failed)/$($checks.Count) checks passed"
foreach ($c in $checks | Where-Object { -not $_.passed }) { Write-Host "  FAILED: $($c.check)" }
if ($failed -gt 0) { exit 1 }
