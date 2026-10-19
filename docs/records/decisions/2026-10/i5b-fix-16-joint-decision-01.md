---
decision_id: I5B-FIX-16-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 35738 (the two C14 / v13 first reports kept by SHA-256 — Codex 77 and Claude 70, both UYGUN DEĞİL on criterion 02; Codex C14-F1 / -F2 and Claude C14-F1 / -F2; Codex's separate solution view: one context reader, consumer policies and fence grammars unchanged, raw text not read as Markdown, no delete-then-search), 35776 (Codex corrects 'no Markdown in raw text' to HTML blocks only: in a paragraph Markdown goes on, so a closing tag shown in code does not close), 35801 (Codex's render of N1–N3: N2 and N3 hidden, the exact N1 visible — a normal positive; N1b in its place), 35842 (the joint decision to include N4 — an inline comment read by its paragraph — and the N1c div form as the red, the span form a declared-model refusal; same-root cases in the same package without a new scope round), 35903 (Codex: the interim source b5afb5b1 loops on a blank line), 35917 (Codex: the rule-line readers and CORE-SPAN take the same lexer; the import rule outside_code unchanged), 35987 and 36065 (Codex's interim and frozen-source measurements; a hang mutation counted red only with a targeted time limit; the product loop and the harness error recorded apart)
scope: İ5B-FIX-16 — after the C14 audits, one context for the readers of the instruction files in sync_adapters.py: comments, code spans and raw HTML. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-15-JOINT-DECISION-01 (the HTML reader this rebuilds); I5B-FIX-14-JOINT-DECISION-01 (the comment scanner); I5B-FIX-10 / -09 (escapes, autolinks, the core's raw-HTML rule — kept); İ5B-FIX-04's comment contract (kept); the first C14 / v13 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-16 — one context for comments, code and HTML

## Why

Each reader of the instruction files kept its own grammar: the comment scanner read code spans within a line, the HTML
reader blanked code and fences first and searched tags after, the core's raw-HTML scan and the rule-line readers used a
regex that knew no escape. Where they disagreed, a required text hidden from a reader still counted (write / check 0 / 0),
and some visible ones were refused:

- an outer element's closing tag inside a script closed the outer element (Codex C14-F1);
- a block-level script's own `</script>` between backticks was taken for code (Codex C14-F2, a false refusal);
- an escaped or unpaired backtick, or a code span across two lines, opened the wrong span (Claude C14-F1 — the heading
  reader, AGENTS.md and the core);
- inside an HTML block a backtick pair or a fence line was read as Markdown (N2, N3);
- an inline `<!--` not closed in its paragraph masked a live element after it (N4), and so did a `<!--` inside a code
  span across lines (N1c);
- `List<T>` as text said "(only inside a code block)" (Claude C14-F2).

## What was decided

| Item | Before (`1199d23a8`) | Now |
|---|---|---|
| Code spans | three grammars (a line-local scan, a regex, a regex without escapes) | one lexer for every reader: an ASCII-punctuation escape first, then a backtick run closed by the next run of exactly the same length in the same paragraph, across line ends. An unmatched run is text. The rule-line readers (`on_rule_line`, `link_metadata`, `unreadable_link`), CORE-SPAN and the core's raw-HTML scan take it too; `INLINE_CODE` is gone |
| Paragraph end | — | a blank line, an ATX heading, the reader's own fence opener, a thematic or setext line, a line-start `<!--`, an HTML block start (types 1–6): the run is text there. A block quote or list line is not read: a pair found only past one is **not verified** |
| Comments | a comment ran to the first `-->` anywhere | a **block comment** (a line starting with `<!--`) runs to the first `-->` and an unclosed one hides the rest (İ5B-FIX-04, core:start unchanged); an **inline** `<!--` is a comment only when `-->` follows in its paragraph, otherwise text; a `<!--` inside an HTML block must close in the block, or **not verified** |
| HTML blocks | read as Markdown by the element reader | read raw: no code span, escape or fence. Fences for the element reader are read where no HTML block masks a line |
| Elements | the same name searched in a code-blanked view | one left-to-right reader with a stack. A raw-text element (script, style, textarea, title) ends at its first real closing tag: raw in a block, outside code in a paragraph. No other tag inside it counts, so an outer element's closing tag inside it does not close the outer one. A tag never crosses its paragraph's or block's end |
| Diagnosis | an element never closed said nothing | it says "after an HTML element opened at line N and never closed — not verified"; a span or comment the lexer cannot read says so too |
| Not changed | — | `level2_sections` and every consumer policy, the two fence grammars, the core's refusal of raw HTML even in fences, the link rules, `known_spans`, `outside_code` (imports), AGENTS.md, the core, the manifest and the adapters |

## Known consequences (measured)

- The real files: the visible text, the level-2 sections, the comment view and the core view are byte-equal to C14's;
  the 30 required texts on the 11 rule lines decide the same; no raw-HTML opener is found in or before the core.
- Codex F2's block-level script with a fence or tilde line inside, and an escaped backtick before a real code span, are
  now visible (0 / 0) — C14 refused them.

## Limits kept open

- Not an HTML, CSS or Markdown engine. Any text inside an element is not evidence, and an element ends only at its own
  closing tag — a browser may end it earlier (an implied end tag), so the model refuses more. The span form of N1c is
  such a declared-model refusal, not a measured hiding.
- A block quote or list line between a backtick run and its pair, and a comment left open in an HTML block, are not read:
  not verified. A bare `a<b c` is refused as an HTML tag the check cannot read.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- A dependency, a new policy (no raw-HTML ban), a change to the import or link rules, AGENTS.md or the adapters, a live
  model call.
