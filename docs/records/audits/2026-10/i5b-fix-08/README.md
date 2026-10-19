# Evidence — İ5B-FIX-08, the module-request answer contract (2026-10-18)

Authority: `I5B-FIX-08-JOINT-DECISION-01` (`docs/records/decisions/2026-10/i5b-fix-08-joint-decision-01.md`). Branch
`chore/agents/i5b-fix-08` on `60878562a1b671e4b8c7af0d4d392ee720a20eb5` (İ5B-FIX-07), local only. Written and checked by
the AI session; Codex measured the byte count of the agreed text independently before it was applied.

| File | Change | SHA-256 |
|---|---|---|
| `AGENTS.md` | core item 7, the module-request sentence (B9); 21,993 bytes | `c8d380a421aacb07db50a8c2f5154558167c094d45e802751d43ca61185b6313` |
| `.claude/rules/agents-core.md` | regenerated; source digest `abb98eaa05b5289f487935685c431e16595258d4b6efd515950721ea0e450085` | `4b3b99a93c29e8b0d72e2df9b9489fa840b722f236c596446cfef9cd76a27241` |
| `.antigravity/adapters.json` | five required texts added after the module-request anchor (30 in all); nothing removed | `8fc59a01e2e332b6a4f32832791641e7066c640a7a95ab4772d5b6ca178e84d1` |
| `scripts/tests/test_sync_adapters_budget.py` | the contract parts in the item-7 test and in the manifest; `test_each_contract_part_removed_or_hidden_in_a_comment_is_lost` (33 tests) | `14a461b79a92a53004d2478627be95a439d8a8456ea855d77e5fdafc6f9d5451` |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — against `60878562a`'s AGENTS.md, core and manifest | 15 failed subtests in the two contract tests; every other test green |
| Green | 33 / 33 and `sync_adapters.py --check` "OK 9 adapter(s) match their sources" (the AI session); Codex's own run of the same 33 in `checks.txt` |
| The production command line (`cli-checks.txt`) | each of the five parts removed or hidden in an HTML comment: the write refused, `--check` exit 1 with one CORE-RULE line naming it — 10 / 10; the normal copy passes |
| Guard removal (`guard-removal.txt`) | 2 / 2: one, then all five new required texts removed from the manifest — the named tests red, the control green |
| Codex, independently (its file holds local paths; SHA-256 only) | 22 / 22 through the real CLI: 5 removals, 5 comments, 5 larger code spans refused; each manifest protection removed → 5 cases 0; 2 normal 0 — `cc3844d4dce328840dd717ffb2cc60c5d9aeae8f7868304de54f6c35b689dacd`. Not model-behaviour evidence |

## Limits

- Folder: `red-first.txt`, `guard-removal.txt`, `cli-checks.txt`, `checks.txt`.
- Source protection only: whether a session gives one first scope and every open decision is a new pilot's question.
- The core changed: the earlier load05 pair (on `d207e869f167…`) and the C6 normal pilot do not measure this text; no new
  loading pair was run on C6.
- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed.
