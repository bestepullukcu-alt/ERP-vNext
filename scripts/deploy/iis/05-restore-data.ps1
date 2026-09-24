<#
.SYNOPSIS
    Eski sunucudan alinan MongoDB arsivini yeni Diten MongoDB'sine (27018) geri yukler ve kayit sayilarini karsilastirir.

.DESCRIPTION
    -ExportDir: 01-export-old-server.ps1'in urettigi klasor (diten-mongo.archive.gz, counts.txt, export-summary.txt).
    1. Arsivin SHA256'sini export-summary.txt ile karsilastirir (tasima sirasinda bozulma var mi).
    2. Hedefte Diten veritabanlari zaten varsa -Drop olmadan DURUR (yanlislikla ustune yazmasin).
    3. mongorestore --oplogReplay ile geri yukler (-Drop: once hedefteki ayni koleksiyonlari siler).
    4. counts.txt'deki her koleksiyonun kayit sayisini hedefle karsilastirir.
    5. Tenant ve alan adi kayitlarini listeler (ditenteknoloji.com icin tenant kimligi).
    Servisler (IIS vNext siteleri) calisiyorsa once durdurun; geri yukleme sirasinda yazmasinlar.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\05-restore-data.ps1 -ExportDir C:\DitenMigration\export-20260924-150000
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\05-restore-data.ps1 -ExportDir C:\DitenMigration\export-20260925-200000 -Drop
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ExportDir,
    [int]$Port = 27018,
    [switch]$Drop
)

$ErrorActionPreference = "Stop"
$toolsBin = "C:\DitenMigration\tools\bin"
$mongosh = Join-Path $toolsBin "mongosh.exe"
$mongorestore = Join-Path $toolsBin "mongorestore.exe"
foreach ($t in @($mongosh, $mongorestore)) { if (-not (Test-Path $t)) { throw ("bulunamadi: " + $t + " - once 04-install-diten-mongo.ps1 calistirin") } }

$archive = Join-Path $ExportDir "diten-mongo.archive.gz"
$countsFile = Join-Path $ExportDir "counts.txt"
$summaryFile = Join-Path $ExportDir "export-summary.txt"
if (-not (Test-Path $archive)) { throw ("arsiv yok: " + $archive) }
$uri = "mongodb://localhost:$Port/?replicaSet=rs0"
$log = Join-Path $ExportDir ("restore-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")

Write-Host "=== 1. Arsiv butunlugu" -ForegroundColor Cyan
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash
if (Test-Path $summaryFile) {
    $expected = (Select-String -Path $summaryFile -Pattern 'SHA256:\s*([0-9A-Fa-f]{64})' | Select-Object -First 1).Matches[0].Groups[1].Value
    if ($expected -and $expected -ne $hash) { throw ("SHA256 UYUSMUYOR - arsiv tasima sirasinda bozulmus. beklenen " + $expected + ", gelen " + $hash) }
    Write-Host "  [OK] SHA256 eslesiyor" -ForegroundColor Green
} else { Write-Host ("  export-summary.txt yok, SHA256: " + $hash) -ForegroundColor Yellow }

Write-Host "=== 2. Hedef kontrolu" -ForegroundColor Cyan
$ditenDbs = @("diten_auth_v3", "diten_personalization_v3", "diten_background_jobs", "DitenERP", "DitenEnterpriseDb",
              "DitenHumanCapital", "DitenTalentEcosystem", "diten_deven_v1", "diten_procurement_v1", "diten_ppm")
$existing = & $mongosh $uri --quiet --eval "db.adminCommand({ listDatabases: 1, nameOnly: true }).databases.map(function (d) { return d.name; }).join(',')" 2>&1
$present = ("$existing".Split(',')) | Where-Object { $ditenDbs -contains $_ }
if ($present -and -not $Drop) {
    throw ("Hedefte zaten Diten verisi var: " + ($present -join ", ") + ". Ustune yazmak icin -Drop ile calistirin (hedefteki bu veriler SILINIR).")
}
Write-Host ("  hedefteki veritabanlari: " + $existing)
$running = Get-Website -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "vNext-*" -and $_.State -eq "Started" }
if ($running) { throw ("Once vNext sitelerini durdurun: " + (($running | ForEach-Object Name) -join ", ")) }

Write-Host "=== 3. mongorestore" -ForegroundColor Cyan
$restoreArgs = @("--uri=$uri", "--gzip", "--archive=$archive", "--oplogReplay")
if ($Drop) { $restoreArgs += "--drop" }
$ErrorActionPreference = "Continue"
& $mongorestore @restoreArgs 2>&1 | ForEach-Object { [string]$_ } | Tee-Object -FilePath $log | Select-Object -Last 5 | Write-Host
$code = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($code -ne 0) { throw ("mongorestore hata verdi (cikis " + $code + "). Log: " + $log) }
Write-Host "  [OK] geri yukleme tamamlandi" -ForegroundColor Green

Write-Host "=== 4. Kayit sayisi karsilastirmasi" -ForegroundColor Cyan
if (Test-Path $countsFile) {
    $pairs = Get-Content $countsFile | Where-Object { $_ -match "`t" } | ForEach-Object {
        $p = $_.Split("`t"); '["' + $p[0].Trim() + '",' + [int64]$p[1].Trim() + ']'
    }
    $js = "var expected = [" + ($pairs -join ",") + "]; var bad = 0, ok = 0;" +
          "expected.forEach(function (e) { var i = e[0].indexOf('.'); var dbn = e[0].substring(0, i), coll = e[0].substring(i + 1);" +
          "  if (dbn === 'local' || dbn === 'admin' || dbn === 'config') return;" +
          "  var n = db.getSiblingDB(dbn).getCollection(coll).countDocuments({});" +
          "  if (n !== e[1]) { bad++; print('  FARK ' + e[0] + ': eski=' + e[1] + ' yeni=' + n); } else { ok++; } });" +
          "print('  eslesen koleksiyon: ' + ok + ', farkli: ' + bad);"
    $jsFile = Join-Path $ExportDir "_verify.js"
    Set-Content -Path $jsFile -Value $js -Encoding ASCII
    & $mongosh $uri --quiet --file $jsFile 2>&1 | Write-Host
    Remove-Item $jsFile -ErrorAction SilentlyContinue
    Write-Host "  (Prova dump'i siteler calisirken alindiysa kucuk farklar normaldir; son gecis dump'inda fark olmamali.)" -ForegroundColor DarkGray
} else { Write-Host "  counts.txt yok, karsilastirma atlandi" -ForegroundColor Yellow }

Write-Host "=== 5. Tenant ve alan adi kayitlari" -ForegroundColor Cyan
$tjs = "var p = db.getSiblingDB('diten_personalization_v3');" +
       "p.tenants.find({}, { Name: 1, Code: 1, Slug: 1, Domain: 1, Status: 1, IsDeleted: 1 }).forEach(function (t) { print('  tenant ' + EJSON.stringify(t)); });" +
       "p.tenant_domains.find({}, { TenantId: 1, DomainName: 1, Status: 1, IsPrimary: 1, IsDeleted: 1 }).forEach(function (d) { print('  domain ' + EJSON.stringify(d)); });"
& $mongosh $uri --quiet --eval $tjs 2>&1 | Write-Host
Write-Host ""
Write-Host ("Log: " + $log) -ForegroundColor Green
