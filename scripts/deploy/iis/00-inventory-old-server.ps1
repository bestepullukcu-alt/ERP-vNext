<#
.SYNOPSIS
    Diten ERP - eski sunucu envanteri (Asama 0). YALNIZCA OKUR, hicbir seyi degistirmez.

.DESCRIPTION
    Eski (kaynak) sunucuda calistirilir. Yeni IIS sunucusuna tasima icin gereken her bilgiyi toplar:
      - Isletim sistemi, .NET runtime'lari, ASP.NET Core Hosting Bundle
      - IIS siteleri, baglamalar (binding), uygulama havuzlari
      - Calisan dotnet surecleri, dinlenen portlar, Windows servisleri, zamanlanmis gorevler
      - Diten uygulama klasorleri: appsettings*.json, web.config, Data\uploads boyutu
      - MongoDB surumu, mongod.cfg, veritabanlari / koleksiyonlar / kayit sayilari, tenant kayitlari

    Iki cikti uretir:
      <OutDir>\summary.txt   -> Sifreler MASKELI. Claude ile PAYLASILABILIR.
      <OutDir>\secrets\      -> Ham ayar dosyalari (sifreler dahil). PAYLASMAYIN; yeni sunucuya guvenli tasiyin.

    Maskeli degerler "<set len=44 sha=1a2b3c4d>" bicimindedir: deger gosterilmez ama bos mu dolu mu,
    ve servisler arasinda ayni mi (ornegin JwtSettings:Secret) karsilastirilabilir.

.PARAMETER MongoUri
    MongoDB baglanti adresi. Kimlik dogrulama aciksa: "mongodb://kullanici:sifre@localhost:27017/?authSource=admin"

.PARAMETER ExtraRoots
    IIS/surec disinda uygulama aranacak ek klasorler (ornegin D:\Diten).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\00-inventory-old-server.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\00-inventory-old-server.ps1 -MongoUri "mongodb://admin:***@localhost:27017/?authSource=admin" -ExtraRoots D:\Apps
#>
[CmdletBinding()]
param(
    [string]$OutDir = ("C:\DitenMigration\inventory-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string]$MongoUri = "mongodb://localhost:27017",
    [string[]]$ExtraRoots = @()
)

$ErrorActionPreference = "Continue"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$secretsDir = Join-Path $OutDir "secrets"
New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null
$summary = Join-Path $OutDir "summary.txt"
Set-Content -Path $summary -Value "" -Encoding UTF8

function Write-Section([string]$title) {
    $line = "`r`n" + ("=" * 90) + "`r`n== " + $title + "`r`n" + ("=" * 90)
    Add-Content -Path $summary -Value $line -Encoding UTF8
    Write-Host ("[*] " + $title) -ForegroundColor Cyan
}

function Write-Out($obj) {
    if ($null -eq $obj) { return }
    $text = ($obj | Out-String -Width 400).TrimEnd()
    Add-Content -Path $summary -Value $text -Encoding UTF8
}

function Get-ShortHash([string]$value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))
    return (($bytes[0..3] | ForEach-Object { $_.ToString("x2") }) -join "")
}

function Get-Masked([string]$value) {
    if ([string]::IsNullOrEmpty($value)) { return "<EMPTY>" }
    return ("<set len=" + $value.Length + " sha=" + (Get-ShortHash $value) + ">")
}

