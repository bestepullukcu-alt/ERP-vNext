---
module: MOD-0187
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0187 Claims UI — l10n correctness review (closes R-4c F-R4c-6), no errors, 5 items for a human translator

    reviewed  : 2026-10-18 via the l10n-agent · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/Claims/ClaimsIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : no definite error found; **0 resx changed**. Parity holds (95 keys in every language). R-4c
                F-R4c-6 ("the Q403 strings reuse R-4a Carrier translations; the other ~94 keys ×6 were not
                re-reviewed") is answered: the reuse is benign, the 94 keys are sound. 5 domain-term calls
                are deferred to a human translator; a few cross-module drifts are left per XMC-001.

## Method

570 values (95 keys × 6 non-English languages) read and compared; `en` is the source/wire language and was
not touched. Rule read: `.antigravity/rules/localization-standard.md`. No key name or `en` value changed.
`git status` under `Resources/.../Claims` is clean — the review applied nothing.

## F-R4c-6 concern 1 — Q403 strings copied from Carrier? No

- `ClaimsIndex` and `CarriersIndex` share 4 key names (`PageDescription`, `QuickView`, `ReasonCode`,
  `SupportReference`); the shared ones are generic UI terms, not carrier-specific.
- The only verbatim copy is `ErrIdempotencyKeyReused` — identical to Carriers in all 7 languages, but the
  text is carrier-neutral ("This request was already received with different values…"), so not an error.
- The Claims-specific Q403 strings are genuinely Claims-specific (`ErrInvalidClaimTransition`,
  `ErrClaimStorageUnavailable`, `ClaimCreateReplayed`, `TransitionReplayed`, …). Carriers' replay-success
  sentences did not leak into Claims. "carrier" survives in exactly the 4 keys where it is meant
  (`PageDescription`, `LinkShipmentCarrier`, `NoShipmentCarrier`, `ErrClaimCarrierMismatch`).

## F-R4c-6 concern 2 — the 94 keys ×6 languages

Automated: no empty values, no stray `{0}`, no leading/trailing/double spaces, no Latin-script English
leaking into ar/ru/zh (only UUID/EUR/ID remain Latin), numeric/constant examples preserved. By reading:
status names (Open, Investigating, Approved, Rejected, Settled, Closed, Withdrawn) correct in all 6 with
consistent gender; the "claim" term is standard and consistent (tr Talep, fr Réclamation, es Reclamación,
zh 索赔, ar مطالبة, ru Претензия).

## Deferred to a human translator (domain-term judgment, NOT bugs)

| key(s) | lang | issue |
|---|---|---|
| `ClaimsTitle`/`PageDescription` + the "Talep" surfaces | tr | title/desc say "Hasar Talepleri" (damage) while other surfaces say plain "Talep"; pick one domain term (e.g. "Tazminat Talepleri") |
| `StatusSettled`/`ActionSettle`/`TargetSettledLabel`/`SettledNoPaymentNote` | zh | "结算" reads as accounting settlement vs en "no payment posted"; "已和解/和解" may fit better |
| `TotalLabel` ("Claims returned") | ar | "المُعادة" drifts to "returned/repeated" vs "returned from the query" |
| `ResolutionCode` | ar | "رمز الحل" is literal ("solution"); a claim-outcome term may fit ("التسوية"/"القرار") |
| `ShipmentStatusException` | tr/ru/ar | literal "exception"; fr/es already use Incident/Incidencia — cross-module (below) |

## Cross-module drifts — left untouched (XMC-001)

Claims itself is internally consistent; these span SupplyChain modules and are not this review's to change:
fr `Exception` status (Shipments "Exception" vs Returns/Claims "Incident"); Loads shipment-status gender
(fr/es/ru) differs from Claims/Returns; zh `ShipmentId` ("货运 ID" in Claims/Returns vs "发运单 ID" in
Loads). These want one SupplyChain-wide terminology pass, as a separate owner-scoped item.

## Conclusion

Claims UI l10n is sound: 7 languages, 95-key parity, no leak, no placeholder, no wrong carrier reuse. The
F-R4c-6 review is **closed with no code change**. The 5 deferred items need a native/domain translator
decision (CT does not invent domain-term translations); the cross-module drifts need one SupplyChain-wide
l10n pass under XMC-001. None blocks the Claims UI; they are quality refinements for the independent UI VER
to fold in.

## Files

- `REPORT.md` — this review. No resx was modified.
