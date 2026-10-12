#!/usr/bin/env bash
set -euo pipefail

SOURCE_ROOT=/private/tmp/mvp6-mod0186-r01-rework-01
EVIDENCE_ROOT=/private/tmp/mvp6-mod0186-r01-rework-evidence-01
API_DLL="$SOURCE_ROOT/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Release/net8.0/Diten.SupplyChainService.Api.dll"

export DOTNET_ROLL_FORWARD=Major
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://127.0.0.1:51862
export Mongo__ConnectionString='mongodb://127.0.0.1:27187/?replicaSet=returns_r01_rework&directConnection=true'
export Mongo__DatabaseName=returns_r01_rework_db
export JwtSettings__Secret=runtime-secret-for-mod0186-http-lane-01-20260921
export JwtSettings__Issuer=returns-http-issuer
export JwtSettings__Audience=returns-http-audience
export Returns__ReferenceBaseUrl=http://127.0.0.1:51862/

exec dotnet "$API_DLL" >> "$EVIDENCE_ROOT/raw/api-process.log" 2>&1
