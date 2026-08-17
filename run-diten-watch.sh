#!/bin/bash

echo "🚀 Starting Diten ERP Multi-Service Suite with Hot Reload (watch)..."

echo "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058 and killing dotnet processes..."
lsof -ti :5000,5001,5056,5057,5058 | xargs kill -9 2>/dev/null || true
killall -9 dotnet 2>/dev/null || true
pkill -9 -f dotnet 2>/dev/null || true
sleep 1

echo "✨ Launching services with dotnet watch run in new Terminal windows..."
echo "💡 Note: Browser refresh is disabled to prevent 401/WSS errors, but server will still hot-reload."

# We set DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1 to fix the 401/WSS errors in the console.
# The server will still rebuild and restart on changes (Hot Reload).

/usr/bin/osascript <<APPLESCRIPT
tell application "Terminal"
    do script "cd '$PWD/services/Diten.AuthService/src/Diten.AuthService.Api' && export ASPNETCORE_ENVIRONMENT=Development && dotnet watch run --urls http://localhost:5056"
    do script "cd '$PWD/services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api' && export ASPNETCORE_ENVIRONMENT=Development && dotnet watch run --urls http://localhost:5058"
    do script "cd '$PWD/services/Diten.Platform/src/Diten.Platform.API' && export ASPNETCORE_ENVIRONMENT=Development && dotnet watch run --urls http://localhost:5057"
    do script "cd '$PWD/gateway/Diten.ApiGateway' && export ASPNETCORE_ENVIRONMENT=Development && dotnet watch run --urls http://localhost:5000"
    do script "cd '$PWD/frontend/Diten.Web' && export ASPNETCORE_ENVIRONMENT=Development && export DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1 && dotnet watch run --urls http://localhost:5001"
end tell
APPLESCRIPT

echo "✅ All services launched!"
