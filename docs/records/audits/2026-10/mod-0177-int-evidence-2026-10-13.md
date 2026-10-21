# MOD-0177 INT-RECALL — evidence (WP-0177-INT, CT-MVP1-0177-INT v1) — 2026-10-13

> **Status: agent result, pending CT.** Work branch `feature/int/mod-0177-recall-routes` from `32267f884` (the MVP-1 tip, R1 + FIX-F4R1
> CT-accepted). Isolated worktree `C:\Users\Ali\erp-vnext-wt-0177-int`. No push, no PR, nothing on `main` or the MVP-1 branch; no
> `origin/main` sync (WP rule). The owner's gateway (5000) was not restarted or called; the main checkout was not edited.

## 1. Pattern (measured) — WP-0176-INT, commit `91d89847e`

- `/api/expiry` + `/api/expiry/{everything}` → localhost:5067, GET / POST / PUT / OPTIONS, no AuthenticationOptions / header transform /
  rate limit (`gateway/Diten.ApiGateway/ocelot.json:1026-1061` at base), placed after `/api/v1/esign/{everything}` (D-INT-1: the INT-QE order
  test pins the four blocks after trace).
- Test `gateway/Diten.ApiGateway.Tests/MvpOneExpiryRouteTests.cs` (contract parse + Ocelot-like matching helpers) — copied as the recall test.
- Contract measured: `docs/analysis/contracts/recall.openapi.yaml` 1.0.0 (`:14`), server `/api/recall`, **50 operations**: 22 GET, 26 POST,
  2 PUT (`updateSettings`, `updateDrillPlan`), no PATCH / DELETE → methods **GET, POST, PUT + OPTIONS** — exactly the INT-EXPIRY set, so the
  pattern needs no different setting (no BLOCKED condition). No existing route matches a recall path (no catch-all; the prefix test shows only
  the two new templates claim `/api/recall`).

## 2. Change

`gateway/Diten.ApiGateway/ocelot.json` — one hunk `@@ -1061,0 +1062,36 @@` (36 lines added, none removed), right after
`/api/expiry/{everything}`:

| Block | Upstream template (line) | Downstream | Methods |
|---|---|---|---|
| 1 | `/api/recall` (`:1071`) | `http://localhost:5067/api/recall` (`:1063`) | GET, POST, PUT, OPTIONS |
| 2 | `/api/recall/{everything}` (`:1089`) | `http://localhost:5067/api/recall/{everything}` (`:1081`) | GET, POST, PUT, OPTIONS |

New test `gateway/Diten.ApiGateway.Tests/MvpOneRecallRouteTests.cs` (30 test cases). No other block changed or moved, no port changed, no
service code, no contract file.

## 3. Matching table — path × method → block

All 50 contract operations (`Every_path_and_method_of_recall_openapi_is_served_by_exactly_the_recall_route`): each matches exactly one route,
`/api/recall/{everything}` → 5067, and that route carries the operation's method.

| Contract path (server `/api/recall`) | Methods | Block |
|---|---|---|
| `/dashboard`, `/trace`, `/trace/export` | GET | `{everything}` |
| `/cases` | GET, POST | `{everything}` |
| `/cases/{caseId}`, `/cases/{caseId}/scope`, `/scope/diff`, `/stock`, `/customers`, `/returns`, `/effectiveness`, `/timeline` | GET | `{everything}` |
| `/cases/{caseId}/scope/items`, `/scope/marks`, `/scope/submit`, `/scope/return`, `/pending-action`, `/scope/approve`, `/scope/revisions`, `/cancel`, `/quarantine-requests`, `/quarantine-requests/{requestId}/retry`, `/segregations`, `/notifications`, `/consumption-declarations`, `/return-links`, `/effectiveness/start`, `/effectiveness/complete`, `/close` | POST | `{everything}` |
| `/lots/{lotId}/status` (getLotRecallStatus — 0174 badge, 0176 / 0178) | GET | `{everything}` (own test `The_lot_recall_status_path_reaches_InventoryService`) |
| `/drill-plans` | GET, POST | `{everything}` |
| `/drill-plans/{planId}` | PUT | `{everything}` |
| `/drill-runs` | GET, POST | `{everything}` |
| `/drill-runs/{runId}` | GET | `{everything}` |
| `/drill-runs/{runId}/steps/{step}`, `/pending-result`, `/result`, `/cancel` | POST | `{everything}` |
| `/evidence/preview`, `/evidence/exports/{exportId}/file` | GET | `{everything}` |
| `/evidence/exports` | GET, POST | `{everything}` |
| `/settings` | GET, PUT | `{everything}` |
| `/settings/history` | GET | `{everything}` |
| `/settings/pending-change` | POST | `{everything}` |
| `/esign-subjects/{subjectType}/{subjectId}` (R4 Platform provider) | GET | `{everything}` (own theory, 3 subject types) |
| bare `/api/recall` | — (no contract operation) | block 1 |

