# Evidence — İ5B-FIX-20, an opening tag's name and HTML's raw-text elements (2026-10-18)

Authority: `I5B-FIX-20-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-20-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-20` on `979abc8f7795ed8d03ffe740cc3d89a48fb8ae57` (İ5B-FIX-19), local only. The AI session is the
single writer; Codex (root) measured the real CLI on the final sources. As agreed, this is one short record; the drivers
and raw outputs stay outside the repository and are named by SHA-256 below.

## Final sources (SHA-256)

| File | What | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | an opening tag on an HTML block line needs `HTML_NAME_END` after its name, else `tag`: not verified; `RCDATA_ELEMENTS`, `RAWTEXT_ELEMENTS`, `SCRIPT_DATA_ELEMENTS` → `RAW_TEXT_ELEMENTS` (HTML's sets, source named); `UNREAD_TEXT_ELEMENTS` noscript, plaintext → `tag`: not verified. The inline reader and `TAG_ATTRS` are unchanged | `25c36d629985ebeccafd88c0ad116faab8dd33b622d5ed6797f8a78b7a112395` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_an_opening_tag_name_and_the_raw_text_elements_are_read_as_html_reads_them` (one new method); no earlier test or helper changed | `6317404c7d7da43dceb4660745387f51779187e076bf01fef06146918d769c6a` |

## Red first — the final test against C18's `sync_adapters.py` (`c9d9466b…`), before the code

`Ran 1 test in 6.052s · FAILED (failures=40)` (the new method). Red: the opening name for the 25 characters Python calls
whitespace that HTML does not (CR included), `<br` + NBSP, xmp / iframe / noembed / noframes in a block and in a
paragraph (8), noscript three ways and plaintext (4), the two AGENTS.md cases. Green on C18 too: the four earlier raw
names (controls) and every positive (ASCII / TAB / FF / LF / slash openers, the paragraph opener, the closed new raw
elements, a lone `</noscript>`).

## After — the whole budget suite and the adapter check, on the final sources

`Ran 56 tests in 63.812s · OK`; `--check`: `OK 9 adapter(s) match their sources`. Codex (root) found the 55 earlier tests
and every helper AST-equal, with one new method.

## Guard removal — the new guards and the one earlier guard this change moved, in one restored copy

Each guard undone alone in one copy restored before every mutation; the named test must fail with an assertion and a
control test (`test_a_dead_link_is_still_a_dead_link`) must pass. **10 of 10 proven.** The earlier guards untouched by
this change were not run again.

| Guard | Named test | Result |
|---|---|---|
| G-T1 an opening tag's name boundary in an HTML block not checked | the new method | FAILED (failures=27) |
| G-T2 the opening-tag name boundary checked in a paragraph too | the new method | FAILED (failures=1) |
| G-T3 noscript read as an ordinary element | the new method | FAILED (failures=3) |
| G-T4 noscript read as a raw-text element (always RAWTEXT) | the new method | FAILED (failures=3) |
| G-T5 plaintext read as an ordinary element | the new method | FAILED (failures=1) |
| G-T6 xmp not a raw-text element | the new method | FAILED (failures=3) |
| G-T7 iframe not a raw-text element | the new method | FAILED (failures=2) |
| G-T8 noembed not a raw-text element | the new method | FAILED (failures=2) |
| G-T9 noframes not a raw-text element | the new method | FAILED (failures=2) |
| G-Y8' (İ5B-FIX-16's G-Y8 on its new anchor) a raw-text element counts the tags inside it | `test_one_context_reader_for_comments_code_and_html`, the new method | FAILED (failures=20) |

## Real CLI — not run again here

As agreed, the CLI set was not widened or rerun by the writer. Red on C18: the two first C18 reports and Codex's root
records below (raw opener name 5 cases / 10 calls; raw-text elements 6 cases / 12 calls). Green on the final sources:
Codex's component record (20 cases / 40 calls, all as expected — the C18 opener cases 1 / 1, the eight standard raw-text
names 1 / 1, two noscript fixtures illustrating the two mode-dependent readings (no browser mode was run) and plaintext
1 / 1, normal / ASCII and the closed new raw elements 0 / 0). It is a
component measurement bound to the two SHA-256 above, not a run on a combined candidate or the full tree.

## Raw records (outside the repository, SHA-256)

| Record | What | SHA-256 |
|---|---|---|
| `red-01.log` | red first, the new method on C18's code | `d018424dfa192f0ee78335cf051b9d0bea360da4fcde1a4abb0e13c7d7371bc2` |
| `full-01.log` | the whole suite on the final sources | `6028d168c1b2fd4b37e62eed342d6e86772850afab3233d1bccc5968471249a8` |
| `guard/harness20.py` | guard driver (harness19c with its list replaced) | `a752dc2809702144a94e26f1b8e9940616e569dd54962c83976cbd9fdba2ae96` |
| `guard/i5b20-guard-01.json` | guard result | `8a10cf577b5f99ea88c8f87f3c8f22fd909b72bb959a649f92149fbba3cbf9a4` |
| `guard/i5b20-guard-01.log` | guard log | `5855b1a13cdb054ff57ff00dd60e0abda208ea4416fcafca05eb9d593c870021` |
| Codex C18 / v17 first report | 77, UYGUN DEĞİL (kept unchanged) | `ccb3b61ed524f2745e8d021b2c71728cca4c944b755821cd3b39cbd8cdf0be46` |
| Claude C18 / v17 first report | 72, UYGUN DEĞİL (kept unchanged) | `80dc2b4639046701a315de152931bfef4bf152aa347becf04e6d570e184cbd88` |
| `C18-own-raw-opener-name-CLI-01.json` | Codex root, red on C18 | `a4ed06306ae1165e0a55c53624724a84bc6756bcdb888c016e0d4f44f64d4a32` |
| `C18-own-rawtext-elements-CLI-01.json` | Codex root, red on C18 | `c24b9d7a6b6dca5978fe6fdc973d88b7e913864a035c5d7d75182f68f05707b9` |
| `C18-rawtext-spec-basis-01.txt` | Codex root, the specification reading | `350172120deee3de59821397e8bd96310f3697023e9a6a39efcf6e38fd5eb9cb` |
| `FIX20-own-two-families-component-01.json` | Codex root, green on the final sources | `301bc715afbc84672eb162f8e53c696edb8be057914571b1d34b8c6990c6497f` |
| `FIX20-old55-AST-interim-01.json` | Codex root, the earlier 55 tests and helpers AST-equal | `581ac4703b05446bf8ce7d5c1482f8fe5ee144d1c79935bd7b58d8ff6d40ecc7` |

The one recorded run on the combined candidate (C19) is planned after the commit; it is not a result yet.
