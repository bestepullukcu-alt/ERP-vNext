---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: pass
target: AL-MVP6-6UI-VER, VER, HIGH
role: read-only-auditor (Phase A), persisted by CT (Phase B)
mode: strict repository-read-only
---

# Independent VER — six MVP-6 SupplyChain UIs — REPORT

    auditor       : Diten read-only-auditor (strict repository-read-only), dispatched 2026-10-18
    persisted by  : CT (gmg-10) — the auditor runs read-only and cannot write; CT persisted this report and
                    regenerated SHA256SUMS authoritatively (`shasum -a 256`), and INDEPENDENTLY re-verified
                    the headline numbers + both findings against the tree (see "CT re-verification" below).
    repo / branch : /Users/natig/Projects/ERP-vNext-recovery · feature/mvp6-logistics
    HEAD (frozen) : 514b8f064b97069d2eabd6f7795ad74bda10946d
    freeze note   : UI source byte-identical to the hand-off freeze; the 101 in-scope files are unchanged
                    between 514b8f064 and later doc-only commits (CT confirmed `git diff --name-only
                    514b8f064 HEAD -- frontend/Diten.Web` is EMPTY).

## SOP §22 OVERALL VERDICT: PASS

All six NE checks pass; all four gated expectations hold (7/7 parity · 0 dangling keys on render/error
paths · 0 plain-infra resx keys across the five shipment-bundle modules · Carriers `ERROR_KEY` targets 7/7
with `ErrInternalError` fallback). Two non-blocking quality findings (1 Medium, 1 Low), both diagnostic/
rule-letter residue, no user-visible or security impact. No DOĞRULA fail condition found. This report is
**evidence for the owner, not a release** — it does not release MOD-0184 `:376` and claims no owner
acceptance.

## CT re-verification (independent, 2026-10-18 — not taken on the auditor's word)

Re-measured on the tree, all confirmed:
- parity 7/7 per module: Shipments 72, Carriers 45, Loads 62, Returns 106, Claims 95, DemandPlanning 113.
- plain-infra keys (`AccessDenied|ValidationError|InternalError|PersistenceUnavailable|ListUnavailable|EmptyList|Actions`) as resx keys across the five shipment-bundle modules: **0**.
- Carriers `ERROR_KEY` targets all present 7/7.
- F-1 confirmed: `Carriers/index.l10n.js:6-17` carries 10 stale pre-A2 names; `Loads/index.l10n.js` 4.
- F-2 confirmed: `DemandPlanning/Index.cshtml:49` uses `@Html.Raw(JsonSerializer.Serialize(...))`.
- UI files unchanged since the freeze (empty diff).

## Scope (101 tracked files)

| module | views (cshtml+cs) | resx (langs) | js (non-l10n / l10n) |
|---|---|---|---|
| MOD-0183 Shipments | 10 + 1 | 7 | 3 / 1 |
| MOD-0184 Carriers | 6 + 1 | 7 | 1 / 1 |
| MOD-0185 Loads | 5 + 1 | 7 | 1 / 1 |
| MOD-0186 Returns | 6 + 1 | 7 | 1 / 1 |
| MOD-0187 Claims | 6 + 1 | 7 | 1 / 1 |
| MOD-0188 DemandPlanning | 4 + 1 | 7 | 3 / 1 (flat `demand-planning-*.js`) |

## NE results (auditor, CT-confirmed)

- **NE1 l10n parity — PASS.** By `<data name>` MATCH count: 7/7 languages, identical key-name set, no
  empty/placeholder, no English-leak defect (apparent leaks are acronyms/proper nouns/cognates:
  fr `ActionsHeader=Actions`/`Note`/`Mode`/`Exception`, es `Manual`, DemandPlanning `SKU`/`MAE`/`MASE`/
  `Holt-Winters`/`Naive`). 0 dangling keys on user-visible render/error paths; every bridge
  `Localizer`/`SharedLocalizer` property and every runtime `t('…')`/`L.<prop>` resolves. The flagged
  `demand-planning-preview.js:208 t('State')` reads its own `#demandPlanningTranslations` payload where
  `Index.cshtml:25` sets `translations["State"]=SharedLocalizer["Status"]` — resolves, not dangling.
