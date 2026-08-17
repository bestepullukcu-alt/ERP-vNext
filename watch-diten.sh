#!/bin/bash

# Diten ERP - All-in-One Start Script for Mac with Watch (Hot Reload)
echo "🚀 Starting Diten ERP Multi-Service Suite with WATCH..."

# 1. Kill old processes
echo "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058..."
lsof -ti :5000,5001,5056,5057,5058 | xargs kill -9 2>/dev/null || true
sleep 1

# Function to launch
launch_service() {
    local NAME=$1
    local DIR=$2
    local PORT=$3
    echo "Starting $NAME on port $PORT with watch..."
    
    # Try AppleScript to open in new Terminal tabs (Mac standard)
    /usr/bin/osascript <<EOF
tell application "Terminal"
    do script "cd '$PWD/$DIR' && dotnet watch run --urls http://localhost:$PORT"
end tell
EOF
}

launch_service "Auth" "services/Diten.AuthService/src/Diten.AuthService.Api" 5056
launch_service "DevEnablement" "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api" 5058
launch_service "Platform" "services/Diten.Platform/src/Diten.Platform.API" 5057
launch_service "Gateway" "gateway/Diten.ApiGateway" 5000
launch_service "Web" "frontend/Diten.Web" 5001

echo "✨ Services are launching in separate Terminal tabs."
echo "🔗 Access: http://localhost:5001/platform/login"
