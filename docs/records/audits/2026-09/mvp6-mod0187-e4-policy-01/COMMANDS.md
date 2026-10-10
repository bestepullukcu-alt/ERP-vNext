# Redacted execution ledger

| Stage | Command / boundary | Exit |
|---|---|---|
| Source | Copy AUTH-05 source snapshot, verify `source-manifest.tsv` | 97/97 match before and after |
| Restore | `dotnet restore services/Diten.SupplyChainService/Diten.SupplyChainService.sln --disable-parallel -v minimal` | 0 |
| Build | `dotnet build ...sln -c Debug --no-restore -m:1 /nr:false` | 0 |
| Mongo | `mongod --config .../mongo/mongod.conf` (`27931`, `claims_e4_policy`) | writable primary; later shut down |
| Controlled dependency | `reference_fixture.py --port 18087 ...` | stopped after policy matrix |
| Fixture API | exact API DLL; `5072`; Claims reference base `18087`; test DB only | started, probed, stopped |
| Policy matrix | `policy_probe.py` with redacted JWT secret | 1 because two asserted product failures; 172 results persisted |
| Refusal | fixture stopped; `refusal_probe.py` against API 5072 | 0 |
| Real producer API | same DLL; `5071`; Claims reference base self | started, probed, stopped |
| Producer uptake | `producer_uptake_probe.py` | 1 because real nil-root path failed |
| Header controls | `header_boundary_probe.py` | 0 for ASCII/duplicate/missing controls |
| Error controls | `error_matrix_probe.py` with Mongo failpoint/test fixture | 0 |
| Policy supplement | `policy_supplement.py` | 0 |
| Cleanup | stop APIs/fixture; `db.shutdownServer({force:true})`; remove ephemeral secret | ports 5071/5072/18087/27931 closed |

The JWT signing secret and bearer tokens are absent from the archive. Full stdout/stderr, request/response bodies, DB snapshots and process/listener records are in `evidence.tar.gz`.
