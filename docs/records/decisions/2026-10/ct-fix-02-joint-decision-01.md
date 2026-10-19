---
decision_id: CT-FIX-02-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave earlier (Claude single writer, Codex independent verifier, joint local implementation, test and commit); the owner said "sen devam et" (relayed by Codex)
decided_at_local: 2026-10-17 (scope agreed after the two v3 audits; implementation notes agreed during the work)
decided_by: Codex and the AI session; the owner's answer came through Codex and was relayed — the AI session did not see it directly
decision_source: the AI session's transcript, 0-based lines 20430 (the two v3 audit reports as pasted), 20590 (Codex's own verification and its objection to an unconditional stop), 20603 (the go-ahead and the agreed scope), 20863 (should-rerun included; the AGENTS.md size wording), 20957 (the quote finding)
scope: CT-FIX-02 — the v3 audit findings on the Control Tower line (K1–K4 and one small stop) in scripts/ct_queue.py, scripts/prompt_gate.py, scripts/status_report_v2.py and scripts/known_failures.py. The loading claims (K5) are fixed on the İ5-B line as İ5B-FIX-02 and recorded there. Later items F-6 to F-9 are not in this decision.
relates_to: CT-FIX-01-JOINT-DECISION-01; R8 r3 (preparation vs in force); the operating card §0 (terminal states); PR #147 (`chore/ct/pqg-01`); PR #149 (İ5-B)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# CT-FIX-02 — one approval reader, a profile main registered differently stops every consumer, a trusted known-failures registry

## Why

Two independent v3 audits of 2026-10-17 found the verdict **UYGUN DEĞİL** on the CT-FIX-01 sources (`0dae2dcc6`): an
approval reader that still took `approved-but-draft` (criteria 07, 14), a profile conflict with main that only
`--in-force` stopped (05), and a known-failures check that never validated its registry (16). Codex measured the same
on the unmodified production command lines. Line 20603, first sentence copied byte-for-byte:

> Sahip önce durdurdu, sonra yanlış anlama giderilince “sen devam et” dedi; şimdi de neden beklediğini sordu.

## What was decided

| Item | Before (`0dae2dcc6`) | Now |
|---|---|---|
| **K1** approval reader | the prompt gate read one closed künye with the `GOV_APPROVED` grammar; the -R3 reassessment check and the effort-baseline approval of the report kept `^(approved\|decided)\b` (and the -R3 check took an unclosed künye) | one reader, `ct_queue.kunye_status` + `ct_queue.GOV_APPROVED`, used by all three. Quotes: one balanced pair of the same quote is unwrapped; a missing, mixed or doubled quote is refused (the gate's old reader stripped them) |
| **K3** profile conflict | `farklı profil` was printed but stopped only `require --in-force` and the gate's dispatch mode; `strict=yes` could name any vocabulary, so a copied `sop7x` made FAILED dispatchable | origin/main registering the queue with another strict / vocabulary / baseline_commit (`farklı profil`), another task source (`farklı görev kaynağı`) or an invalid row (`kayıt geçersiz`) stops require, guard, summary, writers and the prompt gate (DUR), is a HATA of validate, and makes the report incomplete without percentages. `kayıt dosyası yok` / `kayıtlı değil` stay preparation (R8 r3, unchanged); real dispatch still needs `--in-force` / the gate's dispatch mode. `strict=yes` requires vocabulary `sop7`; SCE stays `strict=no` |
| **K2** known failures | `check` classified from any registry — a duplicate (suite, test) let the last row win, so a changed failure read as BİLİNEN; `should-rerun` could print KOŞ and ATLA for one test | `check` and `should-rerun` first apply `validate()` and the item-list checks; any problem is DUR, exit 2. Without `--suite`, a test name open in two suites is exit 2 (a TRX does not name its suite). The registry and the item lists are never changed |
| **K4** self-tests | two CT-FIX-01 wiring tests searched their own file's text; with the wiring broken and the text kept they still passed | behaviour tests through `ct_queue.main()` and `status_report_v2.read_strict_queue()`; they fail when the wiring is broken |
| small | a missing states / registry / transition file was a traceback | a DUR line |

## Known consequence

A branch that changes a registered queue's profile stops every consumer until the change is in main: profile changes go
through main first. Today origin/main has no registry file, so nothing changes for the real queue (`sce-mvp6`).

## Real data, old vs new reading

80 decision records: the -R3 reader and the report reader each count 39 approvals before and 38 after; the only
difference is `docs/records/decisions/2026-09/mvp6-loads-uptake-ph15-owner-decision-01.md` (status
`approved (PH15-185-UPTAKE) + F-1: …`) — the same record CT-FIX-01 already left without an automatic pass at the gate (the
gate's count is 38 before and after). It is not reclassified, not edited, and no strict queue uses it as an -R3 record.
163 module packs: 92 approved before and after. SCE: 500 ids, 40 dispatchable, no decision changed. The known-failures
registry validates; the committed architecture TRX gives BİLİNEN=7 before and after.

## What did not change

The operating card, SOP, REP-001, the registry, the state and transition files, the SCE queue, the known-failures TSV and
JSON, every earlier record, acceptance_record.py, ct_route.py and list_screen_touch_hook.py. No push, PR update, merge or
rebase; the branch is local.

## Limits

- CI on these sources has not run (NOT VERIFIED).
- The prompt gate's quote handling is tested through its production function `check_authority` and its self-test; a
  full §36.1 prompt pack on its command line was not built by the AI session's driver.
- Evidence: `docs/records/audits/2026-10/ct-fix-02/README.md`.
