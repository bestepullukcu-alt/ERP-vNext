<#
.SYNOPSIS
    Diten vNext kurulumu oncesi: yeni sunucudaki eski siteleri durdurur (silmez) ve cakisan baglamalari kaldirir.
    -Revert ile her sey kaydedilen haline geri doner.

.DESCRIPTION
    Yeni sunucuda eski nesil Diten / PharmaCovigilance siteleri, vNext'in kullanacagi portlari (5000, 5001, 5004,
    5056-5065) ve ditenteknoloji.com alan adini tutuyor. Bu script:
      1. -Keep listesindekiler ve "vNext-" ile baslayanlar DISINDAKI tum siteleri durdurur,
         otomatik baslatmayi kapatir; yalnizca bu sitelere ait uygulama havuzlarini durdurur.
      2. Durdurulan sitelerden, vNext portlarini ya da ditenteknoloji.com'u kullanan baglamalari kaldirir.
      3. Onceki durumu (site, havuz, kaldirilan baglamalar) JSON dosyasina yazar.
    Hicbir site, havuz, dosya ya da sertifika SILINMEZ.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\03-stop-legacy-sites.ps1            # once ne yapilacagini gosterir
    powershell -ExecutionPolicy Bypass -File .\03-stop-legacy-sites.ps1 -Apply     # uygular
    powershell -ExecutionPolicy Bypass -File .\03-stop-legacy-sites.ps1 -Revert    # geri alir
