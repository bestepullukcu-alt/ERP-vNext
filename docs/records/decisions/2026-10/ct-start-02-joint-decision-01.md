---
decision_id: CT-START-02-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave ("ok devam"; Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-19
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 39606 (after the one guided C23 call: a short first answer, line references only from a numbered read, discovery once with no temporary file), 39620 (the comparison with a Legacy sample is no A/B; an unread-source count is not invented; the line target never hides a critical conflict; a reasoned rerun stays allowed), 39637 (the six-file package started), 39677 (ask a question only when an owner decision is needed)
scope: CT-START-02 — the `control-tower` skill's text only (§2, §5, §6), three anchors for it in `.antigravity/adapters.json`, and the two copies `sync_adapters.py` generates. The CT tools, the operating card, AGENTS.md, the tests and the product code are not changed.
relates_to: CT-START-01-JOINT-DECISION-01 (the skill); the C23 guided call run-02 and the two sealed first scores (both kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-19
---

# CT-START-02 — a shorter first answer, numbered line references, one discovery

## Why

The one guided `/control-tower Employee` call on C23 (run-02) followed the skill's steps, but both first scores failed
REPORT-01: the answer said it wrote no file while it had redirected the discovery output to a temporary file, and it
gave pack line numbers it had not read with numbers (12–14 for lines 9–11). One first score also failed EMP-10: the
start answer was long and technical.

## What was decided

| Item | Now |
|---|---|
| Discovery (§2) | run it once and read its output on screen; no redirection to any file, a temporary one included; no rerun of the same source and query — after a changed source or a technical error one reasoned rerun is allowed; a cut output is called partial |
| First call (§5) | no file written, a temporary one included (added to the existing no-queue / no-registry / no-prompt-draft rule) |
| First answer (§6) | short (target about 12 lines): one-line state; the known missing or unreadable sources by name ("kısmi inceleme"; no invented count); a critical conflict or blocker is never hidden by the line target; one reasoned proposal marked "öneri — onay değil"; one question only when an owner decision is needed. Facts stay apart from inferences; the source table, facts, inferences and candidates come on request |
| Line references (§6) | only from a numbered read (`grep -n`, `nl -ba` or the reading tool's line number); otherwise `path@commit` only |
| Guard | three more anchor sentences in the skill's existing `required_sections` (§2 one, §6 two) |

## Known consequences (measured)

- Eleven adapters; the nine other generated files are unchanged; the two copies equal the source except the banner.

## Limits kept open

- No live call: whether a model now writes a shorter answer, cites only numbered lines or writes no temporary file is
  NOT VERIFIED. The run-02 evidence and both first scores stay as they are. Instructions are not an OS guard; plan mode
  and the tool allowlist did not stop a Bash write in run-02.

## Not in this decision

- A `/tmp` watch or any new framework, a new launcher, a change to the CT tools, the card, AGENTS.md, the tests or
  product code, a live model call.
