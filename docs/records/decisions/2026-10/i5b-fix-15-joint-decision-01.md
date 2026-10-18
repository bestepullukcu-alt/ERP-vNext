---
decision_id: I5B-FIX-15-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 34734 (Codex: the C13 / v12 first reports kept; C13-F1 — REF's required section inside '<script type="text/plain">', write / check 0 / 0 — to be reproduced, no production change yet), 34779 (Codex: a required heading inside '<div>' and an anchor sentence inside a script also 0 / 0; an unrelated '<div>Example</div>' with a blank line passes; the same heading / body / anchor contract, no new policy), 34832 (Codex: the anchor case is a mid-paragraph '<script>'; a declared safe refusal for required evidence in raw-HTML context; no HTML / CSS engine), 34846 (Codex's two objections: a mid-line opener in the previous paragraph must carry through; no blanket refusal of inline HTML in a required section), 34907 (Codex on prototype 02: a script holding '</pre>'; a context reset at the paragraph boundary), 34985 (Codex on prototype 03: '<div>example</div>' right before the heading — an HTML block ends at a blank line; '<script/>' does not close a script), 35125 (Codex on prototype 04: a tag the reader cannot read, '<script data-example="<">', was skipped as text; an explicit 'not verified' is enough), 35272 (the joint decision: prototype 05's behaviour into production on chore/agents/i5b-fix-15 from 1f7858f35; only sync_adapters.py and its budget test and new evidence / decision files; prototype comments rewritten as the real limit; the core and default comment reader, the import and link rules, the manifest, AGENTS.md, the core and the adapters unchanged; no claim of HTML / CSS / Markdown support; the bare 'a<b c' message difference recorded as a limited diagnosis gap, no prototype 06)
scope: İ5B-FIX-15 — after the C13 audits, Codex Denetim001's C13-F1 family: raw HTML around a required heading, section or anchor in the instruction files. sync_adapters.py's heading and section reader and its budget test. AGENTS.md, the core, adapters.json, the generated adapters and the other instruction inputs are not changed.
relates_to: I5B-FIX-14-JOINT-DECISION-01 (the comment scanner this builds on); I5B-FIX-09-JOINT-DECISION-01 (raw HTML inside the core, kept); I5B-FIX-10-JOINT-DECISION-01 (escaped '<' and autolinks, kept); the first C13 / v12 reports and Claude Denetim001's EK-01 (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-15 — raw HTML around a required heading, section or anchor hides it

## Why

The heading and section reader (`visible_text`, then `level2_sections`) masked comments and fences but read raw HTML as
plain text. A required section inside `<script type="text/plain">`, `<pre>` or `<div>`, or an anchor sentence inside a
mid-paragraph `<script>` or `<span hidden>`, still counted: write / check 0 / 0. In each case a reader of the rendered
file does not see that text.

## What was decided

| Item | Before (`1f7858f35`) | Now |
|---|---|---|
| HTML blocks (CommonMark types 1, 3–7) | read as text | masked by `scan_comments(…, html_blocks=True)`: types 1 and 3–5 to their own end marker, types 6 and 7 to a blank line, so a heading right after `<div>example</div>` is inside the block |
| HTML elements | read as text | masked by `mask_html` from the opening tag to the element's own closing tag: the same name, nesting counted, searched outside fences, inline code and escapes. A raw-text element ends only at its own closing tag. An element never closed hides the rest of the file, and one left open across a blank line still holds what follows. `<x/>` of a non-void element opens it |
| A tag the reader cannot read (a `<` inside an attribute) | skipped as text | hides the rest of the file; a lost required text says "after an HTML tag at line N the check cannot read — not verified" |
| The reader's view | — | a character is visible only when neither the block reader nor the element reader hides it |
| Allowed (unchanged) | — | unrelated closed HTML, HTML in a code example or inline code, an escaped `<`, a URI or e-mail autolink, a void tag |
| Not changed | — | the core's raw-HTML rule and its comment reader (İ5B-FIX-09 / -14), `outside_code` (imports), the link rules, `known_spans`, the manifest, AGENTS.md, the core and the adapters |

## Known consequences (measured)

- The three real files: the visible text is byte-equal to the comment-masked text, so nothing in them is HTML. Every
  required heading, section, body and anchor is the same; `--check` reports 9 adapters in sync.
- Any text inside an element is not evidence: an attribute is never read. That is a declared safe refusal.

## Limits kept open

- Not an HTML, CSS or Markdown engine, and no claim of support for them.
- A tag's attributes may cross a blank line here, unlike CommonMark. A bare `a<b c` before a section is still refused
  (1 / 1), but its message says only "missing", not "not verified". This is a limited diagnosis gap, not a pass of
  hidden text, and it is not reworked here.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- A dependency, a new grammar study, a prototype 06, a change to the core reader, the import or link rules, AGENTS.md
  or the adapters, a live model call.
