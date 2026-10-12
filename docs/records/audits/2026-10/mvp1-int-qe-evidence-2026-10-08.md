# MVP-1 INT-QE — gateway routes for MOD-0175 Quarantine + MOD-0022 e-signature (WP-MVP1-INT-QE, CT-MVP1-INT-QE v2)

| field | value |
|---|---|
| Modules | MOD-0175 INT-QUARANTINE (pack §1 row INT-QUARANTINE) + MOD-0022 INT-ESIGN (pack §1 row INT-ESIGN) |
| Branch / base | `feature/sce/mvp1-int-qe` from `720a58ede` (clean) · isolated worktree `C:\Users\Ali\erp-vnext-wt-int-qe` · no push, no PR, no merge |
| Commits | `ae2055739` routes + tests · this evidence + the two pack §19 notes |
| Status | **agent result — pending CT** |

## 1. Change — `gateway/Diten.ApiGateway/ocelot.json`, 4 blocks, +74 lines, 0 removed

Inserted right after the `/api/trace/{everything}` block, each a clone of the INT-TRACE block with only path, port and method
list changed (no `AuthenticationOptions`, no header transform, no rate limit — the service authenticates):

| upstream = downstream | target | methods |
|---|---|---|
| `/api/quarantine` | localhost:**5066** (Diten.InventoryService, AGENTS.md §3) | GET, POST, PATCH, PUT, OPTIONS |
| `/api/quarantine/{everything}` | localhost:**5066** | GET, POST, PATCH, PUT, OPTIONS |
| `/api/v1/esign` | localhost:**5057** (Diten.Platform, AGENTS.md §3) | GET, POST, PUT, OPTIONS |
| `/api/v1/esign/{everything}` | localhost:**5057** | GET, POST, PUT, OPTIONS |

`git diff 720a58ede..HEAD -- gateway/Diten.ApiGateway/ocelot.json`: one hunk at line 909, 74 `+` lines, **no `-` line**; JSON parses;
291 → 295 routes, the 291 existing routes unchanged in content and order (the hunk only inserts). Line endings: index LF /
working copy CRLF, as before.

### 1.1 Method list — measured from the contracts

| contract | operations | methods | DELETE |
|---|---|---|---|
| `quarantine.openapi.yaml` (`servers: /api/quarantine`) | 25 | GET 11 · POST 11 · PUT 2 · PATCH 1 | none |
| `esign.openapi.yaml` (`servers: /`), the 34 `/api/v1/esign/*` operations (of 38) | 34 | GET 15 · POST 16 · PUT 3 | none |
| `esign.openapi.yaml`, other paths | 2 + 2 | `POST /api/tenant-auth/esign/{reauth,ticket}` (existing `/api/tenant-auth/{everything}` → 5056, GET/POST/PUT/OPTIONS/DELETE — no new route); `POST /internal/esign-tickets/consume`, `POST /api/internal/esign/actions/register` (internal — **no route**, measured 404 in §3) | — |

### 1.2 Collision check

No route template can take the new prefixes: no `/{…}`, `/api/{…}`, `/api/v1/{…}` or `/api/internal…` template exists (291 templates
scanned); no existing template starts with `/api/quarantine` or `/api/v1/esign`; `/api/v1/workflow[/{everything}]` is a different
literal prefix. The tests measure it the other way too: every contract operation matches **exactly one** route.

### 1.3 Request body limit (e-signature PNG)

Gateway `Program.cs:38-41` only raises `MaxRequestHeadersTotalSize` (64 KB); `MaxRequestBodySize` is not set → Kestrel default
**30 000 000 bytes**. The largest contract body is `pngBase64` "decodes to ≤ 1 048 576 bytes" (+ JSON). Measured at runtime (§3 B):
1 MiB PNG as base64 = **1 398 223-byte** body → forwarded byte-identical (sha256 equal); 2 × that (2 796 223 bytes) → forwarded
byte-identical; a 31 000 000-byte body → **413** at the gateway. Headroom ≈ 21×; the limit is sufficient — no change.

## 2. Tests

| suite | before (`720a58ede`) | after (`ae2055739`) |
|---|---|---|
| Diten.ApiGateway.Tests | 88 / 89 (1 known red) | **101 / 102** (1 known red; +13 new, all green) |
| tests/architecture (TenantArchitecture, incl. DocsPathGuard) | — | **18/18** |

New `MvpOneQuarantineEsignRouteTests` (13 cases): each new route exactly once, downstream = upstream, `localhost`, port, exact
method set, no DELETE, no auth / header transform / rate limit (4); only the two new routes claim each prefix (2); the blocks are
the trace blocks with only path / port / methods changed and follow `/api/trace/{everything}` in order (1); every path + method
of `quarantine.openapi.yaml` is matched by exactly the quarantine catch-all (1); every `/api/v1/esign/*` operation by exactly the
esign catch-all, the two `/api/tenant-auth/esign/*` by exactly `/api/tenant-auth/{everything}` (5056), the two internal ones by
**no** route (1); neighbours `/api/v1/workflow[/…]`, `/api/trace/lots`, `/api/inventory/movements` still match only their own route
(4). Contracts are found by name under `docs/` (DocsPathGuard), parsed with `ReadAllLines` (CRLF-safe).

