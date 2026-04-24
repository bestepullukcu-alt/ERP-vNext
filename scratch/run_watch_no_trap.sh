#!/bin/bash
export PATH=$PATH:/usr/local/share/dotnet
# run_watch_no_trap.sh - Same as run_watch.sh but without trap 'kill 0' EXIT

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
echo "🚀 Servisler WATCH modunda (No Trap) başlatılıyor..."
echo "========================================="
echo ""

# Terminate processes on our target ports
lsof -ti :5000,5001,5050,5056,5057 | xargs kill -9 2>/dev/null || true
killall -9 dotnet 2>/dev/null || true

# Watch mode commands
prefix_logs "[AUTH    ]" "dotnet watch run --non-interactive --project services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj --urls http://0.0.0.0:5056" &
sleep 2
prefix_logs "[BACKEND ]" "dotnet watch run --non-interactive --project services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj --urls http://0.0.0.0:5050" &
prefix_logs "[PLATFORM]" "dotnet watch run --non-interactive --project services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj --urls http://0.0.0.0:5057" &
prefix_logs "[GATEWAY ]" "dotnet watch run --non-interactive --project gateway/Diten.ApiGateway/Diten.ApiGateway.csproj --urls http://0.0.0.0:5000" &
prefix_logs "[FRONTEND]" "dotnet watch run --non-interactive --project frontend/Diten.Web/Diten.Web.csproj --urls http://0.0.0.0:5001" &

# We still wait so the script has time to output initial logs
sleep 15
echo "✨ Services started in background. Monitor logs or access via ports."
