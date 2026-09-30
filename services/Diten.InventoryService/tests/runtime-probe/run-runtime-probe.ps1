<#
.SYNOPSIS
  WP-0173-S1 section R - runtime E4 probe of Diten.InventoryService (MOD-0173) as a REAL process on port 5066.

.DESCRIPTION
  1. Builds the Api and the probe console.
  2. Starts Prism 4.10.5 mocks of the FROZEN PRODUCT-MASTER (4010) and LOCATION (4011) contracts - one node process
     each, on the cached CLI entry point (no cmd/npx chain, so nothing is orphaned).
  3. Starts the service (environment Mock, JWT secret minted for this run and passed by environment variable -
     never written to a file) and waits for /health.
  4. Runs the probe "flows" phase (AC2, AC3, AC4, AC5, AC7, AC9, AC10): request, response, X-Correlation-Id and the
     persisted Mongo documents are written as JSON evidence.
  5. STOPS the service process, starts a NEW process from the same freshly built binary, and runs the probe
     "after-restart" phase. PIDs, start times and the binary's timestamp + SHA-256 are recorded in r-process.json.
  6. Stops everything it started.

.PARAMETER EvidenceDir
  Where the JSON / log evidence is written (created if missing).
#>
param(
    [Parameter(Mandatory = $true)][string]$EvidenceDir,
    [int]$ServicePort = 5066,
    [int]$ProductMasterPort = 4010,
    [int]$LocationPort = 4011,
    [string]$MongoConnectionString = 'mongodb://localhost:27017',
    [string]$MongoDatabase = 'diten_inventory_mock'
)

$ErrorActionPreference = 'Stop'
$serviceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$repoRoot = (Resolve-Path (Join-Path $serviceRoot '..\..')).Path
$apiProject = Join-Path $serviceRoot 'src\Diten.InventoryService.Api'
$probeProject = Join-Path $serviceRoot 'tests\Diten.InventoryService.RuntimeProbe'
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null
$EvidenceDir = (Resolve-Path $EvidenceDir).Path
$started = New-Object System.Collections.ArrayList

function Assert-PortFree([int]$port) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "Port $port is already in use; stop whatever listens there first."
    }
}

