#!/bin/bash

# Diten ERP - All-in-One Start Script for Mac
echo "🚀 Starting Diten ERP Multi-Service Suite..."

# 1. Kill old processes
echo "🧹 Cleaning up ports 5000, 5001, 5056, 5057, 5058, 5059..."
lsof -ti :5000,5001,5056,5057,5058,5059 | xargs kill -9 2>/dev/null || true
echo "🧹 Cleaning up ports 4010, 5066 (Prism product-master mock + Inventory)..."
lsof -ti :4010,5066 | xargs kill -9 2>/dev/null || true
sleep 1

# 2. Build everything
echo "🛠️ Building projects..."
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
dotnet build services/Diten.AuthService/src/Diten.AuthService.Api/Diten.AuthService.Api.csproj -c Debug
dotnet build services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/Diten.DevEnablementService.Api.csproj -c Debug
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet build services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj -c Debug
dotnet build services/Diten.InventoryService/src/Diten.InventoryService.Api/Diten.InventoryService.Api.csproj -c Debug
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug

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
    do script "cd '$PWD/$DIR' && dotnet run --no-build --urls http://localhost:$PORT"
end tell
EOF

    if [ $? -ne 0 ]; then
        echo "⚠️ Could not open new terminal tab for $NAME, running in background..."
        cd "$PWD/$DIR" && dotnet run --no-build --urls "http://localhost:$PORT" > "/tmp/diten_$NAME.log" 2>&1 &
        cd - > /dev/null
    fi
}

launch_service "Auth" "services/Diten.AuthService/src/Diten.AuthService.Api" 5056
launch_service "DevEnablement" "services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api" 5058
launch_service "Platform" "services/Diten.Platform/src/Diten.Platform.API" 5057
launch_service "Mdm" "services/Diten.MdmService/src/Diten.MdmService.Api" 5059
# FU-PM-01: MDM PRODUCT-MASTER gerçek uç noktası gelene kadar geçici — Prism product-master mock (4010)
PRISM_JS=""
for c in "${LOCALAPPDATA//\\//}"/npm-cache/_npx/*/node_modules/@stoplight/prism-cli/dist/index.js "$HOME"/.npm/_npx/*/node_modules/@stoplight/prism-cli/dist/index.js; do
    [ -f "$c" ] && PRISM_JS="$c" && break
done
PM_SPEC=$(find docs -name 'product-master-bundle.openapi.yaml' -print -quit 2>/dev/null)
if [ -n "$PRISM_JS" ] && [ -n "$PM_SPEC" ]; then
    echo "Starting PrismProductMaster on port 4010..."
    /usr/bin/osascript <<EOF
tell application "Terminal"
    do script "cd '$PWD' && node --max-http-header-size=65536 '$PRISM_JS' mock '$PM_SPEC' --port 4010 --host 127.0.0.1"
end tell
EOF
    if [ $? -ne 0 ]; then
        echo "⚠️ Could not open new terminal tab for PrismProductMaster, running in background..."
        node --max-http-header-size=65536 "$PRISM_JS" mock "$PM_SPEC" --port 4010 --host 127.0.0.1 > "/tmp/diten_PrismProductMaster.log" 2>&1 &
    fi
else
    echo "⚠️ Prism product-master mock (4010) atlandı: prism-cli önbellekte ya da product-master-bundle.openapi.yaml bulunamadı (önbelleğe almak için: npx --yes @stoplight/prism-cli@4.10.5 --version)"
fi
echo "Starting Inventory on port 5066..."
/usr/bin/osascript <<EOF
tell application "Terminal"
    do script "cd '$PWD/services/Diten.InventoryService/src/Diten.InventoryService.Api' && Inventory__Upstream__ProductMasterBaseUrl=http://127.0.0.1:4010/ dotnet run --no-build --launch-profile http --urls http://localhost:5066"
end tell
EOF
if [ $? -ne 0 ]; then
    echo "⚠️ Could not open new terminal tab for Inventory, running in background..."
    (cd "$PWD/services/Diten.InventoryService/src/Diten.InventoryService.Api" && Inventory__Upstream__ProductMasterBaseUrl=http://127.0.0.1:4010/ dotnet run --no-build --launch-profile http --urls "http://localhost:5066" > "/tmp/diten_Inventory.log" 2>&1 &)
fi
launch_service "Gateway" "gateway/Diten.ApiGateway" 5000
launch_service "Web" "frontend/Diten.Web" 5001

echo "✨ Services are launching."
echo "🔗 Access: http://localhost:5001/platform/login"
