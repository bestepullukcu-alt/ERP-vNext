---
decision_id: CT-FIX-11-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 34734 (Codex: the C13 / v12 first reports kept; the triage of Claude Denetim001's C13-G1 asked, no production change yet), 34779 (Codex's 12 real gate CLI: with the queue unreadable a record bound only by the row gets the false 'record sütunu … göstermiyor'; the draft and the explicit W10 refusals right on their own; an explicit W1 is a known relation and must not be marked 'ölçülemedi'; the real queue error still stops the dispatch), 34846 (the joint decision: a new chore/ct/ct-fix-11 branch on 0afba1bf5; only prompt_gate.py and test_ct_cli_regression.py and new evidence / decision files; 'bağı ölçülemedi' only when work_packages_field finds no field and row_known is False; explicit W1, explicit W10, a broken field and draft keep their own results; the shared governance_binding and ct_queue.py unchanged; red first, named sabotage, narrow checks; no commit before the frozen sources and Codex's full staged review)
scope: CT-FIX-11 — after the C13 audits, Claude Denetim001's C13-G1: the gate's reason for a governance record whose binding to the work could only be read through the queue row, when the queue could not be read. scripts/prompt_gate.py and one test file.
relates_to: CT-FIX-10-JOINT-DECISION-01 (the same 'not measured' line for a product pack); CT-FIX-09-JOINT-DECISION-01; the first C13 / v12 reports of Codex Denetim001 and Claude Denetim001 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-11 — a governance record's row-bound relation is "not measured" when the queue cannot be read

## Why

- **C13-G1.** When the queue could not be read, `check_authority` still called the shared `governance_binding` with an
  unknown row. A governance record with no `work_packages` field is bound to the work only through the queue row's
  record column. The gate then wrote "kuyruk satırının record sütunu bu kaydı göstermiyor". The row was never read, so
  that reason was false. The real DUR and exit 1 were right.

## What was decided

| Item | Before (`0afba1bf5`) | Now |
|---|---|---|
| No `work_packages` field, the queue unreadable (`row_known` False) | the false "record sütunu … göstermiyor" | "kuyruk okunamadı — kapsam kaydının bu işe bağı ölçülemedi". The real DUR stays, and the exit stays 1 |
| An explicit `work_packages` that lists the work (W1) | — | unchanged: a known relation, no "ölçülemedi" line |
| An explicit field that does not list it (W10), a broken field | — | unchanged: their own refusals from `governance_binding` |
| The record's status (draft) | — | unchanged: refused on its own, beside the "ölçülemedi" line |
| An intact queue | — | unchanged: every binding check, the record-column reason when nothing binds |
| `governance_binding`, `ct_queue.py` | — | unchanged |

## Known consequences (measured)

- A draft record bound only by the row, with the queue unreadable, shows two lines: the draft refusal and "ölçülemedi".
- The intact-queue paths give the same output as before.

## Limits kept open

- Only this relation is marked "ölçülemedi". The other governance checks are unchanged.
- No full `scripts/tests` run (the agreed narrow check). GitHub CI has not run.

## Not in this decision

- A change to the shared `governance_binding`, `ct_queue.py`, `fail()` or any other CLI output, a new policy, a model or
  CI run, İ5B-FIX-15 (the raw-HTML context, a separate decision still open).
