<#
.SYNOPSIS
    Diten vNext'i kaynaktan derler (Release) ve IIS'e yayinlar: 13 site, her biri kendi havuzunda.

.DESCRIPTION
    Once HEPSI derlenir (C:\DitenMigration\publish-stage). Bir tanesi bile derlenemezse IIS'e dokunulmaz.
    Sonra her servis icin:
      - Havuz "vNext-<ad>": .NET CLR yok, AlwaysRunning, bosta kapanma yok
      - Klasor <SiteRoot>\<ad>, dosyalar kopyalanir (logs ve Data klasorleri korunur)
      - Ayarlar ESKI SUNUCUDAN: config-SECRETS\<ad>\appsettings.Production.json ve web.config ortam degiskenleri
        * eski genel adres (-OldPublicUrl) -> -PublicUrl ile degistirilir
        * mongodb:// ile baslayan her deger -> -MongoUri
        * -Rehearsal: arka plan isleri, outbox ve SMTP KAPALI (eski sunucu hala canliyken cift e-posta gitmesin)
      - Site "vNext-<ad>": yalnizca 127.0.0.1:<port> ve [::1]:<port> (disaridan erisilemez)
      - Web sitesi ayrica: <PublicIp>:80:<alan adi> ve *:443:<alan adi> (SNI, mevcut sertifika)
        + http->https yonlendirmesi, + -DefaultTenantId verilirse /account/login -> ?tenantId=... yonlendirmesi
    Baslatma sirasi: auth, platform, digerleri, gateway, web. Her biri /health ile isitilir ve sonuc tablosu yazilir.

.PARAMETER SourceDir   Reponun koku (services, gateway, frontend klasorlerini iceren). GitHub'dan "Download ZIP" ile indirilip acilabilir.
.PARAMETER ConfigDir   Eski sunucu disa aktarimindaki config-SECRETS klasoru.
.PARAMETER Only        Yalnizca bu servisleri yayinla (ornek: -Only web,platform).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\06-publish-and-iis.ps1 -SourceDir C:\DitenSource\ERP-vNext-main -ConfigDir C:\DitenMigration\export-20260924-150000\config-SECRETS -Rehearsal
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$ConfigDir,
    [string]$SiteRoot = "C:\inetpub\diten-vnext",
    [string]$PublicUrl = "https://ditenteknoloji.com",
    [string]$OldPublicUrl = "http://85.105.124.47:53390",
    [string]$MongoUri = "mongodb://localhost:27018/?replicaSet=rs0",
    [string]$PublicIp = "178.18.196.202",
    [string[]]$HostNames = @("ditenteknoloji.com", "www.ditenteknoloji.com"),
    [string]$DefaultTenantId = "",
    [string[]]$Only = @(),
    [switch]$Rehearsal,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration
$stage = "C:\DitenMigration\publish-stage"
$log = "C:\DitenMigration\publish-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log"
Start-Transcript -Path $log | Out-Null
function Step([string]$t) { Write-Host ""; Write-Host ("=== " + $t) -ForegroundColor Cyan }
function Ok([string]$t) { Write-Host ("  [OK] " + $t) -ForegroundColor Green }
function Warn([string]$t) { Write-Host ("  [!] " + $t) -ForegroundColor Yellow }

