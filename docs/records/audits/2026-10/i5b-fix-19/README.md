# Evidence — İ5B-FIX-19, where a tag name ends and where an HTML block starts (2026-10-18)

Authority: `I5B-FIX-19-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-19-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-19` on `140bc6374958177ccb26c34ff8d0078bd8642504` (İ5B-FIX-18), local only. The AI session is the
single writer and ran every check here. As agreed, this is one readable record; the drivers and raw outputs stay outside
the repository and are named by SHA-256 below.

## Final sources (SHA-256)

| File | What | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `HTML_WS` / `HTML_NAME_END` (what ends a tag name in HTML's tokenizer: TAB, LF, FF, SPACE, `/`, `>`) and its check on an HTML block's closing tag; `MD_SPACE` (CommonMark's start-condition whitespace) in the type-1 and type-6 start lines; a strict type-7 start line (`TYPE7_NAME`, `TYPE7_ATTR`: CommonMark's complete open or closing tag, ASCII SPACE / TAB only, an opening tag not pre / script / style / textarea). The inline reader and `TAG_ATTRS` are unchanged | `c9d9466b9415edb0b5ed34ee7babbec1aa508185226ac5b1ef5f8692030449c5` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it` (one new method); no earlier test changed | `4aecdd4f3bc8ef075d3e87524a84d7d7d2f5e969b76d2c71f8f971bfd82ebebc` |

## Red first — the test against C17's `sync_adapters.py` (`54c647a2…`), before the code, in one restored copy

The new method grew as the agreed scope grew (each step a case the auditors or Codex's root had shown); every run is kept.

| Run | Test | Added | Result |
|---|---|---|---|
| 01 | `65c70ef3…` | the first test (NBSP, VT, U+3000, script, CR, AGENTS) | Ran 55 tests in 38.778s · FAILED (failures=6) |
| 02 | `54104d6c…` | + the whitespace class for the closing name | Ran 55 tests in 40.590s · FAILED (failures=31) |
| 03 | `fe68f70d…` | + Codex's three type-7 cases and two raw positives | Ran 55 tests in 40.279s · FAILED (failures=34) |
| 04 | `6720b5fb…` | + the start whitespace class for types 1, 6, 7 (the new method only) | Ran 1 test in 11.586s · FAILED (failures=112) |
| 05 | `de8387e8…` | + a type-7 attribute after NBSP | Ran 55 tests in 47.758s · FAILED (failures=113) |
| 06 | `e45b1c76…` | + a closing '</script>' type-7 positive (the exception is for opening tags only) | Ran 55 tests in 48.689s · FAILED (failures=113) |
| 07 | `4aecdd4f…` | + the closing start lines of types 6 and 7 followed by a line (final) | Ran 55 tests in 47.899s · FAILED (failures=113) |

In every full run all failures are in the new method and the 54 earlier tests pass. The final run (07): 113 cases red —
the closing name for the 25 characters Python calls whitespace that HTML does not (CR included) and the named NBSP /
VT / U+3000 / script / AGENTS cases; the start whitespace class for types 1, 6 and 7 (26 characters each, FF included);
Codex's `<x !>`, `<x>`+NBSP and `<script/>`; a type-7 attribute after NBSP. The raw and ASCII positives pass on C17 too.
The closing start lines of types 6 and 7 (run 07) also pass on C17 — İ5B-FIX-18 already made those arms ASCII; they are
there to show those arms (guards G-Q6, G-Q7').

## Real CLI — 25 cases, write and `--check`, each in one copy restored before every case

before C17 (`54c647a2…`) 12 of 25 as expected; after (`c9d9466b…`) 25 of 25. One AGENTS.md case passes 22000 bytes and prints WARN.

| Case | Expected | C17 | Final |
|---|---|---|---|
| normal | 0/0 | 0/0 | 0/0 |
| hidden: '</div'+NBSP+'>' on an HTML block line | nonzero | 0/0 | 1/1 |
| hidden: '</div'+VT+'>' on an HTML block line | nonzero | 0/0 | 1/1 |
| hidden: '</div'+U+3000+'>' on an HTML block line | nonzero | 0/0 | 1/1 |
| hidden: '</script'+NBSP+'>' in a block inside an inline script | nonzero | 0/0 | 1/1 |
| hidden: an AGENTS.md heading after '</div'+NBSP+'>' on a block line | nonzero | 0/0 | 1/1 |
| declared: a lone CR after the name | nonzero | 0/0 | 1/1 |
| hidden: type 6 '<p'+NBSP+'>' starts no block | nonzero | 0/0 | 1/1 |
| hidden: type 1 '<script'+NBSP+'>' starts no block | nonzero | 0/0 | 1/1 |
| hidden: type 7 '<x'+NBSP+'>' starts no block | nonzero | 0/0 | 1/1 |
| hidden: Codex '<x !>' is no complete open tag | nonzero | 0/0 | 1/1 |
| hidden: Codex '<x>'+NBSP after the tag | nonzero | 0/0 | 1/1 |
| hidden: Codex '<script/>' starts no type-7 block | nonzero | 0/0 | 1/1 |
| hidden: type 7 attribute after NBSP | nonzero | 0/0 | 1/1 |
| hidden: type 6 closing start line with NBSP | nonzero | 1/1 | 1/1 |
| hidden: type 7 closing start line with NBSP | nonzero | 1/1 | 1/1 |
| passes: '</div x>' on a block line | 0/0 | 0/0 | 0/0 |
| passes: '</div >' on a block line | 0/0 | 0/0 | 0/0 |
| passes: '</div'+FF+'>' on a block line | 0/0 | 0/0 | 0/0 |
| passes: '</div/>' on a block line | 0/0 | 0/0 | 0/0 |
| passes: '</div'+NBSP+'>' in a paragraph with nothing open | 0/0 | 0/0 | 0/0 |
| passes: '<p>' starts a type-6 block | 0/0 | 0/0 | 0/0 |
| passes: '<script>' starts a type-1 block | 0/0 | 0/0 | 0/0 |
| passes: '<x title="ok">' starts a type-7 block | 0/0 | 0/0 | 0/0 |
| passes: '</script>' closing line starts a type-7 block | 0/0 | 0/0 | 0/0 |

## Guard removal — the new guards and the earlier ones on the block start and the closing branch, in one restored copy

Run 01 was stopped when the source changed (opening-only exception); it counts for nothing. Run 02, on the final source
with test `e45b1c76…`: **18 of 20**; G-Q6 and G-Q7' stayed green.

| Mutation | Named test | Named | Control | Proven |
|---|---|---|---|---|
| G-W1 a closing tag's name boundary in an HTML block not checked | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=31) | OK | True |
| G-W2 any character Python calls whitespace ends the name | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=31) | OK | True |
| G-W3 FF does not end a tag name | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=1) | OK | True |
| G-W4 a type-1 start takes any Unicode whitespace | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=26) | OK | True |
| G-W5 a type-6 opening start takes any Unicode whitespace | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=26) | OK | True |
| G-W6 a type-7 line takes any Unicode whitespace after the tag | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=1) | OK | True |
| G-W7 a type-7 line may be pre / script / style / textarea | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=1) | OK | True |
| G-W8 a type-7 line's attributes read with the inline TAG_ATTRS | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=54) | OK | True |
| G-W9 a type-7 attribute may follow any Unicode whitespace | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=1) | OK | True |
| G-Q7' (İ5B-FIX-18's G-Q7 on its new anchor) a type-7 closing line takes any Unicode whitespace before '>' | test_a_closing_tag_has_the_commonmark_form | green — OK | OK | False |
| G-W10 the raw-name exception applied to a type-7 closing line too | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it | RED — FAILED (failures=1) | OK | True |
| G-Z9' (İ5B-FIX-17's G-Z9 on its new anchor) a '>' inside a quoted value ends a type-7 start line's tag | test_a_tag_ends_outside_quotes_and_a_close_needs_evidence | RED — FAILED (failures=1) | OK | True |
| G-Q1 a closing tag of any form closes in a paragraph | test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=9) | OK | True |
| G-Q2 a malformed closing token is skipped whole | test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=1) | OK | True |
| G-Q3 the closing form is required in an HTML block too | test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=2) | OK | True |
| G-Q4 no line end allowed in a closing tag | test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=1) | OK | True |
| G-Q6 an HTML block's closing start arm takes any Unicode whitespace | test_a_closing_tag_has_the_commonmark_form | green — OK | OK | False |
| G-Y6 HTML block lines read as Markdown | test_one_context_reader_for_comments_code_and_html | RED — FAILED (failures=2) | OK | True |
| G-X4' an HTML block ends with its first line | test_raw_html_around_a_required_text_hides_it | RED — FAILED (failures=1) | OK | True |
| G-X2 the HTML blocks ignored (elements only) | test_raw_html_around_a_required_text_hides_it | RED — FAILED (failures=1) | OK | True |

**G-Q6 and G-Q7' — a real test gap, not a redundant mutant.** These two arms decide whether a closing tag line starts an
HTML block, so whether the *next* line is read raw. İ5B-FIX-18's cases had no next line, and this package's raw-name
check refuses the closing tag itself anyway, so undoing the arms showed nothing. With a next line holding a backtick
`</div>`, a raw block lets that `</div>` close the div. Two subtests show that (the same rule, not a new requirement);
the test became `4aecdd4f…`, the code stayed `c9d9466b…`. Run 03, only these two, on the final pair:

| Mutation | Named test | Named | Control | Proven |
|---|---|---|---|---|
| G-Q7' (İ5B-FIX-18's G-Q7 on its new anchor) a type-7 closing line takes any Unicode whitespace before '>' | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it, test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=2) | OK | True |
| G-Q6 an HTML block's closing start arm takes any Unicode whitespace | test_a_closing_tag_name_in_an_html_block_ends_where_html_ends_it, test_a_closing_tag_has_the_commonmark_form | RED — FAILED (failures=1) | OK | True |

The other 18 were not run again. Run 02 stays as it was: 18 of 20, with G-Q6 and G-Q7' not proven on test `e45b1c76…`. Run 03 is a separate record: those two proven on the final test `4aecdd4f…`, after two subtests were added for them. The earlier guards untouched by this change were not run again.

## The real files — C17's reader against this one

- AGENTS.md: visible text equal True · level-2 sections equal True · html blocks equal True · diagnosis ''
- CLAUDE.md: visible text equal True · level-2 sections equal True · html blocks equal True · diagnosis ''
- .antigravity/AGENTS-REFERENCE.md: visible text equal True · level-2 sections equal True · html blocks equal True · diagnosis ''
- core view equal True · rule-line decisions equal True
- check_instruction_budget on the real files: ([], [])

## Not run here

- Planned after the commit: one recorded run on the combined candidate (C18) — 55 budget tests and `--check` 9. CT is
  inherited from earlier candidates on the same blobs.
- Full `scripts/tests`, architecture, a live model call, CI: not run. Not an HTML, CSS or Markdown engine. P4,
  EMP-REPORT-01 and daily use / token benefit stay NOT VERIFIED; this source check does not close them.

## Codex, independently (by name)

- The C17 / v16 first reports: Codex 79 / 100 UYGUN 20/0/0 (`a81c2579…`), Claude 72 / 100 UYGUN DEĞİL 18/1/1
  (`32c83f27…`, C17-F1). Codex's root reproductions: `C17-own-raw-close-boundary-CLI-01.json`,
  `C17-own-type7-block-classifier-CLI-04.json`, `C17-own-Unicode-block-start-families-CLI-03.json`; the spec basis
  `C17-raw-close-spec-basis-01.txt`.
- The final component, code `c9d9466b…` with test `e45b1c76…`: 16 scenarios / 32 real CLI all as expected, the 54
  earlier tests and helpers AST-equal (`FIX19-own-known-boundaries-final-component-01.json`). The final test
  `4aecdd4f…` adds the two subtests above; its difference is for Codex's review.

## Drivers and raw outputs (outside the repository)

| File | What | SHA-256 |
|---|---|---|
| `guard/harness19b.py` | run 02 driver | `7ab09bf4c7db055fbd5f2a2e7545528b0ad32c1ad7b7c04dd04c431246e8fff1` |
| `guard/harness19c.py` | run 03 driver | `3c48d6cbf9e0cf0ccb4af6b77b7ea190235b2af0f8273cb6f699b27ceef63841` |
| `guard/i5b19-guard-02.json` | run 02 result | `8f13f9b8005236dbbe82423361289a5df05bfaa0836548fad27449172c98539b` |
| `guard/i5b19-guard-03.json` | run 03 result | `dd23a39a39086e6adcbc1685fa5333f8af9cd82502a7a8489ca255ffd4175d46` |
| `red-i5b19-pre-07.txt` | final red-first log | `1ec60eb3027fe7620614a134da72e599505a01772e718e8f916f534a57866b84` |
| `real-data-03.txt` | real-file comparison | `824f78a7d655d791b3ddd1f4dea282e7ba3f66206a99266d3d07aa047c737888` |
| `probe_i5b19.py` | real-CLI driver | `1c86d185a8ad2ef45cfc0429d0a4880a47d676e06eef167927be426de4e232aa` |
| `probe-i5b19-before-01.json` | real CLI on C17 | `b64cc589025d8a1633d656f75c60bbdfc8eb202f1a0f74b1301ee5ab5efa540e` |
| `probe-i5b19-final-01.json` | real CLI on the final source | `57fa4c381b27e04027fc14f93643b898d87ff1a06da4dfefb5b2a841cf62e1f6` |
