---
module: MOD-0185
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0185 Loads UI — l10n correctness review: 1 Arabic orthography fix; the "gender drift" was a false flag

    reviewed  : 2026-10-18 via the l10n-agent (after Claims + Returns + Carriers) · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/Loads/LoadsIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : 372 values (62 keys × 6 non-English) reviewed. **1 value fix, `ar`** (tanvin). 3 domain
                items deferred. The fr/es/ru "status gender drift" earlier reviews flagged is **not a drift**
                — it is correct grammar. Verified by CT: only `ar` changed, parity 62×7, en untouched, valid
                XML, value-only.

## Verification (CT)

`git diff --stat`: only `LoadsIndex.ar.resx` (1 line); en + 5 others clean. Parity 62 keys per language;
ar valid XML; no key name or `en` value changed. The one change is `ErrPersistenceUnavailable` ar
"حالياً" → "حاليًا" (tanvin form used by the rest of the file and by Carriers/Returns/Claims).

## Loads-internal — clean

"Load" term consistent (tr Yük, fr Chargement, es Carga, zh 装载计划, ar الحمولة, ru Рейс); R-4b's Q403/409
strings correct and matching the shared family; the date-time help (`PlannedDepartAtHelp/Invalid`) matches
Returns' `OccurredAt*` in all 7; no empty/placeholder/English-leak; gender agreement internally correct.

## The "gender drift" was a false flag — corrected here

Earlier reviews noted Loads fr/es/ru shipment-status gender differing from Claims/Returns and I had listed
it as a convergence candidate. **It is not a drift.** Loads' status labels describe a **load** (fr/ru
masculine "Planifié/Запланирован", es/ar feminine "Planificada/مخططة"), while Claims/Returns labels describe
a **shipment** (different grammatical gender). The agreement is correct in each; forcing them to match would
be wrong. Removed from the consistency pass's converge list.

## Deferred to a human translator (3, domain)

`StatusTendered` zh ("已招标" = broad tender/bidding vs logistics "offered to a carrier"); ru "Рейс" for
Load (common but shifts "load-plan" sense — file-consistent, not wrong); `ActionPickup` tr/ru ("loading" for
pickup). CT does not invent domain-term translations.

## Cross-module items → the consistency pass (XMC-001, not fixed)

Loads is mostly the **minority side** of a two-camp split, so these are the pass's to converge, not Loads'
to change unilaterally. Added to `mvp6-supplychain-l10n-consistency-01`:

- **Two naming/wording camps.** {Carriers, Loads} (earlier R-4a/R-4b lanes) use `EmptyList` /
  `ListUnavailable` / `AccessDenied` / `Err`-less key names, es "Agregar", fr "ID …" / "assistance", zh
  "发运单" ship-term, `NotProvided` = "Sağlanmadı/Non fourni/No proporcionado". {Claims, Returns} (later
  R-4c/R-2 lanes) use `ActionsHeader` / `EmptyState` / `ListErrorState` / `ErrXxx`, es "Añadir", fr
  "Identifiant …" / "support", zh "货运", `NotProvided` = "Belirtilmemiş/Non renseigné/No indicado". The
  later lanes standardised one way; the pass must pick the canonical camp (and it touches key names + `en`,
  not only translations).
- `ModeRoad` (fr Route / es Carretera / ru Автодорожный) and `ModeParcel` ru — consistent between Loads and
  Carriers; converge both together.

## Files

- `REPORT.md` — this review. Code: `LoadsIndex.ar.resx` (1 ar value edit).
