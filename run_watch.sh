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
lsof -ti :5000,5001,5056,5057,5058,5059,5060 | xargs kill -9 2>/dev/null || true
lsof -ti :4010,5067 | xargs kill -9 2>/dev/null || true # Prism product-master mock + Inventory
killall -9 dotnet 2>/dev/null || true

# Watch mode commands
# Note: dotnet watch run --project <path>
prefix_logs "[AUTH    ]" "dotnet watch run --non-interactive --project services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj --urls http://0.0.0.0:5056" &
sleep 2 # Auth service needs more startup time for seeding
prefix_logs "[DEVEN   ]" "dotnet watch run --non-interactive --project services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj --urls http://0.0.0.0:5058" &
prefix_logs "[PLATFORM]" "dotnet watch run --non-interactive --project services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj --urls http://0.0.0.0:5057" &
prefix_logs "[MDM     ]" "dotnet watch run --non-interactive --project services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj --urls http://0.0.0.0:5059" &
prefix_logs "[HCM     ]" "dotnet watch run --non-interactive --project services/Diten.HcmService/src/Diten.HcmService.Api/Diten.HcmService.Api.csproj --urls http://0.0.0.0:5060" &
# FU-PM-01: MDM PRODUCT-MASTER gerçek uç noktası gelene kadar geçici — Prism product-master mock (4010)
PRISM_JS=""
for c in "${LOCALAPPDATA//\\//}"/npm-cache/_npx/*/node_modules/@stoplight/prism-cli/dist/index.js "$HOME"/.npm/_npx/*/node_modules/@stoplight/prism-cli/dist/index.js; do
    [ -f "$c" ] && PRISM_JS="$c" && break
done
PM_SPEC=$(find docs -name 'product-master-bundle.openapi.yaml' -print -quit 2>/dev/null)
if [ -n "$PRISM_JS" ] && [ -n "$PM_SPEC" ]; then
    prefix_logs "[PRISM-PM]" "node --max-http-header-size=65536 $PRISM_JS mock $PM_SPEC --port 4010 --host 127.0.0.1" &
else
    echo "⚠️ Prism product-master mock (4010) atlandı: prism-cli önbellekte ya da product-master-bundle.openapi.yaml bulunamadı (önbelleğe almak için: npx --yes @stoplight/prism-cli@4.10.5 --version)"
fi
prefix_logs "[INVENTRY]" "env Inventory__Upstream__ProductMasterBaseUrl=http://127.0.0.1:4010/ dotnet watch run --non-interactive --project services/Diten.InventoryService/src/Diten.InventoryService.Api/Diten.InventoryService.Api.csproj --launch-profile http --urls http://0.0.0.0:5067" &
prefix_logs "[GATEWAY ]" "dotnet watch run --non-interactive --project gateway/Diten.ApiGateway/Diten.ApiGateway.csproj --urls http://0.0.0.0:5000" &
# FU-PM-01: MDM PRODUCT-MASTER gelene kadar geçici (docs/roadmap/plans/mod-0290-product-master-bundle-requirements-for-mvp1-2026-10-02.md §6) — WP-0173-PM0
# Diten.Web'in /api/product-master okumaları Prism mock'a (4010) — yalnız bu Web süreci alır ve yalnız Development'ta geçerlidir.
export Inventory__ProductMasterBaseUrl=http://127.0.0.1:4010/
prefix_logs "[FRONTEND]" "dotnet watch run --non-interactive --project frontend/Diten.Web/Diten.Web.csproj --urls http://0.0.0.0:5001" &
unset Inventory__ProductMasterBaseUrl # yalnız Diten.Web süreci aldı

wait