**Known red `EveryRoute_DownstreamPortIsInKnownServiceSet` (FU-INT1-01), not fixed:** violations 128 → **130**; the two added
lines are exactly `/api/quarantine` and `/api/quarantine/{everything}` (port 5066); nothing removed. The esign blocks add none
(5057 is in `KnownDownstreamPorts`).

## 3. Runtime proof (own ports; `run-probe.ps1` / `probe.mjs` / `echo.js`, scratchpad)

- **Gateway A** 127.0.0.1:15290 — worktree Release build with the repo `ocelot.json` (bin copy, byte-equal to the source).
- **Gateway B** 127.0.0.1:15291 — a TEMP copy of `ocelot.json` in the scratchpad where only the four new blocks point at own echo
  stubs (quarantine → 15366, esign → 15357); every other route unchanged; the repo file was only read.
- `--urls` + `ASPNETCORE_URLS` + `Kestrel__Endpoints__Http__Url`; random JWT secret in memory; no Authorization token was minted.

**The owner's fleet was UP during the run** (it had been restarted; not started or stopped by this WP). Gateway A therefore
forwarded to the owner's live 5066 / 5057 / 5056: every request carried no token, every answer was the service's refusal
(401 / 404 / 405) — nothing was read or written there. The gateway log names each downstream URI:

| request (gateway A) | gateway log | status |
|---|---|---|
| GET `/api/quarantine/holds`, PUT `/api/quarantine/settings` | `… request URI: http://localhost:5066/api/quarantine/…` | 401 (InventoryService) |
| GET `/api/quarantine` | `http://localhost:5066/api/quarantine` | 404 (service: the contract has no root operation) |
| PATCH `/api/quarantine/holds/x` | `http://localhost:5066/api/quarantine/holds/x` | 405 (service: hold PATCH is contracted, built in Q4) |
| GET `/api/v1/esign/policy`, POST `/api/v1/esign/samples`, PUT `/api/v1/esign/meanings/APPROVE` | `http://localhost:5057/api/v1/esign/…` | 401 (Platform) |
| POST `/api/tenant-auth/esign/ticket` | `http://localhost:5056/api/tenant-auth/esign/ticket` (existing route) | 401 (AuthService) |
| GET `/api/v1/workflow/tasks` (neighbour) | `http://localhost:5057/api/v1/workflow/tasks` | 401 |
| DELETE `/api/quarantine/holds/x` | `UnableToFindDownstreamRouteError` (gateway) | 404 |
| POST `/internal/esign-tickets/consume` | `UnableToFindDownstreamRouteError` | **404** — not exposed |
| POST `/api/internal/esign/actions/register` | `UnableToFindDownstreamRouteError` | **404** — not exposed |

Gateway B (echo downstream) — header pass-through and body:

| request | downstream saw | response |
|---|---|---|
| POST `/api/v1/esign/samples`, 1 398 223 B (1 MiB PNG base64) | 15357, same path, 1 398 223 B, sha256 equal; `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id`, `Idempotency-Key`, `If-Match`, `Content-Type` unchanged | 200, `ETag W/"7"` and `X-Correlation-Id` passed back |
| POST `/api/v1/esign/signatures`, 2 796 223 B | 15357, bytes + sha256 equal, same headers | 200 |
| PUT `/api/quarantine/settings`, PATCH `/api/quarantine/holds/abc` | 15366, bytes equal, same headers | 200, ETag + correlation back |
| GET `/api/quarantine/holds?status=Open&page=2` | 15366, query string intact | 200 |
| POST 31 000 000 B | — | **413** (Kestrel default limit) |

Processes: gateway A pid 39488, gateway B pid 34388, echo stubs 21964 / 33456 — all stopped; no listener left on 15290 / 15291 /
15366 / 15357. Owner PIDs before = after: `4010=31728 5000=32248 5001=36416 5056=10764 5057=5592 5058=22808 5059=35252
5060=35456 5066=27736 27017=4904`.

## 4. K3 sabotages (`sabotage.sh`: edit ocelot.json → gateway tests → `git checkout` → porcelain → same run)

| # | sabotage | result | restored |
|---|---|---|---|
| port | `/api/v1/esign/{everything}` → port 5058 | **3 red** (new route test, trace-clone test, + the known red) — 99/102 | 101/102 (known red only), porcelain empty |
| narrow | `/api/quarantine/{everything}` upstream → `/api/quarantine/holds/{everything}` | **5 red** incl. "every quarantine contract operation served" and "only the two routes claim the prefix" — 97/102 | 101/102, porcelain empty |

## 5. Notes for CT

- MOD-0022 pack §1 INT-ESIGN names only `/api/v1/esign/{everything}`; the WP asked for the root block too (trace pattern) — both
  are in. The root paths `/api/quarantine` and `/api/v1/esign` have no contract operation (service answers 404).
- `POST /api/internal/esign/actions/register` (e-signature contract, internal module registration) is also off the gateway —
  measured 404, like `/internal/esign-tickets/consume`.
- FU-INT1-01 (`KnownDownstreamPorts` lacks 5064 / 5065 / 5066) unchanged by instruction; this WP adds exactly 2 lines to its list.
