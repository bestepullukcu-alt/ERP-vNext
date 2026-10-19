# Evidence — İ5B-FIX-07, no container around the core, no rule inside a larger code span (2026-10-18)

Authority: `I5B-FIX-07-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-07-joint-decision-01.md`, this
branch). Branch `chore/agents/i5b-fix-07` on `5cd4cc527783880176f2f72fa1d488d92ce3aa9f` (İ5B-FIX-06), local only. The
AI session is the single writer and ran every check in this folder; Codex measured independently. The Control Tower
fixes of the same v7 audit are CT-FIX-06, whose evidence folder (`ct-fix-06/`) holds candidate 5's identity.

## What changed (final sources, SHA-256)

| File | Change | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | the text before `core:start` holds no raw HTML outside comments, fences and inline code (`CORE-HTML … before the core block`); `on_rule_line`: a required text inside a larger inline code span on its rule line is lost (`CORE-RULE … (only inside inline code)`), a short code term that is the span itself still counts | `21a57128f7b985dfe0232e848fe43f3914c9fccf761467fe4365146d59a8da2b` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_a_container_opened_before_the_core_is_refused`, `test_a_rule_inside_a_larger_code_span_is_lost`; the real-file measurement through `on_rule_line` (32 tests in all) | `6d27f0b436432fdcf7b038771063b0364d30c6babec39b64f6b3b03cc027236b` |
| `docs/records/decisions/2026-10/i5b-fix-07-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `drivers/harness3.py.txt` | in the commit |

`AGENTS.md`, the core and the adapters are unchanged: the core's source digest (the text between the `AGENTS.md` markers,
`strip()` and one final LF) is `d207e869f1670c9a54537c6fc8462bf241dc74f885bf82f4ed2c6b8b06696819`; the generated
`.claude/rules/agents-core.md` file is `1404d034c4206dcf3127b87fcb3e9e6d6dd00b862bd604ce20ca2c35e42ad5a7` — both as in candidate 4.

## The cases (the real adapter check on a copy of `AGENTS.md`)

| Form | Before (`5cd4cc527`) | Now |
|---|---|---|
| `<pre>`, `<details>` or `<div hidden="">` before `core:start`, closed after `core:end` | write 0, check 0 | `CORE-HTML … before the core block` |
| HTML after `core:end`; a tag in inline code or in a fence before the core | passes | passes (the document after the core is free; an example is not markup) |
| every numbered rule body inside one five-backtick span on its own line | write 0, check 0 | every required text `CORE-RULE … (only inside inline code)` |
| short code terms (`` `TenantId` ``, `` `verify_module_id.py` ``, a text carrying its own short spans) | count | count |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — this package's tests against `5cd4cc527`'s sources | the container test (3 forms) and the larger-span test fail; the real-file measurement errors (25 subtests: the base has no `on_rule_line`); every earlier test green |
| Green | 32 / 32; `sync_adapters.py --check` "OK 9 adapter(s) match their sources" |
| Guard removal (`guard-removal.txt`) | 3 / 3 proven: HTML before `core:start` not checked; a required text inside a larger span counts; a short code term no longer counts (the real file then fails) |
| The real core | 0 raw HTML tags before `core:start`; every one of the 25 required texts on a numbered rule line and not inside a larger span |
| Candidate 5 (`89eab8a450c6298dbcdcee1b33ec00761f581b88`, see `ct-fix-06/` on the Control Tower line) | these 2 files and CT-FIX-06's 5 over candidate 4. On its sparse copy: the first run 165 tests with 1 failure and 4 adapter problems (the set lacked `.codex/` and `.agents/`); the second run, with a full log, `--check` OK 9 and 164 / 165 — the one failure the Cross-DB gate, which cannot scan without `services/` (it passed on Codex's full copy of candidate 5). The adapter tests passed in both runs; details in `ct-fix-06/c5-python-runs.txt` |

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The fixed core rules on a consistent copy of the production script (`21a57128…`): normal short code terms, an outer `<pre>`, every body in code, item 7's body in code, an outer fence, a real `<br>` after the core, a quoted `<pre>` before it — each as a write / check pair | 14 / 14 | `02fe1ba456fbf3e276e48a8f74f4351cde05e6f0f79b38a31ff0ec95317a53b3` |

## Limits

- The format checks are minimums: they check where a required text stands, not that a model reads or follows it.
- The loading pair and the blocked Employee run are not repeated: the core's content is unchanged.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
