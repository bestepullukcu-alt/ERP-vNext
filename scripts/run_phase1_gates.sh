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

# Menü adlarının 7 dilde var olduğunu manifest kaynaklarından türeterek doğrulayan guard buradadır
# (NavManifestL10nGuardTests). Hatta koşmazsa hiçbir şeyi korumaz: bir modülün Nav.Module/Nav.Page
# anahtarı unutulduğunda menü sessizce HAM İNGİLİZCE basar — build geçer, testler yeşil kalır, kusur
# yalnız ekrana bakınca görülür. 2026-08-10'da bu üç kez yaşandı (Edit · Recurring Task Rules ·
# "Görevler / Tasks"), üçü de gözle bulundu. Guard yazıldı ama hat onu koşmuyordu; bu satır o boşluğu
# kapatıyor. Proje ayrıca token bridge ve ekran sözleşmesi testlerini de taşır.
echo "[phase1] test web (nav l10n guard + web contracts)"
dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Debug

echo "[phase1] gates passed"
