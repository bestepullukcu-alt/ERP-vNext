# MVP-1 SEC-PROXY1 — evidence (WP-MVP1-SEC-PROXY1, CT-MVP1-SEC-PROXY1 v2) — 2026-10-14

> **Status: agent result, pending CT.** Base measured: `9362c0402` (as the prompt expects). Branch `fix/mvp1-sec-proxy1`, isolated worktree
> `C:\Users\Ali\erp-vnext-wt-secproxy`; the BEFORE side of the probe ran from a throw-away detached worktree `C:\Users\Ali\erp-vnext-wt-secproxy-base`
> at `9362c0402`. No push, no PR, nothing on `main` or the MVP-1 branch; the main checkout and the owner's fleet (5000–5070, 4010) untouched;
> `AccountKindAcceptanceHost.Tests` not run; no token was signed. Scripts: `mvp1-sec-proxy1-scripts-2026-10-14.md`.
> Location (docs-organization, five questions): a dated, finished measurement record of one WP → Q2 → `docs/records/audits/2026-10/`.

## 1. Commits

| # | Commit | Step |
|---|---|---|
| 1 | `717092d0b` | `fix(web)` — helper `frontend/Diten.Web/Services/Proxy/ProxyPath.cs` + the path line(s) of the 6 proxies |
| 2 | `655103dca` | `test(web)` — `frontend/Diten.Web.Tests/Security/**` (boundary matrix, legitimate paths, helper units, architecture guard) + one line in `ESignatureWebTests` (§7 D-3) |
| 3 | (this commit) | evidence + scripts + pack §19 notes (MOD-0176, MOD-0175, MOD-0022) |

## 2. The finding, reproduced

`path = $"{ApiBase}/{everything.TrimStart('/')}"` → `HttpClient` → `new Uri(...)`: Kestrel decodes `%5C` to `\` in the route value, `Uri` turns `\`
into `/` and collapses dot segments. CT: `/Expiry/api/..%5Clocations?type=Warehouse` → 200 with InventoryService `/api/locations`. Reproduced for
**all six** proxies end-to-end (§5 BEFORE): each one's `..%5C` reached `/api/locations` behind the gateway.

## 3. The helper — `ProxyPath.TryBuild(prefix, everything, out path[, out reason])`

On refusal the proxy returns **400** `{"error":{"code":"INVALID_PROXY_PATH","message":…,"details":{"reason":…}}}` and **no request goes to the
gateway** (the check runs before `SendAsync` in every proxy). The reason is in the body so the tests (and the sabotages) see which rule fired.

| Rule (order) | Reason |
|---|---|
| raw starts with `//` | `scheme-or-host` |
| percent-decode repeatedly until stable, **max 5 rounds** (`MaxDecodeRounds`); still changing after 5 | `too-deeply-encoded` |
| every form (most-decoded first, raw last): control char / NUL | `control-character` |
| … any `\` | `backslash` |
| … `//` at start, `://`, `@`, or `:` in the first segment | `scheme-or-host` |
| … a `.` or `..` segment | `dot-segment` |
| `%2F` / `%5C` / `%2E` (any case) in any form | `encoded-separator` |
| an empty segment inside the path (`a//b`) | `empty-segment` |
| any char outside `A-Z a-z 0-9 - _ ~ . /` in any form (Unicode look-alikes, `%`, `;`, `:`, space, `?`, `#`) | `disallowed-character` |
| last check: `new Uri(gateway, prefix + "/" + rest).AbsolutePath` starts with `prefix + "/"` (or equals the prefix) | `escapes-prefix` |

Empty / whitespace rest = the prefix itself (as before; Tenants keeps its trailing `/`). One leading `/` and one trailing `/` are kept as before.
The query string is not part of the rest: every proxy still forwards `Request.QueryString` unchanged.

Wiring — one entry point per proxy (numstat, product files only):

