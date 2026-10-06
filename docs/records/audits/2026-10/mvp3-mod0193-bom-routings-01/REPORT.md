# MVP-3 · MOD-0193 BOM & Routings — CT acceptance record (2026-10-06)

Lane: MVP-3 (Parallel Lane B after MVP-1) · Module: MOD-0193 BOM & Routings · DCP-009 · Pack:
`execution/domains/supply-chain-execution/module-packs/MOD-0193-bom-routings.md` · Branch: `claude/bold-bell-52tgsz`.
Owner instruction (2026-10-06): "MVP-3 … bu kısım ve modülleri tamamla"; "sadece mvp3 yapılacak, diğer mvp'lere dokunma";
service decision: **ayrı servis**.

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

No earlier MVP-3 work existed on any branch (`git ls-remote` + `git log --all` for BOM / 0193).

## What was built

- **Service** `services/Diten.ManufacturingService` (5 layers, port 5067): JWT; tenant + legal entity from the token, a
  contradicting header 400, a missing legal entity 400; `HasPermission` on every endpoint; the four pipeline behaviours.
- **Frozen v1.0.0 operations unchanged:** `GET /api/bom/{itemId}/current?asOfDate`, `GET /api/bom/version/{id}`,
  `POST /api/bom/explode` — unwrapped `BomView`, contract Error shape.
- **Additive v1.1.0:** list (platform server-mode list contract), export (BL-452, 7 languages), create / update / release /
  delete draft, history.
- **Rules:** one Effective version per item (partial unique index), release supersedes in the same transaction, release
  refused when it closes a loop through the effective structure, Effective/Superseded immutable, revision numbers never
  reused, decimal-string quantities (no float).
- **Audit (AUD-001):** path c `bom-surum-gecmisi` — BOM and history written in ONE Mongo transaction (K2 fail-closed),
  history read on the record's own screen. Ledger `tests/architecture/audit-ledger/Diten.ManufacturingService.md`, no debt.
- **Web** `/Manufacturing/Boms`: Golden Compact, server-mode list, Create/Edit, Details (history, release, delete,
  requirement check), UAS-001 on every page, 7 languages.
- **Gateway:** `/api/bom` + `/api/bom/{everything}` → 5067.

## Evidence (files in `evidence/`)

| level | what | result |
|---|---|---|
| E2 | `service-suite.txt` — 49 tests, real Mongo replica set (3 consecutive runs green) | 49/49 |
| E2 | `service-sabotage.txt` — legal-entity filter removed → isolation test red; history insert removed → 2 tests red | red as expected, green restored |
| E2 | `architecture-suite.txt` | 39/39 |
| E2 | `web-suite.txt` (incl. NavManifest L10n guard and the BOM L10n guard, sabotaged red then green) | 437/437 |
| E2 | `gateway-suite.txt` | 86/87 — the red test lists only 5063/5064 (BL-509, other lanes); 5065/5067: 0 |
| E1 | `verify-datatable.txt` | 95 PASS; the one FAIL (`personalization-client.js`) fails identically on the Golden Compact reference |
| E1 | `dcp002.txt` | exit 0 |
| **E3** | `gateway-e2e.txt` (`gateway-e2e.py`) — real service binary on 5067 + real gateway on 5000 + Mongo replica set: create, release, second release supersedes, current, explode (100 × 2.000 = 200.000), list, XLSX export, history, other tenant 404, other LE 404, missing permission 403 | 13/13 |

## Not done / not measured (stated, not hidden)

- The Web screens were **not driven in a browser**: the Auth + Platform login chain was not run in this environment. UI
  evidence is the build, the DataTable verifier, the Web suite and the L10n guard (E1/E2), not E3.
- Module self-registration to Platform was not exercised (Platform not running; the hosted service logged its retry and
  gave up, as designed). Permissions therefore are not yet granted to any tenant role — entitlement is an owner step.
- MOD-0290 `validate` is not live: the service runs `ProductMaster:Mode=Permissive` (F-0193-07).
- MOD-0209 is absent: `changeControlRef` is format-checked only (W-0193-01, F-0193-04).
- CI runs .NET 8.0.x; locally SDK 10.0.112 was used for the architecture suite (SDK 8.0.131 from Ubuntu cannot compile
  `AuditTrailStandardTests.cs`, a pre-existing C# 12 overload ambiguity). Service, gateway and Web built with both.
