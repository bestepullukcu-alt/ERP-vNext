# Executed controls

```text
/Users/natig/.dotnet/dotnet build services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj -c Debug --no-restore -m:1 /nr:false

/Users/natig/.dotnet/dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj -c Debug --no-build --no-restore --filter FullyQualifiedName~CapacityPlans

python3 /private/tmp/mvp6_bc_exec02_http_probe.py
```

MongoDB ran only on `127.0.0.1:57192` as replica set `rsmod192` with
`enableTestCommands=1`. The API ran only on `127.0.0.1:56932`. Secrets and
bearer tokens are intentionally omitted.

