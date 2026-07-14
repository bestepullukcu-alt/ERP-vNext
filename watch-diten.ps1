# Diten ERP - All-in-One Start Script for Windows with Watch (Hot Reload)
Write-Host "🚀 Starting Diten ERP Multi-Service Suite with WATCH (Hot Reload) on Windows..." -ForegroundColor Cyan

# 1. Kill old processes on target ports (5000, 5001, 5056, 5057, 5058, 5060)
Write-Host "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058, 5060..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000,5001,5056,5057,5058,5060 -ErrorAction SilentlyContinue | 
    Select-Object -ExpandProperty OwningProcess | 
    Unique | 
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

# Also kill any dotnet.exe processes
taskkill /F /IM dotnet.exe /T 2>$null

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
Start-Sleep -Seconds 2 # Auth service needs more startup time for seeding

Launch-Service -Name "DevEnablement" -Path "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api" -Port 5058
Launch-Service -Name "Platform" -Path "services/Diten.Platform/src/Diten.Platform.API" -Port 5057
Launch-Service -Name "Hcm" -Path "services/Diten.HcmService/src/Diten.HcmService.Api" -Port 5060
Launch-Service -Name "Gateway" -Path "gateway/Diten.ApiGateway" -Port 5000
Launch-Service -Name "Web" -Path "frontend/Diten.Web" -Port 5001

Write-Host "`n✨ Services are launching in separate CMD windows." -ForegroundColor Green
Write-Host "🔗 Access: http://localhost:5001/platform/login" -ForegroundColor Cyan