- **NE2 Err* convergence — PASS.** 0 plain-infra resx keys in the five modules. Carriers dispatcher
  (`index.js:298-314`) `ERROR_KEY`+`STATUS_FALLBACK_CODE`+`t()`, unknown→`ErrInternalError`, 8 targets 7/7
  and bridge-emitted. Returns (21 codes), Claims (19), Loads (inline + status fallback), Shipments
  (camelCase bridge → `Err*` keys) all resolve. 6/6 bridges 0 dangling. See F-1 for a diagnostic-only
  residue outside render/error paths.
- **NE3 UAS-001 — PASS.** Server-side `@inject IPermissionSnapshot Perms` + `Perms.Has(...)` +
  `_AccessDenied` early-return on every list/detail surface; 403→denied card (fails closed, empty data),
  401→shared `DtDefaults.handleUnauthorized()` (no page redirect); the only `window.location` uses are a
  URL builder and a post-create success nav, not auth redirects; first load fails closed (skeleton off +
  read-failure alert, never an empty table under a toast).
- **NE4 tenant isolation — PASS.** Every data call LE-scoped via `legalEntityId` + `withScope(url, scope)`;
  no module calls another module's endpoint.
- **NE5 no secrets/PII in URLs — PASS.** 0 hits for token/key/password/secret/jwt/email/ssn in query
  strings; auth/correlation/idempotency travel in headers; `legalEntityId` is a tenant-scope id, not a
  secret.
- **NE6 build/syntax — PASS.** `node --check` 16/16 on a disposable copy; bridges well-formed; marker `.cs`
  present for all six. Full Razor `dotnet build` not independently executed (disproportionate; UI unchanged
  since the passing lane) — no finding.

## Findings (both non-blocking → separate DEV cleanup lane)

- **F-1 (Medium)** — stale `requiredKeys` console-warn guard. `Carriers/index.l10n.js:6-17` lists 10 pre-A2
  names (Actions, CarrierCodeConflict, CarrierNotFound, EmptyList, IdempotencyKeyReused, InternalError,
  InvalidCarrierTransition, ListUnavailable, PersistenceUnavailable, ValidationError); `Loads/index.l10n.js`
  lists 4 (AccessDenied, EmptyList, ListUnavailable, ValidationError). The bridge no longer emits these
  (renamed to `Err*`/`EmptyState`/`ActionsHeader`/`ListErrorState` in Part A/A2), so the guard logs false
  `[L10N WARNING]` on every load and would not catch a real future regression on the new names. Diagnostic/
  console only — NOT resx keys, NOT on any render/error path (NE1/NE2 clean). Returns/Claims guards are
  clean. This is residue of the Part-A2 pass (the `index.l10n.js` loaders were not updated alongside the
  resx/bridge/index.js). Fix: sync the two arrays to the current bridge key set.
- **F-2 (Low)** — `DemandPlanning/Index.cshtml:49` uses `@Html.Raw(JsonSerializer.Serialize(translations))`
  instead of `@Json.Serialize` (`dynamic-localization-standard.md`). Output is JSON inside a
  `<script type="application/json">` block, so practical risk is low; rule-letter deviation. Pre-existing
  (not introduced this session). Fix: switch to `@Json.Serialize`.

### Out of scope (flagged, not verified)
l10n Parts B (native terms) and C (SharedResource Active/Passive); Supplier 0147/0148 (no UI); 0190/0192
(no UI).

## Unchanged-repo proof

Strict repository-read-only throughout; no git mutation; all `node --check`/sweeps ran on a disposable
scratchpad copy. The 101 audited files are byte-identical to the pre-audit freeze (freeze vs recheck SHA256
identical; `git diff --check` clean). Authoritative per-file hashes: `SHA256SUMS` beside this report
(regenerated by CT with `shasum -a 256` at HEAD 514b8f064).