| Controller | Entry point | Prefix | +/− |
|---|---|---|---|
| `ESignatureController` | `ProxyApi` (GET/POST/PUT) | `/api/v1/esign` | 3 / 1 |
| `ExpiryController` | `ProxyAsync` ← `ProxyRead/Write/Replace` | `/api/expiry` | 1 / 1 |
| `QuarantineController` | `ProxyAsync` ← `ProxyRead/Write` | `/api/quarantine` | 1 / 1 |
| `ReferenceDataController` | `ProxyReferenceDataAsync` ← `ProxyApi` (after the existing read-key check) | `/api/v1/reference-data` | 1 / 3 |
| `WorkflowController` | `ProxyWorkflowAsync` ← `ProxyApi` | `/api/v1/workflow` | 1 / 3 |
| `TenantsController` | `LookupsProxy` (GET) | `/api/lookups` | 2 / 1 |

Permission model untouched: ReferenceData keeps its `Platform.BusinessReferenceData.Read` check, Tenants its platform-actor check, the others
leave authorization to the service. Order note: in ESignature / ReferenceData / Workflow the path check runs after the token + tenant check (as the
old path line did); in Expiry / Quarantine / Tenants before it — in both cases before anything is sent.

## 4. Tests — Web before / after

| Suite | Base `9362c0402` | After |
|---|---|---|
| `Diten.Web.Tests` | **1468 / 1468** | **2199 / 2199** (1468 + 731 new) |
| `TenantArchitecture.ArchitectureTests` | 69 / 77 (8 red) | 69 / 77 — **the same 8** (DocsPathGuardTests ×5, MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest, AuditTrailStandardTests ×2; none touches this WP) |
| `dotnet build Diten.Web.csproj --no-incremental` | 0 errors, 18 warnings | 0 errors, **18 warnings** (the same pre-existing CRM / WorkCenter ones; none in the new files) |

New tests (`frontend/Diten.Web.Tests/Security/`, own files — FIX-WEBGATES owns `Expiry/`):

| Test | Cases | What |
|---|---|---|
| `ProxyBoundaryTests.An_attack_path_is_refused_400_and_never_reaches_the_gateway` | **528** = 11 (proxy × verb: 5 proxies × GET/POST + Tenants GET) × 48 inputs | the 19 CT handoff traversals + 29 more: `..%5Clocations`, `..\locations`, `..%255Clocations`, `%2E%2E` encoded 2 / 3 / 5 / 6 layers, `exceptions/../policy`, `exceptions/./policy`, `.`, `..`, `．．` (U+FF0E), `∕` (U+2215), `＼` (U+FF3C), `%EF%BC%8E…`, overlong `%C0%AE`, `%u002e`, NUL / TAB / CRLF, `%2Flocations`, `a//b`, `C:/windows`, `https:%2F%2F…`, `user@…`, `?`, `#`, space, `%20` → 400 `INVALID_PROXY_PATH`, FakeGateway **0** requests; for 20 inputs the **reason** is asserted too |
| `ProxyBoundaryTests.A_legitimate_path_is_forwarded_under_the_prefix_with_the_query_unchanged` | **154** | §6 list → exactly one gateway request, `AbsolutePath == prefix + "/" + rest`, query `?set_code=uom&activeOnly=true&q=a%2Fb` unchanged, 200 |
| `ProxyBoundaryTests` (facts) | 4 | matrix covers all 6 + the CT escape; the legitimate catalog is read from the sources and is not empty per proxy; empty rest = prefix; the colon fixture routes are a known refusal (§7 D-1) |
| `ProxyPathTests` | 43 | helper units: plain paths built, empty rest, 29 refusals each with its reason, the decode bound (1–5 layers → `dot-segment`, 6 → `too-deeply-encoded`), every accepted input stays under the prefix after `Uri`, the 400 body |
| `ProxyPathArchitectureGuardTests` | 2 | reflection over Diten.Web: every action whose route template has `{**x}` must hand that parameter to `ProxyPath.TryBuild` (in the action or the one private method it passes it to) and must not build `$"…/{x…}"`; the guard must find the 9 wired actions; known exceptions listed by name (§7 D-2) |

