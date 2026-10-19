# Evidence — step 1, pre-merge fixes (2026-10-15)

Owner-approved step 1 of the Control Tower work order (CT-NEW-CT-STANDARDS-OWNER-DECISION-01). Branch
`chore/ct/pre-merge-fixes-01` on top of e07df6bf1, local only. This folder holds the evidence for this package only; the
E0 evidence gap of the earlier commits (CT-AUDIT-CORRECTIONS-2026-10-15-01, row 10) stays open.

## Files

| File | What it is |
|---|---|
| `run1.sanitized.trx` | Architecture suite on the clean e07df6bf1 tree, sanitised |
| `run2.sanitized.trx` | Architecture suite with the step-1 changes applied, sanitised |
| `sce-noregression.tsv` | Every git ref holding the SCE / MVP6 queue: ref SHA, queue blob, validate errors and `require` decisions for every id, e07df6bf1's `ct_queue.py` against this package's |
| `checks.txt` | Outputs of the checks listed below, as run on the final files |
| `sanitize_trx.py.txt` | The sanitiser, stored as text (it is not meant to run from here) |

The raw TRX files are kept outside the repository, on the machine that ran them.

## Sanitisation

Tool: `sanitize_trx.py` (sha256 `b015a9b480d0ceec3e0bf561da4cdad56940c518465e814405085cfa10195b72`), Python 3.9.6.
Deterministic rules, applied in this order; no other byte changes:

1. R1: the running user's home directory, matched case-insensitively, becomes `__HOME__`.
2. R2: the value of every `computerName` attribute becomes `__HOST__`.
3. R3: the host after `@` in the `TestRun` name attribute becomes `__HOST__`.
4. R4: the host between underscores in `Deployment` `runDeploymentRoot` becomes `__HOST__`.

The same character sequence anywhere else is data and is left unchanged. The placeholders contain no XML special
characters, so both copies parse as XML.

| Run | Raw sha256 | Sanitised sha256 | Replacements | Left after sanitising |
|---|---|---|---|---|
| 1 | `7dbc78c036726ba8071184fe2b5503cf34205f2e0c48b6cda110388043aa4495` | `93514158540e31cf7e48c725e177c18886ebfcfeb1c1f8136dcf0e108ebd29c8` | R1 306 · R2 145 · R3 1 · R4 1 | home 0 · user 0 · unsanitised computerName 0 |
| 2 | `de6d4c3dbd7909faf3d8dd74b1bb4bc3005d5cd54ff2b61bec1c0fb81fb84570` | `0c77e689f513b15544c6b4e8766d02de61310d19057c99f4211dcd6bc2d8e6fd` | R1 306 · R2 145 · R3 1 · R4 1 | home 0 · user 0 · unsanitised computerName 0 |

**What changed and what did not.**
- **Changed:** the local paths sat only in stack traces and XML attributes, so only those changed.
- **Unchanged (measured):** test names, outcomes and failure messages are identical between each raw file and its
  sanitised copy. The seven failure fingerprints are identical too: run 1 raw = run 1 sanitised = run 2 raw = run 2
  sanitised. Both runs: 138 results (137 distinct names; one pair of passing theory cases share a truncated display
  name), 131 passed, 7 failed — the seven registered known failures.

## Checks and results

| Check | Result |
|---|---|
| Run 1 (clean e07df6bf1) against the registered fingerprints, before any change | 7 BİLİNEN, exit 0 |
| Item lists taken from run 1 hash to the registered `items:` fingerprints | 2 of 2 match (7 and 60 items) |
| Run 2 (changes applied) | 131/138; 7 BİLİNEN, exit 0, raw and sanitised |
| FU05-like simulation on run 2, in memory: one recorded item removed | KISMEN-DÜZELDİ, exit 0 |
| The same with one new item added, alone or together with a removal | YENİ-AYRINTI, exit 1 |
| `known_failures.py --self-test` / `ct_queue.py --self-test` | 23/23 / 27/27 |
| `known_failures.py validate` (TSV and JSON agree) / `ct_queue.py validate` | OK / OK, 0 errors |
| SCE / MVP6 behaviour (`sce-noregression.tsv`) | 48 refs; validate errors and `require` decisions are equal for every ref and every id (40 dispatchable ids on each ref); `strict` = no for `sce-mvp6` |
| `require Q124 --queue sce-mvp6` | Dispatchable. HAZIRLIK: registered in this tree. YÜRÜRLÜK: the local ref origin/main at 3912d568b (last fetch 2026-10-15T11:40 +03:00) has no registry file; remote state not checked |
| `require … --in-force` | DUR, exit 1 (as intended until the chain is on main) |

## NOT VERIFIED

- **CI fingerprints.** In the inspected TRX files, sanitising leaves failure messages and fingerprints unchanged. Equal
  fingerprints between a local run and CI are not verified.
- **Exact final tree.** Run 2 was made before the last change to `scripts/known_failures.py`, which stops a failing
  result from being hidden by a passing result with the same name. That change adds no docs path. Run 1 and run 2 gave
  identical fingerprints although `known_failures.py` differed between them. No third run was made; the owner approved
  two.
- **Remote main.** Only the local ref `origin/main` was read; no fetch was made.
