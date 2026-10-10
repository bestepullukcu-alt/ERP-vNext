# Q198 — Safety backup (OD-BACKUP) — REPORT

- Prompt: Q198 v5 (v1–v4 stopped NOT RUN)
- Date: 2026-10-02 · Executor: Claude Code, Local (Mac)
- Authority: `docs/records/audits/2026-10/mvp6-ct-verdicts-q191-q189-2026-10-02.md`
  sha256 `c44130bd61935fcc81ef4b28111a96931f85672161422aa5e0ffb4fb444b954b` (verified)
- Agent verdict: **PASS** (P1–P7, W1–W2, V1–V6). CT decides ACCEPTED.

## What was backed up

The whole folder `/Users/natig/Projects/ERP-vNext-recovery` (including `.git`, ignored
files, 32 modified tracked files and 7,151 untracked files) as one compressed archive
outside the repo. No git writes, no deletions, no edits to existing repo files.

| item | value |
|---|---|
| archive | `~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz` |
| size | 1,745,856,694 bytes (1.6G) |
| sha256 | `11908716e818bea901ee27d3aa811f0a61572fcd3c4981ab53d501c50fb49542` |
| sha256 file | `~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz.sha256` (124 bytes) |
| archive members | 42,721 |
| folder entries (`find`) | 42,721 before and after tar — difference 0 |
| START (B0) | 2026-10-02T12:34:16Z (15:34:16 +03) |
| tar finished | 15:37 +03, exit code 0, no stderr |

## Baseline and after (V4)

| value | baseline | after |
|---|---|---|
| HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` | identical |
| branch | `feature/mvp6-logistics` | identical |
| `git status --porcelain=v1 -uall` sha256 | `4b01fa71d1ec2b0acbb0e1dbb72e04d5682638fc1b225424877a5f74026b3596` | identical |
| porcelain lines | 7183 (32 ` M` + 7151 `??`) | identical |
| `.git/index` sha256 | `8cb25ba2fbc8ec9c783bb196d6142b4bfa424281d461685bb83b11ec5a582291` | identical |
| stash count | 3 | identical |
| local branches | 37 | identical list |

V5: no file outside `.git` was modified after START
(`find … -newermt "2026-10-02 15:34:16"` printed nothing; the same filter run on
`~/Backups` listed the two new files, so the filter works).

## Disk

| moment | available (`df -h ~`, /dev/disk3s1, 460Gi) |
|---|---|
| before | 6.9Gi |
| when tar ended | ≈4.5Gi (4,770,072 KiB) |
| after verification | 3.2Gi |

The disk guard (stop below 2 GB) never tripped. The archive accounts for 1.6G of the
drop; the remaining ≈2G was consumed by something other than this WP while it ran
(not investigated). The volume is at 100% capacity.

## Spot checks (V3, archive member = repo file, sha256)

- `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` — `754c63fec96900ba50d79a4c212b202059b8dfd8f862992015b5413bcd77008d`
- `docs/records/audits/2026-10/mvp6-ct-verdicts-q191-q189-2026-10-02.md` — `c44130bd61935fcc81ef4b28111a96931f85672161422aa5e0ffb4fb444b954b`
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` — `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8`

## How to restore from the archive

1. Verify: `shasum -a 256 ~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz` must equal the sha256 above.
2. Extract into a NEW empty parent folder (never over the live repo):
   `mkdir ~/Projects/restore-2026-10-02 && tar -xzf ~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz -C ~/Projects/restore-2026-10-02`
3. The result `~/Projects/restore-2026-10-02/ERP-vNext-recovery` is a full working copy
   with its own `.git` (HEAD `4a8d4d4b3`, branch `feature/mvp6-logistics`, same
   uncommitted state). Needs ≈2.9 GB free.
4. Single file: `tar -xzOf <archive> ERP-vNext-recovery/<path> > <new file>`.

## Leftovers and notes

- v4 leftovers kept, not used as proof: `~/mvp6-env/q198/B1-porcelain.txt`,
  `~/mvp6-env/q198/start.marker`.
- Writes outside the repo in v5: W1 and W2 only.
- mongod PID 758 untouched and still running. Other Claude Code sessions stayed open.
- One read-only compound command (V4 recheck of HEAD/branch/stash in a single call) was
  denied by the permission layer; the same checks were then run as single commands, as
  the command style requires. No write was denied.
- This evidence folder was written after the archive, so it is not inside the archive.