#>
[CmdletBinding()]
param(
    [string[]]$Keep = @("gmgmvc", "gmgwebsrv", "gmgApi", "ftpHelp", "ftpPtoje", "financeftp"),
    [int[]]$VNextPorts = @(5000, 5001, 5004, 5056, 5057, 5058, 5059, 5060, 5061, 5062, 5063, 5064, 5065),
    [string[]]$VNextHosts = @("ditenteknoloji.com", "www.ditenteknoloji.com"),
    [string]$StateFile = "C:\DitenMigration\legacy-sites-state.json",
    [switch]$Apply,
    [switch]$Revert
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration
New-Item -ItemType Directory -Force -Path (Split-Path $StateFile) | Out-Null

function Did([string]$t) { Write-Host ("  [YAPILDI] " + $t) -ForegroundColor Magenta }
function Plan([string]$t) { Write-Host ("  [PLAN] " + $t) -ForegroundColor Yellow }

# ------------------------------------------------------------------------------------------------
if ($Revert) {
    if (-not (Test-Path $StateFile)) { throw "Durum dosyasi yok: $StateFile" }
    $state = Get-Content $StateFile -Raw | ConvertFrom-Json
    Write-Host "Geri aliniyor: once vNext siteleri durduruluyor (baglama cakismasi olmasin)..." -ForegroundColor Cyan
    Get-ChildItem IIS:\Sites | Where-Object { $_.Name -like "vNext-*" } | ForEach-Object { Stop-Website -Name $_.Name; Did ("durduruldu: " + $_.Name) }
    foreach ($s in $state.Sites) {
        foreach ($b in @($s.RemovedBindings)) {
            if (-not $b) { continue }
            $parts = $b.bindingInformation.Split(':')
            $ip = $parts[0]; $port = [int]$parts[1]; $hostName = ($parts[2..($parts.Length - 1)] -join ':')
            $bindArgs = @{ Name = $s.Name; Protocol = $b.protocol; Port = $port; IPAddress = $ip; HostHeader = $hostName }
            if ($b.protocol -eq "https") { $bindArgs.SslFlags = [int]$b.sslFlags }
            New-WebBinding @bindArgs
            Did ("baglama geri eklendi: " + $s.Name + " " + $b.protocol + " " + $b.bindingInformation)
        }
        Set-ItemProperty ("IIS:\Sites\" + $s.Name) -Name serverAutoStart -Value ([bool]$s.ServerAutoStart)
        if ($s.PoolWasStarted -and $s.AppPool) {
            Set-ItemProperty ("IIS:\AppPools\" + $s.AppPool) -Name autoStart -Value $true
            if ((Get-WebAppPoolState -Name $s.AppPool).Value -ne "Started") { Start-WebAppPool -Name $s.AppPool }
        }
        if ($s.State -eq "Started") {
            try { Start-Website -Name $s.Name; Did ("baslatildi: " + $s.Name) } catch { Write-Host ("  baslatilamadi: " + $s.Name + " - " + $_.Exception.Message) -ForegroundColor Red }
        }
    }
    Write-Host "Geri alma tamamlandi." -ForegroundColor Green
    return
}

# ------------------------------------------------------------------------------------------------
$sites = Get-ChildItem IIS:\Sites
$keepPools = $sites | Where-Object { $Keep -contains $_.Name -or $_.Name -like "vNext-*" } | ForEach-Object { $_.applicationPool }
$targets = $sites | Where-Object { $Keep -notcontains $_.Name -and $_.Name -notlike "vNext-*" }

Write-Host ("Kalacak siteler: " + ($Keep -join ", ")) -ForegroundColor Green
Write-Host ("Islem gorecek site sayisi: " + @($targets).Count) -ForegroundColor Cyan
if (-not $Apply) { Write-Host "(-Apply verilmedi: yalnizca plan gosteriliyor, hicbir sey degismeyecek)" -ForegroundColor Yellow }

$record = @()
foreach ($s in $targets) {
    $poolState = if ($s.applicationPool) { (Get-WebAppPoolState -Name $s.applicationPool -ErrorAction SilentlyContinue).Value } else { $null }
    $removed = @()
    foreach ($b in $s.Bindings.Collection) {
        if ($b.protocol -notin "http", "https") { continue }
        $parts = $b.bindingInformation.Split(':')
        $port = [int]$parts[1]
        $hostName = ($parts[2..($parts.Length - 1)] -join ':')
        if ($VNextPorts -contains $port -or $VNextHosts -contains $hostName) {
            $removed += [pscustomobject]@{ protocol = $b.protocol; bindingInformation = $b.bindingInformation; sslFlags = $b.sslFlags }
        }
    }
    $record += [pscustomobject]@{
        Name = $s.Name; State = $s.State; ServerAutoStart = $s.serverAutoStart; AppPool = $s.applicationPool
        PoolWasStarted = ($poolState -eq "Started"); RemovedBindings = $removed
    }

    $label = $s.Name + " [" + $s.State + "]"
    if (-not $Apply) {
        Plan ("durdurulacak: " + $label)
        foreach ($r in $removed) { Plan ("   kaldirilacak baglama: " + $r.protocol + " " + $r.bindingInformation) }
        continue
    }
    if ($s.State -eq "Started") { Stop-Website -Name $s.Name }
    Set-ItemProperty ("IIS:\Sites\" + $s.Name) -Name serverAutoStart -Value $false
    Did ("durduruldu: " + $label)
    foreach ($r in $removed) {
        $parts = $r.bindingInformation.Split(':')
        Get-WebBinding -Name $s.Name -Protocol $r.protocol | Where-Object { $_.bindingInformation -eq $r.bindingInformation } | Remove-WebBinding
        Did ("   baglama kaldirildi: " + $r.protocol + " " + $r.bindingInformation)
    }
    if ($s.applicationPool -and ($keepPools -notcontains $s.applicationPool) -and $poolState -eq "Started") {
        Stop-WebAppPool -Name $s.applicationPool
        Set-ItemProperty ("IIS:\AppPools\" + $s.applicationPool) -Name autoStart -Value $false
        Did ("   havuz durduruldu: " + $s.applicationPool)
    }
}

if ($Apply) {
    if (Test-Path $StateFile) { Copy-Item $StateFile ($StateFile + ".bak-" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
    ConvertTo-Json -InputObject @{ Sites = $record; SavedAt = (Get-Date).ToString("s") } -Depth 6 | Set-Content -Path $StateFile -Encoding UTF8
    Write-Host ""
    Write-Host ("Tamamlandi. Onceki durum: " + $StateFile) -ForegroundColor Green
    Write-Host "Geri almak icin: -Revert" -ForegroundColor Green
}
