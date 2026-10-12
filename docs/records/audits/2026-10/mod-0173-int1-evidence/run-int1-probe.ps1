<#
.SYNOPSIS
  WP-0173-INT1 D3 - end-to-end probe THROUGH THE GATEWAY: client -> Diten.ApiGateway (temporary port 5090) ->
  Diten.InventoryService (5066, Mock environment) -> Prism mocks of PRODUCT-MASTER (4010) / LOCATION (4011).

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
        gatewayResponse = [ordered]@{ status = [int]$response.StatusCode; xCorrelationId = $correlation; body = $parsed }
    }
    [void]$script:steps.Add($step)
    return $step
}

$checks = New-Object System.Collections.ArrayList
function Check([string]$name, [bool]$ok) { [void]$script:checks.Add([ordered]@{ check = $name; passed = $ok }) }

try {
    foreach ($p in @($GatewayPort, $ServicePort, 4010, 4011)) { Assert-PortFree $p }
    $script:secret = 'int1-' + [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')

    # -- build (no source change in the service; the gateway carries the new routes) --
    & dotnet build (Join-Path $repo 'gateway\Diten.ApiGateway\Diten.ApiGateway.csproj') -c Debug -nologo -v q | Out-File (Join-Path $EvidenceDir 'd3-build.log') -Encoding utf8
    & dotnet build (Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\Diten.InventoryService.Api.csproj') -c Debug -nologo -v q | Out-File (Join-Path $EvidenceDir 'd3-build.log') -Append -Encoding utf8

    # -- Prism (one node process each, cached CLI) --
    $prismPackage = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\package.json" |
        Where-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).version -eq '4.10.5' } | Select-Object -First 1
    $prismJs = Join-Path $prismPackage.Directory.FullName 'dist\index.js'
    $productSpec = (Get-ChildItem (Join-Path $repo 'docs') -Recurse -Filter 'product-master-bundle.openapi.yaml' | Select-Object -First 1).FullName
    $locationSpec = (Get-ChildItem (Join-Path $repo 'docs') -Recurse -Filter 'location.openapi.yaml' | Select-Object -First 1).FullName
    Start-Tracked 'prism-product-master' 'node' @($prismJs, 'mock', $productSpec, '--port', '4010', '--host', '127.0.0.1') $repo @{} | Out-Null
    Start-Tracked 'prism-location' 'node' @($prismJs, 'mock', $locationSpec, '--port', '4011', '--host', '127.0.0.1') $repo @{} | Out-Null
    Wait-Http 'http://127.0.0.1:4010/' 90 | Out-Null
    Wait-Http 'http://127.0.0.1:4011/' 90 | Out-Null

    # -- InventoryService (Mock) --
    $serviceBin = Join-Path $repo 'services\Diten.InventoryService\src\Diten.InventoryService.Api\bin\Debug\net8.0'
    Start-Tracked 'service' 'dotnet' @((Join-Path $serviceBin 'Diten.InventoryService.Api.dll')) $serviceBin @{
        ASPNETCORE_ENVIRONMENT = 'Mock'
        JwtSettings__Secret = $script:secret
        Mongo__DatabaseName = $MongoDatabase
        Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
        Inventory__Upstream__ProductMasterBaseUrl = 'http://127.0.0.1:4010/'
        Inventory__Upstream__LocationMasterBaseUrl = 'http://127.0.0.1:4011/'
        'Logging__LogLevel__Microsoft.AspNetCore' = 'Information'
    } | Out-Null
    Wait-Http "http://localhost:$ServicePort/health" 60 | Out-Null

    # -- Gateway (Development, temporary port) --
    $gatewayBin = Join-Path $repo 'gateway\Diten.ApiGateway\bin\Debug\net8.0'
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
        permission = @('supplychain.inventory.read', 'supplychain.inventory.movements.post', 'supplychain.inventory.reservations.manage') }
    $item = 'b1f2c3d4-0000-0000-0000-000000000001'; $sku = 'c3d4e5f6-0000-0000-0000-000000000002'
    $run = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')

    # a) movement + replay
    $movement = [ordered]@{ idempotencyKey = "int1-$run-open"; movementType = 'OPENING_BALANCE'; itemId = $item; skuId = $sku; skuLevel = 'Lsku';
        quantity = '100'; uomId = 'EA'; lotId = 'lot-777'; toWarehouseId = 'wh-01'; toLocationId = 'loc-A-12-3'; sourceModule = 'MOD-PROBE';
        sourceType = 'INT1'; transactionDate = '2026-10-01T09:00:00Z'; postingDate = '2026-10-01T10:00:00Z' }
    $correlation = [guid]::NewGuid().ToString()
    $a1 = Send-Step 'a) POST /api/inventory/movements OPENING_BALANCE 100 (+ d: X-Correlation-Id UUID)' 'POST' '/api/inventory/movements' $movement @{ 'Idempotency-Key' = $movement.idempotencyKey; 'X-Correlation-Id' = $correlation } $token
    Check 'a) movement through gateway -> 201' ($a1.gatewayResponse.status -eq 201)
    $a2 = Send-Step 'a) same Idempotency-Key again (replay)' 'POST' '/api/inventory/movements' $movement @{ 'Idempotency-Key' = $movement.idempotencyKey } $token
    Check 'a) replay -> 200 idempotentReplay:true' (($a2.gatewayResponse.status -eq 200) -and ($a2.gatewayResponse.body.idempotentReplay -eq $true))

    # b) availability
    $b = Send-Step 'b) GET /api/inventory/availability' 'GET' "/api/inventory/availability?skuId=$sku" $null @{} $token
    Check 'b) availability 200 onHand 100.000 / available 100.000' (($b.gatewayResponse.status -eq 200) -and ($b.gatewayResponse.body.onHand -eq '100.000') -and ($b.gatewayResponse.body.available -eq '100.000'))

    # c) reservation + release (If-Match through the gateway)
    $reservation = [ordered]@{ idempotencyKey = "int1-$run-rsv"; itemId = $item; skuId = $sku; skuLevel = 'Lsku'; quantity = '30';
        reservationType = 'SALES_ORDER'; sourceModule = 'MOD-0172'; sourceDocumentId = 'SO-INT1'; sourceLineId = '1' }
    $c1 = Send-Step 'c) POST /api/inventory/reservations 30' 'POST' '/api/inventory/reservations' $reservation @{ 'Idempotency-Key' = $reservation.idempotencyKey } $token
    Check 'c) reservation -> 201 with etag W/"1"' (($c1.gatewayResponse.status -eq 201) -and ($c1.gatewayResponse.body.etag -eq 'W/"1"'))
    $rid = $c1.gatewayResponse.body.reservationId
    $c2 = Send-Step 'c) release WITHOUT If-Match' 'POST' "/api/inventory/reservations/$rid/release" $null @{} $token
    Check 'c) release without If-Match -> 400' ($c2.gatewayResponse.status -eq 400)
    $c3 = Send-Step 'c) release with wrong If-Match W/"9"' 'POST' "/api/inventory/reservations/$rid/release" $null @{ 'If-Match' = 'W/"9"' } $token
    Check 'c) wrong If-Match -> 409 RESERVATION_CONFLICT' (($c3.gatewayResponse.status -eq 409) -and ($c3.gatewayResponse.body.error.code -eq 'RESERVATION_CONFLICT'))
    $c4 = Send-Step 'c) release with correct If-Match W/"1"' 'POST' "/api/inventory/reservations/$rid/release" $null @{ 'If-Match' = 'W/"1"' } $token
    Check 'c) correct If-Match -> 200 RELEASED' (($c4.gatewayResponse.status -eq 200) -and ($c4.gatewayResponse.body.status -eq 'RELEASED'))
    $c5 = Send-Step 'c) release again with the now-stale If-Match W/"1"' 'POST' "/api/inventory/reservations/$rid/release" $null @{ 'If-Match' = 'W/"1"' } $token
    Check 'c) stale If-Match -> 409' ($c5.gatewayResponse.status -eq 409)

    # d) correlation
    Check 'd) caller X-Correlation-Id echoed by the gateway response' (@($a1.gatewayResponse.xCorrelationId) -contains $correlation)
    $d2 = Send-Step 'd) request WITHOUT X-Correlation-Id (gateway mints one)' 'GET' "/api/inventory/balance?skuId=$sku" $null @{} $token
    Check 'd) no caller correlation -> 200 (gateway-minted id accepted by the service)' ($d2.gatewayResponse.status -eq 200)

    # e) no token
    $e = Send-Step 'e) no token' 'GET' "/api/inventory/balance?skuId=$sku" $null @{} $null
    Check 'e) no token -> 401 from the service through the gateway (contract Error body)' (($e.gatewayResponse.status -eq 401) -and ($e.gatewayResponse.body.contractVersion -eq 'v1'))

    # f) method not routed
    $f = Send-Step 'f) PUT /api/inventory/movements' 'PUT' '/api/inventory/movements' $movement @{ 'Idempotency-Key' = 'int1-put' } $token
    Check 'f) PUT -> 404 at the gateway (method not routed)' ($f.gatewayResponse.status -eq 404)

    $script:probe = [ordered]@{ tenantId = $tenant; legalEntityId = $le; userId = $user; callerCorrelationId = $correlation; reservationId = $rid; mongoDatabase = $MongoDatabase }
}
finally {
    $remaining = @()
    foreach ($s in $started) {
        try { if (-not $s.Process.HasExited) { $s.Process.Kill(); $s.Process.WaitForExit(15000) | Out-Null } } catch { }
        foreach ($ev in $s.Events) { Unregister-Event -SourceIdentifier $ev.Name -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Seconds 1
    $remaining = @(Get-CimInstance Win32_Process | Where-Object {
        ($_.Name -in @('node.exe', 'dotnet.exe')) -and ($_.CommandLine -match 'prism-cli|Diten\.InventoryService\.Api\.dll|Diten\.ApiGateway\.dll')
    } | ForEach-Object { "$($_.ProcessId) $($_.Name)" })
    [ordered]@{ startedProcesses = @($started | ForEach-Object { "$($_.Name) pid=$($_.Process.Id)" }); remainingAfterStop = $remaining } |
        ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd3-processes.json') -Encoding utf8
    foreach ($log in Get-ChildItem $EvidenceDir -Filter '*.log' -ErrorAction SilentlyContinue) {
        $text = [IO.File]::ReadAllText($log.FullName)
        [IO.File]::WriteAllText($log.FullName, [regex]::Replace($text, 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '***(masked-jwt)'))
    }
}

[ordered]@{ probe = $script:probe; steps = $steps } | ConvertTo-Json -Depth 10 | Out-File (Join-Path $EvidenceDir 'd3-steps.json') -Encoding utf8
$failed = @($checks | Where-Object { -not $_.passed }).Count
[ordered]@{ passed = $checks.Count - $failed; failed = $failed; checks = $checks } | ConvertTo-Json -Depth 4 | Out-File (Join-Path $EvidenceDir 'd3-checks.json') -Encoding utf8
Write-Host "D3: $($checks.Count - $failed)/$($checks.Count) checks passed"
foreach ($c in $checks | Where-Object { -not $_.passed }) { Write-Host "  FAILED: $($c.check)" }
if ($failed -gt 0) { exit 1 }
