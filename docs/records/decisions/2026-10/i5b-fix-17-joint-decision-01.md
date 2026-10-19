---
decision_id: I5B-FIX-17-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 36290 (Codex's quoted-tag counter-example — a '</div>' in a quoted value closed the outer div — and the question whether one quote-aware tag end is enough), 36304 (the two C15 / v14 first reports kept by SHA-256 — Codex 78, Claude 71, both UYGUN DEĞİL on criterion 02; the quoted '>' tag end, and a closing tag in indented code or in a link's destination or title; Codex's objection to narrowing the attribute-name grammar; a closing tag counts only with positive evidence, the unknown is not verified), 36341 (the joint decision, with the two short solution notes bound: one quote-aware tag end, no attribute grammar, a '<' in a quoted value still not verified; a closing tag that would change state counts only in an HTML block or on a proven plain paragraph line — not beside link syntax the check cannot read, not in a container the reader cannot prove; a closing tag that closes nothing stays harmless; the opener over-refusal kept, not widened), 36401 and 36432 (Codex's component reviews of the first frozen source 16a11a3a and of the final 0e456b0f)
scope: İ5B-FIX-17 — after the C15 audits, the tag end and the evidence a closing tag needs in sync_adapters.py's HTML reader. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-16-JOINT-DECISION-01 (the reader this narrows); I5B-FIX-15-JOINT-DECISION-01; the first C15 / v14 reports and the two short solution notes (Codex a90b63c3…, Claude b74bfdcc…; kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-17 — a tag ends outside quotes; a closing tag needs evidence

## Why

- **Codex C15-F1 / -F2.** A tag ended at the first `>`, even inside a quoted value. In `<div hidden title="> </div>">`
  the `</div>` of the value closed the real div, and the required section inside it counted (0 / 0). In
  `<div title="> <script type=text/plain>">` the value's `<script>` became an element never closed (a refusal with a
  wrong reason).
- **Claude C15-F1.** A closing tag in indented code or in a link's title is not HTML — CommonMark writes it escaped — but
  the reader let it close the open element, and the section inside counted (0 / 0, seven variants).

## What was decided

| Item | Before (`eca57404d`) | Now |
|---|---|---|
| A tag's end | the first `>` | the first `>` outside a quoted value (`TAG_ATTRS`, shared by `HTML_TAG` and `HTML_TYPE7`). No attribute-name grammar is added. A `<` inside a quoted value, or a quote left open, is still not read: the tag is not verified |
| A closing tag that would close an open element or a raw-text element | counted wherever it stood outside code | counted only in an HTML block (raw) or on a proven plain paragraph line. Not proven, so **not verified**: a paragraph that starts indented two or more columns (indented code, or a possible list continuation — a paragraph right after indented code is read with it), a paragraph with a block quote or list line, a link's destination or title, an image or a reference definition (`known_spans`, `RULE_REFERENCE_DEF`), a paragraph with link syntax the check cannot read (`unreadable_link`) |
| A closing tag that closes nothing | masks itself | unchanged: harmless |
| An opener in indented code or link metadata | opens an element (the declared over-refusal) | unchanged, not widened |
| Not changed | — | the shared lexer, the comment reader, HTML blocks read raw, the fence grammars, every consumer policy, the link rules, `outside_code`, AGENTS.md, the core, the manifest and the adapters |

## Known consequences (measured)

- The real files: the visible text, the level-2 sections, the core view and the rule decisions are the same as with
  İ5B-FIX-16's reader.
- A type-7 HTML block line whose quoted value holds `>` now starts its block (read raw): C15 refused one such case.
- The first frozen source held a separate indented-code check; its guard stayed green because the indented-paragraph
  rule already refuses the same input. It was removed as redundant (the whole difference is in the evidence folder).

## Limits kept open

- Not an HTML, CSS or Markdown engine. Containers are not tracked beyond what is listed above; where the reader cannot
  prove the context, a closing tag that would change the state is not verified — a refusal, never a pass.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- An attribute grammar, a raw-HTML ban, an end-of-file mask, a new engine or dependency, a change to the import or link
  rules, AGENTS.md or the adapters, a live model call.
