---
module: MOD-0188
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0188 DemandPlanning UI — l10n review: 32 fixes (20 content + 12 fr apostrophe); its own l10n family

    reviewed  : 2026-10-18 via the l10n-agent (6th / last of the SupplyChain UI set) · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/DemandPlanning/DemandPlanningIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : 678 values (113 keys × 6 non-English) reviewed. **32 fixes** across ar/es/fr/ru/tr (en + zh
                unchanged): 20 content (English-leak + intra-module term consistency) + 12 fr apostrophe
                normalisation. 6 domain items deferred. Verified by CT below.

## Verification (CT, the largest change set of the six)

- Scope: `DemandPlanningIndex.{ar,es,fr,ru,tr}.resx` changed; **en and zh unchanged**.
- Parity: 113 keys per language; **key-name set identical to HEAD in every file (0 diffs)** — value-only.
- All five changed files parse as valid XML.
- tr diff spot-check matches the report (Draft→Taslak, Warehouse→Depo, audit→denetim, tenant→kiracı,
  origin→başlangıç noktası).
- fr apostrophe fix confirmed **character-only**: 12 minus lines (`&apos;`/`'`) ↔ 12 plus lines (`’`), and
  after stripping the apostrophe char the two sides are identical (0 lines differ beyond the apostrophe).

## The fixes

- **tr (8):** English words left untranslated while the module translates them elsewhere — `ForecastCreate`/
  `ForecastOpenDraft`/`ForecastCreated` "Draft"→"Taslak", "audit"→"denetim"; `ForecastIntro`/`ForecastScopeHelp`/
  `ForecastInvalidScope` "Warehouse"→"Depo"; `ForecastComparisonHelp` "origin"→"başlangıç noktası";
  `ForecastAuthorityPending` "tenant"→"kiracı".
- **es (2):** `SupersededReadOnly` "versión"→"revisión" (matches `Revision`/`RevisionHistory`);
  `ForecastAuthorityPending` "tenant"→"inquilino".
- **ru (3):** `ForecastSelectExplicitly`/`ForecastScopeLost`/`ForecastAuthorityPending` "серия"→"ряд"
  (matches `Series`/`SelectSeries`).
- **ar (7):** tanvin `اً`→`ًا` (5 keys) and `HistoryLoading` "جار"→"جارٍ"; `HistorySku` →"SKU" (was a 3rd term).
- **fr (12):** apostrophe `&apos;`/`'` → typographic `’` on 12 values, matching the rest of the file and the
  other SupplyChain fr resx. Text unchanged.

Domain terminology (forecast/demand/horizon/backtest/baseline/bias, WAPE/MAE/MASE, Holt/Croston) is otherwise
correct and consistent across the 6 languages, History page included.

## Deferred to a human translator (6)

ar `Revision`/`RevisionHistory` vs `InReview` (both "المراجعة"); tr/zh/ru Revision vs version (one word);
tr `ForecastBias`/`ForecastMethodNaive`/… ("Bias"/"Naive" left English); ar SKU vs "الصنف" in prose;
ru `NoCompanyData` "scope"→"список"; fr/es/tr `UnavailableBody` "Gateway" left untranslated. Domain/product
calls — CT does not invent them.

## Cross-module → consistency pass (XMC-001): DemandPlanning is its OWN family

Unlike the five shipment-bundle modules, DemandPlanning (Diten.PlanningService) has a **third, self-contained
l10n namespace** — `Forecast*` and `History*` prefixes, its own permission/session/scope messages
(`ForecastPermissionLost`, `ForecastSessionExpired`, `ForecastInvalidScope`), and **none** of the shared infra
keys (`AccessDenied`, `ValidationError`, `SupportReference`, `IdempotencyKeyReused`, `EmptyState`/`EmptyList`).
The only key shared with the other five is `Quantity` (identical to Shipments in all 7). So DemandPlanning is
**largely outside the two-camp convergence** — the SupplyChain-wide pass is really about the five
shipment-bundle modules; DemandPlanning only needs internal consistency (done here) plus a few parallel calls
(session-expired wording, Reject zh 驳回 vs 拒绝) noted for the pass.

## Files

- `REPORT.md` — this review. Code: `DemandPlanningIndex.{ar,es,fr,ru,tr}.resx` (32 value edits).
