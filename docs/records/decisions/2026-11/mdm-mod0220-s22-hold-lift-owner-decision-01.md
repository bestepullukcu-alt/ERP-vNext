---
record_id: MDM-MOD0220-S22-HOLD-LIFT-OWNER-DECISION-01
status: approved — owner decision; not a pack approval, not a dispatch permission
decided_at_local: 2026-11-06
decided_by: MDM domain owner (Ali; GMG-ERP, K3)
decision_source: CT · Ürün ana verisi chat (session local_7b0ef2d9), owner's own button click «Kaldırıyorum»
recorded_by: CT · Ürün ana verisi, branch chore/ct/mdm-ct-02
---

# MOD-0220 §22 — the "no ready-for-dev until §22 is settled" hold is lifted

1. **Decision:** the MDM owner lifts his hold on MOD-0220 §22 ("§22 alanı netleşene kadar ready-for-dev yok"),
   recorded in `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D84/4 and D97/5.
2. **Effective:** together with the CT-APR-01 approval row bound to the reviewed pack version (sha256
   `1b1a068088a96249d18e87064d5563f832b6e8f0a5df9bb171baefd8bbb0cf02`; review
   `docs/records/reviews/MOD-0220/2026-11-06-1b1a0680.md`, UYGUN). Without that row no §22 code is written.
3. **Why:** §22 scope is settled — additive `approvalStatus` on `LegalEntityLookupDto`, MDM side delivered first as
   `MDM-0220-DL1` (`docs/records/decisions/2026-11/mdm-mod0220-s22-delivery-owner-decision-01.md`).
4. **Still open (tower work, pack §22.5):** inform the Treasury Intake Control Tower that D10/2's "code together with
   MOD-0126" sentence is replaced, and ask whether the D82/2 gate opens on `MDM-0220-DL1` acceptance.
