# Reproducible command record

Secrets are represented by placeholders. All Mongo databases and ports were isolated.

```bash
git worktree add --detach /private/tmp/mvp6-mod0192-http-integration-01 4a8d4d4b339528a88e6220fb8402e5a2c771136c
tar -xzf <MOD0190-379-source.tar.gz> -C /private/tmp/mvp6-mod0192-http-integration-01
tar -xzf <CAPACITY-43-SOURCE.tar.gz> -C /private/tmp/mvp6-mod0192-http-integration-01
git apply --check PROGRAM-COMPOSITION.patch
git apply PROGRAM-COMPOSITION.patch

mongod --config /private/tmp/mvp6-mod0192-http-dev-mongo.conf
mongosh --quiet --port 57192 --eval 'rs.initiate({_id:"rsmod192",members:[{_id:0,host:"127.0.0.1:57192"}]})'

DOTNET_ROOT=/Users/natig/.dotnet /Users/natig/.dotnet/dotnet test \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  -c Debug --no-restore --filter FullyQualifiedName~CapacityPlans \
  --logger 'trx;LogFileName=capacity-dev-final.trx' -m:1 /nr:false -p:UseSharedCompilation=false

DOTNET_ROOT=/Users/natig/.dotnet ASPNETCORE_ENVIRONMENT=Testing \
ASPNETCORE_URLS=http://127.0.0.1:56192 \
Mongo__ConnectionString='<isolated-rs-connection>' Mongo__DatabaseName='<isolated-db>' \
JwtSettings__Secret='<redacted>' JwtSettings__Issuer=mod0192-evidence JwtSettings__Audience=mod0192-api \
/Users/natig/.dotnet/dotnet services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll

CAPACITY_BASE=http://127.0.0.1:56192 CAPACITY_JWT_SECRET='<redacted>' \
python3 http_probe.py
python3 restart_probe.py
```

The two-host run started the same binary against the same isolated `HttpRace` DB on ports 56192 and 56193. All processes were stopped after evidence capture.
