# MVP-3 · MOD-0193 BOM & Routings — CT acceptance record (2026-10-06, updated 2026-10-12)

Lane: MVP-3 (Parallel Lane B after MVP-1) · Module: MOD-0193 BOM & Routings · DCP-009 · Pack:
`execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md` (status **review**) · Branch:
`feature/mvp3-bom-routings` (M-1; `claude/bold-bell-52tgsz` kept, not deleted).
Owner instruction (2026-10-06): "MVP-3 … bu kısım ve modülleri tamamla"; "sadece mvp3 yapılacak, diğer mvp'lere dokunma";
service decision: **ayrı servis**. Pre-acceptance fix package: M-1 … M-6 (below). Owner decisions 2026-10-12: login
path, main sync, central audit via BL-501, open items for the PR. Changed later the same day by `730a7fa1f` (pushed to
the branch by ElikPenco): BL-501 connection deferred until after the MVPs; the PR does not wait for it.

## What was measured before any work (SOP §12 INSPECT)

| concern | expected | measured | gap |
|---|---|---|---|
| identity | MOD-0193 canonical | registry `reserved / planned`, DCP-002 exit 0 | — |
| pack | approved / ready-for-dev | **none** | wrote it |
| contract | frozen BOM | `bom.openapi.yaml` v1.0.0 FROZEN, x-owner MOD-0193 | — |
| service | DCP-009: `Diten.SupplyChainService` | exists only on MVP-6's unmerged branch (PR #134) | owner: separate service |
| code / UI / gateway / tests | — | **none** | built |
| MOD-0290 | product master | canonical; PRODUCT-MASTER-BUNDLE frozen; `validate` not live | seam, Permissive default |
| MOD-0209 Change Control | backbone | absent from registry and code | waiver W-0193-01 |
| MOD-0003 Data Contract Registry | backbone | `planned / missing` | waiver W-0193-02 |
| MOD-0040 Canonical ID & Correlation | standard | convention only | X-Correlation-Id honoured |

## What was built

- **Service** `services/Diten.ManufacturingService` (5 layers, port **5070**; 5067 until 2026-10-12, when OD-P1 gave 5067 to Inventory): JWT; tenant from the token (a contradicting
  `X-Tenant-Id` → 400); `HasPermission` on every endpoint; the four pipeline behaviours.
- **Legal entity (M-3, MVP-1 pattern):** not a token claim. It travels in `X-Legal-Entity-Id` (GET may use
  `?legalEntityId`); missing → 400 `LEGAL_ENTITY_REQUIRED`; every request is proven against MDM
  `GET /api/legal-entities/{id}/lookup-validation` through the gateway — foreign / not ACTIVE → 422
  `LEGAL_ENTITY_NOT_REFERENCEABLE`, MDM unreachable → 503 `DEPENDENCY_UNAVAILABLE` (fail-closed, nothing read or written).
  No dev bypass: `git grep -n -E 'legal_entity_id|DevBypassLegalEntityId' -- services/Diten.ManufacturingService` is empty.
- **Frozen v1.0.0 operations unchanged:** `GET /api/bom/{itemId}/current?asOfDate`, `GET /api/bom/version/{id}`,
  `POST /api/bom/explode`. **Additive v1.1.0:** list (server-mode), export (BL-452, 7 languages), create / update /
  release / delete draft, history; `legalEntityId` parameter + field documented in the v1.1.0 part only.
- **Rules:** one Effective version per item (partial unique index), release supersedes in the same transaction, release
  refused when it closes a loop, Effective/Superseded immutable, revision numbers never reused, decimal-string quantities.
- **Audit (AUD-001):** path c `bom-surum-gecmisi` — BOM and history in ONE Mongo transaction (K2 fail-closed); the
  history names the actor from a real Auth token and shows changed fields under translated labels.
- **Central audit (BL-501):** the BOM history stays as the product feature and K2 trail. The central Platform log
  connection is an open item for the central team after the MVPs (`730a7fa1f`); no declaration, forwarder or shared
  interface is added ahead of BL-501's real contract. The 4 write commands are listed "BL-501 bekliyor" in the audit ledger.
- **Web** `/Manufacturing/Boms`: Golden Compact, LE filter on the list (from `/api/legal-entities/lookup`, auto-selected
  when only one), LE required on Create, shown on Edit/Details; history, release, delete, requirement check; UAS-001; 7 languages.
- **Gateway:** `/api/bom` + `/api/bom/{everything}` → 5070. **Platform:** self-registration (M-4).

## M-1 … M-6 and the 2026-10-12 package