Not matched: DELETE / PATCH on any recall path (`Methods_outside_the_contract_are_matched_by_no_route`); `/api/recallX`, `/api/recallX/settings`,
`/api/recall-old/cases`, `/api/recallcases` never reach a recall block. Neighbours unchanged (`Neighbouring_paths_still_reach_their_own_route_only`):
`/api/expiry`, `/api/expiry/{everything}`, `/api/quarantine/{everything}`, `/api/trace/{everything}`, `/api/inventory/{everything}` → 5067,
`/api/v1/esign/{everything}` → 5057, `/api/bom/{everything}` → 5070. The blocks equal the `/api/expiry` blocks with only the path changed and
follow them (`The_new_blocks_are_the_expiry_blocks_with_only_the_path_changed_and_follow_them`).

## 4. Tests — before / after

| Suite | Before (`32267f884`) | After |
|---|---|---|
| `Diten.ApiGateway.Tests` | **134 / 135** — red: `OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` (`/api/suppliers` → 5065, from main) | **164 / 165** — the same single red, same message; `MvpOneRecallRouteTests` 30 / 30 |

Build: no new warning (the CS0618 / CS8604 warnings listed are in existing files; the new test suppresses CS0618 like the expiry test).

## 5. Sabotage (K3)

Each: edit `ocelot.json` → `dotnet test` (whole gateway project) → restore the saved copy → re-run. After the three: `ocelot.json` SHA-256
identical to before (`7a6af71f…`).

| # | Sabotage | New reds (besides the known port red) | Restored |
|---|---|---|---|
| (a) | delete the `/api/recall/{everything}` block | **15** — the contract coverage test, prefix test, block-shape test, route test (`{everything}`), 7 named operations, 3 e-sign subject paths, lot-status path | 164 / 165 |
| (b) | port of `/api/recall/{everything}` → 5070 | **7** — contract coverage, route test (`{everything}`), block-shape, 3 e-sign subject paths, lot-status path | 164 / 165 |
| (c) | `PUT` removed from `/api/recall/{everything}` | **5** — contract coverage (updateSettings / updateDrillPlan), route test, block-shape, named `PUT /settings`, named `PUT /drill-plans/{id}` | 164 / 165 |

## 6. Runtime probe — own gateway copy on 5100

The worktree's gateway build copied to the session scratchpad with the new `ocelot.json` (SHA-256 `7A6AF71F69B0E958…D1D452`),
`ASPNETCORE_ENVIRONMENT=Testing`, `--urls` / `ASPNETCORE_URLS` / `Kestrel__Endpoints__Http__Url` = `http://127.0.0.1:5100` (port checked free); a
random JWT secret for start-up only; **no token signed or sent**; a random `X-Tenant-Id` (`81a148d4-…`, no data anywhere) for the gateway's
tenant middleware. Only unauthenticated GETs reached the owner's 5067; nothing was written.

| Request (no token) | Status | Ocelot log |
|---|---|---|
| `GET /api/recall/settings` | **401** | "401 Unauthorized status code of request URI: http://localhost:5067/api/recall/settings" |
| `GET /api/recall/cases` | **401** | "… http://localhost:5067/api/recall/cases" |
| `GET /api/recall/lots/{id}/status` | **401** | "… http://localhost:5067/api/recall/lots/0bbcde21-…/status" |
| `GET /api/recallX` | **404** | "UnableToFindDownstreamRouteError: Failed to match route configuration for upstream: GET /api/recallX" |
| `GET /api/recallX/settings` | **404** | same, no route |
| `GET /api/expiry/policy` (neighbour) | 401 | "… http://localhost:5067/api/expiry/policy" |

Own gateway process (PID 44972) stopped by its own PID.

## 7. `git diff --stat 32267f884..HEAD`

`gateway/Diten.ApiGateway/ocelot.json` (+36), `gateway/Diten.ApiGateway.Tests/MvpOneRecallRouteTests.cs` (new), this evidence, the MOD-0177
pack §19.11 note. Nothing else.

## 8. Open points

None. The known port red (FU-INT1-01) was not touched.
