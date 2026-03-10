#!/bin/bash

# Diten ERP - All-in-One Start Script for Mac
echo "🚀 Starting Diten ERP Multi-Service Suite..."

# 1. Kill old processes
echo "🧹 Cleaning up ports 5000, 5001, 5050, 5056..."
lsof -ti :5000,5001,5050,5056 | xargs kill -9 2>/dev/null || true
sleep 1

# 2. Build everything
echo "🛠️ Building projects..."
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build services/DitenAuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug
dotnet build services/DitenMdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj -c Debug
dotnet build gateway/DitenApiGateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug

echo "✅ Build complete. Launching services..."

# Function to launch
launch_service() {
    local NAME=$1
    local DIR=$2
    local PORT=$3
    echo "Starting $NAME on port $PORT..."
    
    # Try AppleScript to open in new Terminal tabs (Mac standard)
    /usr/bin/osascript <<EOF
tell application "Terminal"
    do script "cd '$PWD/$DIR' && dotnet run --no-build --urls http://127.0.0.1:$PORT"
end tell
EOF

    if [ $? -ne 0 ]; then
        echo "⚠️ Could not open new terminal tab for $NAME, running in background..."
        cd "$PWD/$DIR" && dotnet run --no-build --urls "http://127.0.0.1:$PORT" > "/tmp/diten_$NAME.log" 2>&1 &
        cd - > /dev/null
    fi
}

launch_service "Auth" "services/DitenAuthService/src/Diten.AuthService.Api" 5056
launch_service "Mdm" "services/DitenMdmService/src/Diten.MdmService.Api" 5050
launch_service "Gateway" "gateway/DitenApiGateway/Diten.ApiGateway" 5000
launch_service "Web" "frontend/Diten.Web" 5001

echo "✨ Services are launching."
echo "🔗 Access: http://127.0.0.1:5001/LegalEntities?culture=en"
