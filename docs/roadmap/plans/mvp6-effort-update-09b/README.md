# mvp6-effort-update-09b (Q138) — README

Effort analyst package. Status: **agent output, not CT-accepted.** Written by the Q138 LANE (Cowork LANE 1, Linux VM via the device bridge).
Base `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, `GIT_OPTIONAL_LOCKS=0`, no `.git/index.lock` at start.
Preflight 2026-09-27 17:14:20 +03:00: CT-QUEUE `284ebb60…` (158 rows) and MILESTONE-EVENTS `4654b03a…` (206 lines) matched the dispatch.

Reproduce: `ERP_ROOT=<repo> Q138_OUT=<scratch> python3 RECALCULATE.py` → writes `BOARD-FIGURES.json`, `FIX-PLAN-LINES.tsv`,
`MODULE-FIGURES.tsv` and asserts the 09a figures, the line sums (2,842 / 3,097), delivered ≥ accepted and module totals = overall total.
`CREDIT-LINES.tsv` and `EFFORT-UPDATE-09b.md` were written by this lane from those outputs.

No pack, code, SOP, estimate source or earlier effort package was edited. Inputs are hash-checked in `RECALCULATE.py` and listed in
`CREDIT-LINES.tsv` (path + sha256 per line).
