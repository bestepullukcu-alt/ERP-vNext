---
module: MOD-0183
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0183 Shipments UI — l10n correctness review: 7 fixes (6 ar orthography + 1 fr); Shipments is the status source

    reviewed  : 2026-10-18 via the l10n-agent (5th of the SupplyChain set) · author: CT (gmg-10)
    scope     : frontend/Diten.Web/Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx
    verdict   : 432 values (72 keys × 6 non-English) reviewed. **7 fixes** (6 ar tanvin/šadda + 1 fr
                "intention"→"action"). 3 domain items deferred. Verified by CT: only ar+fr changed, parity
                72×7, en untouched, both valid XML, value-only.

## Verification (CT)

`git diff --stat`: `ShipmentsIndex.ar.resx` + `ShipmentsIndex.fr.resx` only (1 line each; packed files),
en and the other four clean. Parity 72 per language; ar+fr valid XML; no key name or `en` value changed.

## The 7 fixes

- **ar (6, orthography):** `PodAlreadyCaptured`, `Replay`, `IdempotencyKeyReused`, `EvidenceHelp` — tanvin
  `اً`→`ًا`; `NotFound`, `ListUnavailable` — šadda `تعذر`→`تعذّر`. Aligns ar within the file and with the
  other four modules.
- **fr (1):** `PersistenceUnavailable` "Réessayez la même **intention**" (a literal calque of en "intent")
  → "…même **action**", matching the other five languages (tr "işlem", es "acción", ru "действие").

Shipments-internal is otherwise clean: the shipment term is consistent (tr sevkiyat, fr expédition, es
envío, zh 货运, ar شحنة, ru отгрузка); status names correct with gender agreement; no empty/placeholder/leak.

## Deferred to a human translator (3)

`StatusException` tr/ru/ar (literal; but Shipments is the source so "Exception"/"Excepción" is the canonical
form — pass decides); `LifecycleRoot` (backend "root" leaked to the UI, literal in all 6 — a product call);
`RecipientName` tr ("Teslim Alan" drops "Name").

## Cross-module → consistency pass (XMC-001, not fixed)

**Shipments is the status SOURCE.** Vs Claims/Returns (`ShipmentStatusX`), only 4 of 8 statuses differ:
Exception fr "Exception" vs "Incident", es "Excepción" vs "Incidencia"; Closed tr "Kapatıldı" vs "Kapandı";
Delivered ar "مُسلَّمة" vs "تم التسليم". (The agent self-corrected: its earlier Claims report had checked only
Exception/Dispatched/Delivered and missed the tr Closed difference; now recorded.)

**Shipments' naming camp is MIXED — it is not cleanly one camp**, which refines the two-camp model:
- infra keys (`AccessDenied`, `ValidationError`, `InternalError`, `PersistenceUnavailable`,
  `IdempotencyKeyReused`, `ListUnavailable`, `Actions`) = the **{Carriers}** form (no `Err` prefix), while
  Claims/Returns/Loads use `Err*`;
- empty-state key `EmptyState` = the **{Claims, Returns}** form (Carriers/Loads use `EmptyList`);
- status keys bare `StatusX` = the **{Loads}** form (Claims/Returns use `ShipmentStatusX` for shipment
  statuses);
- values mixed: es "Añadir línea" (Claims/Returns/Carriers), fr "ID article/SKU" + "assistance"
  ({Loads/Carriers}).

So the consistency pass cannot label Shipments "camp A/B"; it straddles both. Treat **Shipments statuses as
authoritative** and reconcile the others around the chosen canonical, key names included.

## Files

- `REPORT.md` — this review. Code: `ShipmentsIndex.{ar,fr}.resx` (7 value edits).