| item | commits | state |
|---|---|---|
| M-1 branch split | `b4d988266` `5f78eaca1` `77a6271a9` `fe7c25ff0` cherry-picked onto main `b994c813a`; BL-470 → `fix/bl-470-procurement-gateway-port` (`c47a7ef1d` `1732ebe59`), PR #136 | accepted |
| M-2 main merge | `d545b4b53` (origin/main `b994c813a`) · `c219f0079` (origin/main `a814bad0f`, 9 conflicts resolved by the owner's rules; HEAD..origin/main = 0) | done |
| M-3 legal entity | `6fd434b35` · `0d56bf7f9` · `8e18b8f50` | accepted |
| M-4 Platform | `35221a406` self-registration · `9b5c46a05` entitlement → tenant Admin gets the 6 BOM permissions; UAS-001 denial live | done |
| M-5 browser | `b67b13f7f` (actor name fix) · `d6af82f2d` (history field labels, 7 languages) · `2534ec00d` (evidence) | done |
| M-6 suites | `bbd345ffb`, refreshed after the a814bad0f merge | accepted; re-measured: 0 new red |
| main sync 2 + port | `9e25c6843` (origin/main `77cd61a44`, PR #139) · `872d41105` (Manufacturing 5067 → **5070**, OD-P1) | 0 new red vs `77cd61a44`; live: gateway `/api/bom` → 5070, nothing on 5067 |
| Central audit (BL-501) | `82ebedeb4` pre-wiring, then removed by `730a7fa1f` (deferred after the MVPs; ledger lists the 4 commands) | open item, PR does not wait |

**Login (owner rule 1):** admin login succeeded (`admin@diten.com`, Web `/platform/login`; no password change, reset or
database write). With that admin, through Platform's normal APIs: test tenant `d703d845-…` registered (plan FREE, type
Internal), its initial admin invited (Development returns a temporary password), first login forced the normal
`/account/change-password`. No password is recorded in this repository.

## Evidence (files in `evidence/`)

| level | what | result |
|---|---|---|
| E2 | `service-suite.txt` — temporary Mongo replica set (port 28117), 0 skipped, after `730a7fa1f`, the main sync and the port move | 73/73 |
| E3 | `port-5070-live.txt` — service on its own config listens on 5070; gateway `/api/bom` → `localhost:5070`; nothing on 5067 | done |
| E2 | `service-sabotage.txt`, `m3-mdm-sabotage.txt`, `guards-sabotage-2026-10-12.txt` | each guard red under sabotage, green restored |
| **E3** | `m3-live-mdm.txt` (2026-10-06) — real Gateway + MDM: DRAFT LE 422, ACTIVE LE 201/200, unknown LE 422, no LE 400, other tenant 422, MDM stopped 503 | as expected |
| **E3** | `gateway-e2e.txt` (2026-10-06) — full API flow incl. other active LE 404, no permission 403 | 15/15 |
| E3 | `m4-self-registration.txt` · `m4-entitlement-uas001.txt` — catalog visible; before entitlement: in-shell access-denied, no redirect, no skeleton, gateway 403; after: 36 → 42 permissions, exactly the 6 `manufacturing.bom.*`; `AdminModules` untouched | done |
| **E3** | `m5-browser-flow.txt` — real login; choose LE → create v1 → release → v2 → release → v1 Superseded + history → explode (100 → 250.000 KG / 50.000 L) → Excel export (xlsx, 2 rows) → `ar` RTL list + details; 20 screenshots + xlsx kept outside the repository (sha256 listed) | done |
| E2 | `m6-suite-comparison.txt` — branch vs origin/main `77cd61a44`, same SDK | 0 new red vs `77cd61a44` (architecture 69/77 = main, tenancy 3/3, gateway 110/111 = main, web 1433/1433 vs 1429/1429) |
| E1 | SDK 8.0.131: service + gateway build 0 errors (in `m6-suite-comparison.txt`); `m6-sdk8-build.txt` (2026-10-06) for Web's pre-existing CS0121 | — |
| E1 | `verify-datatable.txt` · `dcp002.txt` | as before |

## Open items (stated, not hidden)

- **Central audit (BL-501):** open item — central team, after the MVPs; the PR does not wait for it (`730a7fa1f`).
- **Company authority:** MDM only proves the legal entity is ACTIVE and the tenant's; whether the USER is authorised in
  that company is not checked. Open until Platform's "my authorised companies" endpoint exists.
- BOM users also need `mdm.legal-entities.read` (the LE lookup is MDM's), recorded in pack §8.
- MOD-0290 `validate` is not live: `ProductMaster:Mode=Permissive` (F-0193-07). MOD-0209 absent: `changeControlRef`
  format-checked only (W-0193-01, F-0193-04). MOD-0003 absent (W-0193-02).
- UAS-001: the shared `_AccessDenied` partial answers HTTP 200 inside the shell (rule table says 403); same on every page
  using it — not an MVP-3 change. Recorded as **BL-510** (CT, open; record only).
- Company authority is pack follow-up **F-0193-08**.
