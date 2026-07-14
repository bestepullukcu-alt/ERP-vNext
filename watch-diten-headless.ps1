# Diten ERP - Headless Background Start Script with Watch (Hot Reload) for Windows
Write-Host "🚀 Starting Diten ERP Multi-Service Suite with WATCH (Hot Reload) in Background..." -ForegroundColor Cyan

# 1. Kill old processes on target ports (5000, 5001, 5056, 5057, 5058, 5059, 5060)
Write-Host "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058, 5059, 5060..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000,5001,5056,5057,5058,5059,5060 -ErrorAction SilentlyContinue | 
    Select-Object -ExpandProperty OwningProcess | 
    Unique | 
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

# Also kill any dotnet.exe processes
taskkill /F /IM dotnet.exe /T 2>$null

Start-Sleep -Seconds 2

# Ensure logs directory exists
$logDir = Join-Path (Get-Location) "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Function to launch a service in background with redirect
function Launch-Service-Headless {
    param (
        [string]$Name,
        [string]$Path,
        [int]$Port
    )
    Write-Host "Starting $Name on port $Port in background..." -ForegroundColor Green
    
    $absPath = (Resolve-Path $Path).Path
    $outFile = Join-Path $logDir "$Name-out.log"
    $errFile = Join-Path $logDir "$Name-err.log"
    
    # Run dotnet watch run with no new window and redirect logs
    Start-Process -FilePath "dotnet" `
        -ArgumentList "watch", "run", "--non-interactive", "--launch-profile", "http" `
        -WorkingDirectory $absPath `
        -NoNewWindow `
        -RedirectStandardOutput $outFile `
        -RedirectStandardError $errFile
}

# Launch all services sequentially with proper delays
Launch-Service-Headless -Name "Auth" -Path "services/Diten.AuthService/src/Diten.AuthService.Api" -Port 5056
Start-Sleep -Seconds 4 # Auth service needs more startup time for database seeding and token keys

Launch-Service-Headless -Name "DevEnablement" -Path "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api" -Port 5058
Start-Sleep -Seconds 2

Launch-Service-Headless -Name "Platform" -Path "services/Diten.Platform/src/Diten.Platform.API" -Port 5057
Start-Sleep -Seconds 2

Launch-Service-Headless -Name "Hcm" -Path "services/Diten.HcmService/src/Diten.HcmService.Api" -Port 5060
Start-Sleep -Seconds 2

Launch-Service-Headless -Name "Mdm" -Path "services/Diten.MdmService/src/Diten.MdmService.Api" -Port 5059
Start-Sleep -Seconds 2

Launch-Service-Headless -Name "Gateway" -Path "gateway/Diten.ApiGateway" -Port 5000
Start-Sleep -Seconds 2

Launch-Service-Headless -Name "Web" -Path "frontend/Diten.Web" -Port 5001
Start-Sleep -Seconds 2

Write-Host "`n✨ All services successfully launched in background (headless watch mode)." -ForegroundColor Green
Write-Host "📝 Logs are being captured under the '$logDir' directory." -ForegroundColor Yellow
Write-Host "🔗 Access: http://localhost:5001/platform/login" -ForegroundColor Cyan
