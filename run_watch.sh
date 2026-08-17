#!/bin/bash
trap 'kill 0' SIGINT SIGTERM EXIT # Kill all spawned jobs on exit

# Function to prefix logs based on service name
prefix_logs() {
    local prefix=$1
    local cmd=$2
    $cmd 2>&1 | while IFS= read -r line; do
        if [[ "$line" == *fail* ]] || [[ "$line" == *error* ]]; then
            echo -e "\033[31m$prefix\033[0m $line"
        elif [[ "$line" == *info* ]]; then
            echo -e "\033[32m$prefix\033[0m $line"
        else
            echo -e "\033[36m$prefix\033[0m $line"
        fi
    done
}

echo "========================================="
echo "🚀 Servisler WATCH modunda başlatılıyor..."
echo "========================================="
echo ""

# Terminate processes on our target ports
lsof -ti :5000,5001,5056,5057,5058 | xargs kill -9 2>/dev/null || true
killall -9 dotnet 2>/dev/null || true

# Watch mode commands
# Note: dotnet watch run --project <path>
prefix_logs "[AUTH    ]" "dotnet watch run --non-interactive --project services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj --urls http://0.0.0.0:5056" &
sleep 2 # Auth service needs more startup time for seeding
prefix_logs "[DEVEN   ]" "dotnet watch run --non-interactive --project services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj --urls http://0.0.0.0:5058" &
prefix_logs "[PLATFORM]" "dotnet watch run --non-interactive --project services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj --urls http://0.0.0.0:5057" &
prefix_logs "[GATEWAY ]" "dotnet watch run --non-interactive --project gateway/Diten.ApiGateway/Diten.ApiGateway.csproj --urls http://0.0.0.0:5000" &
prefix_logs "[FRONTEND]" "dotnet watch run --non-interactive --project frontend/Diten.Web/Diten.Web.csproj --urls http://0.0.0.0:5001" &

wait
