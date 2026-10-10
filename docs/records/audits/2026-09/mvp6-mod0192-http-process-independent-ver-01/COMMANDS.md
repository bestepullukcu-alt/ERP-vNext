# Reproducible command record

Secrets below are placeholders. The exact immutable source archive and manifest are in the DEV handoff.

```bash
tar -xzf SOURCE-422.tar.gz -C <unique-verifier-source>
(cd <unique-verifier-source> && sha256sum -c <manifest-as-sha256>)

mongod --config <verifier-mongod.conf>
mongosh --quiet --port 57192 --eval 'rs.initiate({_id:"rsmod192",members:[{_id:0,host:"127.0.0.1:57192"}]})'

DOTNET_ROOT=/Users/natig/.dotnet /Users/natig/.dotnet/dotnet restore \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  --disable-parallel -v minimal

DOTNET_ROOT=/Users/natig/.dotnet /Users/natig/.dotnet/dotnet build \
  services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj \
  -c Debug --no-restore --disable-build-servers -m:1 /nr:false -p:UseSharedCompilation=false

DOTNET_ROOT=/Users/natig/.dotnet /Users/natig/.dotnet/dotnet test \
  services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj \
  -c Debug --no-restore --disable-build-servers \
  --filter FullyQualifiedName~CapacityPlans --logger 'trx;LogFileName=<verifier>/raw/capacity-ver.trx' \
  -m:1 /nr:false -p:UseSharedCompilation=false

DOTNET_ROOT=/Users/natig/.dotnet ASPNETCORE_ENVIRONMENT=Testing \
ASPNETCORE_URLS=http://127.0.0.1:56292 \
Mongo__ConnectionString='mongodb://127.0.0.1:57192/?replicaSet=rsmod192&serverSelectionTimeoutMS=5000' \
Mongo__DatabaseName='<isolated-db>' JwtSettings__Secret='<redacted>' \
JwtSettings__Issuer=mod0192-evidence JwtSettings__Audience=mod0192-api \
/Users/natig/.dotnet/dotnet services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll

CAPACITY_BASE=http://127.0.0.1:56292 CAPACITY_JWT_SECRET='<redacted>' python3 http_probe.py
CAPACITY_BASE=http://127.0.0.1:56292 CAPACITY_JWT_SECRET='<redacted>' \
CAPACITY_PLAN_ID='<plan>' CAPACITY_SCENARIO_ID='<scenario>' CAPACITY_EVALUATION_ID='<evaluation>' \
python3 restart_probe.py
```

For the two-host run, the same binary and isolated race database were used on ports 56292 and 56293. No gateway was involved.

