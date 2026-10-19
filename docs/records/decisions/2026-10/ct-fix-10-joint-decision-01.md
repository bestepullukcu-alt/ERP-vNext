---
decision_id: CT-FIX-10-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 33797 (Codex's 6 real CLI on C10's sources: G1 and G2 reproduced; a narrow CT-FIX-10 proposed; where to draw the G1 line asked), 33875 (the joint decision: G1 decided in the gate, the shared pack_identity unchanged; only the module relation unknown because of q.error is 'not measured'; the real DUR and exit 1 kept; an intact queue's checks and the missing-row refusal unchanged; G2 through the existing decode with the ref and the path; ct_queue.py, prompt_gate.py and the CT CLI tests only; G6 and older items not taken), 33960 (Codex's before-fix measurement of writers in three contexts)
scope: CT-FIX-10 — after the C10 audits, Claude Denetim001's C10-G1 (the gate invented a module mismatch for a product pack when the queue could not be read) and C10-G2 (writers crashed with a traceback on a queue that is not UTF-8 in a ref, the base or the merge base). scripts/prompt_gate.py, scripts/ct_queue.py and one test file.
relates_to: CT-FIX-09-JOINT-DECISION-01; I5B-FIX-12-JOINT-DECISION-01 (a separate branch); the first C10 / v11 reports of Codex Denetim001 and Claude Denetim001 and Claude Denetim001's EK-01 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# CT-FIX-10 — no invented module mismatch without the queue row; writers names a queue that is not UTF-8

## Why

- **C10-G1.** When the queue could not be read, CT-FIX-09 put the real DUR reason into the gate's report. The row was
  unknown, though, and `check_authority` passed no module ids to the shared `pack_identity`. For a sound product pack
  that added a false "paket MOD-0288 bu işin modülüne (yok) ait değil".
- **C10-G2.** `writers` decoded the base, every ref and the merge base with a raw `.decode("utf-8")`. A queue that was not
  UTF-8 in any of them ended in a traceback.

## What was decided

| Item | Before (`56166f107`) | Now |
|---|---|---|
| A product pack when the queue could not be read | `pack_identity(…, [])` and a false "(yok)" mismatch | the gate gives `pack_identity` the pack's own module (from its künye id), so the künye and file-name identity are still checked. It writes "kuyruk okunamadı — paketin bu işin modülüne bağı ölçülemedi". The real DUR stays, and the exit stays 1 |
| The shared `pack_identity` | — | unchanged (no "None = not measurable" meaning added) |
| An intact queue | — | unchanged: every status, künye, file and module check; a WP not in the queue is still refused as a module mismatch |
| writers | a raw decode, a traceback | base, ref and merge base through the existing `decode(data, what)`, naming the ref and the path: a DUR and exit 2 |

## Known consequences (measured)

- A broken queue with a pack whose künye and file name disagree now shows both lines: "ölçülemedi" and the pack's own
  identity refusal.
- `writers` on the real refs: no DUR and no traceback. Its exit 1 comes from the existing collision report.

## Limits kept open

- A governance record's binding with the queue row unknown is unchanged. It was not shown misleading here: the
  künye's `work_packages` still binds.
- `writers` stops at the first queue that is not UTF-8.
- C10-G6 and other older items are not in this decision. No full `scripts/tests` run (the agreed narrow check).

## Not in this decision

- A change to the shared `pack_identity`, `fail()` or any CLI output other than these, a new policy, a model or CI run.
