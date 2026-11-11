---
record_id: MDM-MOD0220-S22-DELIVERY-OWNER-DECISION-01
status: approved — owner decision; not a pack approval, not a dispatch permission
decided_at_local: 2026-11-06
decided_by: MDM domain owner (Ali; GMG-ERP, K3)
decision_source: CT · Ürün ana verisi chat (session local_7b0ef2d9), owner's own button clicks
recorded_by: CT · Ürün ana verisi, branch chore/ct/mdm-ct-02
---

# MOD-0220 §22 (TRM-14) — MDM side delivered first, as its own MDM WP

1. **Decision (2026-11-06, option "Ana veri önce, ayrı"):** the MDM side of MOD-0220 §22 (additive `approvalStatus` on
   `LegalEntityLookupDto`, AC-T1…T5) is delivered as its own MDM WP (`MDM-0220-DL1`, queue `mdm`), without waiting for
   MOD-0126 DL-1.
2. **Re-applied (2026-11-06, option "Yeniden uygula"):** the pack revision was first reverted after Beste's answer that
   D9 condition 2 is met (MOD-0126 `ready-for-dev` on main). Inspection then measured that §22.5 "together with MOD-0126
   DL-1" conflicts with Treasury decisions D82/2 and D135/2, which make TRM-14 a DL-1 slice gate of four Treasury
   packs including MOD-0126. The owner chose to re-apply the revision.
3. **Not decided here:** the pack revision needs a new independent review (CT-APR-02) and a new approval row (CT-APR-01)
   whose note carries `whole_pack=MDM-0220-DL1`. Treasury's TRM-14 row stays a tracking row in `treasury-intake`.

Sources: `execution/domains/master-data-management/module-packs/MOD-0220-legal-entity-foundation.md` §22.5;
`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` D9;
`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D82/2;
`docs/records/decisions/2026-10/treasury-intake-owner-decisions-06.md` D135/2.
