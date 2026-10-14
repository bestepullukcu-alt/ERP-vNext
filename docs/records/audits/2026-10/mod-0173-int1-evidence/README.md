# WP-0173-INT1 evidence — gateway route family `/api/inventory/**` → Diten.InventoryService (5066), 2026-10-01

Change: `gateway/Diten.ApiGateway/ocelot.json` — 2 routes added after the procurement 5065 block (commit `5f97a4b5`),
nothing else in `gateway/**` touched. Raw outputs; tokens masked by the probe script; the JWT secret was minted per
run and passed only through process environment variables.

| File | What it shows |
|---|---|
| `d1-gateway-build.log` | `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` → 0 errors, 0 warnings |
| `d1-json-parse.log` | `ocelot.json` parses; 287 routes (285 + 2) |
| `d1b-gateway-tests.log` | existing gateway tests: 83/84 — the one red test is `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` |
| `d1c-port-guard-baseline.log` | that guard was ALREADY red at base `dc2b6dd0` (122 routes on 5063 / 5064 / 5065 outside its `KnownDownstreamPorts`); INT-1 adds 2 (5066) |
| `d2-ocelot.diff`, `d2-summary.log` | `git diff dc2b6dd0 -- ocelot.json`: +34 / −0, exactly the two route blocks |
| `run-int1-probe.ps1` | the D3 probe (Prism ×2 + InventoryService :5066 + gateway on temporary :5090; the user's gateway on :5000 untouched) |
| `d3-steps.json` | every request (Authorization masked) and the gateway's response: status, `X-Correlation-Id`, body |
| `d3-checks.json` | 12/12 checks |
| `d3-persisted.json` | Mongo documents of the probe tenant: transaction / audit / outbox carry the caller's correlation UUID; reservation documents carry the gateway-minted id |
| `gateway.log`, `service.log`, `prism-*.log` | process output of the run |
| `d3-processes.json` | the 4 started processes and the empty list of processes remaining after stop |
| `d3-build.log` | builds run by the probe |
