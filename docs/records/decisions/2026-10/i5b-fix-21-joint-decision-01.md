---
decision_id: I5B-FIX-21-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 37796 (the two C19 / v18 first reports kept by SHA-256 — Codex 77 UYGUN DEĞİL, Claude 73 UYGUN DEĞİL; two roots both named C19-F1; the plan: a close below the top is not verified, an svg / math opener is not verified; a top-only close alone does not close the foreign root), 37893 (the docstring's general renderer claim replaced), 37899 (the four İ5B-FIX-19 positive inputs balanced, their purpose kept), 37940 (Codex's component check of c8fafaee / 06c34835), 37968 (the NOT PROVEN guards kept, G-X5' bound to the new method), 37988 (four void / closing cases added for G-W5, W6, W8, W9; the type-7 comment made local), 38037 (the final comment-only and four-case difference reviewed)
scope: İ5B-FIX-21 — after the C19 audits, a closing tag that would close an element below the top of the stack, and foreign content (svg, math), in sync_adapters.py. sync_adapters.py and its budget test. AGENTS.md, the core, adapters.json, the generated adapters, the CT tools and the other instruction inputs are not changed.
relates_to: I5B-FIX-20-JOINT-DECISION-01 (the raw-text sets this keeps to HTML content); I5B-FIX-19 (four of its positive inputs balanced); I5B-FIX-17 (the close evidence rule); the first C19 / v18 reports (kept unchanged)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# İ5B-FIX-21 — a close below the top, and foreign content

## Why

Two roots under the same contract (a hidden required text must not count), both reported as C19-F1:

- **Claude Denetim001.** A closing tag that matched an element below the top of the stack closed it and dropped every
  element above it. HTML ignores `</div>` while a `table`, `template`, `select` or `object` is open (no div in scope), so
  the hidden div stays in force (REF in a block and a paragraph, AGENTS.md: 0 / 0 where 1 / 1 was expected). HTML can
  also reopen a formatting element after such a close (`<p><b hidden></p>`: the next text is inside a new hidden `b`).
- **Codex Denetim001.** Inside `svg` or `math` HTML's raw-text names are foreign elements and `<div>` breaks out as HTML,
  so `<svg><xmp><div hidden></xmp></svg>` leaves an HTML hidden div open; the reader read `xmp` as raw text and passed
  it (REF xmp / noembed, AGENTS.md). A top-of-stack close alone does not close this root (Codex measured it on a design
  mutant).

## What was decided

| Item | Now |
|---|---|
| A closing tag that matches an element below the top | `close`: not verified — never closes it and drops the elements above. A close at the top, and a closing tag that closes nothing, are as before |
| An `svg` or `math` opening tag | `tag`: not verified (`FOREIGN_ELEMENTS`) — no HTML namespace is proven there. A declared limit: a valid SVG or MathML is refused too. Inside a raw-text element it is text |
| Four İ5B-FIX-19 positive inputs | the `<p>` / `<x>` they open is closed before the `` `</div>` `` line (`<p></p>` on the start line; `</x>` on the next line, which cannot start a type-7 block inside a paragraph), so they still prove the type-6 / type-7 block start and no longer rely on an implicit pop. Their labels and assertions are unchanged |
| Four new cases | a void element or a closing tag stacks nothing, so `<hr` + FF + `>`, `<br>` + NBSP, `<br !>` and `<br` + FF + `a>` show that those lines start no HTML block |
| Wording | the `mask_html` docstring no longer claims that the model refuses more than a browser (changed with the code of this package); later, the type-7 comment says only how a refused line is read — that last step (`c8fafaee…` → `492a0330…`) is comments only, the module's AST unchanged |
| Not changed | the inline reader and `TAG_ATTRS`, the block start, the closing-tag rules of İ5B-FIX-18 / -19, the raw-text sets of İ5B-FIX-20, the lexer, the comment reader, every consumer policy |

## Known consequences (measured)

- The real files read the same as with İ5B-FIX-20's reader (`--check`: 9 adapters match).
- HTML relying on implicit or ancestor popping (a closing tag below the top, with an open `<p>` or an unknown element
  above the closed one) is now refused. The structures come from the WHATWG HTML parsing section and one static parse with an
  installed html5lib; no browser, renderer or model was run, and no general equivalence with a renderer is claimed.

## Limits kept open

- Not an HTML, CSS or DOM engine. C16-I1 (a diagnosis) stays deferred. P4, EMP-REPORT-01 and daily use stay open.

## Not in this decision

- Modelling HTML's tree construction (scope lists, implied end tags, foreign content), a raw-HTML ban, a new engine or
  dependency, AGENTS.md or the adapters, a live model call.