function Wait-Http([string]$url, [int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        } catch [System.Net.WebException] {
            if ($_.Exception.Response) { return $_.Exception.Response }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "No HTTP answer from $url within $seconds s."
}

function Start-Service-Process([string]$label) {
    $env:ASPNETCORE_ENVIRONMENT = 'Mock'
    $env:JwtSettings__Secret = $script:secret
    $env:Mongo__ConnectionString = $MongoConnectionString
    $env:Mongo__DatabaseName = $MongoDatabase
    $env:Kestrel__Endpoints__Http__Url = "http://localhost:$ServicePort"
    $env:Inventory__Upstream__ProductMasterBaseUrl = "http://127.0.0.1:$ProductMasterPort/"
    $env:Inventory__Upstream__LocationMasterBaseUrl = "http://127.0.0.1:$LocationPort/"
    $p = Start-Process -FilePath 'dotnet' -ArgumentList "`"$script:dll`"" -WorkingDirectory $script:binDir -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $EvidenceDir "service-$label.log") -RedirectStandardError (Join-Path $EvidenceDir "service-$label.err.log")
    [void]$started.Add($p)
    $health = Wait-Http "http://localhost:$ServicePort/health" 60
    $proc = Get-Process -Id $p.Id
    return [ordered]@{
        label = $label
        pid = $p.Id
        processStartTime = $proc.StartTime.ToString('o')
        healthStatus = [int]$health.StatusCode
        healthBody = [string]$health.Content
    }
}

function Stop-Tracked($process) {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(15000) | Out-Null
    }
}

try {
    foreach ($port in @($ServicePort, $ProductMasterPort, $LocationPort)) { Assert-PortFree $port }

    # -- build -------------------------------------------------------------------------------------------
    & dotnet build $apiProject -c Debug -nologo -v q | Out-File (Join-Path $EvidenceDir 'probe-build.log') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Api build failed.' }
    & dotnet build $probeProject -c Debug -nologo -v q | Out-File (Join-Path $EvidenceDir 'probe-build.log') -Append -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
    $script:binDir = Join-Path $apiProject 'bin\Debug\net8.0'
    $script:dll = Join-Path $script:binDir 'Diten.InventoryService.Api.dll'
    $dllInfo = Get-Item $script:dll
    $script:secret = 'probe-' + [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')

    # -- Prism mocks of the frozen consumed contracts ----------------------------------------------------
    $productSpec = (Get-ChildItem -Path (Join-Path $repoRoot 'docs') -Recurse -Filter 'product-master-bundle.openapi.yaml' | Select-Object -First 1).FullName
    $locationSpec = (Get-ChildItem -Path (Join-Path $repoRoot 'docs') -Recurse -Filter 'location.openapi.yaml' | Select-Object -First 1).FullName
    $prismPackage = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\package.json" -ErrorAction SilentlyContinue |
        Where-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).version -eq '4.10.5' } | Select-Object -First 1
    if (-not $prismPackage) {
        & cmd /c 'npx --yes @stoplight/prism-cli@4.10.5 --version' | Out-Null
        $prismPackage = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\package.json" |
            Where-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).version -eq '4.10.5' } | Select-Object -First 1
    }
    $prismJs = Join-Path $prismPackage.Directory.FullName 'dist\index.js'

    foreach ($mock in @(@{ name = 'product-master'; spec = $productSpec; port = $ProductMasterPort },
                        @{ name = 'location'; spec = $locationSpec; port = $LocationPort })) {
        $p = Start-Process -FilePath 'node' -ArgumentList @("`"$prismJs`"", 'mock', "`"$($mock.spec)`"", '--port', $mock.port, '--host', '127.0.0.1') `
            -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $EvidenceDir "prism-$($mock.name).log") `
            -RedirectStandardError (Join-Path $EvidenceDir "prism-$($mock.name).err.log")
        [void]$started.Add($p)
        Wait-Http "http://127.0.0.1:$($mock.port)/" 90 | Out-Null
    }

    # -- run 1 -------------------------------------------------------------------------------------------
    $run1 = Start-Service-Process 'run1'
    $env:PROBE_JWT_SECRET = $script:secret
    $env:PROBE_MONGO = $MongoConnectionString
    $env:PROBE_DB = $MongoDatabase
    $state = Join-Path $env:TEMP ("mod0173-probe-state-" + [guid]::NewGuid().ToString('N') + '.json')
    & dotnet run --project $probeProject --no-build -- flows "http://localhost:$ServicePort/" $EvidenceDir $state
    $flowsExit = $LASTEXITCODE

    # -- restart: stop the process, start a NEW one from the same built binary ---------------------------
    $first = $started | Where-Object { $_.Id -eq $run1.pid }
    Stop-Tracked $first
    $stoppedAt = (Get-Date).ToString('o')
    $run2 = Start-Service-Process 'run2-after-restart'
    & dotnet run --project $probeProject --no-build -- after-restart "http://localhost:$ServicePort/" $EvidenceDir $state
    $restartExit = $LASTEXITCODE

    $processEvidence = [ordered]@{
        headCommit = (& git -C $repoRoot rev-parse HEAD)
        binary = [ordered]@{
            path = $dllInfo.FullName.Replace($repoRoot, '<repo>')
            lastWriteTime = $dllInfo.LastWriteTime.ToString('o')
            sha256 = (Get-FileHash $script:dll -Algorithm SHA256).Hash
        }
        environment = 'Mock (EnforceLocationLegalEntityMatch=false - Prism static locations carry le-tr; ASSUMPTION-0173-07)'
        mongo = "$MongoConnectionString / $MongoDatabase"
        prism = [ordered]@{ productMaster = "127.0.0.1:$ProductMasterPort"; location = "127.0.0.1:$LocationPort"; entryPoint = '@stoplight/prism-cli 4.10.5' }
        run1 = $run1
        run1StoppedAt = $stoppedAt
        run2 = $run2
        restartedWithSameBinary = ($run2.processStartTime -gt $dllInfo.LastWriteTime.ToString('o')) -and ($run1.pid -ne $run2.pid)
        probeFlowsExitCode = $flowsExit
        probeAfterRestartExitCode = $restartExit
    }
    $processEvidence | ConvertTo-Json -Depth 5 | Out-File (Join-Path $EvidenceDir 'r-process.json') -Encoding utf8
    Remove-Item $state -ErrorAction SilentlyContinue

    Write-Host "flows exit=$flowsExit after-restart exit=$restartExit run1.pid=$($run1.pid) run2.pid=$($run2.pid)"
    if ($flowsExit -ne 0 -or $restartExit -ne 0) { exit 1 }
}
finally {
    foreach ($p in $started) { Stop-Tracked $p }
    # Prism debug logs echo the forwarded bearer; evidence never keeps a token, even a throwaway one.
    Get-ChildItem -Path $EvidenceDir -Filter 'prism-*.log' -ErrorAction SilentlyContinue | ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $masked = [regex]::Replace($text, 'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '***(masked-jwt)')
        [System.IO.File]::WriteAllText($_.FullName, $masked)
    }
    foreach ($name in @('JwtSettings__Secret', 'PROBE_JWT_SECRET')) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
}
