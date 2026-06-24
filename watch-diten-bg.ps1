# Diten ERP - Background Start Script for Antigravity Workspace runner
$ErrorActionPreference = "Continue"
Write-Host "🚀 Starting Diten ERP Multi-Service Suite with WATCH (Hot Reload)..." -ForegroundColor Cyan

# 1. Kill old processes on target ports (5000, 5001, 5056, 5057, 5058)
Write-Host "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000,5001,5056,5057,5058,5059 -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess | 
    Unique | 
    ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }

# Also kill any leftover dotnet.exe processes
taskkill /F /IM dotnet.exe /T 2>$null
Start-Sleep -Seconds 1

# Define services
$services = @(
    @{ Name = "Auth"; Path = "services/Diten.AuthService/src/Diten.AuthService.Api"; Port = 5056 },
    @{ Name = "DevEnablement"; Path = "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api"; Port = 5058 },
    @{ Name = "Mdm"; Path = "services/Diten.MdmService/src/Diten.MdmService.Api"; Port = 5059 },
    @{ Name = "Platform"; Path = "services/Diten.Platform/src/Diten.Platform.API"; Port = 5057 },
    @{ Name = "Gateway"; Path = "gateway/Diten.ApiGateway"; Port = 5000 },
    @{ Name = "Web"; Path = "frontend/Diten.Web"; Port = 5001 }
)

# 2. Clean projects sequentially first to prevent concurrent clean conflicts
Write-Host "🧹 Running dotnet clean sequentially on all projects to avoid build locks..." -ForegroundColor Yellow
foreach ($svc in $services) {
    $absPath = Resolve-Path $svc.Path
    Write-Host "Cleaning $($svc.Name)..." -ForegroundColor Yellow
    Push-Location $absPath
    dotnet clean -c Debug --nologo
    Pop-Location
}

$jobs = @()

foreach ($svc in $services) {
    $absPath = Resolve-Path $svc.Path
    Write-Host "Starting $($svc.Name) on port $($svc.Port) in background job..." -ForegroundColor Green
    
    # Start a background job running dotnet watch run
    $job = Start-Job -Name $svc.Name -ScriptBlock {
        param($path)
        cd $path
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet watch run --no-hot-reload --non-interactive --launch-profile http 2>&1
    } -ArgumentList $absPath
    
    $jobs += $job
    
    # Wait for Auth service seeding
    if ($svc.Name -eq "Auth") {
        Start-Sleep -Seconds 4
    } else {
        Start-Sleep -Seconds 2
    }
}

Write-Host "`n✨ All services successfully started in the background!" -ForegroundColor Cyan
Write-Host "🔗 Access: http://localhost:5001/platform/login" -ForegroundColor Cyan
Write-Host "Streaming logs... Press Ctrl+C to terminate or use manage_task kill.`n" -ForegroundColor Yellow

# Continuously receive and display logs from background jobs to keep task alive
try {
    while ($true) {
        foreach ($job in $jobs) {
            $messages = Receive-Job -Job $job
            if ($messages) {
                foreach ($msg in $messages) {
                    $prefix = "[$($job.Name)]".PadRight(15)
                    Write-Host "$prefix $msg"
                }
            }
        }
        Start-Sleep -Milliseconds 250
    }
}
finally {
    # Cleanup jobs if the script gets terminated
    Write-Host "`n🧹 Cleaning up background jobs..." -ForegroundColor Yellow
    foreach ($job in $jobs) {
        Stop-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -ErrorAction SilentlyContinue
    }
    # Double check and kill any spawned dotnet processes
    taskkill /F /IM dotnet.exe /T 2>$null
    Write-Host "🛑 Stopped all services." -ForegroundColor Red
}
