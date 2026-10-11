# Reproduction commands (secrets redacted)

The raw archive contains complete stdout/stderr, exit codes, TRX, process metadata and HTTP/DB records.

```text
sha256sum R21-claims-owned.patch
git apply --check R21-claims-owned.patch
git apply R21-claims-owned.patch
dotnet build .../Diten.SupplyChainService.Api.csproj -c Debug -t:Rebuild -m:1 /nr:false
dotnet test .../Diten.SupplyChainService.Tests.csproj -c Debug --filter FullyQualifiedName~ClaimReferenceTests
dotnet test .../Diten.SupplyChainService.Tests.csproj -c Debug --filter 'FullyQualifiedName~Diten.SupplyChainService.Tests.Claims.&FullyQualifiedName!~Receipt_TwoIndependentTestProcesses_DurableRecovery'
python3 runtime_probe.py
```

Runtime topology:

- GREEN API `127.0.0.1:5081`, forward-only capture `127.0.0.1:5181`, replica set `127.0.0.1:27941`.
- RED API `127.0.0.1:5082`, forward-only capture `127.0.0.1:5182`, replica set `127.0.0.1:27942`.
- Claims test replica set `127.0.0.1:27943`.
- `DOTNET_ROLL_FORWARD=Major` was required because the host has .NET 10 and the project targets net8. The initial missing setting and the initial inconsistent-worktree build remain preserved as failed evidence.