# Ad, proje, port. Ad = eski sunucudaki klasor adi (config-SECRETS\<ad>) ve yeni site/havuz adi.
$services = @(
    @{ Key = "auth";               Port = 5056; Proj = "services\Diten.AuthService\src\Diten.AuthService.Api\Diten.AuthService.Api.csproj" },
    @{ Key = "platform";           Port = 5057; Proj = "services\Diten.Platform\src\Diten.Platform.API\Diten.Platform.API.csproj" },
    @{ Key = "deven";              Port = 5058; Proj = "services\Diten.DevEnablementService\src\Diten.DevEnablementService.Api\Diten.DevEnablementService.Api.csproj" },
    @{ Key = "mdm";                Port = 5059; Proj = "services\Diten.MdmService\src\Diten.MdmService.Api\Diten.MdmService.Api.csproj" },
    @{ Key = "hcm";                Port = 5060; Proj = "services\Diten.HcmService\src\Diten.HcmService.Api\Diten.HcmService.Api.csproj" },
    @{ Key = "crm";                Port = 5061; Proj = "services\Diten.CrmService\src\Diten.CrmService.Api\Diten.CrmService.Api.csproj" },
    @{ Key = "ppm";                Port = 5062; Proj = "services\Diten.PpmService\src\Diten.PpmService.Api\Diten.PpmService.Api.csproj" },
    @{ Key = "humancapital";       Port = 5063; Proj = "services\Diten.HumanCapitalService\src\Diten.HumanCapitalService.Api\Diten.HumanCapitalService.Api.csproj" },
    @{ Key = "talent";             Port = 5064; Proj = "services\Diten.TalentEcosystemService\src\Diten.TalentEcosystemService.Api\Diten.TalentEcosystemService.Api.csproj" },
    @{ Key = "procurement";        Port = 5065; Proj = "services\Diten.ProcurementService\src\Diten.ProcurementService.Api\Diten.ProcurementService.Api.csproj" },
    @{ Key = "enterprisestrategy"; Port = 5004; Health = "/healthz"; Proj = "services\Diten.EnterpriseStrategyService\src\Diten.EnterpriseStrategy.API\Diten.WebAPI.csproj" },
    @{ Key = "gateway";            Port = 5000; Proj = "gateway\Diten.ApiGateway\Diten.ApiGateway.csproj" },
    @{ Key = "web";                Port = 5001; Proj = "frontend\Diten.Web\Diten.Web.csproj" }
)
if ($Only.Count -gt 0) { $services = $services | Where-Object { $Only -contains $_.Key } }

# ------------------------------------------------------------------------------------------------
Step "0. On kontroller"
if (-not (Test-Path (Join-Path $SourceDir "services"))) {
    $inner = Get-ChildItem $SourceDir -Directory | Where-Object { Test-Path (Join-Path $_.FullName "services") } | Select-Object -First 1
    if ($inner) { $SourceDir = $inner.FullName } else { throw ("Kaynak klasorde services\ yok: " + $SourceDir) }
}
Ok ("kaynak: " + $SourceDir)
if (-not (Test-Path $ConfigDir)) { throw ("config klasoru yok: " + $ConfigDir) }
$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$runtimes = & $dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.0\.(\d+)' } | ForEach-Object { [int]([regex]::Match($_, '8\.0\.(\d+)').Groups[1].Value) }
$maxPatch = ($runtimes | Measure-Object -Maximum).Maximum
Ok ("ASP.NET Core 8 runtime: 8.0." + $maxPatch)
if ($maxPatch -lt 10) { Warn "Runtime cok eski (8.0.$maxPatch). Guvenlik icin Hosting Bundle'i guncelleyin (IIS bir kez yeniden baslar)." }
if ($Rehearsal) { Warn "PROVA MODU: arka plan isleri, outbox ve SMTP kapali calisacak." }

