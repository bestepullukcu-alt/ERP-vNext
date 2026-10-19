---
decision_id: I5B-FIX-19-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 36856 (the two C17 / v16 first reports kept by SHA-256 — Codex 79 UYGUN, Claude 72 UYGUN DEĞİL; Claude's C17-F1, an HTML block's '</div' + NBSP / VT / U+3000 + '>' closing the div; the plan: an HTML block's closing tag name ends only at HTML's tag-name terminators; ASCII-split attributes, slash and FF kept), 36886 (the whitespace class, not examples, in one test method), 36924 and 36944 / 36945 (the same block-start assumption: a type-1 / type-6 start line's whitespace is ASCII, and a type-7 start line is CommonMark's complete open or closing tag with ASCII trailing whitespace and the four raw names excluded; the inline reader and TAG_ATTRS unchanged; the union of these is the whole scope), 37017 (the raw-name exception for opening tags only), 37050 and 37059 (Codex's component review of c9d9466b; G-Q6 and G-Q7' to be judged from the source flow, not killed by a new requirement)
scope: İ5B-FIX-19 — after the C17 audits, where a tag name ends inside an HTML block and whether a line starts an HTML block, in sync_adapters.py. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-18-JOINT-DECISION-01 (the closing-tag form this completes in the block path); I5B-FIX-17 / -16; the first C17 / v16 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-19 — where a tag name ends, and where an HTML block starts

## Why

- **Inside an HTML block** a closing tag was read with Python's `\s`: `</div` + NBSP, VT or U+3000 + `>` closed the div,
  and the hidden section counted (Claude Denetim001 C17-F1). HTML's tokenizer ends a tag name only at TAB, LF, FF, SPACE,
  `/` or `>`; any other character is part of the name.
- **Whether a line starts an HTML block** was read the same way: a type-1 or type-6 name followed by NBSP, and a type-7
  line that is no complete open tag in CommonMark's grammar (`<x !>`, `<x>` + NBSP, `<script/>`), made the next lines raw,
  so a closing tag held in a code span closed the open element (Codex's root, 0 / 0 where 1 / 1 was expected).

## What was decided

| Item | Now |
|---|---|
| A closing tag on an HTML block line | closes only when its name is followed by `HTML_NAME_END` (TAB, LF, FF, SPACE, `/`, `>`); otherwise its `<` is text and the reader goes on with the next character. After the name, an attribute or a slash still closes, as a browser reads it |
| A lone CR | not normalized here (CRLF is, when the file is read): a closing tag with a lone CR after the name is refused — a declared limit, the model refuses more |
| A type-1 start line | after the name only SPACE, TAB, `>` or the line end (`MD_SPACE`, CommonMark §4.6) |
| A type-6 start line | after the name only SPACE, TAB, `>`, `/>` or the line end (the same, with `/>`) |
| A type-7 start line | CommonMark's complete open or closing tag (§6.6: attribute names start with a letter, `_` or `:`; values unquoted, single- or double-quoted; SPACE / TAB only), followed only by SPACE or TAB; an opening tag is not pre, script, style or textarea |
| Not changed | the inline reader and `TAG_ATTRS` (the strict grammar decides only the block start), the closing-tag form of İ5B-FIX-18, the lexer, the comment reader, every consumer policy |

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-18's reader.
- In the measured cases, a line the strict start test refuses stays paragraph text, read as Markdown, so the code span
  still holds the closing tag and the hidden section is refused. This is shown for those cases only; it is not a
  guarantee for every other input.

## Limits kept open

- Not an HTML, CSS or Markdown engine. C16-I1 (a diagnosis) stays deferred. P4, EMP-REPORT-01 and daily use stay open.

## Not in this decision

- A change to openers or `TAG_ATTRS` in the inline reader, a raw-HTML ban, a new engine or dependency, AGENTS.md or the
  adapters, a live model call.
