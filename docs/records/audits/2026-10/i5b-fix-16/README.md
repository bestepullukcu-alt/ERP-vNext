# Evidence — İ5B-FIX-16, one context for comments, code and HTML (2026-10-18)

Authority: `I5B-FIX-16-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-16-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-16` on `1199d23a8630b478ab88063d87e42d744987f89d` (İ5B-FIX-15), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | the shared lexer (`paragraph_limits`, `code_span_end`, `code_spans`, `mask_code`, `html_block_end`); `scan_comments` rebuilt on it — block and inline comments, HTML blocks read raw, its info (`blocks`, `not_verified`); `mask_html` one left-to-right reader with a stack and raw-text elements; `visible_text`, `unreadable_html`, `html_scan`, the rule-line readers and CORE-SPAN on the same lexer; `INLINE_CODE` and `ESCAPED_LT` removed | `93a51469cd13b434ff982fe013119488fdedbb6283ff9e4189507aa7084c9dfb` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_one_context_reader_for_comments_code_and_html`, `test_the_same_reader_holds_for_agents_md_and_the_core`, `test_the_rule_line_readers_use_the_same_lexer`, `test_what_the_reader_cannot_read_is_said_to_be_not_verified`; no earlier test changed | `e6a6939b206bcbe8a9348784be1976d3ba8a1c8d816dcf32ccaceaaa813a6641` |
| `docs/records/decisions/2026-10/i5b-fix-16-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `real-cli.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json`, the generated adapters, the CT tools and the other instruction inputs are not
changed. `outside_code` (imports), the link rules and `known_spans` are unchanged.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against C14's `sync_adapters.py` | 23 cases fail, all in the four new tests (hidden cases at 0 / 0, positives refused, a phrase in a real code span read as stated, the `List<T>` message); the 48 earlier tests pass |
| Real CLI (`real-cli.txt`) — 73 cases, write and `--check`, before and after | before (`31aaf12a…`) 53 of 73 as expected: the 42 earlier cases all as expected, 20 of this package's wrong. After (`93a51469…`) 73 of 73. One work copy, restored before every case |
| Guard removal (`guard-removal.txt`) | **58 of 58 proven**, counted once each: 18 new (G-Y0 … G-Y17; G-Y0 the cursor hang, red as a timeout under a 60 s limit, its control the same test on the intact copy), the 21 earlier guards whose code this package rewrote, ported to their new anchors with their own named tests, and the 19 earlier guards whose anchors are unchanged, again on the final source. Misses on the way are kept and explained there |
| The narrow run (`checks.txt`) | on **this branch's two frozen files**: `test_sync_adapters_budget` 52 / 52; `--check` OK, 9 adapters; source identity the same before and after. The real files read the same as with C14's reader |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** (the agreed narrow check; no new live call) |

## Two interim errors, recorded apart

- **A product defect in an interim source, fixed before freezing.** `b5afb5b1…` (the first build of this package)
  did not end on a blank line: at a line's end the cursor went back to the start of that blank line. Codex found it
  with a 3-second limit; the AI session's own test run had stalled on it and was stopped through the session's task
  control. The cursor now goes one past a line only when the scan stopped at its end, and goes on from a later line
  when a code span or comment ran into it. G-Y0 puts the old cursor back and proves the test hangs.
- **A harness error, not a product defect.** Measuring that fix, the AI session's first probe used Python
  `multiprocessing` from a script read on standard input; on macOS the child cannot re-import such a script
  (`FileNotFoundError: <stdin>`), so the probe itself hung. It was stopped through the task control, and the measure
  was repeated with `subprocess.run(…, timeout=5)`: the short case `abc\n\nX` ends.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 / file |
|---|---|---|
| The C14 / v13 first reports (history) | Codex 77 / 100, UYGUN DEĞİL 19/1/0; Claude 70 / 100, UYGUN DEĞİL 18/1/1; the same criterion 02 | `58f664010af46c32550d28320da488177541c442ffd0cad3458e266ff9a62c47`, `6649710de87612068eb79f2830a648ff1e04978c06e30fb24034478979c0d325` |
| Renders and CLI before this fix (history) | N1–N3 rendered (N1 visible, N2 / N3 hidden), N1c 6 real CLI, N4 / N4c rendered, the rule-line function counter-example | `C14-root-N123-render-01.json`, `C14-root-N1c-CLI-01.json`, `C14-root-N4-render-01.json`, `C14-root-required-rule-inline-01.json` |
| The interim cursor loop | `scan_comments("abc\n\nX")` on `b5afb5b1…` did not end in 3 s | `I5B16-interim-blankline-loop-01.json` |
| An interim source `836f98b1…` | its 21 scenarios / 42 real CLI all as expected (24 calls / 12 scenarios wrong on C14) | `I5B16-interim-independent-02.json`, `I5B16-interim-root-context-02.json` |
| **The frozen source `93a51469…`** | its 42 real CLI all as expected; the 17 instruction inputs byte-equal to C14; the 48 earlier test methods and helpers AST-equal (on test `164b44c8…`; the final test's three added cases are bound at the final review) | `I5B16-frozen-source-independent-03.json`, `I5B16-frozen-root-context-03.json` |

## Limits

- Not an HTML, CSS or Markdown engine; any text inside an element is not evidence, and an element ends only at its own
  closing tag (a browser may end some earlier: the model refuses more).
- A block quote or list line between a backtick run and its pair, and a comment left open in an HTML block, are not
  read: not verified. A bare `a<b c` is refused as a tag the check cannot read.
- The check proves where a required text and a heading stand in the source; it does not prove that a reader follows the
  rules. P4 and EMP-REPORT-01 stay open. GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
