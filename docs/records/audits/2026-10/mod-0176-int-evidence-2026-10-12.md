# MOD-0176 INT-EXPIRY — evidence (WP-0176-INT, CT-MVP1-0176-INT v1) — 2026-10-12

> **Status: agent result, pending CT.** Work branch `feature/int/mod-0176-expiry-routes` opened from `80b417c58` (F1 CT-ACCEPTED),
> isolated worktree `C:\Users\Ali\erp-vnext-wt-0176-int`. Commit `91d89847e` (gateway) + this evidence / pack §19.9 note. No push, no PR,
> nothing on `main` or the MVP-1 branch; the owner's Gateway 5000 and checkout untouched.

## 1. Pattern measured (INT-QE `ae2055739`, port move `2fdedc988`)

- `ae2055739`: four blocks inserted at `ocelot.json` hunk `@@ -909,6 +909,80 @@` right after `/api/trace/{everything}`, cloned from the
  trace blocks — only path, port, method list changed; no `AuthenticationOptions`, no header transform, no rate limit; test
  `MvpOneQuarantineEsignRouteTests.cs` (183 lines): shape, prefix ownership, block-equality + **fixed order** (`templates.Skip(after + 1).Take(4)`
  = quarantine, quarantine/{everything}, esign, esign/{everything}), contract coverage, neighbours.
- `2fdedc988`: InventoryService routes 5066 → 5067 (OD-P1); `/api/quarantine` blocks now `ocelot.json:913-948` → 5067.

## 2. Change

`gateway/Diten.ApiGateway/ocelot.json` — two blocks, upstream templates at `:995` (`/api/expiry`) and `:1013` (`/api/expiry/{everything}`),
directly after `/api/v1/esign/{everything}` (`:977`). Each = the `/api/quarantine[/{everything}]` block with only the path and the method list
changed: `localhost:5067`, `GET, POST, PUT, OPTIONS` (the contract has exactly GET / POST / PUT — measured from `fefo.openapi.yaml`).

**D-INT-1 — placement.** The WP said "immediately after `/api/quarantine/{everything}`". Measured with the blocks there: the existing
`MvpOneQuarantineEsignRouteTests.The_new_blocks_are_the_trace_blocks_with_only_path_port_and_methods_changed_and_follow_them` turns **red**
(it pins the four blocks after trace: quarantine × 2, esign × 2 — "Collections differ, pos 2"); that file is outside the allowed paths. The
blocks therefore close the INT-QE group (after `/api/v1/esign/{everything}`): no other block changed or moved, no existing test touched, and the
match result is identical (Ocelot matches by template, not by order; no other template can match `/api/expiry/**` — test
`Only_the_two_new_routes_claim_the_prefix`). Not a BLOCKED condition (no route collision, no setting differs from INT-QE).

## 3. Match table — every path × method of `fefo.openapi.yaml` (servers `/api/expiry`)

| Path | Method | Block | Port | Slice |
|---|---|---|---|---|
| `/api/expiry/dashboard` | GET | `/api/expiry/{everything}` | 5067 | F4 |
| `/api/expiry/lots/approaching` | GET | `/api/expiry/{everything}` | 5067 | F4 |
| `/api/expiry/lots/expired` | GET | `/api/expiry/{everything}` | 5067 | F4 |
| `/api/expiry/fefo-order` | GET | `/api/expiry/{everything}` | 5067 | F4 |
| `/api/expiry/exceptions` | GET, POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}` | GET | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}/take` | POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}/comments` | POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}/pending-resolution` | POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}/resolve` | POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/exceptions/{exceptionId}/close` | POST | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/policy` | GET, **PUT** | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/policy/history` | GET | `/api/expiry/{everything}` | 5067 | F1 |
| `/api/expiry/evidence/preview` | GET | `/api/expiry/{everything}` | 5067 | F5 |
| `/api/expiry/evidence/exports` | GET, POST | `/api/expiry/{everything}` | 5067 | F5 |
| `/api/expiry/evidence/exports/{exportId}/file` | GET | `/api/expiry/{everything}` | 5067 | F5 |
| `/api/expiry/date-overrides` | POST | `/api/expiry/{everything}` | 5067 | F2/F3 |
| `/api/expiry/esign-subjects/{subjectType}/{subjectId}` | GET | `/api/expiry/{everything}` | 5067 | F3 (Platform provider) |
| `/api/expiry` | GET, POST, PUT, OPTIONS | `/api/expiry` | 5067 | (bare base, no operation) |

