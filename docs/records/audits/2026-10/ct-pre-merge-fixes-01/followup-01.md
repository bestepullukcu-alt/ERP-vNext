# Evidence — step 1, follow-up 01 (2026-10-15)

Owner-approved narrow follow-up to step 1, after an independent review (Codex, 2026-10-15) found two behaviour defects
in 47059c694. The step-1 commit is not changed; this follow-up is a new commit on top of it. `README.md` and
`checks.txt` in this folder describe 47059c694 and are not edited. Details are in `checks-followup-01.txt`.

## The two defects

| # | Defect in 47059c694 | Fix |
|---|---|---|
| 1 | `known_failures.py`: when a red list-type test's message lost its list (e.g. an exception before the violations were collected), every recorded item counted as "gone", giving KISMEN-DÜZELDİ and exit 0 | A red list-type failure with no items gives YENİ-AYRINTI ("liste biçimi kayboldu"), exit 1. KISMEN-DÜZELDİ requires a non-empty proper subset of the recorded items and no new item |
| 2 | `ct_queue.py`: the in-force check accepted any text that had a `queue_id` header and the queue's id, even an incomplete registry, and did not check which task source the row named | `in_force_status` returns `kayıtlı` only when all of these hold:<br>• the registry header equals the registry contract;<br>• every row has the contract's width;<br>• the queue appears exactly once;<br>• every field of its row is filled and valid under the registry checks (confirmation and strict values, the baseline commit exists, the task source file exists at it);<br>• the row names the same task source file.<br>Otherwise it returns one of `kayıt dosyası yok`, `kayıtlı değil`, `kayıt geçersiz` (with the reason) or `farklı görev kaynağı`. The checked `origin/main` SHA is printed in full |

Both defects reproduce on 47059c694's code and are fixed on the follow-up code: the same inputs go from KISMEN-DÜZELDİ /
exit 0 to YENİ-AYRINTI / exit 1, and from `True` to `kayıt geçersiz`. Self-tests now include both as sabotage cases
(`known_failures.py` 24/24, `ct_queue.py` 33/33).

## Run 3 — source identity

The code was pinned before the run and was unchanged after it:

| Item | Value |
|---|---|
| Base commit | 47059c69455a5f3c619a2d488553a254d7e14cc7 |
| Changed files | `scripts/known_failures.py`, `scripts/ct_queue.py` (nothing else) |
| `scripts/known_failures.py` sha256 | `55d73dd5321e4fb0bacbf9adf08091f002d3fd6007740529472ccecea17446fe` |
| `scripts/ct_queue.py` sha256 | `da739e51c9f130b7ac862e4b07a9fd7b9ca7fd4cfbd3d3f5a11d8eced060c21e` |
| Diff of the two files against the base, sha256 | `c486bbdae604dbaf581b8f522d578c9bbedf6e5cd1eb6d2233bf20bec604e0c1` |
| Run 3 raw TRX sha256 (raw kept outside the repository) | `1f77bad5150365e59d40558a27b5dab9fc4ececb227675d7779c0b01d80e9a36` |
| `run3.sanitized.trx` sha256 | `05a58fcdf3fa2e9f9a883c8f801446a584c3321bb43fb54d25f3a43f522d49f0` |
| Sanitisation | Same tool and rules as `README.md`; R1 306 · R2 145 · R3 1 · R4 1; home, user and unsanitised computerName left: 0 |

Result: 131/138; 7 BİLİNEN, exit 0. The compared files are `run1.sanitized.trx`, `run2.sanitized.trx`, `run3.sanitized.trx`
(all in this folder) and the run 3 raw file. All four give the same seven fingerprints and the same verdict.

## Run 2 — what can and cannot be said

The `known_failures.py` used in run 2 has sha256 `df4eef97b4a6abfb16f34292e2dc47dc59faf878c5bad9bfd57952fe9b04e245`.
It is the step-1 version before the duplicate-name fix. This identifies **that file only**. Run 2's full source tree was
not recorded and stays NOT VERIFIED; run 3 replaces it as the evidence for the final code.

## SCE / MVP6

e07df6bf1's `ct_queue.py` compared with the follow-up's, on every ref that holds the queue: 50 refs and 25,028 id
comparisons. Validate errors and `require` decisions are equal everywhere. `strict` is `no` for `sce-mvp6`.

## NOT VERIFIED

- Run 2's full source tree (see above).
- Failure fingerprints in CI.
- The remote state of `main`. Only the local ref `origin/main` (3912d568b18193f627539a1b27965167ac4077b6) was read; no
  fetch was made.
