# Q244 — Disk space account and reclamation plan · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q244 · `AL-CT-DISK` (INS) · E1 (measured, read-only) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent`. **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** |
| Start / End (Europe/Istanbul) | 2026-10-03 00:37:02 +03 / 2026-10-03 00:45 +03 |

```text
Agent Verdict:        MEASURED. Plan written, nothing executed. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 665 lines (89 " M" · 576 "??" · 0 staged) · end 665 lines
                      No .git/index.lock. No git diff. No git write. No gh.
Free space:           4.7 GiB at 00:44:55 (6.9 at 00:37 · 5.5 at 00:41 · 7.2 at 00:43). ABOVE the 2 GiB line, unsteady.
Volume:               /dev/disk3s1 (Data), 460 GiB, 99 % used. One volume; no second disk attached.
Changed files (repo): only this record folder.
Not done:             no rm, no move, no prune, no gc, no build, no test, no process started.
```

## 1. Where the space is

| Consumer | GiB (du) | Inside repo | Git-ignored |
|---|---:|---|---|
| `~/mvp6-env` (16 lane environments) | 63.4 | no | n/a |
| `~/Projects` siblings: three other ERP-vNext checkouts + one zip | 10.2 | no | n/a |
| `~/.codex/worktrees` (6 live worktrees of this repo) | 5.7 | no | n/a |
| Repository folder, all of it | 3.0 | yes | — |
| — of which `.git` | 0.39 | yes | — |
| — of which `.git-backups` | 0.54 | yes | yes |
| — of which build output | 1.01 | yes | `bin`, `obj` yes · `TestResults`, `.trx` no |
| `~/.nuget/packages` | 2.3 | no | n/a |
| `~/Backups` (Q198 archive) | 1.6 | no | n/a |
| Session scratchpad root, `/private/tmp/mvp6-*`, `/private/tmp/q2*` | 0 | no | n/a |

Full list: `SPACE-ACCOUNT.tsv` (55 rows). Home folders measured add up to about 220 GiB of the 429 GiB used;
the rest (system, restricted folders, purgeable space) was not broken down.

