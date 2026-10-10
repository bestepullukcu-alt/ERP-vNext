#!/usr/bin/env bash
set -euo pipefail

DOTNET_ROLL_FORWARD=Major \
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:51863 \
Mongo__ConnectionString='mongodb://127.0.0.1:27286/?replicaSet=returns_r01_ind_ver&directConnection=true' \
Mongo__DatabaseName='returns_r01_ind_ver_db' \
JwtSettings__Secret='<runtime-only; not archived>' \
JwtSettings__Issuer='returns-http-issuer' \
JwtSettings__Audience='returns-http-audience' \
Returns__ReferenceBaseUrl='http://127.0.0.1:51863/' \
dotnet '<source>/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Release/net8.0/Diten.SupplyChainService.Api.dll'
