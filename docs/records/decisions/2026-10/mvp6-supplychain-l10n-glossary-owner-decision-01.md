# SupplyChain l10n glossary — owner ruling (canonical convention for the five shipment-bundle modules)

> **OWNER RULING on Part A (structural canon). Parts B and C are NOT ruled here** — Part B is a native-
> translator's to decide, Part C the SharedResource/Platform owner's. The convergence pass is not yet run;
> it is one authorized hand (XMC-001) across five modules once B is settled. Argument and the full glossary:
> `docs/roadmap/plans/mvp6-supplychain-l10n-consistency-01/GLOSSARY-DECISION.md`.

- owner: **ny@gmgroup.ch** — decision relayed by the Control Tower, 2026-10-18
- scope: `frontend/Diten.Web/Resources/Views/SupplyChain/{Shipments,Carriers,Loads,Returns,Claims}/*.resx`
  (+ the JS/view references for renamed keys). DemandPlanning is its own family and out of scope.

## Ruling — Part A (structural: key names + en source)

- **A1. Canonical convention = the {Claims, Returns} camp.** The shared infra keys converge to `EmptyState`,
  `ActionsHeader`, `ListErrorState`, and the `Err*` error-code prefix. **Carriers, Loads and Shipments** are
  renamed to this convention — key names, `en` text, and the modules' JS/view references — keeping each
  file's 7-language parity. This is a wire-touching change, heavier than the per-module value fixes already
  made.
- **A3. `CarrierId` is the same field (the carrier identifier).** Canonical `en` = **"Carrier ID"**;
  Shipments' "Carrier" is corrected to it. No key rename needed. `CarrierIdHelp` (Loads) stays.
- **A2 (stands, as recommended). `AccessDenied` en = "You do not have permission for this action."** —
  Carriers' "You no longer have permission…" is dropped; the UAS-001 denied card is the generic
  not-authorised card. All six translations realign to this en.
- **A4 (stands, as recommended). Shipment statuses: Shipments is the source.** Claims/Returns adopt
  Shipments' `ShipmentStatusX` values — so `StatusException` = "Exception"/"Excepción" (not
  "Incident"/"Incidencia"), and the Closed (tr) / Delivered (ar) forms take the Shipments value. Exact tr/ar
  wording still gets a native check under Part B (B5/B6).

## Not ruled here (routed)

- **Part B — native-language term calls** (fr id-label & support term, es add verb, zh shipment term, tr
  `StatusClosed`, ar orthography, ru support/titles): go to a native translator. Listed B1–B7 in the
  glossary. The pass applies their canonical choices to all five modules.
- **Part C — SharedResource collision** (`StatusActive` tr "Etkin" vs shared "Aktif"; zh `StatusRetired`
  "已停用" collides with shared Passive "停用"): SharedResource is Platform-owned and outside these five
  modules — the SharedResource owner decides; the pass must not edit SharedResource.

## On execution

With Part A ruled, CT can run the Part A convergence now (key renames + en + JS refs across Carriers, Loads,
Shipments), or hold until Part B's native choices land so the whole pass is one edit. Either way it is a
single authorized pass that keeps 7-language parity and `en` as the wire source, followed by a re-run of the
drift sweep. `frontend/**` is not a Protected Path, so CT can execute it on the owner's go.
