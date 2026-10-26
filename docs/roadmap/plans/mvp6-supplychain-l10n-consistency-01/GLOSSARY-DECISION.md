---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# SupplyChain l10n glossary — the canonical decision (five shipment-bundle modules)

    raised    : 2026-10-18 · author: CT (gmg-10) · scope: Shipments, Carriers, Loads, Returns, Claims
                (DemandPlanning is its own family — out, per WORK-PACKAGE addendum 4)
    status    : RULED 2026-10-18 (owner ny@gmgroup.ch) — Part A: A1 = {Claims, Returns} camp canonical;
                A3 = CarrierId is one field (en "Carrier ID"); A2 + A4 stand as recommended. Parts B/C
                routed (translator / SharedResource owner). Record:
                docs/records/decisions/2026-10/mvp6-supplychain-l10n-glossary-owner-decision-01.md.
                Pass not yet run — one authorized hand (XMC-001) once Part B lands.
    measured  : camps verified in the resx at HEAD 2026-10-18, not taken from the notes.

## The shape of the drift (measured)

The five modules fall into two conventions set by lane order — **{Carriers, Loads}** (earlier) and
**{Claims, Returns}** (later, deliberately standardised); **Shipments straddles** (WORK-PACKAGE addenda 2–3).
Verified key-name camps at HEAD:

| concept | {Carriers, Loads} form | {Claims, Returns} form | Shipments |
|---|---|---|---|
| empty state | `EmptyList` | `EmptyState` | `EmptyState` |
| actions header | `Actions` (Carriers) / — (Loads) | `ActionsHeader` | `Actions` |
| list-load-failed | `ListUnavailable` | `ListErrorState` | `ListUnavailable` |
| error codes | `Err`-less / generic | `Err*` prefix | generic (no `Err*` family) |

`ListUnavailable` and `ListErrorState` are the **same concept** (confirmed: both hold "X could not be
loaded. Try again") — a real drift, not module-specificity.

## Part A — structural canon (CT recommends; owner ratifies)

These are key names and **en** source text — engineering/semantic, not translation.

- **A1. Adopt the {Claims, Returns} convention as canonical** for the shared infra keys: `EmptyState`,
  `ActionsHeader`, `ListErrorState`, and the `Err*` error-code prefix. Converging means renaming those keys
  (and their `en` text) in **Carriers, Loads, Shipments** and updating the modules' JS/view references —
  **wire-touching**, heavier than the per-module value fixes already made. *Why this camp:* the later lanes
  standardised on purpose; three of five already carry `EmptyState`, and `Err*` namespaces error codes away
  from labels. (Alternative: adopt {Carriers, Loads} — fewer modules standardised, less descriptive; not
  recommended. Or leave the key names and converge only values — cheaper but the drift never fully closes.)
- **A2. `AccessDenied` en → "You do not have permission for this action."** (Loads/Shipments form), not
  Carriers' "You no longer have permission…". "No longer" asserts a mid-session revocation; the UAS-001
  denied card is the generic not-authorised card, so the plain form is correct. All six translations follow
  from this (the `اً→ًا`/wording variants in the TSV realign to the chosen en).
- **A3. `CarrierId` — confirm field identity, do not blind-merge.** Same key, two `en` values: Shipments
  "Carrier", Loads "Carrier ID" (Loads also has `CarrierIdHelp`). If both label the *carrier identifier*,
  canonical en = **"Carrier ID"** and Shipments is corrected. If Shipments actually shows the carrier
  *name*, the fix is a **key rename** (`CarrierName` vs `CarrierId`), not a value merge. CT needs the
  Shipments field's intent confirmed before converging.
- **A4. Shipment statuses — Shipments is the source; adopt its values.** Only 4 of 8 `ShipmentStatusX`
  differ from Claims/Returns. Canonical = Shipments' values, so `StatusException` = "Exception"/"Excepción"
  (Claims/Returns diverged to "Incident"/"Incidencia"); likewise Closed (tr) and Delivered (ar) take the
  Shipments form. This is a "use the source" call; the exact tr/ar wording still gets a native check (Part B).

## Part B — native-language term calls (route to a native translator; NOT CT's to invent)

Each is one concept carried two ways across the camps; a native speaker picks the canonical term, then the
pass applies it to all five modules. CT lists, does not decide:

| # | language | concept | the fork (modules) |
|---|---|---|---|
| B1 | fr | id-label style | "Identifiant …" (Claims/Returns) vs "ID …" (Carriers/Loads/Shipments) |
| B2 | fr | support term | "support" (Claims/Returns) vs "assistance" (Carriers/Loads/Shipments) |
| B3 | es | add verb | "Añadir" (Claims/Returns/Carriers/Shipments) vs "Agregar" (Loads) |
| B4 | zh | shipment term | "货运" (Claims/Returns) vs "发运单/发运…" (Loads) |
| B5 | tr | `StatusClosed` | "Kapatıldı" vs "Kapandı" (A4 says Shipments' form wins; native confirms which) |
| B6 | ar | orthography | tanvin `اً`/`ًا`, šadda `تعذر`/`تعذّر` — mostly fixed in-module; ratify the canonical |
| B7 | ru | support / titles | three "support" forms; `ChangeStatusTitle` noun-vs-verb |

(B1/B2 ride with A1's key-name convention but the *word* is a fr choice, so they stay here.)

## Part C — SharedResource collision (SharedResource / Platform owner, beyond the five modules)

`StatusActive` tr "Etkin" vs SharedResource "Aktif"; and zh `StatusRetired` "已停用" **collides** with
SharedResource Passive "停用". Deciding the module status labels requires deciding how they relate to the
shared Active/Passive strings — that touches `SharedResource`, a Platform-owned file outside these five
modules (XMC-001). Route to the SharedResource owner; the five-module pass must not edit SharedResource.

## What is explicitly LEFT (not drift)

Module-specific text that *should* differ — `ConfirmTransition`, `CreateSuccess`, `CreateReplaySuccess`,
`FormTitleCreate`, `TransitionTitle`, each module's own status labels — stays per-module (~15+ of the TSV's
53 rows). The earlier "fr/es/ru shipment-status gender" flag was a **false positive** (load vs shipment =
different grammatical gender) — do not converge (WORK-PACKAGE correction).

## Method (the one authorized pass, after ratification)

1. Owner ratifies Part A (and A3's field-identity answer); translator settles Part B; SharedResource owner
   settles Part C.
2. One hand applies, across all five modules' resx **and** the JS/view references for renamed keys, keeping
   each file's 7-language parity and `en` as the wire source.
3. Re-run the sweep; the converged keys no longer drift, module-specific ones stay "different" and are
   excluded.

## Files

- `GLOSSARY-DECISION.md` — this. Inputs: `DRIFT-INVENTORY.tsv`, `WORK-PACKAGE.md` (+ its four addenda) and
  the six per-module l10n review records under `docs/records/audits/2026-10/mvp6-*-l10n-review-01/`.
