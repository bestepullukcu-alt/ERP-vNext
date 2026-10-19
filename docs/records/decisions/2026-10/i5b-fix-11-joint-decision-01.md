---
decision_id: I5B-FIX-11-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 33213 (Codex: C9 must not close — a reference image's alt text hides the rule, write / check 0 / 0 on İ5B-FIX-10's final source), 33301 (Codex: the reference-image refusal agreed; the "line before" criterion rejected — an indented bridge line keeps the title open), 33331 (the joint decision: one consecutive block 1..N at real core positions after a blank line, and a leftover image opener 'not verified'; a visible reference link label still counts; an image inside a visible link label must not escape; a narrow, source-bound check plan)
scope: İ5B-FIX-11 — after the C9 follow-up, two more hidden-text forms of C8-F1 in the core check of sync_adapters.py: a reference image's alt text, and a title opened before a rule line that swallows it. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters and the other instruction inputs are not changed.
relates_to: I5B-FIX-10-JOINT-DECISION-01; CT-FIX-09-JOINT-DECISION-01; Claude Denetim001's C8 EK-01 and EK-02 (kept unchanged); the first C8 / v9 audit reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-11 — one supported shape for the rule list; a reference image proves no required text

## Why

İ5B-FIX-10 read each rule line on its own. Two hidden-text forms were left.

- **A reference image.** `![rule][lang]`, `![rule][]` and `![rule]` (with a definition elsewhere), or such an image inside
  a visible link label, hide the rule in alt text. Write / check gave 0 / 0.
- **A title opened before a rule line.** `[x]: u "` or `[Dil](u "`, after a blank line, directly or through an indented
  bridge line, keeps rule 4 inside the title. An ordered item not numbered 1 cannot interrupt a paragraph, so rule 4 is
  invisible. Write / check gave 0 / 0. A check of the line before rule 4 alone would not catch this: the bridge line is
  indented, like a rule's continuation.

## What was decided

| Item | Before (`214c12db5`) | Now |
|---|---|---|
| The rule list | each numbered line read on its own | one supported shape. At the real line positions of the core block, every numbered rule line is in one consecutive block numbered exactly 1..N, after a blank line. Otherwise `CORE-SHAPE … — no required text is verified` and a nonzero exit. The shape is refused, not interpreted |
| An image opener | only inline images were read | a known image is masked whole, and of a known link only its destination and title. An unescaped `![` left over makes the line "not verified" |
| Escapes | any `](` refused | an odd number of backslashes before `![` or `](` is text and stays allowed |
| Not changed | — | the visible link label, a visible reference link label, short code terms, escaped openers, autolinks, CORE-HTML, the byte budget, AGENTS.md and the adapters |

## Known consequences (measured)

- The real core passes the shape: rules 1..11 at core-block lines 4..14 (0-based), a blank line before rule 1. `--check` reports 9
  adapters in sync. No earlier test changed.
- An AI-session probe that put a reference definition inside the core, right after rule 4, is now refused by the shape
  (the definition is a line between rules). The valid control places the definition after the core.

## Limits kept open

- Refused although visible, and declared: a loose list, a continuation line between two numbered rules, a second
  numbered list in the core, an undefined literal `![x]` on a rule line. A line after the last rule is outside the block
  and is not judged by the shape.
- One supported shape, not a CommonMark parser; the local render support is not a conformance proof.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- A Markdown engine or a new dependency, a change to AGENTS.md or the adapters, a blanket ban on `][`, a live model
  call, a full C10 test run (the check plan is narrow and source-bound: only `sync_adapters.py` and its test change).
