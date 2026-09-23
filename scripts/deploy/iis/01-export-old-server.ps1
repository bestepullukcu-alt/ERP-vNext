<#
.SYNOPSIS
    Diten ERP - eski sunucudan veri ve ayar disa aktarimi (Asama 3a). Veritabanini DEGISTIRMEZ.

.DESCRIPTION
    Eski (kaynak) sunucuda Yonetici olarak calistirilir:
      1. MongoDB Database Tools yoksa indirir (yalnizca OutDir\tools altina acar, sisteme kurulum yapmaz).
      2. mongodump ile TUM veritabanlarini tek bir sikistirilmis arsive alir (--oplog: tutarli anlik goruntu).
      3. Her koleksiyonun kayit sayisini counts.txt dosyasina yazar (yeni sunucuda karsilastirmak icin).
      4. C:\inetpub\diten\* altindaki appsettings.Production.json ve web.config dosyalarini config\ altina kopyalar.
         BU KLASOR SIFRE ICERIR - paylasmayin, yeni sunucuya guvenli tasiyin.
      5. Arsivin SHA256 ozetini yazar (tasima sonrasi dogrulama icin).

    Deneme (prova) icin siteler calisirken alinabilir. Son gecis (cutover) icin -StopDitenSites kullanin:
    Diten IIS siteleri ve havuzlari durdurulur, boylece dump alinirken yeni veri yazilmaz.
    Siteler otomatik yeniden BASLATILMAZ; gerekirse sonda yazdirilan komutla baslatin.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File C:\Users\ERP\Desktop\01-export-old-server.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File C:\Users\ERP\Desktop\01-export-old-server.ps1 -StopDitenSites
