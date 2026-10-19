# Evidence — İ5B-FIX-15, raw HTML around a required heading, section or anchor hides it (2026-10-18)

Authority: `I5B-FIX-15-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-15-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-15` on `1f7858f352b96275e664ba1697dc7281a11d9cb8` (İ5B-FIX-14), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `scan_comments(…, html_blocks=True)` with `html_block_start` (CommonMark's HTML block starts 1, 3–7 and their ends); `mask_html` (elements to their own closing tag, nesting counted, outside fences, inline code and escapes; a tag it cannot read hides the rest); `visible_text` keeps a character only when neither hides it; `unreadable_html` writes "… the check cannot read — not verified" on a lost section, anchor or heading. The behaviour is prototype 05's (`e0b00a7d…`): the AST is equal apart from docstrings and one unused variable | `31aaf12a2c33a5cc70014f56d41fdb6669898daf970175552d25bc4de60e414f` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_raw_html_around_a_required_text_hides_it` (20 cases), `test_unrelated_or_shown_html_keeps_the_required_text` (20 cases), `test_an_html_opener_the_check_cannot_read_is_not_verified` (3 cases); no earlier test changed | `5e4789e030a101f68ad83d00acc4bbec5be8ec9aed921e8dbf08c9726bdf0e81` |
| `docs/records/decisions/2026-10/i5b-fix-15-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-cli.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters and the other instruction inputs are not changed. The core's
raw-HTML rule, its comment reader, `outside_code`, the link rules and `known_spans` are unchanged.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `1f7858f35`'s `sync_adapters.py` | 23 cases fail: 20 hidden and 3 unreadable. The positives test passes there (the old reader kept every unrelated case), and the 45 earlier tests pass |
| Real CLI (`real-cli.txt`) — the same 42 cases, write and `--check`, before and after | before (`e5b55f88…`): every hidden or unreadable case 0 / 0, 19 of 42 as expected. After (`31aaf12a…`): 42 of 42 as expected. One work copy, restored before every case |
| Guard removal (`guard-removal.txt`, `drivers/harness15.py.txt`) | 14 / 14 proven in one restored copy: G-X1 … G-X14 (no HTML mask, blocks or elements ignored, a block ending with its first line, nesting, `<x/>`, a paragraph end, the raw-text rule, tags searched in fences / inline code / escapes, an unreadable tag skipped, no "not verified", an autolink as an unreadable tag). Run 01 was 12 / 14 because of two harness mistakes, written there. Run 03: İ5B-FIX-14's G-H1, whose anchor this package changed, proven again on its new anchor (`drivers/harness15-gh1.py.txt`); the other 24 İ5B-FIX-14 anchors are in the final source once each |
| The narrow run (`checks.txt`) | on **this branch's two frozen files**: `test_sync_adapters_budget` 48 / 48; `--check` OK, 9 adapters; source identity the same before and after |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** (the agreed narrow check; no new live call) |

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The counter-examples before this fix (history) | C13-F1 and its family: a required section inside a script, a `<div>` heading, an anchor in a script or `<span hidden>`; then prototype 02's two gaps (a script holding `</pre>`, a context reset at the paragraph end), prototype 03's two (`<div>example</div>` before the heading, `<script/>`), prototype 04's one (`<script data-example="<">`). Each 0 / 0 where 1 / 1 was expected | in Codex's files `root-C13-reference-context-boundary-02.json`, `root-proto02-opaque-context-4.json`, `root-proto03-block-boundary-4.json`, `root-proto04-unreadable-tag-2.json` |
| **Prototype 05** (`e0b00a7d…`), 122 real production CLI — measured on the prototype's source, not rerun on the final source; the final source is bound to it by the AST (equal apart from docstrings and the unused `j`) | all as expected: the 36 reference / HTML counter-examples and controls, the earlier 36 context and 50 metadata / escape cases; one fixture in turn, no WARN, every input under 22000 bytes; the source and the other 17 instruction inputs unchanged | `9915405201d97e2a2574f250ec72f919655ca656f462da889242e36d1c13cbf2` |

## Limits

- Not an HTML, CSS or Markdown engine. Any text inside an element is not evidence: an attribute is never read.
- A bare `a<b c` before a section is refused (1 / 1), but its message says only "missing": the tag's attributes may
  cross a blank line here, unlike CommonMark. A limited diagnosis gap, recorded, not reworked.
- The check proves where a required text and a heading stand in the source; it does not prove that a reader follows the
  rules. P4 and EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