The CT handoff file (`ExpiryProxyBoundaryTests`, 77 cases, 44 red at base) is FIX-WEBGATES' to land; its 19 traversals are the first block of
the matrix above (all six proxies, not only Expiry / Quarantine).

## 5. E2E probe — real HTTP through Kestrel (scripts §1–§3)

Own ports only: **Web 127.0.0.1:5104** (the production `Program` via `WebApplicationFactory<Program>` + a second host on **real Kestrel**),
**gateway copy 127.0.0.1:5105** (the built `Diten.ApiGateway.dll`, content root = a scratch folder with **copied** `appsettings*.json` and
`ocelot.json`, every `DownstreamHostAndPorts` rewritten to 5106 — 306 routes, nothing else changed), **fake downstream 127.0.0.1:5106**
(node, records every request target). Identity: a test-host `IStartupFilter` puts a principal (tenantId, sub, legal_entity_id, actor_type,
the ReferenceData read key) on `HttpContext.User` when the request carries `X-Probe-Actor`; `ShellAccessFilter` keeps an authenticated principal
with `actor_type`. The `access_token` cookie is the opaque string `probe-opaque-token` — **no JWT signed**; the gateway copy cannot validate it
(its routes carry no `AuthenticationOptions`), so it forwards on the `X-Tenant-Id` the Web sends. Requests are written as raw HTTP/1.1 on a socket
(no client normalizes the path). All three processes stopped afterwards; 5104–5106 not listening.

