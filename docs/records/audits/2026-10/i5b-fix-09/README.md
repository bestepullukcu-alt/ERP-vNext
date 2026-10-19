# Evidence — İ5B-FIX-09, the raw-HTML opener class and the one-token short-term exception (2026-10-18)

Authority: `I5B-FIX-09-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-09-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-09` on `f829ac5fd4319b5d54cdedafa1e53b2b4dd8f9c8` (İ5B-FIX-08), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `.antigravity/scripts/sync_adapters.py` | `RAW_HTML_OPENER` replaces `RAW_HTML_TAG`: an open tag `<x`, a closing tag `</x`, a comment `<!--`, a declaration `<!X`, CDATA `<![CDATA[`, a processing instruction `<?` — in the core block and before it; the short-term exception of `on_rule_line` holds only for one token without whitespace | `c236273aa24ca009011fb53eeed355f6f6bd8ef90cf6eb45960c9929aeba8552` |
| `scripts/tests/test_sync_adapters_budget.py` | `test_the_raw_html_opener_class_is_refused_before_and_in_the_core`, `test_a_phrase_that_is_a_span_of_its_own_is_lost_and_short_terms_still_count` | `2bc3f9ea3b1856c9c17a345069692e038257489f1c8c2c9f727640c0d2750d41` |
| `docs/records/decisions/2026-10/i5b-fix-09-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

AGENTS.md, the core, `adapters.json` and the generated adapters are not changed; every required text still counts.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `f829ac5fd`'s `sync_adapters.py` | the 2 new tests fail (8 opener forms before and in the core, 1 phrase); the 33 earlier tests pass |
| Guard removal (`guard-removal.txt`, `drivers/harness7.py.txt`) | 2 / 2 proven on a copy whose `sync_adapters.py` equals the final source byte for byte |
| The final run (`checks.txt`) | in **the İ5B-FIX-09 worktree**: `test_sync_adapters_budget` 35 / 35; `sync_adapters.py --check` OK, 9 adapters |
| Architecture, live model calls | **NOT RUN**; no new live call (joint decision: the instruction inputs are byte-equal to C7's) |

**What the final run is and is not.** The İ5B run covers this branch's tree. The full `scripts/tests` run of CT-FIX-08 ran
on the CT branch's tree (`docs/records/audits/2026-10/ct-fix-08/checks.txt`). The two are not one run on a combined
candidate, and neither is presented as a C8 run.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| İ5B-09 interim, bound to `sync_adapters.py` `c236273a…` | 34 / 34 real CLI write / check (17 HTML family, 7 prefix / normal, 10 inline / normal) | `23a48118c08546c6ed44ba53353a597b6f5e98b14570b336167044187c2d0ed1` |
| İ5B-09 source snapshot | the writer source equal to the snapshot; AGENTS, core and manifest equal to C7's | `f71df3a2cd80f6f9cebbfa8b5b991395bb72d80c33835add4245171a6159391a` |
| Pre-commit source binding | 27 CT CLI + 34 İ5B CLI; 30 API apart | `05d4aea44de53924db8982fc49c701da41e09025f901a2efc83478018b55a438` |
| Instruction inputs | every manifest source and generated target plus AGENTS, CLAUDE, the reference and the manifest (17 files) byte-equal to C7 | `cd9c16616115dee445907d5f9d20ba6ed036c575227a19cff217428fb1ee3b20` |

## Limits

- The check reads raw HTML before and in the core; after the core the document stays free (unchanged).
- The rule proves that a required text is on its numbered rule line outside HTML and code. It does not prove that a
  reader follows the rule. No claim is made that a C8 Employee run or P4 improved.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
