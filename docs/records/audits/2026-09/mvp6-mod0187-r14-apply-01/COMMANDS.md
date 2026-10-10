# Reproduction command classes

Commands were run from the isolated snapshot unless stated otherwise. Runtime
secrets were supplied through ephemeral environment variables and are omitted.

| Step | Command class | Exit/result |
|---|---|---|
| Baseline hash | `shasum -a 256 Program.cs` | authorized baseline MATCH |
| Patch validation | `git apply --check Program.cs.patch` | 0 |
| Patch apply | `git apply Program.cs.patch` | 0 |
| Target hash | `shasum -a 256 Program.cs` | authorized target MATCH |
| API baseline build | `dotnet build ...Api.csproj -c Release --no-restore --no-incremental` | 0; 0 warnings/errors |
| API target build | same fresh compile command | 0; 0 warnings/errors |
| Test assembly build | `dotnet build ...Tests.csproj -c Release --no-restore --no-incremental` | 0; 0 warnings/errors |
| Baseline transport | `python3 scripts/raw_utf8_probe.py --phase baseline --port 51874 ...` | measurement complete |
| Target transport | `python3 scripts/raw_utf8_probe.py --phase target --port 51875 ...` | 0; PASS |
| Same-host regression | filtered `dotnet test --no-build --no-restore` for Claims/Returns/Loads/Carriers | 0; 78/78 PASS |
| Shipment regression | filtered `dotnet test --no-build --no-restore` | 0; 12/12 PASS |
| Listener cleanup | `lsof` for 51874, 51875 and 27314 | no listeners |

The raw launch files contain redacted configuration structure. The complete
build/test outputs, exit files and process identity are under `raw/`.
