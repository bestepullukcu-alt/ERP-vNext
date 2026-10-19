---
decision_id: I5B-FIX-07-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 29104 (Codex Denetim001's v7 F5-family counter-examples, reproduced by Codex), 29150 (the HTML check narrowed to the text up to core:end; short code terms kept), 29194 (both first verdicts in; Claude Denetim001's F5 corrected to KALDI in its addendum; the concrete narrow package), 29589 (no architecture run; candidate 5)
scope: İ5B-FIX-07 — the F5 family left open by the v7 audits: a container opened before core:start around the core block, and a required text kept only inside a larger inline code span on its rule line. sync_adapters.py and its test file only.
relates_to: I5B-FIX-06-JOINT-DECISION-01; CT-FIX-06-JOINT-DECISION-01 (Control Tower line)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-07 — no container around the core, no rule inside a larger code span

## Why

After İ5B-FIX-06, two forms still passed `write` and `--check` (Codex and Codex Denetim001, both reproduced; Claude
Denetim001 confirmed them in its addendum and corrected its F5 criterion to KALDI):

- `<pre>` placed before `<!-- core:start -->` and `</pre>` after `<!-- core:end -->`. The check read the block between
  the markers only, so a wrapper outside them was invisible.
- every numbered rule body inside one five-backtick span on its own line. `CORE-SPAN` caught spans across lines only,
  and the required texts were looked for on the rule line wherever they stood.

The rule text is still present in the loaded core today; what was open was the static rule-position contract, not a
measured loss of a loaded instruction.

## What was decided

| Item | Before (`5cd4cc527`) | Now |
|---|---|---|
| HTML around the core | the core block only | also the text from the start of the file to `core:start`, outside comments, fences and inline code; after `core:end` the document is free (no new rule for its HTML) |
| A required text in code | counted anywhere on its rule line | counts only where no larger inline code span holds it; a short code term that is the span itself (with or without its backticks) and a text that carries its own short spans still count — the 12 required texts with code terms are kept |
| Not changed | — | `AGENTS.md`, the core, the adapters; no general Markdown parser, no open-container counting |

## Known consequences (measured)

- The real `AGENTS.md`: 0 raw HTML tags before `core:start`; all 25 required texts count on their numbered lines.
- A future raw HTML tag in the introduction of `AGENTS.md` (before the core) is refused; HTML after the core is not.

## Not in this decision

- A new core sentence, agent or instruction file; a loading or Employee run (the core's content is unchanged).
