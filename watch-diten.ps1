# Diten ERP - All-in-One Start Script for Windows with Watch (Hot Reload)
Write-Host "🚀 Starting Diten ERP Multi-Service Suite with WATCH (Hot Reload) on Windows..." -ForegroundColor Cyan

# 1. Kill old processes on target ports (5000, 5001, 5056, 5057, 5058, 5059, 5060, 5061, 5067)
Write-Host "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058, 5059, 5060, 5061, 5067..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000,5001,5056,5057,5058,5059,5060,5061,5067 -ErrorAction SilentlyContinue | 
    Select-Object -ExpandProperty OwningProcess | 
    Unique | 
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

# Also kill any dotnet.exe processes
taskkill /F /IM dotnet.exe /T 2>$null

# Prism product-master mock (node) + Inventory: ports 4010, 5067
Write-Host "🧹 Cleaning up ports 4010, 5067..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 4010,5067 -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess |
    Unique |
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

Start-Sleep -Seconds 1

# Function to launch a service in a new CMD window
function Launch-Service {
    param (
        [string]$Name,
        [string]$Path,
        [int]$Port
    )
    Write-Host "Starting $Name on port $Port with watch..." -ForegroundColor Green
    
    $absPath = (Resolve-Path $Path).Path
    # We use Start-Process to run cmd.exe in a new window, which runs dotnet watch run
    Start-Process cmd.exe -ArgumentList "/k title Diten ERP - $Name && cd `"$absPath`" && dotnet watch run --non-interactive --launch-profile http"
}

# Launch all services
Launch-Service -Name "Auth" -Path "services/Diten.AuthService/src/Diten.AuthService.Api" -Port 5056
Start-Sleep -Seconds 3 # Auth service needs more startup time for seeding

Launch-Service -Name "DevEnablement" -Path "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api" -Port 5058
Launch-Service -Name "Platform" -Path "services/Diten.Platform/src/Diten.Platform.API" -Port 5057
Launch-Service -Name "Hcm" -Path "services/Diten.HcmService/src/Diten.HcmService.Api" -Port 5060
Launch-Service -Name "Mdm" -Path "services/Diten.MdmService/src/Diten.MdmService.Api" -Port 5059
Launch-Service -Name "Crm" -Path "services/Diten.CrmService/src/Diten.CrmService.Api" -Port 5061
# FU-PM-01: MDM PRODUCT-MASTER gerçek uç noktası gelene kadar geçici — Prism product-master mock (4010)
$prismJs = Get-ChildItem "$env:LOCALAPPDATA\npm-cache\_npx\*\node_modules\@stoplight\prism-cli\dist\index.js" -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
$pmSpec = Get-ChildItem -Path "docs" -Recurse -Filter "product-master-bundle.openapi.yaml" -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if ($prismJs -and $pmSpec) {
    Write-Host "Starting PrismProductMaster on port 4010..." -ForegroundColor Green
    Start-Process cmd.exe -ArgumentList "/k title Diten ERP - PrismProductMaster && node --max-http-header-size=65536 `"$prismJs`" mock `"$pmSpec`" --port 4010 --host 127.0.0.1"
} else {
    Write-Host "⚠️ Prism product-master mock (4010) skipped: prism-cli not in npm cache or product-master-bundle.openapi.yaml not found (cache it: npx --yes @stoplight/prism-cli@4.10.5 --version)" -ForegroundColor Yellow
}
# Inventory: same as Launch-Service, plus Inventory__Upstream__ProductMasterBaseUrl for this window only
Write-Host "Starting Inventory on port 5067 with watch..." -ForegroundColor Green
$inventoryPath = (Resolve-Path "services/Diten.InventoryService/src/Diten.InventoryService.Api").Path
Start-Process cmd.exe -ArgumentList "/k title Diten ERP - Inventory && cd `"$inventoryPath`" && set `"Inventory__Upstream__ProductMasterBaseUrl=http://127.0.0.1:4010/`" && dotnet watch run --non-interactive --launch-profile http"
Launch-Service -Name "Gateway" -Path "gateway/Diten.ApiGateway" -Port 5000
# FU-PM-01: MDM PRODUCT-MASTER gelene kadar geçici (docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md §6) — WP-0173-PM0
# Diten.Web'in /api/product-master okumaları Prism mock'a (4010), yalnız Development'ta. Web penceresi bu değişkeni devralır;
# hemen ardından silinir, sonraki hiçbir pencere almaz.
$env:Inventory__ProductMasterBaseUrl = 'http://127.0.0.1:4010/'
Launch-Service -Name "Web" -Path "frontend/Diten.Web" -Port 5001
Remove-Item Env:Inventory__ProductMasterBaseUrl -ErrorAction SilentlyContinue

Write-Host "`n✨ Services are launching in separate CMD windows." -ForegroundColor Green
Write-Host "🔗 Access: http://localhost:5001/platform/login" -ForegroundColor Cyan
