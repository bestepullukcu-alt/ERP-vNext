---
decision_id: I5B-FIX-10-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 32184 (Codex's C8 triage: F1 and F2 reproduced, a narrow same-scope follow-up proposed), 32353 (the AI session's probe is API level, not the CLI; a parenthesized title holding '(en, tr)' is not valid evidence; no full CommonMark claim), 32362 (the scope agreed; the image alt text in the same metadata family), 32412 (Claude Denetim001's EK-01 counter-example: a numbered line that is a reference definition; both forms taken; the simple '](' regex not used), 32556 (Codex's 30 real write / check CLI counter-examples on the frozen interim files pass), 32908 (Codex: deeper valid nesting fail-open on the interim source, 8 real CLI), 32979 (the joint choice: the small "not verified" refusal, not a balanced scanner; the deep visible label refused as a declared limit)
scope: İ5B-FIX-10 — after the C8 / v9 audits, two İ5B findings in the core check of sync_adapters.py — C8-F1, a required text hidden in link metadata, and C8-F2, an escaped '<' or an autolink refused as raw HTML. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json and the generated adapters are not changed.
relates_to: CT-FIX-09-JOINT-DECISION-01 (C8-G1 / G7, a separate branch); I5B-FIX-09-JOINT-DECISION-01; Claude Denetim001's C8 EK-01 (kept unchanged); the first C8 / v9 audit reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-10 — a required text counts only in visible text; an escaped '<' and an autolink are not raw HTML

## Why

- **F1.** The core check counted a required text anywhere on its numbered rule line outside code and HTML. The whole
  localization rule written only in a valid link title passed write / check 0 / 0, while the visible label was only
  "Dil". The same happened in a destination in angle brackets, in an image's alt text, and on a numbered line that is a
  reference definition (`4. [dil]: AGENTS.md "…"`), which renders nothing. This gap was already in C7, so it is not a C8
  regression.
- **F2.** The raw-HTML opener check refused an escaped `\<` (an odd number of backslashes) and a URI or e-mail autolink.
  These were false refusals, in the fail-closed direction.

## What was decided

| Item | Before (`c909fe7a3`) | Now |
|---|---|---|
| Where a required text counts | anywhere on the rule line outside inline code (and the short-term exception) | only in visible text. Not counted: a link's destination and title, a whole image, and a rule line whose text after the number is a reference definition. The title is read by its own delimiters with escapes, so a `(en, tr)` inside a quoted title stays inside it. A destination may hold one level of balanced parentheses and a label one level of brackets. The CORE-RULE message names "(only inside a link's destination or title, an image or a reference definition)" |
| Raw HTML | `<` after any backslash, and `<https://…>`, refused | before the opener check, `<` after an odd number of backslashes and the common URI and e-mail autolink forms are taken out. A real tag, two backslashes then a tag, a one-letter scheme and angle brackets holding a space stay refused |
| Deeper nesting | a label nested three deep or a destination nested two deep, the rule only in the title, passed 0 / 0 | a rule line with a `](` left after inline code and the links the check reads proves no required text: CORE-RULE "(on a rule line with link syntax the check cannot read — not verified)", nonzero. Not a balanced scanner, no dependency |
| Not changed | — | CORE-HTML and CORE-RULE as contracts, the short-term code exception, the real-tag refusal, the byte budget, AGENTS.md, the core, adapters.json, the generated adapters |

## Known consequences (measured)

- The real instruction files pass unchanged, and `--check` reports 9 adapters in sync.
- A first version of the new regex reused the existing name `INLINE_LINK`, which the dead-link check uses. Four
  dead-link tests turned red, and the regexes were renamed (`LINK_METADATA`, `RULE_REFERENCE_DEF`) before the freeze.

## Limits kept open

- These are the common forms, not a full CommonMark parser. Deeper nesting is refused as "not verified", never passed;
  a deep link whose visible label holds the rule is refused too (a declared false refusal). The 11 real core rule lines
  hold no `](`. HTML entities (`&lt;`) and other link syntaxes are not claimed.
- Codex's 30 / 30 and the 8 depth runs were on the interim source `d65dd0cd`; they stay as history, and only the affected
  cases are rerun on the final source.
- The check proves where a required text stands in the source. It does not prove that a reader follows the rule.

## Not in this decision

- A Markdown engine or a new dependency, a new required text, a change to AGENTS.md or the adapters, a live model call.
