# Evidence — the list-screen verifier's early return honours `--format gaps` (2026-10-16)

Authority: `CT-VDP-EARLY-RETURN-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). Branch `chore/ct/pqg-01` on d2cde8849, local only. The ControlTower
(Legacy) session's single-writer agreement for `.antigravity/scripts/verify_datatable_page.py`, line-level scope, was
relayed by Codex (its answer did not reach the AI session); the AI session told Legacy before writing.

## What changed

One file, `.antigravity/scripts/verify_datatable_page.py` (sha256 `d146fd0e…` → `670c9cd4…`): a shared `print_gaps()`
under `print_report()`; the early return for missing core files (was lines 953–956) prints the gaps format when
`--format gaps` is asked, else the report as before; the end of `main()` calls the same printer. The hook, settings,
`AGENTS.md` and the rules are unchanged. The file is the same at HEAD, the local `origin/main` and their merge base.

## Checks and results

| Check | Result |
|---|---|
| K1 report mode, all 152 list screens: stdout, stderr, exit | byte-identical |
| K2 gaps mode, the 88 screens already in the gaps format | byte-identical |
| K3 gaps mode, the 64 early-return screens (compact 42, slim 5, no reference 17) | only numbered lines — the report's `[FAIL]` names and details in the same order — exit 1, stderr empty |
| K4–K5 the hook's own `classify()` | 64 screens now a deviation (gap list); the other 88 unchanged |
| Tests first | HEAD's verifier: K1, K2, K5 green, K3 and K4 red (0/64) — then the fix, 5/5 |
| Sabotage: 3 guards removed in turn (the gaps branch of the early return; the shared printer keeps details; exit 1 when a gap exists) | each removal turns at least one criterion red |
| End to end: the committed hook (afb5beeca) with JSON on stdin, every list screen | 64 screens from DOĞRULANAMADI to the gap list; 88 unchanged; every call exit 0 |
| The hook's self-test (file unchanged) | 21/21 |
| Codex, independently | 9/9 on the repository file, 5/9 on the old one; synthetic slim/compact/no-reference cases; `print_gaps` edge cases (`independent-review-codex.json`) |
| Architecture suite (one run, the files pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints identical to the İ2 run. The files pinned before the run, unchanged after; raw sha256 `623c5ef1…`, sanitised `a8949679…` (full values in `checks.txt`) |

## Files

| File | What it is |
|---|---|
| `checks.txt` | The outputs above; local paths replaced (`__SCRATCH__`, `__HOME__`, `__TMP__`) |
| `e2e-hook.json` | Every list screen: the hook's kind with the old verifier and with the new one |
| `run.sanitized.trx` | The architecture suite with the change, sanitised (step-1 tool and rules) |
| `independent-review-codex.json` | Codex's independent review, its personal-path-free summary, copied unchanged |

The raw screen outputs (both sides) hold absolute local paths in the verifier's details; they stay outside the
repository.

## Limits — NOT VERIFIED or not machine-checkable

- A live Claude Code session firing the hook after an edit was not observed; the hook's `main` ran with JSON on stdin.
- The 64 screens are not fixed; they now get their gap list.
- `.antigravity` changes need the owner's confirmation before any merge.
