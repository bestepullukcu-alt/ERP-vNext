# Evidence — İ2, the list-screen touch hook says "no deviation" only on a clean signal (2026-10-16)

Authority: `CT-I2-LIST-HOOK-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). Branch `chore/ct/pqg-01` on f324e8711, local only. The ControlTower
(Legacy) session gave its single-writer agreement for `.antigravity/scripts/list_screen_touch_hook.py`, scope named
line by line, before it was written.

## What changed

One file: `.antigravity/scripts/list_screen_touch_hook.py`. The verifier, `.claude/settings.json`, `AGENTS.md` and the
rules are unchanged.

| Verifier answer (gaps format) | Hook before | Hook now |
|---|---|---|
| exit 0, nothing but its own info lines (`[shell]`, `[factory]`, `[pack]`, the producers' exact formats) | no deviation | no deviation |
| exit 1, numbered gaps 1..n (with info lines or not) | the gap list | the gap list, byte-identical text |
| exit 1 or 2 with no numbered gap; the full report (early return); exit 0 with a gap; another exit code; gap numbers not 1..n; an unknown line; the shell WARNING without a gap; anything on stderr | no deviation | DOĞRULANAMADI — reason, and the command to run by hand |
| a timeout | silent | DOĞRULANAMADI — zaman aşımı (25 sn) |
| no verifier | no deviation | DOĞRULANAMADI — doğrulayıcı bulunamadı |
| an error after the file matched | silent | DOĞRULANAMADI — hook iç hatası (type only) |

The verifier's stderr and an exception's text are never printed. The manual command is built with `shlex.join`. A normal
call always exits 0 and writes nothing; only `--self-test` exits 1 when a case fails (stated in the docstring).

## Checks and results

| Check | Result |
|---|---|
| `list_screen_touch_hook.py --self-test` | 21/21 |
| Tests written first (red), then fixed (green) | HEAD's `main()` with the final self-test: 8/21 — the 13 false-"no deviation" and silent cases red. Codex's two boundaries and the manual command: red on the first draft (18/21), green after |
| Sabotage: each of 13 guards removed in turn | every removal turns at least one named test red |
| The two cases the ControlTower (Legacy) session asked for: exit 1 with empty stdout, and a timeout | both DOĞRULANAMADI now; both RED with the fix reverted (HEAD's `main()`, `checks.txt` red section); each RED when its own guard is removed (sabotage "clean only on exit 0", "timeout named") |
| Real data: all 152 list screens, HEAD's hook against the new one | 88 screens with gaps: the same gap text byte for byte; 64 screens HEAD called "no deviation" (the verifier's early return, `verify_datatable_page.py:953-956`): now DOĞRULANAMADI — beklenen gaps biçimi gelmedi; every call exit 0 |
| Codex, independently | the defect reproduced before (`before-codex.json`); the two boundaries red on the draft, 14/14 on the final draft (`before-two-fixes-codex.json`, `checks-codex.json`); four real screens (`real-samples-codex.json`) |
| Architecture suite (one run, the file pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints identical to CT-OPS-04's run. The file pinned before the run, unchanged after; raw sha256 `c13eb038…`, sanitised `5f25dbf3…` (full values in `checks.txt`) |

## Files

| File | What it is |
|---|---|
| `checks.txt` | The outputs above; local paths replaced (`__SCRATCH__`, `__HOME__`, `__TMP__`) |
| `real-before-after.json` | Every list screen: HEAD's kind, the new kind, the new reason |
| `run.sanitized.trx` | The architecture suite with the new hook, sanitised (step-1 tool and rules) |
| `*-codex.json` | Codex's independent checks, copied unchanged |

## Limits — NOT VERIFIED or not machine-checkable

- The hook was exercised by its self-test and by running it the way Claude Code does (JSON on stdin); a live Claude
  Code session firing it after an edit was not observed.
- The verifier still ignores `--format gaps` on its early return; the 64 screens get "doğrulanamadı", not their gap
  list. Fixing the verifier or the screens is a separate item.
- `.antigravity` changes need the owner's confirmation before any merge.