| Proxy | Request to Web :5104 | BEFORE `9362c0402`: Web → downstream received | AFTER `fix/mvp1-sec-proxy1`: Web → downstream received |
|---|---|---|---|
| Expiry | `/Expiry/api/..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| Expiry | `/Expiry/api/..%255Clocations?type=Warehouse` | 200 → `/api/expiry/..%5Clocations?type=Warehouse` | **400** `backslash` → nothing |
| Expiry | `/Expiry/api/%2E%2E%5Clocations` | 200 → **`/api/locations`** | **400** `backslash` → nothing |
| Expiry | `/Expiry/api/..%2Flocations` | 200 → `/api/expiry/..%2Flocations` | **400** `dot-segment` → nothing |
| Expiry | `/Expiry/api/exceptions?status=OPEN` | 200 → `/api/expiry/exceptions?status=OPEN` | 200 → `/api/expiry/exceptions?status=OPEN` |
| Quarantine | `/Quarantine/api/..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| Quarantine | `/Quarantine/api/holds?page=1` | 200 → `/api/quarantine/holds?page=1` | 200 → `/api/quarantine/holds?page=1` |
| ESignature | `/ESignature/api/..%5C..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| ESignature | `/ESignature/api/meanings?activeOnly=true` | 200 → `/api/v1/esign/meanings?activeOnly=true` | 200 → `/api/v1/esign/meanings?activeOnly=true` |
| ReferenceData | `/Platform/ReferenceData/api/..%5C..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| ReferenceData | `/Platform/ReferenceData/api/usage-registrations?set_code=uom` | 200 → `/api/v1/reference-data/usage-registrations?set_code=uom` | 200 → same |
| Workflow | `/Platform/Workflow/api/..%5C..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| Workflow | `/Platform/Workflow/api/definitions` | 200 → `/api/v1/workflow/definitions` | 200 → same |
| Tenants | `/Platform/Tenants/api/lookups/..%5Clocations?type=Warehouse` | 200 → **`/api/locations?type=Warehouse`** | **400** `backslash` → nothing |
| Tenants | `/Platform/Tenants/api/lookups/countries` | 200 → `/api/lookups/countries` | 200 → same |

BEFORE: 8 rows escaped into another service (bold), 15 / 15 answered 200. AFTER: the downstream log holds exactly the **6** legitimate requests
(5 with the caller's `X-Tenant-Id`, lookups without — a non-tenant path at the gateway). Raw output: scripts §4.

## 6. Legitimate path list (what must keep passing)

Read by the tests from the production sources (template parameters → a GUID for ids, `APPROVE` for codes, `pack-applicability` for set codes,
`countries` for lookup keys, `LOT` for subject types), deduplicated: **154** (proxy, verb, rest).

| Proxy | Source | Count |
|---|---|---|
| Expiry | `docs/analysis/contracts/fefo.openapi.yaml` (servers `/api/expiry`, every path × verb) + JS calls `date-overrides`, `evidence/exports`, `exceptions`, `policy`, `/exceptions` (leading slash) | 23 |
| Quarantine | `quarantine.openapi.yaml` (servers `/api/quarantine`) + JS `lots/{lotId}/quality-status` | 25 |
| ESignature | `esign.openapi.yaml` keys under `/api/v1/esign/` (the `/api/tenant-auth/…` and `/internal/…` keys are not behind this proxy) | 34 |
| ReferenceData | Platform `BusinessReferenceDataController` `[Route]` + `[HttpX]` (31 minus the 2 colon fixture routes) + JS `usage-registrations/bulk`, `sets/{code}/hierarchy`, `sets/uom.v2/values` (dot inside a segment) | 30 |
| Workflow | Platform `WorkflowDefinitionsController` routes + `definitions/` (trailing slash) | 24 |
| Tenants lookups | Platform `LookupsController`, `ConsumableReferenceDataController`, `TenantReferenceDataController`, `TenantReferenceLookupsController` (`api/lookups/**`) | 18 |

Also checked by reading (not a test row): every `…/api/…` call site in `wwwroot/assets/js/**` and the views — all plain paths, query strings
built with `encodeURIComponent` stay in the query.

## 7. Decision points / deviations (for CT)

- **D-1 — colon routes of reference-data (not BLOCKED, flagged).** The service declares `POST fixtures/evidence-required:provision` and
  `POST fixtures/evidence-required/{setId:guid}:retire` (non-production fixtures, PSS-012). No contract, no Web client calls them; through the Web
  proxy they are now 400 (`:` is outside the allow-list). They stay reachable directly at the gateway with a token. Pinned by
  `The_colon_fixture_routes_are_a_known_refusal`. The Web JS `reference-data.api.js:163-165` also has `evidence-links`, `evidence-links/{id}` and
  `evidence-requirements:evaluate` via `requestOptional`: **no service anywhere declares them** (repo-wide grep), so they were 404 → `null`
  before and are 404 / 400 → `null` now — the same UI result.
- **D-2 — TimeEntry has the same pattern (out of scope, FU).** `TimeEntryController.Api` (`/TimeEntry/api/{**path}`), `ApprovalsApi`,
  `CategoriesApi` → `UpstreamUrl(path)` = `{gateway}/api/v1/time-entry/{path}`. Not in the six; not touched. The guard lists them by name as
  known exceptions (`FU-SEC-PROXY1-TIMEENTRY`); `ContentSetsRedirectController {**rest}` is a redirect, not a proxy. The guard fails when an
  exception goes stale.
- **D-3 — one changed line in a shared test file.** `ESignatureWebTests.Routes_are_tenant_routes_…` pins every `"/api/…"` literal in
  `ESignatureController.cs`; the `"/api/v1/esign/"` literal moved into the helper (`prefix + "/"`), so the test went red (1 / 2199). The expected
  list lost that one element and gained one comment line — the PARALEL rule says "only add lines" in shared files; this is the smallest
  change that keeps the test's meaning (the proxy still targets only `/api/v1/esign…`). Conflict surface for SEC-ESIGN1: lines 169–171 of that file.
- **D-4 — free-form set codes.** `BusinessReferenceDataSet.SetCode` is `NotEmpty().MaximumLength(64)` only. A code with a space, `:` or a
  non-ASCII letter in `sets/{setCode}/…` is now refused by the Web proxy. Seeded / known codes (`uom`, `market`, `pack-applicability`) pass.
  See note proposal N-1.

## 8. Sabotages (scripts §5 `sabotage-proxy.js`)

Filter `FullyQualifiedName~Diten.Web.Tests.Security` (731). Tool: apply → build → filter → restore (mtime stamped) → build → filter. After the
loop the SHA-256 of the helper and the Workflow controller equal the pre-loop values, `git diff` is byte-identical to the pre-loop diff, no
`*.sabotage-bak` left.

| # | Sabotage | Red | Restored |
|---|---|---|---|
| s1 | the `.` / `..` segment check removed (helper) | **95** — 88 boundary rows (the 6 inputs whose reason is `dot-segment` × 11, plus `exceptions/./policy` and `.` now **forwarded** × 11) + 6 `ProxyPathTests` refusals + the decode-bound test | 731 / 731 |
| s2 | the decode loop reduced to one round | **57** — 55 boundary rows (double / triple / 5-layer encodings and `..%255C` × 11 → `too-deeply-encoded` instead of `dot-segment` / `backslash`) + 2 helper units | 731 / 731 |
| s3 | Workflow off the helper (old `{prefix}/{rest}` back) | **97** — architecture guard `Every_catch_all_action_…` + 96 Workflow boundary rows (2 verbs × 48: the attacks reach the FakeGateway) | 731 / 731 |
| s4 | the `\` check removed (helper) | **48** — 44 boundary rows (`..%5Clocations`, `..\locations`, `..%255Clocations`, `%5C..%5Cinventory` × 11 → `encoded-separator` instead of `backslash`) + 4 helper units | 731 / 731 |

## 9. Diff-stat `9362c0402..HEAD` (before this commit)

```text
 .../ESignature/ESignatureWebTests.cs               |   3 +-
 .../Diten.Web.Tests/Security/ProxyBoundaryTests.cs | 246 +++++++++++++++++++++
 frontend/Diten.Web.Tests/Security/ProxyHarness.cs  | 216 ++++++++++++++++++
 .../Security/ProxyPathArchitectureGuardTests.cs    | 151 +++++++++++++
 .../Diten.Web.Tests/Security/ProxyPathTests.cs     | 106 +++++++++
 .../Diten.Web/Controllers/ESignatureController.cs  |   4 +-
 frontend/Diten.Web/Controllers/ExpiryController.cs |   2 +-
 .../Diten.Web/Controllers/QuarantineController.cs  |   2 +-
 .../Controllers/ReferenceDataController.cs         |   4 +-
 .../Diten.Web/Controllers/TenantsController.cs     |   3 +-
 .../Diten.Web/Controllers/WorkflowController.cs    |   4 +-
 frontend/Diten.Web/Services/Proxy/ProxyPath.cs     | 141 ++++++++++++
 12 files changed, 871 insertions(+), 11 deletions(-)
```

This commit adds this file, the scripts file and three pack §19 notes (additions only).

## 10. Follow-ups and note proposals (owners' files NOT changed)

- **FU-SEC-PROXY1-TIMEENTRY** — wire the three TimeEntry catch-alls to `ProxyPath.TryBuild` (prefix `/api/v1/time-entry`, rests
  `approvals/…`, `categories/…`) and drop them from the guard's exception list.
- **FU-SEC-PROXY1-GATEWAY** — gateway hardening (not done here, as instructed): refuse request targets with dot segments / encoded separators /
  backslashes before Ocelot matching; consider route-level `AuthenticationOptions` (the probe showed the routes forward an unvalidated bearer
  with an `X-Tenant-Id` header — the services validate the token themselves, so this is defence in depth, not a hole by itself).
- **N-1 ReferenceData team** — give `SetCode` a pattern (e.g. `^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$`) so every code is path-safe; decide whether
  the two `…:provision` / `…:retire` fixture routes need a Web path (if yes, a dedicated action, not the catch-all). Remove or implement the three
  `requestOptional` calls with no service route (`evidence-links`, `evidence-links/{id}`, `evidence-requirements:evaluate`).
- **N-2 Tenants (Platform) team** — `LookupsProxy` forwards an empty rest as `/api/lookups/` (kept as-is); `{lookupKey}` values should stay
  within the same pattern.
- **N-3 Workflow team** — the `{**everything}` proxy accepts every verb (GET/POST/PUT/PATCH/DELETE) though the service has only GET/POST
  routes; narrowing the verbs is a permission-model question left to the team (not changed here).
