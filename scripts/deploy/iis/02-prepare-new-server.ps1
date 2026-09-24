<#
.SYNOPSIS
    Diten ERP - yeni sunucu hazirligi (Asama 1). Onkosullari kurar; tekrar calistirilabilir.

.DESCRIPTION
    Yeni (hedef) sunucuda Yonetici olarak calistirilir. Her adim once kontrol eder, kuruluysa atlar:
      1. Windows surumunu raporlar (Server / Server Core / Client).
      2. IIS + WebSockets + yonetim araclari
      3. IIS URL Rewrite 2.1 (http->https yonlendirme ve tenant giris yonlendirmesi icin)
      4. .NET 8 Hosting Bundle (IIS'ten SONRA kurulmali) ve .NET 8 SDK (sunucuda derleme icin)
      5. Git for Windows (main dalini cekmek icin)
      6. MongoDB. Makinede MongoDB servisi VARSA kurulum yapilmaz, yalnizca incelenir (surum, mongod.cfg,
         mevcut veritabanlari, replica set). Replica set yoksa ancak -EnableReplicaSet verilirse mongod.cfg
         yedeklenip "replSetName: rs0" eklenir ve servis bir kez yeniden baslatilir.
         Servis YOKSA MongoDB 7.0.14 eski sunucuyla AYNI surum ve duzende kurulur:
            C:\mongodb\bin, C:\mongodb\data, C:\mongodb\log, C:\mongodb\mongod.cfg
            yalnizca 127.0.0.1, replica set "rs0" (Platform servisi transaction icin bunu sart kosar)
         mongosh / mongorestore bulunamazsa C:\DitenMigration\tools\bin altina indirilir.
      7. Guvenlik duvari: yalnizca 80 ve 443 disariya acilir. 27017 ve 5000-5065 ACILMAZ.
      8. Klasorler: C:\inetpub\diten, C:\DitenMigration

    Indirilenler C:\DitenMigration\downloads altina kaydedilir. Tum cikti C:\DitenMigration\prepare-*.log dosyasina yazilir.
    Bir indirme adresi degismisse ilgili -...Url parametresiyle yenisi verilebilir.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File C:\Users\<kullanici>\Desktop\02-prepare-new-server.ps1
#>
[CmdletBinding()]
param(
    [string]$HostingBundleUrl = "https://aka.ms/dotnet/8.0/dotnet-hosting-win.exe",
    [string]$DotnetSdkUrl     = "https://aka.ms/dotnet/8.0/dotnet-sdk-win-x64.exe",
    [string]$UrlRewriteUrl    = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi",
    [string]$GitUrl           = "https://github.com/git-for-windows/git/releases/download/v2.46.0.windows.1/Git-2.46.0-64-bit.exe",
    [string]$MongoZipUrl      = "https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-7.0.14.zip",
    [string]$MongoshZipUrl    = "https://downloads.mongodb.com/compass/mongosh-2.3.1-win32-x64.zip",
    [string]$MongoToolsZipUrl = "https://fastdl.mongodb.org/tools/db/mongodb-database-tools-windows-x86_64-100.10.0.zip",
    [string]$MongoRoot        = "C:\mongodb",
    [switch]$EnableReplicaSet,
    [switch]$SkipSdk,
    [switch]$SkipGit
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Bu script Yonetici (Administrator) olarak calistirilmalidir."
}

$work = "C:\DitenMigration"
$downloads = Join-Path $work "downloads"
New-Item -ItemType Directory -Force -Path $downloads, "C:\inetpub\diten" | Out-Null
$log = Join-Path $work ("prepare-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
Start-Transcript -Path $log | Out-Null

function Step([string]$t) { Write-Host ""; Write-Host ("=== " + $t) -ForegroundColor Cyan }
function Ok([string]$t)   { Write-Host ("  [OK] " + $t) -ForegroundColor Green }
function Skip([string]$t) { Write-Host ("  [ATLANDI] " + $t) -ForegroundColor DarkGray }

function Get-File([string]$url, [string]$name) {
    $target = Join-Path $downloads $name
    if (Test-Path $target) { return $target }
    Write-Host ("  indiriliyor: " + $url)
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
    return $target
}

function Invoke-Installer([string]$file, [string[]]$arguments) {
    $p = Start-Process -FilePath $file -ArgumentList $arguments -Wait -PassThru
    # 3010 = basarili, yeniden baslatma gerekli
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) { throw ("Kurulum basarisiz: " + $file + " (cikis kodu " + $p.ExitCode + ")") }
    if ($p.ExitCode -eq 3010) { $script:rebootNeeded = $true }
}
$script:rebootNeeded = $false

# ---------------------------------------------------------------------------------------------
Step "1. Windows surumu"
$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$os = Get-CimInstance Win32_OperatingSystem
$installType = $cv.InstallationType
Write-Host ("  " + $os.Caption + " | surum " + $cv.DisplayVersion + $cv.ReleaseId + " | build " + $cv.CurrentBuild + " | tur: " + $installType)
$isServer = $installType -like "Server*"
$isCore = $installType -eq "Server Core"
if ([int]$cv.CurrentBuild -lt 17763) { Write-Host "  BILGI: Windows Server 2016 / eski surum - MongoDB 7.0'in resmi destek listesi burada dogrulanmali; mevcut kurulum calisiyorsa sorun degil." -ForegroundColor Yellow }

# ---------------------------------------------------------------------------------------------
Step "2. IIS"
if ($isServer) {
    $features = @("Web-Server", "Web-WebServer", "Web-Static-Content", "Web-Default-Doc", "Web-Http-Errors",
                  "Web-Http-Logging", "Web-Request-Monitor", "Web-Filtering", "Web-Stat-Compression",
                  "Web-WebSockets", "Web-Mgmt-Tools", "Web-Scripting-Tools")
    if (-not $isCore) { $features += "Web-Mgmt-Console" }
    $missing = $features | Where-Object { -not (Get-WindowsFeature -Name $_).Installed }
    if ($missing) {
        $r = Install-WindowsFeature -Name $missing
        if ($r.RestartNeeded -eq "Yes") { $script:rebootNeeded = $true }
        Ok ("kuruldu: " + ($missing -join ", "))
    } else { Skip "IIS ozellikleri zaten kurulu" }
} else {
    $features = @("IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-StaticContent", "IIS-DefaultDocument",
                  "IIS-HttpErrors", "IIS-HttpLogging", "IIS-RequestMonitor", "IIS-RequestFiltering",
                  "IIS-HttpCompressionStatic", "IIS-WebSockets", "IIS-ManagementConsole", "IIS-ManagementScriptingTools")
    $missing = $features | Where-Object { (Get-WindowsOptionalFeature -Online -FeatureName $_).State -ne "Enabled" }
    if ($missing) {
        Enable-WindowsOptionalFeature -Online -FeatureName $missing -All -NoRestart | Out-Null
        Ok ("etkinlestirildi: " + ($missing -join ", "))
    } else { Skip "IIS ozellikleri zaten etkin" }
}

# ---------------------------------------------------------------------------------------------
Step "3. IIS URL Rewrite 2.1"
if (Test-Path "$env:windir\System32\inetsrv\rewrite.dll") { Skip "URL Rewrite kurulu" }
else {
    $f = Get-File $UrlRewriteUrl "rewrite_amd64.msi"
    Invoke-Installer "msiexec.exe" @("/i", "`"$f`"", "/qn", "/norestart")
    Ok "URL Rewrite kuruldu"
}

# ---------------------------------------------------------------------------------------------
Step "4. .NET 8 Hosting Bundle ve SDK"
function Get-DotnetRuntimes {
    $exe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    if (Test-Path $exe) { return (& $exe --list-runtimes 2>$null) } else { return @() }
}
$ancm = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$hasAspNet8 = (Get-DotnetRuntimes) -match '^Microsoft\.AspNetCore\.App 8\.'
if ((Test-Path $ancm) -and $hasAspNet8) { Skip "Hosting Bundle (ASP.NET Core 8 + ANCM V2) kurulu" }
else {
    $f = Get-File $HostingBundleUrl "dotnet-hosting-8-win.exe"
    Invoke-Installer $f @("/install", "/quiet", "/norestart")
    # IIS'in yeni modulu gormesi icin
    & net stop was /y | Out-Null
    & net start w3svc | Out-Null
    Ok "Hosting Bundle kuruldu, IIS yeniden baslatildi"
}
if ($SkipSdk) { Skip "SDK (-SkipSdk)" }
else {
    $exe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    $sdks = if (Test-Path $exe) { & $exe --list-sdks 2>$null } else { @() }
    if ($sdks -match '^8\.') { Skip ".NET 8 SDK kurulu" }
    else {
        $f = Get-File $DotnetSdkUrl "dotnet-sdk-8-win-x64.exe"
        Invoke-Installer $f @("/install", "/quiet", "/norestart")
        Ok ".NET 8 SDK kuruldu"
    }
}

# ---------------------------------------------------------------------------------------------
Step "5. Git"
$gitExe = "C:\Program Files\Git\cmd\git.exe"
if ($SkipGit) { Skip "Git (-SkipGit)" }
elseif ((Get-Command git -ErrorAction SilentlyContinue) -or (Test-Path $gitExe)) { Skip "Git kurulu" }
else {
    $f = Get-File $GitUrl "git-64-bit.exe"
    Invoke-Installer $f @("/VERYSILENT", "/NORESTART", "/NOCANCEL", "/SP-", "/SUPPRESSMSGBOXES")
    Ok "Git kuruldu"
}

# ---------------------------------------------------------------------------------------------
Step "6. MongoDB (replica set rs0, yalnizca 127.0.0.1)"
$toolsBin = Join-Path $work "tools\bin"
New-Item -ItemType Directory -Force -Path $toolsBin | Out-Null

# mongosh / mongorestore: once mongod'un yaninda, sonra PATH, sonra C:\DitenMigration\tools\bin; yoksa oraya indirilir.
function Get-MongoTool([string]$exeName, [string]$zipUrl, [string]$zipName, [string]$besideDir) {
    foreach ($dir in @($besideDir, $toolsBin)) {
        if ($dir -and (Test-Path (Join-Path $dir $exeName))) { return (Join-Path $dir $exeName) }
    }
    $c = Get-Command $exeName -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $f = Get-File $zipUrl $zipName
    $tmp = Join-Path $downloads ($zipName + "-extract")
    Expand-Archive -Path $f -DestinationPath $tmp -Force
    Get-ChildItem $tmp -Recurse -File | Where-Object { $_.Extension -in ".exe", ".dll" -and $_.DirectoryName -like "*\bin" } |
        ForEach-Object { Copy-Item $_.FullName -Destination $toolsBin -Force }
    Ok ($exeName + " indirildi: " + $toolsBin)
    return (Join-Path $toolsBin $exeName)
}

$existingSvc = Get-CimInstance Win32_Service -Filter "Name='MongoDB'" -ErrorAction SilentlyContinue
$usingExisting = [bool]$existingSvc
if ($usingExisting) {
    # --- Mevcut MongoDB: kurulum YAPILMAZ, yalnizca incelenir. Degisiklik yalnizca -EnableReplicaSet ile. ---
    Write-Host ("  Mevcut MongoDB servisi bulundu: " + $existingSvc.PathName)
    if ($existingSvc.PathName -match '^"?([^"]*mongod\.exe)"?') { $mongod = $Matches[1] } else { throw "mongod.exe yolu servis tanimindan okunamadi." }
    $mongoCfg = $null
    if ($existingSvc.PathName -match '(?i)--config\s+"?([^"]+?\.cfg)"?(\s|$)') { $mongoCfg = $Matches[1] }
    $mongoBin = Split-Path $mongod -Parent
    Write-Host ("  Servis durumu: " + $existingSvc.State + " / baslangic: " + $existingSvc.StartMode + " / hesap: " + $existingSvc.StartName)
} else {
    # --- Temiz kurulum: eski sunucuyla ayni duzen ---
    $mongoBin = Join-Path $MongoRoot "bin"
    $mongod = Join-Path $mongoBin "mongod.exe"
    $mongoCfg = Join-Path $MongoRoot "mongod.cfg"
    New-Item -ItemType Directory -Force -Path $mongoBin, (Join-Path $MongoRoot "data"), (Join-Path $MongoRoot "log") | Out-Null
    if (Test-Path $mongod) { Skip "mongod.exe mevcut" }
    else {
        $f = Get-File $MongoZipUrl "mongodb-7.0.14.zip"
        $tmp = Join-Path $downloads "mongodb-extract"
        Expand-Archive -Path $f -DestinationPath $tmp -Force
        $binSrc = Get-ChildItem $tmp -Directory -Recurse | Where-Object { $_.Name -eq "bin" -and (Test-Path (Join-Path $_.FullName "mongod.exe")) } | Select-Object -First 1
        Copy-Item (Join-Path $binSrc.FullName "*") -Destination $mongoBin -Recurse -Force
        Ok ("mongod kopyalandi: " + $mongoBin)
    }
    if (Test-Path $mongoCfg) { Skip ("mongod.cfg mevcut, degistirilmedi: " + $mongoCfg) }
    else {
        @"
storage:
  dbPath: $MongoRoot\data
systemLog:
  destination: file
  logAppend: true
  path: $MongoRoot\log\mongod.log
net:
  port: 27017
  bindIp: 127.0.0.1
replication:
  replSetName: rs0
"@ | Set-Content -Path $mongoCfg -Encoding ASCII
        Ok ("mongod.cfg yazildi: " + $mongoCfg)
    }
    # Eski sunucudaki gibi LocalSystem altinda, otomatik baslayan servis.
    New-Service -Name MongoDB -DisplayName "MongoDB" -StartupType Automatic `
        -BinaryPathName ("`"" + $mongod + "`" --config `"" + $mongoCfg + "`" --service") | Out-Null
    Ok "MongoDB servisi olusturuldu"
}

Write-Host ("  mongod: " + $mongod + " | " + ((& $mongod --version | Select-Object -First 1)))
$mongosh = Get-MongoTool "mongosh.exe" $MongoshZipUrl "mongosh.zip" $mongoBin
$mongorestore = Get-MongoTool "mongorestore.exe" $MongoToolsZipUrl "mongodb-database-tools.zip" $mongoBin
Write-Host ("  mongosh: " + $mongosh)
Write-Host ("  mongorestore: " + $mongorestore)

$cfgText = ""
if ($mongoCfg -and (Test-Path $mongoCfg)) {
    $cfgText = Get-Content $mongoCfg -Raw
    Write-Host ("  --- " + $mongoCfg + " ---")
    ($cfgText -split "`r?`n") | Where-Object { $_ -and $_ -notmatch '^\s*#' } | ForEach-Object { Write-Host ("    " + $_) }
} else {
    Write-Host "  UYARI: mongod.cfg bulunamadi; servis ayar dosyasi olmadan calisiyor olabilir." -ForegroundColor Yellow
}
if ($cfgText -match '(?m)^\s*bindIp:\s*(.+)$') {
    $bind = $Matches[1].Trim()
    if ($bind -notmatch '^(127\.0\.0\.1|localhost)$') { Write-Host ("  UYARI: bindIp = " + $bind + " (yalnizca 127.0.0.1 onerilir; degistirilmedi)") -ForegroundColor Yellow }
    else { Ok ("bindIp = " + $bind) }
}
if ($cfgText -match '(?m)^\s*authorization:\s*enabled') { Write-Host "  BILGI: kimlik dogrulama ACIK - baglanti adreslerine kullanici/sifre eklenecek." -ForegroundColor Yellow }

if ((Get-Service MongoDB).Status -ne "Running") { Start-Service MongoDB; Start-Sleep -Seconds 5 }
Ok ("MongoDB servisi: " + (Get-Service MongoDB).Status)

$direct = "mongodb://127.0.0.1:27017/?directConnection=true"
# Mevcut veritabanlari: geri yuklenecek adlarla cakisma var mi?
$dbList = & $mongosh $direct --quiet --eval "db.getMongo().setReadPref('primaryPreferred'); db.adminCommand({ listDatabases: 1 }).databases.map(function (d) { return d.name + ' (' + (d.sizeOnDisk / 1048576).toFixed(1) + ' MB)'; }).join('; ')" 2>&1
Write-Host ("  Mevcut veritabanlari: " + $dbList)
$ditenDbs = @("diten_auth_v3", "diten_personalization_v3", "diten_background_jobs", "DitenERP", "DitenEnterpriseDb",
              "DitenHumanCapital", "DitenTalentEcosystem", "diten_deven_v1", "diten_procurement_v1", "diten_ppm")
$clash = $ditenDbs | Where-Object { "$dbList" -match ('(^|; )' + [regex]::Escape($_) + ' \(') }
if ($clash) { Write-Host ("  UYARI: bu makinede zaten Diten veritabanlari var: " + ($clash -join ", ") + " - geri yuklemeden once karar verilmeli.") -ForegroundColor Yellow }
else { Ok "Diten veritabani adlariyla cakisma yok" }

# Replica set
$hasReplSet = $cfgText -match '(?m)^\s*replSetName:\s*\S+'
if (-not $hasReplSet) {
    if ($EnableReplicaSet -and $mongoCfg) {
        if ($cfgText -match '(?m)^replication:') { throw "mongod.cfg icinde 'replication:' bolumu var ama replSetName yok - elle duzenlenmeli." }
        $backup = $mongoCfg + ".bak-" + (Get-Date -Format "yyyyMMdd-HHmmss")
        Copy-Item $mongoCfg $backup
        Add-Content -Path $mongoCfg -Value "`r`nreplication:`r`n  replSetName: rs0" -Encoding ASCII
        Ok ("replSetName: rs0 eklendi (yedek: " + $backup + "), servis yeniden baslatiliyor")
        Restart-Service MongoDB
        Start-Sleep -Seconds 8
        $hasReplSet = $true
    } else {
        Write-Host "  !! MongoDB replica set olarak CALISMIYOR. Platform servisi bunu sart kosar." -ForegroundColor Yellow
        Write-Host "     Onayliyorsaniz scripti -EnableReplicaSet ile tekrar calistirin: mongod.cfg yedeklenir," -ForegroundColor Yellow
        Write-Host "     'replication: replSetName: rs0' eklenir ve MongoDB servisi bir kez yeniden baslatilir." -ForegroundColor Yellow
    }
}
if ($hasReplSet) {
    $rsState = & $mongosh $direct --quiet --eval "try { rs.status().ok } catch (e) { e.codeName }" 2>&1
    if ("$rsState" -match '^1$') { Skip "replica set zaten baslatilmis" }
    else {
        $init = & $mongosh $direct --quiet --eval "JSON.stringify(rs.initiate({ _id: 'rs0', members: [ { _id: 0, host: 'localhost:27017' } ] }))" 2>&1
        Write-Host ("  rs.initiate: " + $init)
        Start-Sleep -Seconds 5
    }
    $rsName = & $mongosh $direct --quiet --eval "try { rs.conf()._id + ' / ' + db.hello().isWritablePrimary } catch (e) { e.codeName }" 2>&1
    if ("$rsName" -match 'true') { Ok ("replica set hazir: " + $rsName) } else { Write-Host ("  UYARI: replica set PRIMARY dogrulanamadi: " + $rsName) -ForegroundColor Yellow }
}

# ---------------------------------------------------------------------------------------------
Step "7. Guvenlik duvari"
foreach ($rule in @(@{ Name = "Diten HTTP (80)"; Port = 80 }, @{ Name = "Diten HTTPS (443)"; Port = 443 })) {
    if (Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue) { Skip ($rule.Name + " kurali mevcut") }
    else {
        New-NetFirewallRule -DisplayName $rule.Name -Direction Inbound -Protocol TCP -LocalPort $rule.Port -Action Allow | Out-Null
        Ok ($rule.Name + " acildi")
    }
}
$exposed = Get-NetFirewallPortFilter -Protocol TCP -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -match '^(27017|50[0-6]\d)$'
} | Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object { $_.Enabled -eq "True" -and $_.Direction -eq "Inbound" -and $_.Action -eq "Allow" }
if ($exposed) { Write-Host ("  UYARI: 27017 / 5000-5069 portlarini disariya acan kurallar var: " + (($exposed | ForEach-Object DisplayName) -join ", ")) -ForegroundColor Yellow }
else { Ok "27017 ve servis portlari disariya kapali" }

# ---------------------------------------------------------------------------------------------
Step "Ozet"
$env:Path = [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [Environment]::GetEnvironmentVariable("Path", "User")
$dotnetExe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
Write-Host ("  Windows      : " + $os.Caption + " (" + $installType + ", build " + $cv.CurrentBuild + ")")
Write-Host ("  IIS          : " + ((Get-Service W3SVC -ErrorAction SilentlyContinue).Status))
Write-Host ("  URL Rewrite  : " + (Test-Path "$env:windir\System32\inetsrv\rewrite.dll"))
Write-Host ("  ANCM V2      : " + (Test-Path $ancm))
if (Test-Path $dotnetExe) {
    Write-Host ("  .NET runtime : " + ((& $dotnetExe --list-runtimes | Where-Object { $_ -match 'AspNetCore.App 8' }) -join "; "))
    Write-Host ("  .NET SDK     : " + ((& $dotnetExe --list-sdks) -join "; "))
}
Write-Host ("  Git          : " + $(if (Test-Path $gitExe) { (& $gitExe --version) } else { "yok" }))
Write-Host ("  MongoDB      : " + (& $mongod --version | Select-Object -First 1) + " / servis " + (Get-Service MongoDB).Status)
if ($script:rebootNeeded) { Write-Host "  !! Yeniden baslatma gerekiyor. Sunucuyu yeniden baslatip scripti bir kez daha calistirin." -ForegroundColor Yellow }
Write-Host ""
Write-Host ("Log dosyasi: " + $log) -ForegroundColor Green
Stop-Transcript | Out-Null
