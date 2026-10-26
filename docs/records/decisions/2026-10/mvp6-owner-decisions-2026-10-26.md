# MVP-6 — five owner decisions that remove outside questions (owner decision, 2026-10-26)

- owner: **Natig** (`ny@gmgroup.ch`, `gmg-10`, person key **K1**, CEO / management), decided in chat 2026-10-26 and
  recorded by the MVP-6 Control Tower
- status: **ACCEPTED**
- why: open MVP-6 work was waiting on questions to Beste, Ali and Cihan that were within K1's own authority. These
  decisions take them back. The two-person approval rule (`execution/approvals/POLICY-START.md`) is **unchanged**:
  the approver of a pack is not its author, and a person other than the one who added the row merges it.
- modules: MOD-0147, MOD-0148 (SCE); MOD-0140 Revision 2 (Procurement, Cihan); MOD-0183 / MOD-0186 (WP-P1)

## Decisions

| # | Decision | Replaces |
|---|---|---|
| D1 | **MOD-0140 S-2, MVP-6 side:** for MOD-0147 v1, the evaluating role gets `mdm.legal-entities.read` through Roles → Permissions. This is the same interim path as BL-652 (`mvp6-bl652-option2-owner-decision-2026-10-21.md`). A Procurement → MDM service identity comes later, with BL-652 Option 2. Cihan is informed; MOD-0140's own S-2 answer stays his. | the question to Cihan |
| D2 | **Permission keys and composition for MOD-0147 / MOD-0148** (`supplychain.supplier-performance.*`, `supplychain.supplier-portal.*`): the **MVP-6 CT is the single writer**, in the SupplyChain service's own manifests (tenant-assignable, granted through Roles → Permissions, never the default role template). The keys are written with the first code slice (DL-1), after CT-APR-01. The DC-03 binding pack still decides how the `supplier_user` actor gets `.read` / `.submit`. | the question to Ali |
| D3 | **MOD-0140 Revision 2 approval path:** the **MVP-6 CT runs the CT-APR-02 review** in a session separate from the author. **Natig (K1) is the CT-APR-01 approver** (K1 has `high: evet` and is not the author; the author is Cihan, K4). A person other than the one who adds the approval row merges it. This replaces the 2026-10-22 choice of Beste (K2) as approver (plan v10.0 §4a). | the review request to the MVP-1 CT; Beste as approver |
| D4 | **WP-P1 G2, G3, G4** are decided as recommended: G2 (c) first slice without reservations, (a) later as DL-6; G3 (a) REVERSAL sent as `MOD-0173` / `REVERSAL`; G4 (a) per-line deterministic keys with `DispatchPending` and REVERSAL compensation. Recorded in `docs/roadmap/plans/mvp6-integration-wp-p1-01/DECISION-DRAFT.md` §3a. The MVP-1 CT is informed: no Inventory rule or contract changes. | the question to the MVP-1 CT |
| D5 | **Second-person steps on #236 and #237:** any available approver who is not the author (K2 Beste, K3 Ali, K4 Cihan) may add the CT-APR-01 rows / give the approving review. Natig may merge when he did not add the row. For #237, the pack owner's (MVP-1 CT) review is not required. | "Beste adds the rows"; "MVP-1 CT reviews #237" |

## Still with others (not changed here)

- Cihan writes and freezes `supplier.openapi.yaml` 1.3.0 (MOD-0140 DL-1) after D3's approval.
- Beste builds the DC-03 binding (D9, 2026-10-21) and accepts the canonical-code requirement (sent 2026-10-26).
- Ali runs Q358 in production when Natig says production authorizations are updated.
- The Procurement owner answers the MOD-0147 v1.1 read-contract request (not sent yet).

## What this record does not do

It grants no code authority. MOD-0147 / MOD-0148 code still needs #236's CT-APR-01 rows and the frozen 1.3.0. WP-P1 code
still needs its pack patches updated for G2–G4, then CT-APR-02 and CT-APR-01.
