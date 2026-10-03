# Protected for both consumer writers

The exact existing source closure is listed in each lane input TSV. All existing source paths outside that lane's released allowlist remain read-only; input availability is not write ownership. All repository paths not explicitly released to that lane are protected (default deny).

- Other lane's entire owned-path set in owned-paths.tsv.
- services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs — single integration owner only after separate lease.
- Existing Shipment, Carrier, Loads, SourceIntake files; all shared Domain/Common, serializer, repository, DI, csproj and Building.Blocks inputs listed in the source closure.
- docs/analysis/contracts/**, publication/guard/decision files, tests/architecture/**, .antigravity/**, gateway/**, module packs and registries.
- Other lane evidence directories; consumer writers cannot edit root producer source or accepted evidence.

Only this audit directory is writable by the present inspection. No runtime writer is dispatched.
