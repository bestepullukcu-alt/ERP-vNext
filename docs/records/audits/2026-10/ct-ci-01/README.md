# Evidence — CT-CI-01, the Control Tower tools' self-tests in CI and evidence retention (2026-10-16)

Authority: `CT-CI-01-JOINT-DECISION-01` (joint decision of Codex and the AI session; the owner said "ok" before any
PR — relayed by Codex). Branch `chore/ct/pqg-01` on `ddafb1e91`, local only. The ControlTower (Legacy) session gave the
single-writer window for the two files directly; the AI session wrote only those lines and closed it.

## What changed — two source files

| File | Change |
|---|---|
| `scripts/run_phase1_gates.sh` | 12 lines inserted after the cross-db check, before the first build: a comment, `echo "[phase1] CT tool self-tests"` and seven `python3 -B <tool> --self-test`. No line changed; mode stays 100755. SHA-256 `63424ce4c914ca8dbae61a7c41ea4579cac49dffcddf940e57484a3aa5eac3a4` |
| `docs/guides/operations/control-tower-operating-card.md` | §5: one item, "Kanıt saklama". SHA-256 `2a8d3fc32243c78555e53a3350721f6a48b663018430e57c32836f25542d12e9` |

Unchanged: both workflows, `CiServiceTestsGuardTests.cs`, the gate's hardening (two `||`, no `exit 0` / `trap` /
`set +` / `--filter`, the exact tenancy / architecture / web `dotnet test` lines, fail fast), `--audit-ledger-only`,
`tests/known-failures.tsv`, REP-001 and every earlier record.

## Checks and results

| Check | Result |
|---|---|
| The seven self-tests without packages | system Python, no venv, site-packages off (`-E -S`), `openpyxl` not visible: all exit 0 (69 · 100 · 71 · 75 · 21 · 24 · 167); no network module in the tools or the helpers they load; `openpyxl` only inside two `verify_module_id` functions, guarded, not on these paths. Same in a depth-1 clone with a package-free venv. The tools are byte-identical in the branch and in the merged tree |
| The real gate, branch (`gate_probe.py.txt` → `gate.txt`) | depth-1 clone, package-free Python first on PATH, a `dotnet` spy: 24/24 — the block runs and passes before the first `dotnet` call; the 18 `dotnet` calls are the old gate's, same order; a failing build still stops the gate at once; each of the seven self-tests broken in turn → exit 1, no build reached (7/7); `--audit-ledger-only` byte-identical; the hardening equalities hold; insertion only |
| The real gate, merged tree (`gate-merged.txt`) | the same probe on the merged gate (`aaf6a755…`, main's PlanningService.Cycles.Tests build line kept): 24/24, 19 `dotnet` calls equal to the merged old gate's |
| Merged tree | `git merge-tree --write-tree 3912d568b <the two sources on ddafb1e91>`: exit 0, tree `429dcf1516a9da32998051b951e20920f9121245` — the tree Codex computed independently; no merge commit |
| Architecture suite on the merged tree (one run) | 131/138; `known_failures.py check` BİLİNEN=7, YENİ=0, exit 0 (raw and sanitised); fingerprints = raw = the CT-ROUTE-01 run; `CiServiceTestsGuardTests` (it reads the gate script) 44/44 passed, named in `checks.txt`; the tree unchanged before and after |
| `run.sanitized.trx` | the one closing TRX: clone root normalised, then R1–R4; SHA-256 `277c0abdcdfba5489a56cd96b184c907f254560a45dd279430d0a6aa700ef02f`; user name, home, clone and scratch roots: 0; `computerName` fields sanitised |
| Codex, independently | its own black-box probe 18/18 on the branch gate and 18/18 on the merged gate; Codex independently parsed the original closing TRX and checked the pinned test tree; no second architecture run |
| Protected files | registry, queue template, the SCE queue, `ct_queue.py`, `prompt_gate.py`, `acceptance_record.py`, SOP from §36.2: unchanged |
| Remote `main` | `3912d568b18193f627539a1b27965167ac4077b6` — Codex, read-only `git ls-remote`, 2026-10-16; no fetch |

## Files

| File | What it is |
|---|---|
| `checks.txt` | The measurements above; local paths replaced (`__SCRATCH__`, `__HOME__`, `__TMP__`) |
| `gate_probe.py.txt` | The gate probe (scratch clone, `dotnet` spy, package-free Python). SHA-256 `2022753137373159f54256fb1c31cd568e60205e4715cf33455c31006c0af995`. One change after the two runs: `env=ENV0` added to its git common-dir call, so every git call it makes carries `GIT_OPTIONAL_LOCKS=0`; that call gives the same directory with or without it (checked read-only); the probe was not re-run |
| `gate.txt` · `gate-merged.txt` | Its outputs on the branch and on the merged tree |
| `run.sanitized.trx` | The architecture suite on the merged tree, sanitised — the single closing TRX |

## Limits — NOT VERIFIED

- CI on Linux (ubuntu runner, its Python) is measured only after a push; every result here is local (macOS).
- If the seven registered architecture failures occur on CI too, CI stays red — expected, not measured.
- Nothing pins the new block against removal; while the architecture suite is red, CI does not reach the web tests.
- Publication is the owner's decision.
