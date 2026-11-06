---
record_id: MDM-FU06-DL1-E3-DEFERRED-OWNER-DECISION-01
status: approved — owner decision; not a pack approval, not an acceptance
decided_at_local: 2026-11-06
decided_by: MDM domain owner (Ali; GMG-ERP, K3)
decision_source: CT · Ürün ana verisi chat (session local_7b0ef2d9), owner's own button click «Şimdi, doğrulama sonra»
recorded_by: CT · Ürün ana verisi, branch chore/ct/mdm-ct-02
---

# MDM-FU06-DL1 — code now, E3 local-stack evidence later

1. **Decision:** the DL-1 worker writes the code, the tests (T1–T4-b) and the sabotage evidence now (prompt
   `execution/domains/master-data-management/work-packs/prompts/MDM-FU06-DL1-v1.1.md`). The E3 upgrade-path run (pack
   MOD-0290-FU06 §18, AC-9) is not part of that prompt.
2. **Why:** the prompt review (`docs/records/reviews/MOD-0290-FU06/PROMPT-REVIEW-MDM-FU06-DL1-v1.0.md`) found that the
   evidence script `execution/domains/master-data-management/work-packs/scripts/mdm_fu06_dl1_e3.py` stops at the setup
   step on every run (exit 3): the path to create a test tenant, grant Admin / Viewer roles, the `modules.max` quota and
   the platform administrator token were not measured. Card S-83 asks for a red baseline before dispatch or an owner
   question; this record is that owner answer.
3. **Consequence:** AC-9 and the acceptance of MDM-FU06-DL1 wait until the tower measures the setup path, gives the
   script a new SHA-256, records its red baseline and runs E3 under a separate prompt. The script's location under
   `work-packs/scripts/` (the pack §18 names the evidence directory) is the tower's choice; the evidence itself still
   lands in `docs/records/audits/2026-11/mod0290-fu06-dl1/`.
