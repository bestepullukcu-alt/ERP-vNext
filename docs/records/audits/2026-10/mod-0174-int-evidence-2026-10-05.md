# WP-0174-INT evidence — gateway routes `/api/trace` → InventoryService 5066, 2026-10-05

> WP-0174-INT / CT-MVP1-0174-INT v1 · branch `feature/sce/mod-0174-lot-serial-tracking` · base `5b4264d1d` · worked in the
> isolated worktree `C:\Users\Ali\erp-vnext-wt-0174-int` (no fast-forward; the owner's checkout and fleet were not touched).
> Agent self-audit — **agent PASS ≠ CT ACCEPTED**. Live end-to-end through the owner's gateway is **not** claimed here; the
> CT measures it.

## 1. Commits

| Commit | Message |
|---|---|
| `e4719214d` | feat(gateway): WP-0174-INT — /api/trace and /api/trace/{everything} → InventoryService 5066 (INT-TRACE) |
| (this commit) | docs(sce): WP-0174-INT evidence + MOD-0174 pack §19 INT note |

## 2. `ocelot.json` diff (only the two blocks)

Inserted directly after `/api/locations/{everything}` (INT-2), before `/api/personalization/views`:

```diff
@@ -873,6 +873,42 @@
       ],
       "UpstreamScheme": "http"
     },
+    {
+      "DownstreamPathTemplate": "/api/trace",
+      "DownstreamScheme": "http",
+      "DownstreamHostAndPorts": [
+        {
+          "Host": "localhost",
+          "Port": 5066
+        }
+      ],
+      "UpstreamPathTemplate": "/api/trace",
+      "UpstreamHttpMethod": [
+        "GET",
+        "POST",
+        "PATCH",
+        "OPTIONS"
+      ],
+      "UpstreamScheme": "http"
+    },
+    {
+      "DownstreamPathTemplate": "/api/trace/{everything}",
+      ... identical block with "/api/trace/{everything}" ...
+    },
```

| measurement | result |
|---|---|
| numstat | `36 0` — additions only |
| JSON valid | yes (parsed by node and by Ocelot `FileConfiguration` in the tests) |
| route count | 289 → 291 |
| existing routes | identical content, identical order (node deep-compare of the 289 old routes: `true`) |
| line endings | CRLF throughout (5558/5558 lines) |
| conflict check (before the edit) | no upstream template containing `trace`; no catch-all (`/{x}`, `/api/{x}`) → no route conflict |
| methods | GET, POST, PATCH, OPTIONS — no DELETE / PUT (the TRACE contract has no delete) |
| gateway auth / headers / rate limit | none — no `AuthenticationOptions`, no header transforms, no rate limiting (INT-1/INT-2 pattern) |

## 3. Gateway tests — before / after

`dotnet test gateway/Diten.ApiGateway.Tests` (Release), same worktree:

| | total | passed | failed |
|---|---|---|---|
| before (`5b4264d1d`) | 84 | 83 | 1 |
| after (`e4719214d`) | 89 | 88 | 1 |

- New: `Mod0174TraceRouteTests` — 5 tests, all green:
  - theory × 2 (`/api/trace`, `/api/trace/{everything}`): downstream = upstream template, `http`, single target
    `localhost:5066`, methods exactly {GET, POST, PATCH, OPTIONS}, no DELETE/PUT, no gateway authentication provider,
    no header transforms, rate limiting off;
  - `Only_these_two_routes_claim_the_trace_path`;
  - `The_trace_blocks_are_the_locations_blocks_with_only_the_path_changed` (JSON string equality with the INT-2 blocks);
  - `Every_contract_path_and_method_of_trace_openapi_is_served` (parses `trace.openapi.yaml`, ≥16 operations, every
    method is in the `{everything}` route).
- Red count unchanged (1). The one red is the known pre-existing
  `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` (its `KnownDownstreamPorts` lacks 5063–5066;
  FU-INT1-01 — **not** fixed in this WP, as instructed). Its violation list grew 126 → 128 lines, by exactly the two
  new routes:

```
120a121,122
> /api/trace/{everything}: unrecognized downstream port 5066 (host localhost)
> /api/trace: unrecognized downstream port 5066 (host localhost)
```

  No existing test was loosened or edited; no other test changed state.

## 4. K3 sabotage — the new test is not a tautology

`/api/trace/{everything}` port 5066 → 5067 in the built `ocelot.json`:

```
sha256 before e744b1b0d3c3f1dc627c5eb3e71675e7606f20f71527a89d1510f6745adbcfcf
--- sabotage diff:
900c900
<           "Port": 5066
---
>           "Port": 5067
--- RED expected:
Failed ...The_trace_route_goes_to_InventoryService_5066_with_the_contract_methods_and_no_gateway_auth(template: "/api/trace/{everything}")
Failed ...The_trace_blocks_are_the_locations_blocks_with_only_the_path_changed
Failed: 2, Passed: 3, Total: 5
sha256 restored e744b1b0d3c3f1dc627c5eb3e71675e7606f20f71527a89d1510f6745adbcfcf
--- GREEN expected:
Passed! Failed: 0, Passed: 5, Total: 5
 gateway/Diten.ApiGateway/ocelot.json | 36 ++++++++++++++++++++++++++++++++++++
```

## 5. Routing proof on an own port

The worktree's gateway build ran as its own process on `127.0.0.1:5190` (`--urls`; the owner's 5000 was never bound),
environment Development. Three anonymous GETs — no token minted or sent — then the process was stopped.

