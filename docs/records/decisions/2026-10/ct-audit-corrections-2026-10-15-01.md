---
decision_id: CT-AUDIT-CORRECTIONS-2026-10-15-01
status: correction record — no new owner decision; written under the owner's approval of the fix package, 2026-10-15T15:30+03:00
decided_at_local: 2026-10-15T15:30+03:00 (approval of the fix package)
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: independent technical audit of 2026-10-15 (three reviewers without the AI session's earlier reports; findings re-measured by the AI session), and the owner's approval of the fix package in the Claude Code session "AI" (accdcf92)
scope: errors and stale statements in the Control Tower records and tools of commits a6082e248 to e121aef81
recorded_by: the AI session (Claude Code) on 2026-10-15; this record makes no decision of its own
---

# Corrections after the independent audit of 2026-10-15

Earlier records are not edited (`.antigravity/rules/docs-organization.md` §3). Each row names what it corrects.

| # | Record or file @commit | What it says | Correction |
|---|---|---|---|
| 1 | `ct-r8-transitional-deviation-owner-decision-01.md` @93a6ab851, "Relation to earlier decisions" | It calls itself "the explicit, recorded release" that decision 4, item 6 asks for | This was the AI session's interpretation, not the owner's words, and it left out "Genel R8 istisnası oluşturulmasın." The owner's text calls the arrangement a known process deviation. Its period and end are now in CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01 |
| 2 | `ct-operating-model-owner-decisions-01.md` @6d4a5cb7d, "Work order" | "The R8/T4 fix changes T4, the SCE Control Tower's writing template" | Not so. On 2026-10-15T13:52+03:00 the owner decided that T1–T4 stay unchanged, and SOP r3 does not touch T4 |
| 3 | The same record, opening sentence "Every quotation is copied byte-for-byte from the owner's message" | — | Decision 6 quotes the session's question tool. The owner selected the option; the option's description was written by the AI session. That text is in the session transcript, not in an owner message |
| 4 | `ct-r8-registered-task-source-owner-decision-01.md` @e121aef81, `bound_to` "§36.2 unchanged … before and after" | — | True against its parent 93a6ab851. Against `main` 6b8a62d5a, lines 3–4 of the §36.2 introduction changed in a6082e248 (a withdrawn workflow reference). The T1–T4 template blocks are identical to `main` |
| 5 | `control-tower-operating-card.md` §8 @a6082e248 | SCE row "MOD-0147, 0148, 0183–0192"; rows naming "Kapanış Control Tower'ı" | MOD-0189 and MOD-0191 have no CT-QUEUE row, and the backlog tags only read "CT". Corrected in card v1.4 (this commit) |
| 6 | `scripts/known_failures.py` @a6082e248, `scripts/ct_queue.py` @849f9847a | "read-only" | Not fully: `known_failures.py` ran `git status` with optional locks, and `ct_queue.py` passed `--ref` to git as given, so `--ref=--output=…` made git write a file. Both fixed in this commit |
| 7 | `scripts/known_failures.py` @a6082e248 | `check` exit 0 = no regression | Empty, unreadable or incomplete runs returned 0, and a new violation inside an already red test was invisible. Fixed in this commit: fail-closed, TRX and failure fingerprint |
| 8 | `scripts/ct_queue.py` @849f9847a | `require` exit 0 = dispatchable | It ignored data loss on the same id and accepted a DONE row reopened in place. Fixed in this commit |
| 9 | `ai-dev-system-agent-loading-owner-decisions-01.md` @6d4a5cb7d | PR #145 "open, not merged" | Merged on 2026-10-15 13:12 +03:00 (22b056c8c) by bestepullukcu-alt. The owner's merge approval is not among the AI session's sources |
| 10 | Commit messages of a6082e248, 849f9847a and e121aef81 | "Mimari testler 131/138" | The test outputs were not stored in the repository, so under REP-001 this is E0 evidence. Still open |

## Still open

| Item | State |
|---|---|
| Row 10, evidence storage | Open |
| `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders` | It fails at its first step (the authority hash), so it never scans code files. While it stays red, a wrong docs path in code is not detected; the failure fingerprint only shows that the failure itself changed. The fix belongs to the owner of BL-615 |