## 2. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q244-1** | 🔴 Blocker for the plan | No move destination exists. `/Volumes` holds only `Macintosh HD`; a move within the volume frees nothing. Every step of the plan waits on the owner attaching a disk. Reclaimable by moves today: 0 GiB. | `RECLAMATION-PLAN.md` §0 |
| **F-Q244-2** | 🟡 Medium | The Mac restarted at 00:30:18, seven minutes before this WP. `/private/tmp` was emptied: every `/private/tmp/mvp6-*` and `/private/tmp/q2*` folder and the whole session scratchpad are gone. That is why free space rose from 2.8 to about 7 GiB. Record folders that point at scratch paths (Q217 `service.log`, Q232 `.trx`, Q236 copies, Q214/Q216 logs) now point at nothing. The lane mongod (`rsq208s1`, port 31994) is not running; only the homebrew mongod on 27017 is. | `SPACE-ACCOUNT.tsv`, `DUPLICATES.tsv` rows 1–3 |
| **F-Q244-3** | 🟡 Medium | `~/mvp6-env` is the dominant consumer: 63.4 GiB, 923,944 files, 16 lanes, 100 copies of `services/Diten.SupplyChainService`. The service copies themselves are only 2.9 GiB; the bulk is whole-repo copies (52 GiB) and `bin` (8.4 GiB). Lanes are APFS clones, so the 63.4 GiB is an upper bound on what moving them frees. | `DUPLICATES.tsv`, plan §1 |
| **F-Q244-4** | 🟡 Correction | The WP expected 11 prunable registrations. There are 17 registrations: 1 main, **10** prunable (all under `/private/tmp/mvp6-*`, directories absent), 6 live under `~/.codex/worktrees`. Not pruned. | `WORKTREES.tsv` |
| **F-Q244-5** | 🟡 Medium | The 6 surviving worktrees hold 5.7 GiB and 341 uncommitted entries (33–99 each), all at HEAD `4a8d4d4b3`, last touched 20–24 Sep. Four are on a detached HEAD. That work exists nowhere else. | `WORKTREES.tsv` rows 12–17 |
| **F-Q244-6** | 🟢 Confirmed, with an addition | Q230's "7 test-output files not ignored" is correct: 5 `.trx` under the SupplyChain `Tests/TestResults` and 2 under `tests/architecture/…/TestResults`, all untracked. `TestResults/` has no `.gitignore` line. Addition: 5 more files under `services/Diten.CrmService/tests/…/TestResults` are **tracked** (already committed), which Q230 did not count. | §3 below |
| **F-Q244-7** | ⚪ Info | Build output in the repo is 1.01 GiB: `bin` 0.87, `obj` 0.08, `TestResults` 0.01, loose `.trx` 0.05 (148 files, all under `docs/`, i.e. filed evidence). Only 0.10 GiB belongs to SupplyChain + BuildingBlocks. | `SPACE-ACCOUNT.tsv` |
| **F-Q244-8** | 🟡 Medium | Free space is not steady with nothing building: 6.9 → 5.5 → 7.2 → 4.7 GiB in eight minutes — a swing of 2.5 GiB, larger than the margin a lane needs. Cause not identified (swap is 2.0 GiB; app caches and this session's own app data are candidates). A lane that checks once at start can still cross the 2 GiB line mid-run. | header block |
| **F-Q244-9** | ⚪ Info | Q198 archive is intact in place (1.6 GiB, `~/Backups`). Its hash was not recomputed in this WP; the plan requires it at the destination before any move. | plan step 6 |
| **F-Q244-10** | ⚪ Info | `.git-backups` (0.54 GiB, ignored) holds two bundles, two tarballs, patches and checksum files of 25–26 Sep. Per Q235 they carry the exposed secret value; moving them adds a location to the secret ruling. Not opened here. | plan step 7 |

## 3. Build output and ignore status

| Kind | Count | GiB | Ignored |
|---|---:|---:|---|
| `bin/` | 57 dirs | 0.87 | yes (`.gitignore:1`) |
| `obj/` | 57 dirs | 0.08 | yes (`.gitignore:2`) |
| `TestResults/` | 3 dirs | 0.01 | **no** |
| `.trx` outside those | 148 files | 0.05 | **no** (under `docs/`) |
| **Total** | | **1.01** | |

## 4. Duplicates of the service tree

`DUPLICATES.tsv`, 112 rows: 100 copies in `~/mvp6-env` (22 build clones 1.94 GiB, 40 source/evidence copies
0.61 GiB, 40 overlay stages of a few files each), 8 in the Codex worktrees (0.38 GiB), the working tree itself
(0.10 GiB), and 3 rows for Q220, Q232 and Q236. Q220 never made a copy (it stopped). The Q232 and Q236 scratch
copies were in the session scratchpad and are gone since the restart.

## 5. Is there room for one more build lane plus a ten-run suite?

Yes at the last reading, narrowly, and without any move: 4.7 GiB free against roughly 1.2 GiB for a scratch lane of the
Q232/Q236 kind (figure from those lanes, not re-measured). That leaves about 1.5 GiB above the 2 GiB line, less than the 2.5 GiB swing seen during this WP, so it is not safe to rely on. A full `mvp6-env`-style lane does not fit.
The plan frees up to 82 GiB by `du` (18.9 GiB of it independent of clone sharing) once a destination exists.

## 6. Refused / not done

- No `rm`, no `mv`, no `cp` of any measured item; no `git worktree prune`, `git prune`, `git gc`, `git clean`.
- No build, no test, no process started. Nothing pointed at 27017.
- No deletion is recommended anywhere in the plan.
- `.git-backups` contents and the Q198 archive were sized and listed, not opened.

## 7. Files

`SOP-22.md` · `SPACE-ACCOUNT.tsv` · `WORKTREES.tsv` · `DUPLICATES.tsv` · `RECLAMATION-PLAN.md` · `ARTIFACTS.sha256`

Return to CT; CT decides.
