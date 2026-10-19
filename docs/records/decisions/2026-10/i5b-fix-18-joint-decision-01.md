---
decision_id: I5B-FIX-18-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 36585 (the two C16 / v15 first reports kept by SHA-256 — Codex 79 UYGUN 20/0/0, Claude 72 UYGUN DEĞİL 18/1/1; Claude's C16-F1, a closing tag with an attribute or a slash closing the open element in a paragraph, reproduced by Codex's root in 20 real CLI; one shared closing-token form; raw HTML block lines unchanged; no wholesale skip of a malformed token), 36614 (the joint decision: ASCII space or tab with at most one line end, not Python's broad whitespace; the valid token at the same position and with the same end as HTML_TAG; a malformed token read on one character at a time; the closing arm of an HTML block's start line from the same ASCII rule, openers and TAG_ATTRS unchanged; '</div x>' still closing in a raw block; NBSP as a normative model, not a claim about every renderer; C16-I1 deferred), 36662 (Codex's component review of the frozen source 54c647a2 / test 4fc7cd62)
scope: İ5B-FIX-18 — after the C16 audits, the form of a closing tag in sync_adapters.py's HTML reader. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-17-JOINT-DECISION-01 (the closing-evidence rule this completes); I5B-FIX-16-JOINT-DECISION-01; the first C16 / v15 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-18 — a closing tag has the CommonMark form

## Why

`HTML_TAG` read a closing tag with the opener's attribute part and trailing slash. In a Markdown paragraph,
`</div x>`, `</div/>` and `</div title="x">` — and `</script x>` — are text in CommonMark (written escaped), but the
reader let them close the open element, and the required section inside counted (write / check 0 / 0, Claude
Denetim001 C16-F1, five variants; Codex's root reproduced the three div forms). The context check of İ5B-FIX-17 passed,
because the line was a proven plain paragraph line: the token itself was not a closing tag.

## What was decided

| Item | Before (`2de454f49`) | Now |
|---|---|---|
| A closing tag in a paragraph | any `</name…>` that `HTML_TAG` read | only CommonMark's form, `CLOSING_TAG`: `</name`, ASCII spaces or tabs with at most one line end (`CLOSE_WS`), `>` — at the same position and with the same end as `HTML_TAG`. Otherwise its `<` is text and the reader goes on with the next character, so nothing inside the token is skipped |
| A closing tag on a raw HTML block line | closes | unchanged: a browser reads `</div x>` as a closing tag too |
| The closing arm of an HTML block start line (types 6 and 7) | any Unicode whitespace (`\s`) | the same ASCII space or tab (`CLOSE_SPACE`); `</div >` alone on a line starts no block |
| Openers, `TAG_ATTRS` | — | unchanged |

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-17's reader (visible text, sections, HTML blocks, core view, rule
  decisions).
- NBSP is not CommonMark whitespace: `</div >` is text in a paragraph and starts no block. This follows the
  normative model; no claim is made about every renderer.
- The "same end" half of the condition showed no effect: a seeded random search found no input where a CLOSING_TAG
  match ends elsewhere than HTML_TAG's (finite support, not a proof for every input). It is kept as a defensive check,
  with no guard that proves it.

## Limits kept open

- Not an HTML, CSS or Markdown engine. C16-I1 (a heading inside a script block said to be "only inside a code block") is
  a diagnosis, deferred.
- No claim that a reader follows the rules. P4 and EMP-REPORT-01 stay open.

## Not in this decision

- An attribute grammar, a change to openers or `TAG_ATTRS`, a raw-HTML ban, a new engine or dependency, AGENTS.md or
  the adapters, a live model call.
