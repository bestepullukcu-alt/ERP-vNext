# Missing decisions — delta only

Scope: MOD-0183 Shipment UI acceptance matrix reconciliation (`ACCEPTANCE-MATRIX.tsv`, `SCOPE-CHANGE-RECORD.tsv`).
These are proposals for the owner and Control Tower, not decisions. No decision below is recorded by this package.

## Already prepared — NOT regenerated here

| Item | Prepared in | Queue |
|---|---|---|
| DN-01 — A10 evidence-only loopback fault proxy | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/DECISION-NEEDS.md` | Q10 |
| DN-02 — bounded DataTable verification profile | same file | Q11 |
| PC-02 / PC-03 / PC-04 / PC-28 — policy conflicts, with owner decision form | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/SCOPE-AWARE-VERIFIER-PROPOSAL.md` | Q12 |
| Integration selection / execution in one checkout (needed by UI183-A16) | `docs/records/audits/2026-09/mvp6-integration-baseline-exec-01/AUTHORITY-REQUEST.md` | Q14 / Q15 |
| A12 runtime VER authority | owner decision 2026-09-25 quoted in `docs/roadmap/plans/mvp6-process-pilot-01/LANE-PROMPTS.md` (Lane-1 records it in its own AUTHORITY.md) | Q04 |

Durable PNG (PRES-183-04, Q13) is an environment capability gap, not a missing decision. A decision would only be needed if no supported export exists after the Q04/Q06 capability check; none is proposed here.

## Genuinely missing

### MD-01 — CT disposition of UI183-A07 (Control Tower)

The independent policy/error VER (`docs/records/audits/2026-09/mvp6-shipment-ui-policy-error-ver-01/ACCEPTANCE.tsv`, 24 Sep, manifest `e6551f45…ab98ab3`) returned **A07 PASS**: real-Auth 403 and zero writes for dispatch-only cancel, cancel-only dispatch and read-only transition/POD, plus a filtered browser action surface. No CT record accepts it. The functional consolidation still shows A07 PARTIAL, and the CT dispositions of 25 Sep do not mention A07. After that run, `SupplyChainShipmentsController.cs` changed in the 12-path shared-UI delta.
Needed: a CT decision to either (a) accept A07 bounded on e6551f and require a targeted regression on the final manifest, or (b) keep it PARTIAL until the final VER.

### MD-02 — Evidence-reuse decision for rows accepted before the shared-UI and A12 changes (Control Tower)

`EVIDENCE-REUSE.tsv` has rows only for A08/A09, the A12 data/API row, PRES-183-01, A03 and PRES-183-02. It has **no inherit/rerun row** for UI183-A01, A02, A04, A05, A06, A07, A15 or the A03 missing-read portion. Those rows were accepted on the 354-source manifest `e6551f`. Two later changes may affect them:
- the 12-path shared-UI delta: `Program.cs`, `SupplyChainShipmentsController.cs`, `dt-defaults.js`, `_DataTableL10n.cshtml` and the resx files;
- the A12 delta in `details.js`, which is used by the A06 detail render and by the A15 confirmations.

Under process §6, evidence can be inherited only when the change has no impact. Needed: one row per criterion in `EVIDENCE-REUSE.tsv` that records inherit or targeted rerun. The matrix records the proposed runs in `final_ver_runs_required`.

### MD-03 — DN-02 wording does not cover every OUT row (owner / shared quality-gate owner; delta to the existing DN-02 text, not a new pack)

The DN-02 authorization text lists Edit, delete, bulk delete, QuickView, direct-Gateway, generic Active/Passive and unapproved import/export/save-view/column-visibility. Two of the 23 OUT rows are **not named**:
- `SCR-07` generic `Unknown` status key;
- `SCR-22` `ShowAll` toolbar tool.

Two more rows are covered only through the general word "delete":
- `SCR-14` `AreYouSure`;
- `SCR-34` `reloadWithToast` delete lifecycle.

If DN-02 is approved exactly as written, SCR-07 and SCR-22 would be neither decided nor visible, which is a silent-waiver risk. Proposed: when DN-02 is recorded, the owner names these four checks explicitly, or gives a separate yes/no for SCR-07 and SCR-22.

### MD-04 — Parent-level UI183-A09 disposition (Control Tower, minor)

CT closed `UI183-A09-409` and `UI183-A09-422` (CLOSED_EXACT). The positive POD path, including Delivered, gating and the no-binary rule, is accepted only as part of SHIP-UI-B02-GREEN. No CT record states whether UI183-A09 as a whole is closed by combining these three rows. Needed: a CT statement when the Q04 targeted regression is dispositioned.

## Not a decision — evidence only

- UI183-A03 missing-read (UAS-001 on list/detail, direct adapter 403) and UI183-A11 (copy and error precedence) need fresh VER runs only.
- The 8 evidence-gap rows (SCR-08, 10, 15, 16, 19, 20, 21, 25) need fresh rendered evidence only. For SCR-15, evidence must also cover the localized SweetAlert Cancel button (see the SCOPE-CHANGE-RECORD reason).