$cert = $null
foreach ($store in @("WebHosting", "My")) {
    $c = Get-ChildItem ("Cert:\LocalMachine\" + $store) -ErrorAction SilentlyContinue |
        Where-Object { $_.NotAfter -gt (Get-Date) -and ($_.DnsNameList.Unicode -contains $HostNames[0]) } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($c -and (-not $cert -or $c.NotAfter -gt $cert.NotAfter)) { $cert = $c; $certStore = $store }
}
if ($cert) {
    Ok ("sertifika: " + $cert.Subject + " | bitis " + $cert.NotAfter.ToString("yyyy-MM-dd") + " | " + $certStore + " | " + $cert.Thumbprint)
    if ($cert.NotAfter -lt (Get-Date).AddDays(20)) { Warn "Sertifikanin bitmesine 20 gunden az var - yenileme ayarlanmali." }
    $certNames = $cert.DnsNameList.Unicode
} else { Warn ("Gecerli bir " + $HostNames[0] + " sertifikasi bulunamadi - yalnizca HTTP baglamasi yapilacak.") }

# ------------------------------------------------------------------------------------------------
Step "1. Derleme (Release)"
if (-not $SkipBuild) {
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    $failed = @()
    foreach ($s in $services) {
        $out = Join-Path $stage $s.Key
        if (Test-Path $out) { Remove-Item $out -Recurse -Force }
        Write-Host ("  derleniyor: " + $s.Key + " ...")
        $ErrorActionPreference = "Continue"
        $buildOut = & $dotnet publish (Join-Path $SourceDir $s.Proj) -c Release -o $out --nologo -v:minimal 2>&1
        $code = $LASTEXITCODE
        $ErrorActionPreference = "Stop"
        if ($code -ne 0) {
            $failed += $s.Key
            $buildOut | Select-String -Pattern "error" | Select-Object -First 10 | ForEach-Object { Write-Host ("     " + $_) -ForegroundColor Red }
        } else { Ok $s.Key }
    }
    if ($failed.Count -gt 0) { Stop-Transcript | Out-Null; throw ("Derlenemeyenler: " + ($failed -join ", ") + " - IIS'e dokunulmadi. Log: " + $log) }
} else { Warn "derleme atlandi (-SkipBuild), mevcut stage kullanilacak" }

# ------------------------------------------------------------------------------------------------
function Get-OldEnv([string]$key) {
    $wc = Join-Path (Join-Path $ConfigDir $key) "web.config"
    $vars = [ordered]@{}
    if (Test-Path $wc) {
        [xml]$x = Get-Content $wc -Raw
        foreach ($e in $x.SelectNodes("//environmentVariable")) { $vars[$e.name] = $e.value }
    } else { Warn ("eski web.config yok: " + $wc) }
    return $vars
}

function Write-SiteConfig($s, [string]$dir) {
    # appsettings.Production.json (eski sunucudan, adres degistirilerek)
    $oldProd = Join-Path (Join-Path $ConfigDir $s.Key) "appsettings.Production.json"
    if (Test-Path $oldProd) {
        $text = (Get-Content $oldProd -Raw).Replace($OldPublicUrl, $PublicUrl)
        [IO.File]::WriteAllText((Join-Path $dir "appsettings.Production.json"), $text, (New-Object Text.UTF8Encoding($false)))
    }
    # ortam degiskenleri
    $vars = Get-OldEnv $s.Key
    foreach ($k in @($vars.Keys)) {
        if ([string]$vars[$k] -like "mongodb://*") { $vars[$k] = $MongoUri }
        elseif ([string]$vars[$k] -like ($OldPublicUrl + "*")) { $vars[$k] = ([string]$vars[$k]).Replace($OldPublicUrl, $PublicUrl) }
    }
    $vars["ASPNETCORE_ENVIRONMENT"] = "Production"
    if ($Rehearsal -and $s.Key -eq "platform") {
        $vars["BackgroundJobs__Enabled"] = "false"; $vars["Eventing__WorkerEnabled"] = "false"; $vars["Smtp__Enabled"] = "false"
    }
    if ($Rehearsal -and $s.Key -eq "auth") { $vars["Smtp__Enabled"] = "false" }

    # web.config: publish'in urettigini al, ortam degiskenlerini ve (web icin) yonlendirmeleri ekle
    $wcPath = Join-Path $dir "web.config"
    [xml]$x = Get-Content $wcPath -Raw
    $anc = $x.SelectSingleNode("//aspNetCore")
    $anc.SetAttribute("stdoutLogEnabled", "true")
    $anc.SetAttribute("stdoutLogFile", ".\logs\stdout")
    $envNode = $anc.SelectSingleNode("environmentVariables")
    if ($envNode) { $anc.RemoveChild($envNode) | Out-Null }
    $envNode = $x.CreateElement("environmentVariables")
    foreach ($k in $vars.Keys) {
        $e = $x.CreateElement("environmentVariable"); $e.SetAttribute("name", $k); $e.SetAttribute("value", [string]$vars[$k]); $envNode.AppendChild($e) | Out-Null
    }
    $anc.AppendChild($envNode) | Out-Null

    if ($s.Key -eq "web") {
        $ws = $x.SelectSingleNode("//system.webServer")
        $old = $ws.SelectSingleNode("rewrite"); if ($old) { $ws.RemoveChild($old) | Out-Null }
        $hostRx = "^(www\.)?" + [regex]::Escape($HostNames[0]) + "$"
        $rules = "<rewrite><rules>" +
            "<rule name='acme-challenge' stopProcessing='true'><match url='^\.well-known/acme-challenge/.*' /><action type='None' /></rule>"
        if ($cert) {
            $rules += "<rule name='https' stopProcessing='true'><match url='(.*)' /><conditions><add input='{HTTPS}' pattern='off' /><add input='{HTTP_HOST}' pattern='$hostRx' /></conditions><action type='Redirect' url='https://{HTTP_HOST}/{R:1}' redirectType='Permanent' /></rule>"
        }
        if ($DefaultTenantId) {
            $rules += "<rule name='tenant-login' stopProcessing='true'><match url='^account/login$' /><conditions><add input='{REQUEST_METHOD}' pattern='^GET$' /><add input='{QUERY_STRING}' pattern='tenantId=' negate='true' /><add input='{HTTP_HOST}' pattern='$hostRx' /></conditions><action type='Redirect' url='/account/login?tenantId=$DefaultTenantId' appendQueryString='true' redirectType='Found' /></rule>"
        }
        $rules += "</rules></rewrite>"
        $frag = $x.CreateDocumentFragment(); $frag.InnerXml = $rules
        $ws.AppendChild($frag) | Out-Null
    }
    $x.Save($wcPath)
}

# ------------------------------------------------------------------------------------------------
Step "2. IIS: havuz, klasor, ayar, site"
New-Item -ItemType Directory -Force -Path $SiteRoot | Out-Null
foreach ($s in $services) {
    $name = "vNext-" + $s.Key
    $dir = Join-Path $SiteRoot $s.Key
    $poolPath = "IIS:\AppPools\" + $name

    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $name | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ""
    Set-ItemProperty $poolPath -Name startMode -Value "AlwaysRunning"
    Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true
    if ((Get-WebAppPoolState -Name $name).Value -eq "Started") { Stop-WebAppPool -Name $name }
    $t = 0; while ((Get-WebAppPoolState -Name $name).Value -ne "Stopped" -and $t -lt 30) { Start-Sleep -Seconds 1; $t++ }

    New-Item -ItemType Directory -Force -Path $dir, (Join-Path $dir "logs") | Out-Null
    & robocopy (Join-Path $stage $s.Key) $dir /MIR /XD logs Data /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw ("dosya kopyalama hatasi: " + $s.Key) }
    Write-SiteConfig $s $dir
    & icacls $dir /grant ("IIS AppPool\" + $name + ":(OI)(CI)M") /T /Q | Out-Null

    if (-not (Get-Website -Name $name)) {
        New-Website -Name $name -PhysicalPath $dir -ApplicationPool $name -IPAddress "127.0.0.1" -Port $s.Port | Out-Null
    }
    Set-ItemProperty ("IIS:\Sites\" + $name) -Name physicalPath -Value $dir
    Set-ItemProperty ("IIS:\Sites\" + $name) -Name applicationDefaults.preloadEnabled -Value $true
    $existing = (Get-WebBinding -Name $name) | ForEach-Object { $_.protocol + " " + $_.bindingInformation }
    $want = @(@{ P = "http"; Ip = "127.0.0.1"; Port = $s.Port; H = "" }, @{ P = "http"; Ip = "[::1]"; Port = $s.Port; H = "" })
    if ($s.Key -eq "web") {
        foreach ($h in $HostNames) {
            $want += @{ P = "http"; Ip = $PublicIp; Port = 80; H = $h }
            if ($cert -and ($certNames -contains $h)) { $want += @{ P = "https"; Ip = "*"; Port = 443; H = $h } }
        }
    }
    foreach ($w in $want) {
        $info = $w.P + " " + $w.Ip + ":" + $w.Port + ":" + $w.H
        if ($existing -contains $info) { continue }
        if ($w.P -eq "https") {
            New-WebBinding -Name $name -Protocol https -IPAddress "*" -Port 443 -HostHeader $w.H -SslFlags 1
            $sslShow = & netsh http show sslcert hostnameport=("{0}:443" -f $w.H) 2>&1
            if ("$sslShow" -notmatch [regex]::Escape($cert.Thumbprint)) {
                if ("$sslShow" -match 'Certificate Hash') { & netsh http delete sslcert hostnameport=("{0}:443" -f $w.H) | Out-Null }
                (Get-WebBinding -Name $name -Protocol https -HostHeader $w.H).AddSslCertificate($cert.Thumbprint, $certStore)
            }
        } else {
            New-WebBinding -Name $name -Protocol http -IPAddress $w.Ip -Port $w.Port -HostHeader $w.H
        }
        Ok ($name + " baglama: " + $info)
    }
    # varsayilan olarak olusan *:port baglamasini kaldir (disaridan erisilmesin)
    Get-WebBinding -Name $name -Protocol http | Where-Object { $_.bindingInformation -eq ("*:" + $s.Port + ":") } | Remove-WebBinding
    Ok ($name + " hazir: " + $dir)
}

# ------------------------------------------------------------------------------------------------
Step "3. Baslatma ve saglik kontrolu"
$results = @()
foreach ($s in $services) {
    $name = "vNext-" + $s.Key
    Start-WebAppPool -Name $name -ErrorAction SilentlyContinue
    try { Start-Website -Name $name } catch { Warn ($name + " baslatilamadi: " + $_.Exception.Message) }
    $status = "?"; $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt 90) {
        try {
            $hp = if ($s.Health) { $s.Health } else { "/health" }
            $r = Invoke-WebRequest -Uri ("http://127.0.0.1:" + $s.Port + $hp) -UseBasicParsing -TimeoutSec 30
            $status = [string]$r.StatusCode; break
        } catch {
            $resp = $_.Exception.Response
            if ($resp) { $status = [string][int]$resp.StatusCode; if ($status -notmatch '^50[23]$') { break } }
            else { $status = "baglanti yok" }
            Start-Sleep -Seconds 3
        }
    }
    $results += [pscustomobject]@{ Site = $name; Port = $s.Port; Health = $status; Sure = [int]$sw.Elapsed.TotalSeconds }
    # 5xx ya da baglanti yoksa uygulama acilamamistir; 404/401 uygulamanin calistigini gosterir (health yolu yok / yetki ister)
    $started = $status -match '^[1-4]\d\d$'
    $results[-1] | Add-Member -NotePropertyName Durum -NotePropertyValue $(if ($started) { "CALISIYOR" } else { "HATA" })
    if (-not $started) {
        $so = Get-ChildItem (Join-Path (Join-Path $SiteRoot $s.Key) "logs") -Filter "stdout*" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($so) { Warn ($name + " stdout son satirlar:"); Get-Content $so.FullName -Tail 15 | ForEach-Object { Write-Host ("     " + $_) } }
    }
}
$results | Format-Table -AutoSize | Out-String | Write-Host
if ($cert) {
    try {
        $r = Invoke-WebRequest -Uri ($PublicUrl + "/account/login") -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 30 -ErrorAction SilentlyContinue
        Ok ("genel adres " + $PublicUrl + "/account/login -> " + $r.StatusCode)
    } catch { Warn ("genel adres denetimi: " + $_.Exception.Message) }
}
Write-Host ("Log: " + $log) -ForegroundColor Green
Stop-Transcript | Out-Null
