# MVP-3 · MOD-0193 BOM & Routings — CT acceptance record (2026-10-06)

Lane: MVP-3 (Parallel Lane B after MVP-1) · Module: MOD-0193 BOM & Routings · DCP-009 · Pack:
`execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md` (status **review**) · Branch:
`feature/mvp3-bom-routings` (M-1; `claude/bold-bell-52tgsz` kept, not deleted).
Owner instruction (2026-10-06): "MVP-3 … bu kısım ve modülleri tamamla"; "sadece mvp3 yapılacak, diğer mvp'lere dokunma";
service decision: **ayrı servis**. Pre-acceptance fix package: M-1 … M-6 (below).

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

- **Service** `services/Diten.ManufacturingService` (5 layers, port 5067): JWT; tenant from the token (a contradicting
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
- **Audit (AUD-001):** path c `bom-surum-gecmisi` — BOM and history in ONE Mongo transaction (K2 fail-closed).
- **Web** `/Manufacturing/Boms`: Golden Compact, LE filter on the list (from `/api/legal-entities/lookup`, auto-selected
  when only one), LE required on Create, shown on Edit/Details; history, release, delete, requirement check; UAS-001; 7 languages.
- **Gateway:** `/api/bom` + `/api/bom/{everything}` → 5067. **Platform:** self-registration (M-4).

## M-1 … M-6

| item | commits | state |
|---|---|---|
| M-1 branch split | `b4d988266` `5f78eaca1` `77a6271a9` `fe7c25ff0` cherry-picked onto main `b994c813a`; BL-470 → `fix/bl-470-procurement-gateway-port` (`c47a7ef1d` `1732ebe59`), PR #136 | done |
| M-2 main merge | `d545b4b53` (`--no-ff`, origin/main `b994c813a`); shared files differ only by MVP-3 lines | done |
| M-3 legal entity | `6fd434b35` (code, contract v1.1.0 part, pack, 7 languages, tests) · `0d56bf7f9` (sabotage + live MDM evidence) · `8e18b8f50` (acceptance grep: last comment hit removed) | done |
| M-4 Platform | `35221a406` self-registration evidence | **partial** — entitlement / Admin token / UAS-001 HTTP not measured (login blocked) |
| M-5 browser | — | **blocked** — no usable login (see below) |
| M-6 suites | this commit | done for suites + SDK 8; MVP-3 PR **not opened** (M-5 stop condition) |

## Evidence (files in `evidence/`)

| level | what | result |
|---|---|---|
| E2 | `service-suite.txt` — real Mongo replica set | 69/69 |
| E2 | `service-sabotage.txt` — LE filter removed → isolation red; history insert removed → 2 red | red as expected, restored green |
| E2 | `m3-mdm-sabotage.txt` — LE validation middleware removed → 2 LE tests red | red as expected, restored green |
| **E3** | `m3-live-mdm.txt` — real Gateway + MDM + service: DRAFT LE 422, ACTIVE LE 201/200, unknown LE 422, no LE 400, other tenant 422, MDM stopped 503, MDM back 201 | as expected |
| **E3** | `gateway-e2e.txt` (`gateway-e2e.py`) — create, release, second release supersedes, current, explode (100 × 2.000 = 200.000), list, XLSX export, history, other tenant 422, other active LE same tenant 404, unknown LE 422, no LE 400, no permission 403 | 15/15 |
| E3 | `m4-self-registration.txt` — catalog `BOM-ROUTINGS` / `MANUFACTURING`, page BOMS, 5 actions, 6 `manufacturing.bom.*` permissions in Auth | registered |
| E2 | `m6-suite-comparison.txt` — branch vs origin/main, same SDK | 0 new red (architecture 38/39 = main, tenancy 3/3, gateway 86/87 = main, web 655/655 vs 652/652) |
| E2 | `architecture-suite.txt`, `gateway-suite.txt`, `web-suite.txt` | the two reds are byte-identical on main (26 CRM commands; ports 5063/5064/5065) |
| E1 | `m6-sdk8-build.txt` — SDK 8.0.131 | service + gateway build; Web fails on 8 pre-existing CS0121 in 7 non-MVP-3 files (also on main) |
| E1 | `verify-datatable.txt` | 95 PASS; the one FAIL (`personalization-client.js`) fails identically on the Golden Compact reference |
| E1 | `dcp002.txt` | exit 0 |

## Not done / not measured (stated, not hidden)

- **M-5 browser evidence and the rest of M-4 are blocked.** All six services run locally, but the only seeded user
  (`admin@diten.com`, platform_admin) has an unknown password; overwriting its hash in local Mongo was refused by the
  session's safety policy and was not pursued by another route. Without a login there is no Admin token, no entitlement
  grant through the UI, no UAS-001 HTTP proof and no screenshots. Needs an owner decision (a local fixture user/password,
  or dev credentials). Per the stop condition the MVP-3 PR is **not** opened.
- MOD-0290 `validate` is not live: `ProductMaster:Mode=Permissive` (F-0193-07).
- MOD-0209 is absent: `changeControlRef` is format-checked only (W-0193-01, F-0193-04). MOD-0003 absent (W-0193-02).
- BOM users also need `mdm.legal-entities.read` (the LE lookup is MDM's), recorded in pack §8.
