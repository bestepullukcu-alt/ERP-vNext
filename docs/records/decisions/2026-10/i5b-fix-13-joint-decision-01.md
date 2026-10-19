---
decision_id: I5B-FIX-13-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 34211 (Codex: C11 not sent — a fence opened before core:start and closed after core:end hid the rules, write / check 0 / 0), 34252 (the joint decision: one context check on the whole source; one small position-keeping scanner, no delete-then-join; the shared fence grammar; outside_code's own policy kept; known_spans inline only; the acceptance list), 34311 (Codex: İ5B-FIX-04's comment contract read independently — a comment ended by the marker's '-->' leaves the rules visible; the context read to the end of the marker line; no new 'marker not in a comment' policy), 34330 (Codex: the heading reader's false refusal kept apart from this package and not hidden; helper-level positives and real CLI results written apart)
scope: İ5B-FIX-13 — after the C11 follow-up, the context at core:start in sync_adapters.py's core check: a fenced code block opened before the marker. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters and the other instruction inputs are not changed.
relates_to: I5B-FIX-12-JOINT-DECISION-01; I5B-FIX-11-JOINT-DECISION-01; İ5B-FIX-04's comment contract (test_an_unclosed_comment_at_the_top_hides_the_rest, unchanged); the first C10 / v11 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-13 — the context at core:start read on the whole source

## Why

The core check read its contexts (fences, comments, code spans) on `extract_core`'s block. A fence opened right before
core:start and closed after core:end, or inside the core, lies outside the block. The rules were then a code example in
the source, while the check saw a plain core: write / check 0 / 0. İ5B-FIX-12's blank-line rule breaks a title but not a
fence.

## What was decided

| Item | Before (`98bf161b8`) | Now |
|---|---|---|
| The context at core:start | not read | one small scanner on the whole source (`open_context`), with nothing deleted and no pieces joined. It uses the shared fence grammar (`fence_opener` / `fence_closes`, `outside_fenced_code`'s own, unchanged) and tracks HTML comments and inline code within a line. core:start must stand alone on its physical line (at most three spaces), and nothing may be open at the end of that line. Otherwise `CORE-CONTEXT … — no required text is verified` |
| An HTML comment opened before core:start | — | ends at the marker's own `-->`, so the rules after it are visible. İ5B-FIX-04's contract and its test are kept, and no "marker not inside a comment" policy is added |
| Declared safe refusals | — | a text in front of the marker on its line; a marker indented four or more spaces; a `<!--` after an inline code span that crosses lines |
| Not changed | — | `outside_code`'s import policy, `known_spans` (inline only), the heading and section reader (`visible_text`), the shape, title, inline, link and escape guards, AGENTS.md, the adapters |

## Known consequences (measured)

- The real file passes: the marker alone, nothing open before it. `--check` reports 9 adapters in sync. No earlier test
  changed.
- One of the new test's positives, a closed code example holding a literal `<!--` before core:start, raises no
  CORE-CONTEXT. It is still refused end to end by the global heading reader, which removes comments before it reads
  fences. That is an existing false refusal of another reader. It is kept open and apart, and it is not hidden: the
  helper-level positive is not an end-to-end pass.

## Limits kept open

- One small scanner, not a CommonMark parser.
- The heading reader's order (comments removed before fences) would be its own decision. It is not in this package.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- A Markdown engine or a dependency, a change to `visible_text` / `level2_sections`, a new comment policy, a change to
  AGENTS.md or the adapters, a live model call.
