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
echo "🚀 Servislerin projeleri derleniyor..."
echo "========================================="
dotnet build services/DitenMdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj -v q
dotnet build gateway/DitenApiGateway/Diten.ApiGateway/Diten.ApiGateway.csproj -v q
dotnet build frontend/Diten.Web/Diten.Web.WebUI.csproj -v q

echo "========================================="
echo "🚀 Başlatılıyor: Backend (MdmService.Api)"
echo "🚀 Başlatılıyor: Gateway (ApiGateway)"
echo "🚀 Başlatılıyor: Frontend (Diten.Web)"
echo "========================================="
echo ""

# Terminate processes on our target ports
lsof -ti :5000,5001,5050 | xargs kill -9 2>/dev/null || true
killall -9 dotnet 2>/dev/null || true

prefix_logs "[BACKEND ]" "dotnet run --no-build --project services/DitenMdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj --urls http://0.0.0.0:5050" &
prefix_logs "[GATEWAY ]" "dotnet run --no-build --project gateway/DitenApiGateway/Diten.ApiGateway/Diten.ApiGateway.csproj --urls http://0.0.0.0:5000" &
prefix_logs "[FRONTEND]" "dotnet run --no-build --project frontend/Diten.Web/Diten.Web.WebUI.csproj --urls http://0.0.0.0:5001" &

wait
