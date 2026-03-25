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

echo "=========================================================="
echo "🚀 Servisler WATCH / HOT-RELOAD Modunda Başlatılıyor..."
echo "=========================================================="
echo "🚀 İzleniyor: Auth (AuthService.Api)"
echo "🚀 İzleniyor: Backend (MdmService.Api)"
echo "🚀 İzleniyor: Platform (Diten.Platform.API)"
echo "🚀 İzleniyor: Gateway (ApiGateway)"@
echo "🚀 İzleniyor: Frontend (Diten.Web)"
echo "=========================================================="
echo ""

# Terminate processes on our target ports
pkill -f run_all.sh 2>/dev/null || true
lsof -ti :5000,5001,5050,5056,5057 | xargs kill -9 2>/dev/null || true
killall -9 dotnet 2>/dev/null || true

prefix_logs "[AUTH    ]" "dotnet watch --project services/DitenAuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj run --urls http://0.0.0.0:5056" &
sleep 2 # Auth service needs more startup time for seeding
prefix_logs "[BACKEND ]" "dotnet watch --project services/DitenMdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj run --urls http://0.0.0.0:5050" &
prefix_logs "[PLATFORM]" "dotnet watch --project services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj run --urls http://0.0.0.0:5057" &
prefix_logs "[GATEWAY ]" "dotnet watch --project gateway/DitenApiGateway/Diten.ApiGateway/Diten.ApiGateway.csproj run --urls http://0.0.0.0:5000" &
prefix_logs "[FRONTEND]" "dotnet watch --project frontend/Diten.Web/Diten.Web.csproj run --urls http://0.0.0.0:5001" &

wait
