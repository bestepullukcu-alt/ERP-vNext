#!/usr/bin/env bash
# Evidence-only redacted command view. Executed helper: start-api.sh.
DOTNET_ROLL_FORWARD=Major \
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:51862 \
Mongo__ConnectionString='mongodb://127.0.0.1:27187/?replicaSet=returns_r01_rework&directConnection=true' \
Mongo__DatabaseName=returns_r01_rework_db \
JwtSettings__Secret='<synthetic-test-secret; see exact local helper>' \
JwtSettings__Issuer=returns-http-issuer \
JwtSettings__Audience=returns-http-audience \
Returns__ReferenceBaseUrl=http://127.0.0.1:51862/ \
dotnet '<source>/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Release/net8.0/Diten.SupplyChainService.Api.dll'
