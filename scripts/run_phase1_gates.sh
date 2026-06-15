#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

echo "[phase1] validate event schemas"
./scripts/validate_event_schemas.sh

echo "[phase1] cross-db enforcement"
./scripts/check_cross_db_enforcement.sh

echo "[phase1] build gateway"
dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug

echo "[phase1] build auth"
dotnet build services/Diten.AuthService/Diten.AuthService.sln -c Debug

echo "[phase1] build platform"
dotnet build services/Diten.Platform/Diten.Platform.sln -c Debug

if [ -f "services/Diten.MdmService/Diten.MdmService.sln" ]; then
  echo "[phase1] build mdm"
  dotnet build services/Diten.MdmService/Diten.MdmService.sln -c Debug
else
  echo "[phase1] skip mdm: services/Diten.MdmService/Diten.MdmService.sln not found"
fi

echo "[phase1] test tenancy"
dotnet test tests/tenancy/TenantArchitecture.TenancyTests/TenantArchitecture.TenancyTests.csproj -c Debug

echo "[phase1] test architecture"
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj -c Debug

echo "[phase1] gates passed"
