# Q236 — Startup and HTTP, Development, proposed bytes

Measured in the scratch copy (`services/Diten.SupplyChainService` + `services/Diten.Building.Blocks`, copied
without `bin`/`obj`), run A of `SABOTAGE-PROOF.md`. Run C repeated it with the same results.

## How it was started

Working directory: the copy's `src/Diten.SupplyChainService.Api` (so the unchanged `appsettings.json` loads;
it sets `Urls = http://127.0.0.1:5061`). Port 5061 was free. No file was edited to make it start.

```text
ASPNETCORE_ENVIRONMENT=Development   DOTNET_ENVIRONMENT=Development
Mongo__ConnectionString=mongodb://127.0.0.1:31994/?replicaSet=rsq208s1&serverSelectionTimeoutMS=5000
Mongo__DatabaseName=diten_q236_unbreak
JwtSettings__Secret=<64 random hex characters from openssl, generated in the shell, never printed or stored>
JwtSettings__Issuer=q236-unbreak     JwtSettings__Audience=q236-unbreak
dotnet bin/Debug/net8.0/Diten.SupplyChainService.Api.dll
```

Lane replica set: `rsq208s1` on `127.0.0.1:31994`, PRIMARY (checked before the run). 27017 was not used.

## Startup log (run A)

```text
[00:16:09 WRN] Shipment outbox transport is not registered. Durable events remain pending; integration is held.
[00:16:09 INF] Now listening on: http://127.0.0.1:5061
[00:16:09 INF] Application started. Press Ctrl+C to shut down.
[00:16:09 INF] Hosting environment: Development
…
[00:16:39 INF] Application is shutting down...
```

`Unable to resolve service`: 0 occurrences. Up for 30 s under observation, then stopped with SIGTERM; exit 0;
port 5061 free afterwards. The one warning is the known missing event transport (not part of this WP).

The hosted schema services that are composed did run: the database holds 16 collections — 8 `sce_*` (Shipments)
and 8 Carrier/Load collections. None for Claims, Returns, Capacity or S&OP (their schema services are not
registered; unchanged by this WP).

## Unauthenticated calls

No token was sent. `X-Correlation-Id` sent where marked: `5344bb91-e760-42d3-97d0-2de8fdd8f084`.

| # | Request | Correlation sent | Status | `X-Correlation-Id` in response | Body |
|---:|---|---|---:|---|---|
| 1 | GET `/health` | no | 200 | — | `{"status":"up","module":"MOD-0183","warehouseIntake":"unimplemented","outboxTransport":"integration-owned"}` |
| 2 | **(a)** GET `/api/shipment-bundle/shipments` | no | 400 | — (a generated id is in the body only) | `{"error":{"code":"INVALID_REQUEST","message":"A non-empty UUID X-Correlation-Id is required.","correlationId":"<generated>"},"contractVersion":"v1"}` |
| 3 | **(a)** GET `/api/shipment-bundle/shipments` | yes | 401 | echoed | `{"error":{"code":"INVALID_REQUEST","message":"Authentication required.","correlationId":"<sent>"},"contractVersion":"v1"}` |
| 4 | **(b)** GET `/api/shipment-bundle/claims` | no | 400 | — | same body as #2 |
| 5 | **(b)** GET `/api/shipment-bundle/claims` | yes | 401 | echoed | same body as #3 |
| 6 | GET `/api/shipment-bundle/carriers` | yes | 401 | echoed | `…"message":"Authentication required."…`; `WWW-Authenticate: Bearer` |
| 7 | GET `/api/shipment-bundle/loads` | yes | 401 | echoed | `…"message":"Load request could not be completed."…`; `WWW-Authenticate: Bearer` |
| 8 | GET `/api/shipment-bundle/returns` | yes | 401 | echoed | same body as #3 |
| 9 | GET `/api/supply-chain/capacity-plans/{uuid}` | yes | 401 | — | empty; `WWW-Authenticate: Bearer` |
| 10 | GET `/api/supply-chain/sandop-plans/{uuid}` | yes | 401 | — | empty; `WWW-Authenticate: Bearer` |

Raw headers and bodies: `evidence/http-A-fixed-*.headers` / `.body` (and `http-C-restored-*`).

### (a) The wired path — Shipments

Answered by the Shipment middleware with the Shipment contract body, as designed: 400 without a correlation
header, 401 with one.

### (b) The Claims path — excluded from handler registration

**The Shipment middleware still answers it, with the Shipment contract body** — exactly what Q217 measured.
Rows 4 and 5 are byte-identical to rows 2 and 3 apart from the generated id. Returns (row 8) behaves the same.

This WP does not change that, and could not: the request never reaches a controller or a handler without a
token. The Claims path is in the Shipment middleware branch because `Program.cs:68` excludes only Carrier and
Load paths. That is Q209's file.

Capacity and S&OP paths (rows 9, 10) are outside `/api/shipment-bundle`, so the Shipment middleware passes them
through and the plain `[Authorize]` challenge answers: 401, empty body, no correlation header.

## Not measured

- Any authenticated call. No token was minted.
- What an authenticated call to an excluded module returns. Before this change its handler could not be
  constructed; after it, no handler is registered. Either way it cannot succeed; the exact status was not observed.
- Production. Not needed for this WP; Q217 and Q232 covered it for the unchanged and four-line states.