#>
[CmdletBinding()]
param(
    [string]$OutDir = ("C:\DitenMigration\export-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string]$MongoUri = "mongodb://localhost:27017/?directConnection=true",
    [string]$SitesRoot = "C:\inetpub\diten",
    [string]$ToolsZipUrl = "https://fastdl.mongodb.org/tools/db/mongodb-database-tools-windows-x86_64-100.10.0.zip",
    [switch]$StopDitenSites
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Write-Host ("Cikti klasoru: " + $OutDir) -ForegroundColor Cyan

# ---------------------------------------------------------------------------------------------
# 1. mongodump bul ya da indir
function Find-Tool([string]$name, [string]$extraRoot) {
    $c = Get-Command $name -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    foreach ($root in @($extraRoot, "C:\Program Files\MongoDB", "C:\mongodb")) {
        if ($root -and (Test-Path $root)) {
            $hit = Get-ChildItem $root -Filter ($name + ".exe") -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    return $null
}

$toolsDir = "C:\DitenMigration\tools"
$mongodump = Find-Tool "mongodump" $toolsDir
if (-not $mongodump) {
    Write-Host "[*] mongodump bulunamadi, MongoDB Database Tools indiriliyor..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $zip = Join-Path $toolsDir "mongodb-database-tools.zip"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $ToolsZipUrl -OutFile $zip -UseBasicParsing
    Expand-Archive -Path $zip -DestinationPath $toolsDir -Force
    $mongodump = Find-Tool "mongodump" $toolsDir
    if (-not $mongodump) { throw "mongodump indirilen paket icinde bulunamadi: $toolsDir" }
}
Write-Host ("mongodump: " + $mongodump) -ForegroundColor Green
& $mongodump --version | Select-Object -First 1 | Write-Host

# ---------------------------------------------------------------------------------------------
# 2. (istege bagli) Diten sitelerini durdur
$stoppedSites = @()
if ($StopDitenSites) {
    Import-Module WebAdministration
    Write-Host "[*] Diten IIS siteleri durduruluyor (son gecis modu)..." -ForegroundColor Yellow
    foreach ($s in (Get-ChildItem IIS:\Sites | Where-Object { $_.Name -like "Diten*" })) {
        if ($s.State -eq "Started") {
            Stop-Website -Name $s.Name
            $stoppedSites += $s.Name
        }
        $pool = $s.applicationPool
        if ((Get-WebAppPoolState -Name $pool).Value -eq "Started") { Stop-WebAppPool -Name $pool }
    }
    Start-Sleep -Seconds 5
}

# ---------------------------------------------------------------------------------------------
# 3. mongodump
$archive = Join-Path $OutDir "diten-mongo.archive.gz"
$dumpLog = Join-Path $OutDir "mongodump.log"
Write-Host "[*] mongodump calisiyor (veri boyutuna gore birkac dakika surebilir)..." -ForegroundColor Cyan
# mongodump ilerleme bilgisini stderr'e yazar; Stop tercihinde bu hata sayilmasin diye gecici olarak gevsetilir.
$ErrorActionPreference = "Continue"
& $mongodump --uri="$MongoUri" --oplog --gzip --archive="$archive" 2>&1 | ForEach-Object { [string]$_ } | Tee-Object -FilePath $dumpLog | Out-Null
$dumpExit = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($dumpExit -ne 0) {
    Write-Host ("mongodump HATA verdi (cikis kodu " + $dumpExit + "). Ayrinti: " + $dumpLog) -ForegroundColor Red
    Get-Content $dumpLog -Tail 20 | Write-Host
    exit 1
}

# Kayit sayilari: "done dumping <db>.<koleksiyon> (<n> documents)"
$counts = Select-String -Path $dumpLog -Pattern 'done dumping (\S+) \((\d+) documents?\)' | ForEach-Object {
    [pscustomobject]@{ Collection = $_.Matches[0].Groups[1].Value; Documents = [int64]$_.Matches[0].Groups[2].Value }
} | Sort-Object Collection
$countsFile = Join-Path $OutDir "counts.txt"
$counts | ForEach-Object { $_.Collection + "`t" + $_.Documents } | Set-Content -Path $countsFile -Encoding UTF8
$dbSummary = $counts | Group-Object { $_.Collection.Split('.')[0] } | ForEach-Object {
    [pscustomobject]@{ Database = $_.Name; Collections = $_.Count; Documents = ($_.Group | Measure-Object Documents -Sum).Sum }
}

# ---------------------------------------------------------------------------------------------
# 4. Ayar dosyalari (SIFRE ICERIR)
$configDir = Join-Path $OutDir "config-SECRETS"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
if (Test-Path $SitesRoot) {
    foreach ($d in (Get-ChildItem $SitesRoot -Directory)) {
        $dest = Join-Path $configDir $d.Name
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        foreach ($f in @("appsettings.Production.json", "web.config")) {
            $src = Join-Path $d.FullName $f
            if (Test-Path $src) { Copy-Item $src -Destination $dest -Force }
        }
    }
}
# IIS site/havuz tanimlari (referans icin)
$appcmd = Join-Path $env:windir "system32\inetsrv\appcmd.exe"
if (Test-Path $appcmd) {
    & $appcmd list site /config /xml | Set-Content -Path (Join-Path $configDir "_iis-sites.xml") -Encoding UTF8
    & $appcmd list apppool /config /xml | Set-Content -Path (Join-Path $configDir "_iis-apppools.xml") -Encoding UTF8
}

# ---------------------------------------------------------------------------------------------
# 5. Ozet
$hash = (Get-FileHash -Path $archive -Algorithm SHA256).Hash
$sizeMb = [math]::Round((Get-Item $archive).Length / 1MB, 2)
$summary = Join-Path $OutDir "export-summary.txt"
$lines = @()
$lines += "Diten ERP disa aktarim - " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + " - " + $env:COMPUTERNAME
$lines += "Son gecis modu (siteler durduruldu): " + [bool]$StopDitenSites
$lines += "Arsiv: " + $archive + "  (" + $sizeMb + " MB)"
$lines += "SHA256: " + $hash
$lines += ""
$lines += ($dbSummary | Format-Table -AutoSize | Out-String -Width 200).TrimEnd()
$lines += ""
$lines += "Koleksiyon bazinda sayilar: counts.txt"
$lines | Set-Content -Path $summary -Encoding UTF8

Write-Host ""
Get-Content $summary | Write-Host
Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Green
Write-Host ("  Bana gonderin      : " + $summary + "  ve  " + $countsFile) -ForegroundColor Green
Write-Host ("  Yeni sunucuya tasiyin: " + $archive) -ForegroundColor Green
Write-Host ("  GIZLI (paylasmayin) : " + $configDir) -ForegroundColor Yellow
if ($stoppedSites.Count -gt 0) {
    Write-Host ""
    Write-Host "Durdurulan siteleri yeniden baslatmak gerekirse:" -ForegroundColor Yellow
    Write-Host '  Import-Module WebAdministration; Get-ChildItem IIS:\Sites | ? Name -like "Diten*" | % { Start-WebAppPool $_.applicationPool; Start-Website $_.Name }'
}
