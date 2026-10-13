# Q202a — Baseline before / after

Branch `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` at start and end. No `.git/index.lock`.
`git status --porcelain` only; no `git diff`; no git write; nothing staged.

| `git status --porcelain` | Start (18:01:08 +03) | After the write (18:05:48 +03) | Change |
|---|---:|---:|---:|
| modified (` M`) | 32 | 89 | +57 |
| untracked (`??`) | 533 | 576 | +43 |
| staged | 0 | 0 | 0 |
| **total** | **565** | **665** | **+100** |

With `-uall` after the write: 89 modified + 7,412 untracked = 7,501 (Q198 measured 7,183 before any of today's records).

## Why +57 and +43, when 294 files were written

- **+57 modified.** 60 written files are tracked and now differ from HEAD. 3 of them were already ` M`
  (the 3 tracked UC-01 conflict files), so 57 are new ` M` lines.
- The 2 untracked UC-01 files were overwritten in place; they were `??` before and still are.
- **+43 untracked.** 229 files were added, plus this record folder. Default porcelain shows a wholly untracked
  directory as one line, so 229 new files appear as 43 lines.

## What was written (294)

| Class | Written | Held | Q201 rows |
|---|---:|---:|---:|
| ADD | 229 | 2 | 231 |
| OVERWRITE | 60 | 2 | 62 |
| CONFLICT (record-decided, Q131 wins) | 5 | 0 | 5 |
| **Total** | **294** | **4** | **298** |

| Layer | Written |
|---|---:|
| BASE / L2 A12-360 | 254 |
| BASE / L3 Auth-22 | 17 |
| Q117 | 8 |
| Q121 | 2 |
| Q131 | 13 |

## Not touched (hash-checked after the write)

| Path | sha256 (first 16) | State |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | `7fdb5ef0d322c3b9` | same as the Q198 spot check and Q201 |
| `docs/analysis/contracts/sandop-capacity.openapi.yaml` | `5213b5353267ad4c` | not in the write list |
| `gateway/Diten.ApiGateway/ocelot.json` | `b0121d2f5b7f809d` | not in the write list |
| `gateway/**`, `.antigravity/**` | — | 0 files newer than the WP start |
| 15 SKIP paths + MOD-0183 pack | — | 16/16 still equal to their Q201 working-tree sha256 (`SKIPPED-PATHS.tsv`) |
