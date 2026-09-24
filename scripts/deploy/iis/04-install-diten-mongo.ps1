<#
.SYNOPSIS
    Diten vNext icin AYRI bir MongoDB kurar (mevcut MongoDB'ye dokunmaz). Tekrar calistirilabilir.

.DESCRIPTION
    Makinedeki mevcut MongoDB (27017, eski sistem) oldugu gibi kalir. Bu script:
      - MongoDB 7.0.14'u (eski sunucuyla ayni surum) C:\DitenMongo\bin altina acar
      - C:\DitenMongo\data, C:\DitenMongo\log, C:\DitenMongo\mongod.cfg olusturur
      - Yalnizca 127.0.0.1:27018 dinler, replica set "rs0" (Platform servisi transaction ister)
      - WiredTiger onbellegini -CacheSizeGB ile sinirlar (makinede SQL Server, PostgreSQL ve diger MongoDB de var)
      - "DitenMongoDB" adli otomatik baslayan Windows servisi olusturur ve replica set'i baslatir
    Baglanti adresi: mongodb://localhost:27018/?replicaSet=rs0

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\04-install-diten-mongo.ps1
#>
[CmdletBinding()]
param(
    [string]$Root = "C:\DitenMongo",
    [int]$Port = 27018,
    [double]$CacheSizeGB = 3,
    [string]$ServiceName = "DitenMongoDB",
    [string]$MongoZipUrl = "https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-7.0.14.zip",
    [string]$MongoshZipUrl = "https://downloads.mongodb.com/compass/mongosh-2.3.1-win32-x64.zip",
    [string]$MongoToolsZipUrl = "https://fastdl.mongodb.org/tools/db/mongodb-database-tools-windows-x86_64-100.10.0.zip"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = "SilentlyContinue"
$downloads = "C:\DitenMigration\downloads"
$toolsBin = "C:\DitenMigration\tools\bin"
New-Item -ItemType Directory -Force -Path $downloads, $toolsBin | Out-Null

function Ok([string]$t) { Write-Host ("  [OK] " + $t) -ForegroundColor Green }
function Get-File([string]$url, [string]$name) {
    $target = Join-Path $downloads $name
    if (-not (Test-Path $target)) { Write-Host ("  indiriliyor: " + $url); Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing }
    return $target
}
function Expand-Bin([string]$zip, [string]$dest, [string]$mustHave) {
    $tmp = Join-Path $downloads ([IO.Path]::GetFileNameWithoutExtension($zip) + "-extract")
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $bin = Get-ChildItem $tmp -Directory -Recurse | Where-Object { $_.Name -eq "bin" -and (Test-Path (Join-Path $_.FullName $mustHave)) } | Select-Object -First 1
    if (-not $bin) { throw ($mustHave + " arsivde bulunamadi: " + $zip) }
    Copy-Item (Join-Path $bin.FullName "*") -Destination $dest -Recurse -Force
}

$bin = Join-Path $Root "bin"
$mongod = Join-Path $bin "mongod.exe"
$cfg = Join-Path $Root "mongod.cfg"
New-Item -ItemType Directory -Force -Path $bin, (Join-Path $Root "data"), (Join-Path $Root "log") | Out-Null

Write-Host "=== MongoDB ikili dosyalari" -ForegroundColor Cyan
if (-not (Test-Path $mongod)) { Expand-Bin (Get-File $MongoZipUrl "mongodb-7.0.14.zip") $bin "mongod.exe" }
Ok (& $mongod --version | Select-Object -First 1)
if (-not (Test-Path (Join-Path $toolsBin "mongosh.exe"))) { Expand-Bin (Get-File $MongoshZipUrl "mongosh.zip") $toolsBin "mongosh.exe" }
if (-not (Test-Path (Join-Path $toolsBin "mongorestore.exe"))) { Expand-Bin (Get-File $MongoToolsZipUrl "mongodb-database-tools.zip") $toolsBin "mongorestore.exe" }
$mongosh = Join-Path $toolsBin "mongosh.exe"
Ok ("araclar: " + $toolsBin)

Write-Host "=== Ayar dosyasi" -ForegroundColor Cyan
if (Test-Path $cfg) { Ok ("mevcut, degistirilmedi: " + $cfg) }
else {
    @"
storage:
  dbPath: $Root\data
  wiredTiger:
    engineConfig:
      cacheSizeGB: $CacheSizeGB
systemLog:
  destination: file
  logAppend: true
  path: $Root\log\mongod.log
net:
  port: $Port
  bindIp: 127.0.0.1
replication:
  replSetName: rs0
"@ | Set-Content -Path $cfg -Encoding ASCII
    Ok ("yazildi: " + $cfg)
}

Write-Host "=== Servis" -ForegroundColor Cyan
if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    New-Service -Name $ServiceName -DisplayName "MongoDB (Diten vNext)" -StartupType Automatic `
        -BinaryPathName ("`"" + $mongod + "`" --config `"" + $cfg + "`" --service") | Out-Null
    Ok ("servis olusturuldu: " + $ServiceName)
}
if ((Get-Service $ServiceName).Status -ne "Running") { Start-Service $ServiceName; Start-Sleep -Seconds 6 }
Ok ("servis: " + (Get-Service $ServiceName).Status)

Write-Host "=== Replica set" -ForegroundColor Cyan
$direct = "mongodb://127.0.0.1:$Port/?directConnection=true"
$st = & $mongosh $direct --quiet --eval "try { rs.status().ok } catch (e) { e.codeName }" 2>&1
if ("$st" -notmatch '^1$') {
    $init = & $mongosh $direct --quiet --eval "JSON.stringify(rs.initiate({ _id: 'rs0', members: [ { _id: 0, host: 'localhost:$Port' } ] }))" 2>&1
    Write-Host ("  rs.initiate: " + $init)
    Start-Sleep -Seconds 6
}
$check = & $mongosh "mongodb://localhost:$Port/?replicaSet=rs0" --quiet --eval "db.hello().isWritablePrimary + ' / ' + db.version()" 2>&1
if ("$check" -match '^true') { Ok ("PRIMARY hazir, surum " + ("$check" -replace '^true / ', '')) } else { throw ("replica set dogrulanamadi: " + $check) }
Write-Host ""
Write-Host ("Baglanti adresi: mongodb://localhost:" + $Port + "/?replicaSet=rs0") -ForegroundColor Green