# JSON / XML metnindeki gizli degerleri maskeler. Anahtar adi gizli gorunuyorsa degeri gizlenir;
# baglanti adreslerindeki kullanici:sifre kismi da gizlenir.
function Protect-Text([string]$text) {
    if ($null -eq $text) { return "" }
    $secretKey = '(?i)(secret|password|passwd|pwd|apikey|api_key|token|hashsecret|activesecret|previoussecret|identifier|keyfile)'
    # "Key": "value"  (JSON)
    $text = [regex]::Replace($text, '"([^"]*)"\s*:\s*"([^"]*)"', {
        param($m)
        $k = $m.Groups[1].Value; $v = $m.Groups[2].Value
        if ($k -match $secretKey) { return ('"' + $k + '": "' + (Get-Masked $v) + '"') }
        return $m.Value
    })
    # name="Key" value="value"  (web.config environmentVariable)
    $text = [regex]::Replace($text, '(?i)name="([^"]*)"\s+value="([^"]*)"', {
        param($m)
        $k = $m.Groups[1].Value; $v = $m.Groups[2].Value
        if ($k -match $secretKey) { return ('name="' + $k + '" value="' + (Get-Masked $v) + '"') }
        return $m.Value
    })
    # PreviousSecrets dizileri: ["a","b"]
    $text = [regex]::Replace($text, '(?i)("PreviousSecrets"\s*:\s*\[)([^\]]*)(\])', {
        param($m)
        $items = [regex]::Matches($m.Groups[2].Value, '"([^"]*)"') | ForEach-Object { '"' + (Get-Masked $_.Groups[1].Value) + '"' }
        return ($m.Groups[1].Value + ($items -join ", ") + $m.Groups[3].Value)
    })
    # mongodb://user:pass@host
    $text = [regex]::Replace($text, '(?i)(mongodb(\+srv)?://)([^:@/"\s]+):([^@"\s]+)@', '$1***:***@')
    return $text
}

Add-Content -Path $summary -Value ("Diten ERP eski sunucu envanteri - " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + " - " + $env:COMPUTERNAME) -Encoding UTF8
Add-Content -Path $summary -Value "Bu dosyada sifreler maskelidir. Ham dosyalar 'secrets' klasorundedir ve PAYLASILMAMALIDIR." -Encoding UTF8

# ---------------------------------------------------------------------------------------------
Write-Section "1. Sistem"
$os = Get-CimInstance Win32_OperatingSystem
Write-Out ([pscustomobject]@{
    Computer = $env:COMPUTERNAME
    OS = $os.Caption
    Version = $os.Version
    RAM_GB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    PowerShell = $PSVersionTable.PSVersion.ToString()
})
Write-Out (Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | Select-Object DeviceID, @{n="Size_GB";e={[math]::Round($_.Size/1GB,1)}}, @{n="Free_GB";e={[math]::Round($_.FreeSpace/1GB,1)}})

Write-Section "2. .NET runtime / SDK / Hosting Bundle"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    Write-Out ("dotnet: " + $dotnet.Source)
    Write-Out (& dotnet --list-runtimes 2>&1)
    Write-Out "-- SDK'lar:"
    Write-Out (& dotnet --list-sdks 2>&1)
} else {
    Write-Out "dotnet komutu PATH'te bulunamadi."
}
$ancm = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
Write-Out ("ASP.NET Core Module V2 (Hosting Bundle): " + (Test-Path $ancm))

# ---------------------------------------------------------------------------------------------
Write-Section "3. IIS siteleri ve uygulama havuzlari"
$appRoots = New-Object System.Collections.Generic.List[string]
$iisAvailable = $false
try {
    Import-Module WebAdministration -ErrorAction Stop
    $iisAvailable = $true
} catch {
    Write-Out "IIS (WebAdministration modulu) bulunamadi - IIS kurulu degil ya da kullanilmiyor."
}
if ($iisAvailable) {
    $sites = Get-ChildItem IIS:\Sites
    foreach ($s in $sites) {
        $bindings = ($s.Bindings.Collection | ForEach-Object { $_.protocol + " " + $_.bindingInformation + $(if ($_.sslFlags) { " sslFlags=" + $_.sslFlags } else { "" }) }) -join " | "
        $phys = [Environment]::ExpandEnvironmentVariables($s.PhysicalPath)
        Write-Out ([pscustomobject]@{ Site = $s.Name; State = $s.State; AppPool = $s.applicationPool; Path = $phys; Bindings = $bindings } | Format-List)
        if ($phys) { $appRoots.Add($phys) }
        foreach ($app in (Get-WebApplication -Site $s.Name)) {
            $ap = [Environment]::ExpandEnvironmentVariables($app.PhysicalPath)
            Write-Out ("   uygulama: " + $app.Path + " -> " + $ap + " (pool: " + $app.applicationPool + ")")
            if ($ap) { $appRoots.Add($ap) }
        }
    }
    Write-Out "-- Uygulama havuzlari:"
    Write-Out (Get-ChildItem IIS:\AppPools | ForEach-Object {
        [pscustomobject]@{
            Name = $_.Name
            State = $_.State
            ClrVersion = $_.managedRuntimeVersion
            Identity = $_.processModel.identityType
            User = $_.processModel.userName
            StartMode = $_.startMode
            IdleTimeout = $_.processModel.idleTimeout
        }
    } | Format-Table -AutoSize)
}

