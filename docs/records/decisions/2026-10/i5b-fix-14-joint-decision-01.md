---
decision_id: I5B-FIX-14-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 34471 (the joint decision: close the heading reader's false refusal before the audit; the same scanner for every comment reader, comments masked in place with line ends kept, no delete-and-join; each reader's fence grammar a parameter; outside_code and known_spans unchanged; G-H1 … G-H3), 34485 (Codex's 26 real CLI before the fix, and the REF counter-example '#<!--x--># 2. …' where comment deletion made a heading), 34584 (Codex's 26 real CLI on the final code; the stale İ5B-FIX-13 test comment to be corrected, its assertions unchanged)
scope: İ5B-FIX-14 — after the C12 follow-up, the order in which the instruction-file readers of sync_adapters.py read HTML comments and fences: the required-heading and section reader, the comment diagnosis, the core block's view and the text before the core. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters and the other instruction inputs are not changed.
relates_to: I5B-FIX-13-JOINT-DECISION-01 (its known heading-reader false refusal, closed here); I5B-FIX-09-JOINT-DECISION-01 (raw HTML inside the core, kept); İ5B-FIX-04's comment contract (kept); the first C10 / v11 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-14 — one scanner masks comments in place for every reader of the instruction files

## Why

`visible_text` deleted every HTML comment with a regex before `level2_sections` read fences, and joined the pieces.
The core block's view and the text before the core did the same.

- A literal `<!--` in a closed code example, in inline code or escaped ran to the next `-->` and took the closing fence
  with it. The headings after it were lost (write / check 1 where 0 was expected). İ5B-FIX-13 left this open as a
  separate false refusal.
- `#<!--x--># 2. Proje Klasör Yapısı` became `## 2. Proje Klasör Yapısı` after the deletion: a level-2 heading that is
  not in the document (write / check 0 where 1 was expected).
- A tilde example holding `<!--` in the core's preamble hid every rule after it.

## What was decided

| Item | Before (`bb2a6e741`) | Now |
|---|---|---|
| Reading comments | `HTML_COMMENT.sub("", …)` — deleted first, the pieces joined | `scan_comments`: masked in place (spaces, line ends kept), outside fences and outside inline code read within its line. A fence line inside a comment is not a fence, and an unclosed comment hides the rest. The regex is removed |
| Its readers | four separate deletions | `visible_text` (headings and sections), `heading_in_comment`, the core view, the text before the core, `open_context`: one scanner |
| Fence grammar | each reader its own | still each reader its own, as a parameter: the heading reader's 0–3 spaces (`level2_sections`), the core's any indentation (`outside_fenced_code`). Not merged |
| Not changed | — | `level2_sections`, the body and anchor rules, `outside_code` (imports), `known_spans`, the core's raw-HTML rule (İ5B-FIX-09), İ5B-FIX-04's comment contract, AGENTS.md and the adapters |

## Known consequences (measured)

- Real files: 15 headings, 8 sections, every non-blank section body, the core rule lines and the required-text decisions
  the same. AGENTS.md's raw visible text differs only by the masking.
- A comment-split text on a rule line (`foo<!--x-->bar`) no longer reads as `foobar`. The real core has no comment.
- A tilde example holding `<!--` inside the core no longer hides the rules. The core's raw-HTML rule still refuses it,
  with the right reason.
- İ5B-FIX-13's test comment was corrected (the AST unchanged), so the test file has a second identity. The runs were
  repeated on it.

## Limits kept open

- One small scanner, not a CommonMark parser. An inline span across lines is not read.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- A Markdown engine or a dependency, a merged fence grammar, a change to the heading reader's body or anchor rules, a
  change to AGENTS.md or the adapters, a live model call.
