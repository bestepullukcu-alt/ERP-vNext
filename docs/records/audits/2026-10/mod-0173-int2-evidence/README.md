# WP-0173-INT2 evidence — gateway route family `/api/locations/**` → Diten.InventoryService (5066), 2026-10-02

Change: `gateway/Diten.ApiGateway/ocelot.json` — 2 routes added right after the INT-1 `/api/inventory` blocks (commit
`6594b3c2d`), nothing else in `gateway/**` touched. Raw outputs; tokens masked by the probe script (a scan for `eyJ…`
and for the minted secret finds nothing); the JWT secret was minted per run and passed only through process
environment variables.

| File | What it shows |
|---|---|
| `d1-gateway-build.log` | `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug` → 0 errors, 0 warnings |
| `d1-json-parse.log` | `ocelot.json` parses; routes 287 → 289; the two new routes (upstream = downstream, 5066, GET/POST/PATCH/OPTIONS, no AuthenticationOptions); routes outside the port guard's known set: 5066 2 → 4, others unchanged |
| `d1b-gateway-tests.log` | gateway tests 83 / 84 — the one red test is the known `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` (FU-INT1-01, not fixed here); INT-2 adds 2 violations to it (5066) |
| `d2-ocelot.diff`, `d2-summary.log` | `git diff de976722b -- ocelot.json`: one hunk, +36 / −0, exactly the two route blocks; CRLF and no-final-newline kept |
| `run-int2-probe.ps1` | the D3 probe — a copy of INT-1's `run-int1-probe.ps1` (helpers unchanged), see notes 1–2 |
| `d3-steps.json` | every request through the gateway (Authorization masked) and the gateway's answer: status, `X-Correlation-Id`, `ETag`, body |
| `d3-checks.json` | **16 / 16** checks (a–f below) |
| `d3-gateway-copy-override.json` | the probe gateway's `ocelot.json`: SHA-256 identical to the committed file, then ONLY the two `/api/locations` `"Port"` lines changed 5066 → 5067 (note 1) |
| `d3-ports-before.json`, `d3-ports-after.json`, `d3-processes.json` | listeners before / after; the 3 started processes, none remaining; the foreign Prism on 4010 listed and untouched; scratch gateway copy removed |
| `gateway.log`, `service.log`, `prism-product-master.log`, `d3-build.log` | process output of the run (Release builds 0 / 0) |

D3 checks: a) create a Site → 201 + `ETag W/"1"`, same key → 200 same id · b) GET → 200 + `ETag W/"1"`; PATCH with
`If-Match W/"1"` → 200 + `ETag W/"2"`; stale `W/"1"` → 409 `LOCATION_CONFLICT`; missing → 400 · c) bulk dry run under a
StorageLocation → 200 preview of 8 codes, 3 locations before and after · d) path → 200 Site › Warehouse ›
StorageLocation; history → 200 `Updated, Created`; `/search` → 200 (literal segment) · e) the caller's
`X-Correlation-Id` UUID comes back on the response and is persisted on the history row · f) no token → 401 from the
service (contract Error body); `PUT` and `DELETE /api/locations/{id}` → 404 at the gateway (Ocelot
`UnableToFindDownstreamRouteError` in `gateway.log`).

## Notes

1. **5066 was taken by the user's own stack.** During the run the user had their Gateway (5000), InventoryService
   (5066, from `bin\Debug`), Auth / Platform / MDM and a Prism PRODUCT-MASTER (4010) running — none started, stopped
   or called by this probe. The probe therefore ran its own InventoryService on **5067** (Prism on 4012, gateway on
   5090), built in **Release** so the user's locked `bin\Debug` files were not rebuilt. The gateway ran from a scratch
   copy of its Release output whose `ocelot.json` was proven byte-identical to the committed file and then had ONLY
   the two new routes' `"Port"` rewritten to 5067 (`d3-gateway-copy-override.json`); the copy was deleted afterwards.
   Path templates, methods, the absence of AuthenticationOptions and header handling — what INT-2 adds — are exactly
   the committed ones. ASSUMPTION-INT2-01.
2. Probe changes against INT-1's copy: Prism serves PRODUCT-MASTER only (the LOCATION master is in-process since
   LOC1; its HTTP client and the org-unit client point at a closed port and are never called); permissions
   `inventory.locations.read` / `.manage`; each step also records the response `ETag`; the remaining-process check is
   limited to the processes the probe started (it lists, never touches, others of the same kind).
