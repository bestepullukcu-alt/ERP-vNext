# Evidence — CT-FIX-11, a governance record's row-bound relation is "not measured" when the queue cannot be read (2026-10-18)

Authority: `CT-FIX-11-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-11-joint-decision-01.md`). Branch
`chore/ct/ct-fix-11` on `0afba1bf588f09bf9bdf28d9b140747bad4a13a1` (CT-FIX-10), local only. The AI session is the single
writer and ran every check in this folder; Codex measured independently (listed by SHA-256 at the end).

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/prompt_gate.py` | `check_authority`, the governance branch: when `governance_binding` finds no binding, the row is unknown (`row_known` False) and the record has no `work_packages` field (`work_packages_field` gives `(None, None)`), the reason is "kuyruk okunamadı — kapsam kaydının bu işe bağı ölçülemedi". An explicit or a broken field still decides through `governance_binding`, unchanged | `d74b5e56cd55275cb7f620871898cec97b457ef75c70a3d41d369b08a82fbcb8` |
| `scripts/tests/test_ct_cli_regression.py` | `CTFIX11_GovernanceUnknownRow` (1 test, 7 cases) | `a49c5ab13447b30319a79a99ea2c802817ca6da0b5144b433c78a83ec811b5c2` |
| `docs/records/decisions/2026-10/ct-fix-11-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `checks.txt`, `drivers/` | in the commit |

The shared `governance_binding` and `scripts/ct_queue.py` are unchanged.

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the final test file against `0afba1bf5`'s `prompt_gate.py` | 2 cases fail: a row-bound record, approved and draft, with the queue unreadable (the false record-column reason, no "ölçülemedi"). The explicit W1, W10 and broken-field cases and the intact-queue controls pass there; Controls.* pass |
| Guard removal (`guard-removal.txt`, `drivers/harness14.py.txt`) | 3 / 3 proven: G-V1 the unmeasured relation not written, G-V2 an explicit or a broken field ignored without the row, G-V3 the queue error ignored (a known row marked unmeasured too) |
| The narrow component run (`checks.txt`) | on **the CT branch's two frozen files**: the `ct_queue` and `prompt_gate` self-tests (141, 105) and 18 tests (CTFIX11, CTFIX10, CTFIX09, CTFIX04_I2, CTFIX04_I3, Controls), all OK; source identity the same before and after |
| Full `scripts/tests`, architecture, live model calls | **NOT RUN** (the agreed narrow check; no new live call) |

**What the run is and is not.** It is a narrow run on this branch's two files, not a full `scripts/tests` run and not
a combined-candidate run.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| C13-G1 triage (history: before this fix), 12 real gate CLI | an intact row-bound record 0 before and after; with the queue unreadable the false "record sütunu … göstermiyor" for a row-bound record; the draft and the explicit W10 refusals right on their own; the report hashes right, the source unchanged | `4ae9f4297b789b55896270a6a4bbe123c34e3d990cc63a40b39c186d3d98523a` |
| **Final:** 12 own real gate CLI on the CT-FIX-11 source snapshot | the false record reason gone; an intact queue 0 before and after; a broken queue 1; the draft and the explicit W10 refusals the same; the report hashes right. In the 16-file snapshot only `prompt_gate.py` (`d74b5e56…`) and its test (`a49c5ab1…`) differ | `c371fb12e8ec4ba1ed0e652a33c6ed1d6d5561089a1cd739072bf7fe64a19450` |
| **Final:** the full component review | its own 12 CLI, the raw 18-test run and the self-tests (141, 105), the three named mutations with their control, and the copies' 16 sources equal to the frozen ones — all verified | `e7baebd13932332815f1ab3aadc6b671c67347e12a565712bdd9bb2d4ad73c69` |

## Limits

- Only the row-bound relation is marked "ölçülemedi". The other governance checks are unchanged.
- GitHub CI on these sources has not run (NOT VERIFIED). Nothing is pushed.