```json
{ "gatewayPid": 412, "boundTo": "127.0.0.1:5190", "stopped": true, "listenerLeftOn5190": false,
  "fleetBefore": ["5000=30904", "5066=18400"], "fleetAfter": ["5000=30904", "5066=18400"] }
```

| request (via 5190) | status | body | gateway log for the request id |
|---|---|---|---|
| `GET /api/trace/lots` | 404 | empty | `404 Not Found status code of request URI: http://localhost:5066/api/trace/lots` |
| `GET /api/tracex/lots` (control, no route) | 404 | empty | `UnableToFindDownstreamRouteError: Failed to match route configuration for upstream: GET /api/tracex/lots` |
| `GET /api/inventory/balance` (control, INT-1 route) | 401 | `{"error":{"message":"A valid bearer token is required.",...},"contractVersion":"v1"}` | `401 Unauthorized status code of request URI: http://localhost:5066/api/inventory/balance` |

Reading:

- `/api/trace/lots` matched the new route and was **forwarded to `localhost:5066`**. The 404 is InventoryService's own
  answer, logged by Ocelot as a downstream status. It is **not** Ocelot's route-not-found: that error appears only for
  the control `/api/tracex/lots`.
- Why a 404 and not a 401: the owner's 5066 runs the **0173 branch**, which has no `/api/trace` endpoint (P1 is on the
  0174 branch only). An unknown path does not reach the bearer challenge, so the 404 comes from the service. Once the
  0174 branch runs in the fleet, the same request is expected to return the TRACE 401 / 200. That live step is the CT's.
- The two 404s look identical to the client (both empty bodies). The forwarding proof is therefore the gateway log,
  correlated by `X-Correlation-Id` / RequestId (`f5b8905c…` vs `6377995f…`).
- Each request also logs `TenantResolution dev bypass applied in gateway` (environment Development, existing behaviour,
  unchanged by this WP).

## 6. Owner fleet — PIDs unchanged

| port | before | after |
|---|---|---|
| 5000 (owner's gateway) | 30904 | 30904 |
| 5066 (owner's InventoryService) | 18400 | 18400 |

The probe gateway (PID 412) was stopped; no listener was left on 5190. The owner's 5066 received only the three
read-only GETs above. The owner's checkout `C:\Users\Ali\erp-vnext` was not edited.

## 7. Probe script (scratchpad, not committed)

`run-gateway-probe.ps1`:

- refuses to run if 5190 is in use;
- records the 5000/5066 PIDs;
- starts `dotnet Diten.ApiGateway.dll --urls http://127.0.0.1:5190` from the worktree's `bin/Release/net8.0` with
  `ASPNETCORE_ENVIRONMENT=Development`, stdout/stderr to `gateway.log` / `gateway.err.log`;
- waits for the listener and records the bound endpoints of the probe PID;
- sends the three GETs with `System.Net.Http.HttpClient` (PS 5.1 needs `Add-Type -AssemblyName System.Net.Http`; the
  first run failed on this before any request, the gateway was stopped in `finally`, and 5190 was verified free before
  the re-run);
- stops the process in `finally` and records the PIDs again.

## 8. Scope

`git diff --stat 5b4264d1d..HEAD`, recorded in the final report, lists only:

- `gateway/Diten.ApiGateway/ocelot.json`
- `gateway/Diten.ApiGateway.Tests/Mod0174TraceRouteTests.cs`
- this file
- `execution/domains/supply-chain-execution/module-packs/MOD-0174-lot-serial-tracking.md` (§19 INT note)

Not done, by instruction: `KnownDownstreamPorts` (FU-INT1-01); any reordering or editing of existing routes; push;
fast-forward.