21 operations, each matched by **exactly one** route; DELETE / PATCH by none; `/api/expiryX`, `/api/expiryX/policy`, `/api/expiry-old/policy`,
`/api/expirypolicy` by neither expiry block. Neighbours: `/api/quarantine[/holds]` → quarantine blocks (5067), `/api/trace/lots`,
`/api/inventory/movements` (5067), `/api/v1/esign/policy` (5057), `/api/v1/workflow/instances` → their own route only.

## 4. Tests — before / after

| | Before (`80b417c58`) | After (`91d89847e`) |
|---|---|---|
| `gateway/Diten.ApiGateway.Tests` | **110 / 111** | **134 / 135** (+24 `MvpOneExpiryRouteTests`) |
| Red | `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` (5063 / 5064 / 5065, FU-INT1-01) | the same test, failure message **identical** (no expiry route in it; 5067 is a known port) |
| New warnings in the new file | — | 0 (the obsolete-key check wrapped like the intent; CS0618 / CS8604 warnings of the existing files unchanged) |

## 5. Sabotage (K3) — red, restored green, `git diff` empty

| # | Sabotage | Tests | Sabotaged | Restored | diff |
|---|---|---|---|---|---|
| a | delete the `/api/expiry/{everything}` block (18 lines) | `MvpOneExpiryRouteTests` | **10 failed / 24** | 24 / 24 | empty |
| b | port 5067 → 5066 on both expiry blocks | `MvpOneExpiryRouteTests` | **6 failed / 24** | 24 / 24 | empty |
| c | remove `PUT` from both expiry blocks | `Named_operations_reach_the_expiry_route_with_their_method` | **1 failed / 4** (`updatePolicy` PUT) | 4 / 4 | empty |

Runner (`sabotage.sh`): `perl` edit of `ocelot.json` → `dotnet test --filter` → `git checkout -- ocelot.json` → `git diff --quiet` → rerun.

## 6. Runtime probe — own gateway copy on 5100

The worktree's gateway build copied to a scratch folder with the new `ocelot.json` (SHA-256 `6F922039…D18D4`), `ASPNETCORE_ENVIRONMENT=Testing`,
`--urls` + `ASPNETCORE_URLS` + `Kestrel__Endpoints__Http__Url` = `http://127.0.0.1:5100`; a random JWT secret for start-up only; **no token
signed or sent**. First run without a tenant header: every path 400 "Missing Tenant" from the gateway's `TenantResolutionMiddleware` (tenant
endpoints need `X-Tenant-Id` or a token claim — measured `Middleware/TenantResolutionMiddleware.cs:205-218`). Second run with a random
`X-Tenant-Id` (`d7d05d0f-…`, no data anywhere):

| Request (no token) | Status | Evidence |
|---|---|---|
| `GET /api/expiry/policy` | **401**, `WWW-Authenticate: Bearer` | Ocelot log: "401 Unauthorized status code of request URI: http://localhost:5067/api/expiry/policy" — the owner's InventoryService answered |
| `GET /api/expiry/exceptions` | **401** | "…of request URI: http://localhost:5067/api/expiry/exceptions" |
| `GET /api/expiryX` | **404** | "UnableToFindDownstreamRouteError: Failed to match route configuration for upstream: GET /api/expiryX" |
| `GET /api/expiryX/policy` | **404** | same, no route |
| `GET /api/quarantine/settings` (neighbour) | 401 | "…http://localhost:5067/api/quarantine/settings" |

Own gateway process (PID 14432; first run 27880) stopped by its own PID. Only unauthenticated GETs reached 5067; nothing was written.

## 7. `git diff --stat 80b417c58..HEAD`

`gateway/Diten.ApiGateway/ocelot.json` (+36), `gateway/Diten.ApiGateway.Tests/MvpOneExpiryRouteTests.cs` (new), this evidence, MOD-0176 pack
§19.9 — nothing else.

## 8. Open for CT

- **D-INT-1** placement after `/api/v1/esign/{everything}` instead of after `/api/quarantine/{everything}` (§2) — accept, or order a follow-up that
  lets the INT-QE order test admit the expiry blocks.