# ---------------------------------------------------------------------------------------------
Write-Section "4. Calisan surecler ve dinlenen portlar"
$procs = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -match '^(dotnet|w3wp|mongod|nssm)\.exe$' -or $_.Name -like 'Diten*'
}
Write-Out ($procs | Select-Object ProcessId, Name, ExecutablePath, CommandLine | Format-List)
foreach ($p in $procs) {
    if ($p.Name -eq 'dotnet.exe' -and $p.CommandLine -match '([A-Za-z]:\\[^"]+?\.dll)') {
        $appRoots.Add((Split-Path $Matches[1] -Parent))
    }
    if ($p.Name -like 'Diten*' -and $p.ExecutablePath) {
        $appRoots.Add((Split-Path $p.ExecutablePath -Parent))
    }
}
Write-Out "-- Dinlenen TCP portlari (5000-5100, 27017, 80, 443):"
$listen = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    ($_.LocalPort -ge 5000 -and $_.LocalPort -le 5100) -or $_.LocalPort -in 80, 443, 27017, 5341
}
Write-Out ($listen | Sort-Object LocalPort | Select-Object LocalAddress, LocalPort, OwningProcess, @{n="Process";e={(Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName}} | Format-Table -AutoSize)

Write-Section "5. Windows servisleri ve zamanlanmis gorevler (dotnet/diten/mongo/nssm)"
Write-Out (Get-CimInstance Win32_Service | Where-Object { $_.PathName -match '(?i)dotnet|diten|mongo|nssm' -or $_.Name -match '(?i)diten|mongo' } |
    Select-Object Name, State, StartMode, StartName, PathName | Format-List)
Write-Out (Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object {
    ($_.Actions | ForEach-Object { $_.Execute + " " + $_.Arguments }) -match '(?i)dotnet|diten|mongo'
} | Select-Object TaskName, TaskPath, State, @{n="Action";e={($_.Actions | ForEach-Object { $_.Execute + " " + $_.Arguments }) -join "; "}} | Format-List)

Write-Section "6. Makine duzeyi ortam degiskenleri (ASPNETCORE / DOTNET / ayar anahtarlari)"
$envVars = [Environment]::GetEnvironmentVariables("Machine")
foreach ($k in ($envVars.Keys | Sort-Object)) {
    if ($k -match '(?i)^(ASPNETCORE|DOTNET)|__|Jwt|Mongo|Diten') {
        $v = [string]$envVars[$k]
        if ($k -match '(?i)secret|password|key|token') { $v = Get-Masked $v }
        Write-Out ($k + " = " + (Protect-Text $v))
    }
}

# ---------------------------------------------------------------------------------------------
Write-Section "7. Diten uygulama klasorleri"
foreach ($r in @("C:\inetpub", "C:\PharmaCoviligance", "C:\Diten", "D:\Diten", "C:\Sites", "D:\Sites") + $ExtraRoots) {
    if (Test-Path $r) { $appRoots.Add($r) }
}
# Her kok altinda appsettings.json iceren ve Diten*.dll barindiran klasorleri bul.
$appDirs = @{}
foreach ($root in ($appRoots | Where-Object { $_ } | Sort-Object -Unique)) {
    if (-not (Test-Path $root)) { continue }
    $candidates = @(Get-Item $root) + @(Get-ChildItem -Path $root -Directory -Recurse -Depth 3 -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(node_modules|wwwroot|runtimes|obj|\.git)(\\|$)' })
    foreach ($d in $candidates) {
        if ((Test-Path (Join-Path $d.FullName "appsettings.json")) -and
            (Get-ChildItem -Path $d.FullName -Filter "Diten*.dll" -ErrorAction SilentlyContinue | Select-Object -First 1)) {
            $appDirs[$d.FullName] = $true
        }
    }
}
if ($appDirs.Count -eq 0) {
    Write-Out "Diten uygulama klasoru bulunamadi. -ExtraRoots ile klasor verip tekrar calistirin."
}
$i = 0
foreach ($dir in ($appDirs.Keys | Sort-Object)) {
    $i++
    # Ana giris dll'i: <isim>.runtimeconfig.json dosyasi olan dll
    $entry = Get-ChildItem -Path $dir -Filter "*.runtimeconfig.json" -ErrorAction SilentlyContinue | Select-Object -First 1
    $entryName = if ($entry) { $entry.Name -replace '\.runtimeconfig\.json$', '' } else { "?" }
    $entryDll = Join-Path $dir ($entryName + ".dll")
    $built = if (Test-Path $entryDll) { (Get-Item $entryDll).LastWriteTime.ToString("yyyy-MM-dd HH:mm") } else { "?" }
    Add-Content -Path $summary -Value ("`r`n--- [" + $i + "] " + $entryName + "  (" + $dir + ")  dll tarihi: " + $built) -Encoding UTF8

    $safeName = ($i.ToString("00") + "-" + $entryName)
    $dest = Join-Path $secretsDir $safeName
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Set-Content -Path (Join-Path $dest "_source-path.txt") -Value $dir -Encoding UTF8

    $cfgFiles = @(Get-ChildItem -Path $dir -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^(appsettings.*\.json|web\.config|ocelot.*\.json)$' })
    foreach ($f in $cfgFiles) {
        Copy-Item $f.FullName -Destination $dest -Force
        if ($f.Name -like 'ocelot*') {
            Add-Content -Path $summary -Value ("  " + $f.Name + ": " + ([regex]::Matches((Get-Content $f.FullName -Raw), '"Port"\s*:\s*(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique) -join ",") -Encoding UTF8
            continue
        }
        Add-Content -Path $summary -Value ("  >> " + $f.Name) -Encoding UTF8
        Add-Content -Path $summary -Value (Protect-Text (Get-Content $f.FullName -Raw)) -Encoding UTF8
    }

    foreach ($sub in @("Data\uploads", "App_Data", "Data", "logs", "Logs", "keys", "DataProtection-Keys")) {
        $p = Join-Path $dir $sub
        if (Test-Path $p) {
            $files = Get-ChildItem -Path $p -File -Recurse -ErrorAction SilentlyContinue
            $sizeMb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 2)
            Add-Content -Path $summary -Value ("  klasor " + $sub + ": " + @($files).Count + " dosya, " + $sizeMb + " MB") -Encoding UTF8
        }
    }
}

# ASP.NET Core Data Protection anahtarlari (kullanici profilinde tutulur)
Write-Section "8. ASP.NET Core Data Protection anahtar klasorleri"
$dpCandidates = @()
$dpCandidates += Get-ChildItem "C:\Users\*\AppData\Local\ASP.NET\DataProtection-Keys" -Directory -ErrorAction SilentlyContinue
$dpCandidates += Get-ChildItem "C:\Windows\System32\config\systemprofile\AppData\Local\ASP.NET\DataProtection-Keys" -Directory -ErrorAction SilentlyContinue
if ($dpCandidates.Count -eq 0) { Write-Out "Bulunamadi (IIS'te anahtarlar kayit defterinde olabilir: HKLM\SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys)." }
foreach ($d in $dpCandidates) {
    Write-Out ($d.FullName + " : " + @(Get-ChildItem $d.FullName -File).Count + " anahtar dosyasi")
}

# ---------------------------------------------------------------------------------------------
Write-Section "9. MongoDB"
$mongodSvc = Get-CimInstance Win32_Service | Where-Object { $_.PathName -match '(?i)mongod(\.exe)?' } | Select-Object -First 1
$mongodExe = $null
if ($mongodSvc -and $mongodSvc.PathName -match '"?([^"]*mongod\.exe)"?') { $mongodExe = $Matches[1] }
if (-not $mongodExe) {
    $cmd = Get-Command mongod -ErrorAction SilentlyContinue
    if ($cmd) { $mongodExe = $cmd.Source }
}
if ($mongodExe -and (Test-Path $mongodExe)) {
    Write-Out ("mongod: " + $mongodExe)
    Write-Out (& $mongodExe --version 2>&1 | Select-Object -First 3)
} else {
    Write-Out "mongod.exe bulunamadi."
}
if ($mongodSvc) {
    Write-Out ("Servis: " + $mongodSvc.Name + " / " + $mongodSvc.State + " / " + $mongodSvc.PathName)
    if ($mongodSvc.PathName -match '(?i)--config\s+"?([^"]+\.cfg)"?') {
        $cfg = $Matches[1]
        if (Test-Path $cfg) {
            Copy-Item $cfg -Destination $secretsDir -Force
            Write-Out ("-- " + $cfg + ":")
            Write-Out (Protect-Text (Get-Content $cfg -Raw))
        }
    }
}

# Mongo arac yollari: PATH ya da standart kurulum klasorleri
function Find-MongoTool([string]$name) {
    $c = Get-Command $name -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $hit = Get-ChildItem "C:\Program Files\MongoDB" -Filter ($name + ".exe") -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    $hit = Get-ChildItem "C:\Program Files\mongosh" -Filter ($name + ".exe") -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}
$mongosh = Find-MongoTool "mongosh"
$legacyShell = if ($mongosh) { $null } else { Find-MongoTool "mongo" }
$mongodump = Find-MongoTool "mongodump"
Write-Out ("mongosh: " + $(if ($mongosh) { $mongosh } else { "YOK" }))
Write-Out ("mongo (eski shell): " + $(if ($legacyShell) { $legacyShell } else { "YOK" }))
Write-Out ("mongodump: " + $(if ($mongodump) { $mongodump + "  " + ((& $mongodump --version 2>&1 | Select-Object -First 1)) } else { "YOK - MongoDB Database Tools kurulmali" }))

$shell = if ($mongosh) { $mongosh } else { $legacyShell }
if ($shell) {
    # Salt-okunur sorgular: listDatabases, koleksiyon sayilari, tenant/domain kayitlarinin ozet alanlari.
    $js = @'
var out = [];
var admin = db.getSiblingDB("admin");
var bi = admin.runCommand({ buildInfo: 1 });
out.push("MongoDB surumu: " + bi.version);
try { var fcv = admin.runCommand({ getParameter: 1, featureCompatibilityVersion: 1 }); out.push("FCV: " + JSON.stringify(fcv.featureCompatibilityVersion)); } catch (e) {}
var dbs = admin.runCommand({ listDatabases: 1 }).databases;
dbs.forEach(function (d) {
  if (["admin", "config", "local"].indexOf(d.name) >= 0) return;
  out.push("");
  out.push("### DB " + d.name + "  (" + (d.sizeOnDisk / 1048576).toFixed(1) + " MB)");
  var sdb = db.getSiblingDB(d.name);
  sdb.getCollectionNames().sort().forEach(function (c) {
    var n = sdb.getCollection(c).estimatedDocumentCount();
    out.push("   " + c + " : " + n);
  });
  sdb.getCollectionNames().filter(function (c) { return /tenant/i.test(c) && !/(setting|feature|module|audit|brand|subscription|security|messag|lookup)/i.test(c); })
    .forEach(function (c) {
      out.push("   -- " + c + " kayitlari (ilk 50, ozet alanlar):");
      sdb.getCollection(c).find({}, { _id: 1, Name: 1, Code: 1, Slug: 1, Domain: 1, DomainName: 1, TenantId: 1, Status: 1, IsPrimary: 1, IsDeleted: 1 })
        .limit(50).forEach(function (t) { out.push("      " + JSON.stringify(t)); });
    });
});
print(out.join("\n"));
'@
    $jsFile = Join-Path $OutDir "_inventory.js"
    Set-Content -Path $jsFile -Value $js -Encoding ASCII
    if ($mongosh) {
        $result = & $shell $MongoUri --quiet --file $jsFile 2>&1
    } else {
        $result = & $shell $MongoUri --quiet $jsFile 2>&1
    }
    Write-Out ($result | ForEach-Object { Protect-Text ([string]$_) })
    Remove-Item $jsFile -ErrorAction SilentlyContinue
} else {
    Write-Out "mongosh / mongo bulunamadi; veritabani listesi alinamadi. (MongoDB Compass'tan veritabani listesi ekran goruntusu de yeterli.)"
}

# ---------------------------------------------------------------------------------------------
Write-Host ""
Write-Host "Tamamlandi." -ForegroundColor Green
Write-Host ("  Paylasilabilir ozet : " + $summary) -ForegroundColor Green
Write-Host ("  GIZLI ham dosyalar  : " + $secretsDir + "  (PAYLASMAYIN)") -ForegroundColor Yellow
