---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# SupplyChain-wide l10n consistency pass — scoped work package (XMC-001, one authorized hand)

    raised    : 2026-10-18 from the Claims (MOD-0187) and Returns (MOD-0186) l10n reviews, which both hit
                the same cross-module drifts · owner: a single l10n owner authorized to touch all SupplyChain
                module resx at once · author: CT (gmg-10)
    status    : SCOPED, not executed. This spans six modules' resx; XMC-001 forbids piecemeal edits, and the
                convergence needs a canonical-term glossary decision (some of it a native-translator call).
                The measured raw inventory is `DRIFT-INVENTORY.tsv`; nothing is changed by this package.

## Why one pass, not per-module

The Claims and Returns reviews each fixed what was safe *inside* their own module but had to **leave**
cross-module drifts untouched (XMC-001: a module lane may not edit another module's files). The result is a
backlog of "same concept, different wording across SupplyChain modules" that only a single authorized pass
can converge — otherwise each module keeps its own variant and the drift never closes.

## Measured scope

Six SupplyChain UI modules carry l10n: **Shipments, Carriers, Loads, Returns, Claims, DemandPlanning**
(7 languages each). A programmatic sweep (`DRIFT-INVENTORY.tsv`) found **53 keys** whose value differs
across ≥2 modules in ≥1 language. **But most of that is not drift to fix** — it is correct
module-specificity, and a rough auto-heuristic over-flags it. So the inventory is raw input, and the first
task of the pass is a human triage:

- **Correct module-specificity — LEAVE.** The text names the module's own entity, so it *should* differ:
  `ConfirmTransition` ("Change the **return** status" vs "…**claim** status"), `CreateSuccess`,
  `FormTitleCreate`, `TransitionTitle`, and the module's own status labels (`StatusRejected`,
  `StatusCancelled`, …). ~15+ of the 53 are this.
- **True gratuitous drift — CONVERGE.** Same concept, no reason to differ. Confirmed examples (measured):

| key | drift | candidate canonical |
|---|---|---|
| `AccessDenied` | "You do not have permission" (Shipments/Loads) vs "You no longer have permission" (Carriers) in all 7 langs | the UAS-001 denied-card text — pick one |
| `ShipmentId` (zh) | "货运 ID" (Claims/Returns) vs "发运单 ID" (Loads) | one zh ship-term SupplyChain-wide |
| `ShipmentStatusException` (fr) | "Exception" (Shipments) vs "Incident" (Returns/Claims) | one fr term |
| `ErrDependencyResponseInvalid`, `ErrPersistenceUnavailable`, `ErrReferenceStateUnavailable`, `TransitionReplayed`, `CreateReplaySuccess` | support-contact wording and "requête/demande" vary (Returns review aligned its own; other modules differ) | one error-family phrasing per language |
| `CarrierId` | "Carrier" (Shipments, a name field) vs "Carrier ID" (Loads, an id field) | confirm whether these are the same field; if so, one label |
| `NotProvided`, `SupportReference`, `ListErrorState`, `OccurredAtHelp`, `EvidenceHelp` | shared helper strings vary | one each |

The two reviews' per-module "needs human translator" items that are actually cross-module also belong here:
the **zh ship-term split** (货运 vs 发运单/发运行/发运数量), **tr `StatusClosed`** (Kapatıldı vs Kapandı), and
**Loads fr/es/ru shipment-status gender**.

## Method (for the authorized pass)

1. Triage `DRIFT-INVENTORY.tsv`: mark each of the 53 keys **module-specific (leave)** or **drift (converge)**.
2. For each drift, decide the **canonical** value per language — a small SupplyChain l10n glossary
   (ship-term, carrier-term, the error-family phrasing, the denied-card text, the status terms). Domain/
   native-translator calls (the "needs human translator" items from both reviews) are decided here, once.
3. Apply the canonical values across **all six** modules' resx in one change, keeping each file's parity and
   `en` as the wire source. This is the one place XMC-001 is satisfied — a single owner-authorized edit.
4. Re-run the sweep to confirm the converged keys no longer drift; the module-specific ones are expected to
   remain "different" and are excluded from the check.

## What this package does NOT do

No resx is edited here. Convergence needs the glossary decision (and a native translator for the domain
terms), and it crosses six modules — both reasons it is one owner-authorized pass, not a CT edit. The
Claims and Returns reviews already fixed everything safe *within* their modules; this closes the rest.

## Addendum (Carriers review, 2026-10-18) — the drift is also at the KEY-NAME level

`DRIFT-INVENTORY.tsv` compares values under the **same key name**, so it undercounts: the Carriers review
(`mvp6-carriers-l10n-review-01`) found the same concepts carried under **different key names** across
modules — Carriers `Actions` / `EmptyList` / `ListUnavailable` / `InternalError` / `AccessDenied` /
`CarrierNotFound` vs Claims `ActionsHeader` / `EmptyState` / `ListErrorState` / `ErrInternalError` /
`ErrForbidden` / `ErrClaimNotFound`, with some different `en`/wire text too. So the pass must also normalise
**key names + en source** for shared concepts, not only translated values — a deeper (wire-touching) part
that the two later lanes (Returns/Claims) standardised and the earlier Carriers lane did not.

Extra confirmed cross-module drifts from that review: `SupportReference` fr/ru; `StatusActive` tr
("Etkin" vs SharedResource "Aktif") and zh (Retired "已停用" **collides** with SharedResource Passive
"停用"); `ChangeStatusTitle` ru noun-vs-verb. The `StatusActive`/`StatusRetired` vs **SharedResource**
collision means the glossary decision must include how module status labels relate to the shared Active/
Passive strings.

## Files

- `DRIFT-INVENTORY.tsv` — the measured raw inventory (53 keys × the languages that drift, with each value
  and the modules that carry it; a rough `class` column that over-flags and must be human-triaged). Note:
  same-key-name only; see the addendum for key-name-level drift it does not capture.
- `WORK-PACKAGE.md` — this scope.
