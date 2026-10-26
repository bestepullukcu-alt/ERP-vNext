---
module: MOD-0186
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0186 Returns UI — l10n correctness review: 12 consistency fixes applied, 7 items for a human translator

    reviewed  : 2026-10-18 via the l10n-agent (continuation of the Claims review) · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/Returns/ReturnsIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : 636 values (106 keys × 6 non-English) reviewed. **12 value fixes applied** to 6 files
                (en untouched); **7 domain-term items deferred** to a human translator; cross-module drifts
                left per XMC-001. Verified by CT: scope 6 files, parity 106×7, en unchanged, all valid XML,
                diff is value-only.

## Verification (CT, not taken on the agent's word)

- `git diff --stat`: ar 1, es 2, fr 5, ru 1, tr 2, zh 1 — 12 lines across **6** files; `ReturnsIndex.en.resx`
  unchanged.
- Parity: every language still 106 keys. Each changed file still parses as valid XML.
- The diff is `<value>`-only: no key name changed, no `en` value changed, no structural/line-ending drift.

## The 12 fixes (value-only, consistency/terminology)

| key | lang | fix |
|---|---|---|
| `AddNewReturns` | es | `Agregar` → `Añadir devolución` (matches "Añadir referencia" / Claims "Añadir reclamación") |
| `ReturnId` | fr | `ID du retour` → `Identifiant du retour` (matches the `ShipmentId` = "Identifiant de …" pattern) |
| `ReturnId` | ru | `ID возврата` → `Идентификатор возврата` (same pattern) |
| `ReturnCreateReplayed`, `TransitionReplayed`, `ErrReferenceStateUnavailable`, `ErrPersistenceUnavailable` | fr | `requête` → `demande` (intra-file + Claims consistency); first two also got the Claims "; il s’affiche comme terminé" phrasing |
| `EligibilityNote` | tr | note said "Kapatıldı" while the status label `ShipmentStatusClosed` = "Kapandı" — aligned to "Kapandı" |
| `ErrDependencyResponseInvalid` | tr / zh / ar | support-contact wording aligned to the rest of the family ("destek ekibine başvurun" / "请联系支持团队" / "التواصل مع الدعم") |
| `ReceivedManualNote` | es | "stock" → "inventario" (the term the rest of the screen uses) |

## Reuse / terminology checks (clean)

- No wrong cross-module term leaked into Returns (no "carrier"/"claim" where it shouldn't be); the verbatim
  shared strings (`QuickView`, `NotProvided`, `ErrInternalError`, the `Notice*`/`ErrShipment*` family, and
  the 8 shared Shipment status names) are neutral and fit — same judgement as the Claims `ErrIdempotencyKeyReused`.
- "return" term consistent (tr iade, fr retour, es devolución, zh 退货, ar مرتجع, ru возврат); status-name
  gender agreement correct; `OccurredAtHelp`/`OccurredAtInvalid` (R-2 rewrite) correct in all 6.
- The 8 shared Shipment status names are **identical to Claims** in all 6 languages — no drift.

## Deferred to a human translator (7, not applied — domain-term judgment)

zh ship-term split ("货运" labels vs "发运单/发运行" bodies — SupplyChain-wide call); `Disposition` family
(tr/fr/es/ar weak equivalents); ar `StatusRequested` "مطلوب" (also "required"); ar/tr `TotalLabel`
("returned" drift, same as Claims); tr/ru/ar `ShipmentStatusException` (literal, same as Claims); fr
`ShipmentStatusException` = "Incident" vs Shipments "Exception"; ar/tr `StatusAuthorized` vs Approve sharing
one word. CT does not invent domain-term translations.

## Cross-module drifts left per XMC-001

tr `StatusClosed` "Kapatıldı" (Returns) vs "Kapandı" (Claims/Shipments) — left (the verb form "the return
was closed" is arguably intentional; the *EligibilityNote reference* to it was aligned, the status label
was not); zh `ShipmentId` ("货运 ID" Returns/Claims vs "发运单 ID" Loads); Loads shipment-status gender.
These want one SupplyChain-wide l10n pass.

## Files

- `REPORT.md` — this review. Code: 6 `ReturnsIndex.*.resx` value edits (en unchanged).
