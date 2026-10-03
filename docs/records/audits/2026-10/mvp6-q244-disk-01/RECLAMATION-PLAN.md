# Q244 — Reclamation plan (moves only)

Nothing in this plan was executed. Nothing is deleted, and no step recommends a deletion.
Every step is a move with a named destination and a check.

## 0. The blocker: there is no destination on this machine

`/Volumes` holds only `Macintosh HD`. The Mac has one APFS container (disk3, 494.4 GB) and no
second disk is attached. A move inside the same volume frees nothing.

So every step below needs a destination that the owner attaches first. It is written as
`<DEST>` = `/Volumes/<owner's external disk>/mvp6-offload-2026-10-03/`.
Requirements for `<DEST>`: at least 100 GiB free; APFS or Mac OS Extended (not exFAT/FAT — they lose
symlinks, permissions and case, which breaks build trees and git).

Until `<DEST>` exists, **zero GiB can be reclaimed by moves**, and every step is owner-gated for that reason alone.

## 1. How each move is done and verified (same for every step)

1. Before: `df -k /System/Volumes/Data` (note free KiB); write a sha256 manifest of the source
   (`find <src> -type f -exec shasum -a 256 {} +`), keep it in the WP record folder of the lane that does the move.
2. Copy to `<DEST>` with `cp -Rp` (or `ditto`); write the same manifest over the copy; the two manifests must be identical.
3. Only then move the source out of the way. The removal of the source half of a move is the owner's act,
   in the owner's shell — this pilot's agents do not run `rm` (standing rule), and `mv` across volumes is copy + remove.
4. After: `df` again. The freed figure is the `df` delta, **not** the `du` figure in this plan (see caveat).
5. Optional: leave a symlink at the old path pointing to `<DEST>/…` so recipes that name the old path still resolve
   while the disk is attached.

**Caveat on the figures.** Sizes are `du`. BASE-STACK builds lanes with `cp -Rc` (APFS clones), and clones share
blocks, so `du` counts shared blocks once per lane. The 63.4 GiB of `~/mvp6-env` is an upper bound on what moving
it frees; the real figure is only known from the `df` delta after the first lane moves. Move one lane, measure, then decide on the rest.

## 2. Ordered steps — GiB per unit of risk, outside the repo first

| # | What moves | From | To | Frees (du, upper bound) | What breaks | Verify | Who |
|---|---|---|---|---:|---|---|---|
| 1 | Closed lane environments, largest first: `q84b`, `q88b`, `q65b`, `q185`, `q154`, `q119`, `q129`, `q122`, `q64c`, `q64d`, `q64e`, `claims`, `claims-fixes` | `~/mvp6-env/<lane>` | `<DEST>/mvp6-env/<lane>` | 49.2 GiB | Any record that cites a path in that lane as evidence stops resolving on this Mac (fixed by the symlink while the disk is attached). A lane that is still open cannot be rebuilt in place. | §1 manifests identical; `df` delta; one cited evidence path per lane opens through the symlink | **Owner approval.** CT names which lanes are closed. |
| 2 | The BASE-STACK chain lanes `q117`, `q121b` | `~/mvp6-env/q117`, `~/mvp6-env/q121b` | `<DEST>/mvp6-env/…` | 8.3 GiB | BASE-STACK v2 is BASE→Q117→Q121→Q131; a new lane that replays the recipe from these folders cannot start without the disk. | as step 1, plus: BASE-STACK recipe step that reads the lane resolves | **Owner approval**, after CT confirms no new lane will be cut from them |
| 3 | Sibling checkouts and the zip in `~/Projects` that are not this pilot: `ERP-vNext`, `ERP-vNext 31 May 2026`, `ERP-vNext 31 May 2026-1`, `ERP-vNext_BACKUP_before_diten_migration_remediation.zip` | `~/Projects/<name>` | `<DEST>/Projects/<name>` | 10.2 GiB | Nothing in this pilot. Each has its own `.git`; any tool or habit of the owner that opens them at the old path. | §1 manifests; `git -C <DEST>/Projects/<name> status --porcelain` runs | **Owner approval** — owner data, not pilot data |
| 4 | `base` | `~/mvp6-env/base` | `<DEST>/mvp6-env/base` | 5.9 GiB | Every future lane: `base` is what lanes are cloned from. Moving it stops lane creation until it is back or the recipe is repointed. | as step 1 | **Owner approval.** Last of the lane moves; skip if steps 1–3 are enough |
| 5 | Six Codex worktrees | `~/.codex/worktrees/<name>/ERP-vNext-recovery` | `<DEST>/codex-worktrees/<name>/ERP-vNext-recovery` | 5.7 GiB | Each holds uncommitted work (33–99 entries, 341 in total) that exists nowhere else. The registration in `.git/worktrees/<name>/gitdir` points at the old path and becomes "prunable" — one `git worktree prune` by anyone would then drop the registration. Codex loses its working folders. | §1 manifests; then `git worktree repair <new path>` (a git write — the owner's or a DEV lane's, not this lane's) and `git worktree list` shows 6 non-prunable | **Owner approval.** Highest risk per GiB of the outside-repo steps |
| 6 | Q198 safety archive + its `.sha256` | `~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz` | `<DEST>/Backups/` | 1.6 GiB | It is the only full copy of the working tree outside the working tree. While it is on a detachable disk the Mac has no local safety copy. | `shasum -a 256` at the destination must print `11908716e818bea901ee27d3aa811f0a61572fcd3c4981ab53d501c50fb49542`, and `tar -tzf` must list to the end without error — **both before the source is touched**. Confirmer: **the owner (CT)**, in writing in the WP that moves it. | **Owner approval.** Low gain, high consequence — do it last or not at all |
| 7 | GIT-001 artefacts of 25–26 Sep | `<repo>/.git-backups/` | `<DEST>/git-backups/` | 0.54 GiB | The artefacts are the fallback for the pre-pilot state. They also contain the exposed secret value (Q235): moving them makes one more copy location that the secret ruling must list. | §1 manifests; the two `SHA256SUMS` files inside still verify at the destination | **Owner approval**, and only after the Q235 ruling |
| 8 | `bin/` and `obj/` of projects other than SupplyChain and BuildingBlocks | `<repo>/services/**/bin`, `obj`, `frontend`, `gateway`, `tests` | `<DEST>/build-output/<same relative path>` | 0.87 GiB | Those projects need a full rebuild before they run or test. Git-ignored, so git state does not change. | `git status --porcelain` still 665 lines; `df` delta | Could run unattended once `<DEST>` exists; listed last because the gain is small |

Not proposed: `~/.nuget/packages` (2.3 GiB) — every build restores from it and the lanes build offline;
`.git` (0.39 GiB) — the repository; SupplyChain `bin/obj` (0.10 GiB) — the next suite run needs them;
`~/.codex` sessions, `~/Library`, `~/Development`, `~/Desktop` — owner data outside the pilot, measured but not planned.

## 3. What can run unattended

Nothing today: there is no destination. With `<DEST>` attached, only step 8 is safe without a per-item decision.
Steps 1–7 each need the owner.

## 4. Worktree registrations — reported, not run

`git worktree prune` would drop 10 registrations (rows 2–11 of `WORKTREES.tsv`). Their directories no longer exist,
so it would free about 22 MiB of `.git/worktrees` and nothing else. Two of the ten carry a branch
(`codex/mod-0186-r01-rework-01`, `codex/mod-0192-core-dev-01`); the branches themselves would stay.
It was not run and is not part of this plan — it removes rather than moves.

## 5. Totals and the answer to "is it enough?"

| | GiB (du) |
|---|---:|
| Free (00:43 +03 · 00:44:55 +03) | 7.2 · 4.7 |
| Steps 1–2 (lanes except `base`) | up to 57.5 |
| Step 3 (sibling checkouts) | 10.2 |
| Steps 4–6 | up to 13.2 |
| Steps 7–8 (inside repo) | 1.4 |
| **All steps** | **up to 82.3** |
| Steps that do not depend on clone sharing (3, 5, 6, 7, 8) | 18.9 |

- **Today, with no move:** free read 7.2 GiB at 00:43 and 4.7 GiB at 00:44:55, above the 2 GiB line. One scratch build lane of the Q232/Q236 kind
  (service copy + `bin/obj` + ten `.trx` + a lane Mongo dbpath) took about 1.2 GiB in earlier WPs — that figure is
  from those lanes, not re-measured here. It fits at either reading, with only 1.5 GiB to spare at the lower one. A full `~/mvp6-env`-style lane is 1–6 GiB (`du`); the small
  ones fit, a 6 GiB one would end under the 2 GiB line.
- The margin is not stable: free read 6.9, 5.5, 7.2 and 4.7 GiB within eight minutes with no build running (swap and
  app caches). A lane should re-read `df` at its own start and stop below 2 GiB.
- **With step 1 alone**, even if clone sharing halves the figure, there is room for several lanes and ten-run suites.
- **The reboot is why there is room at all.** Free space was 2.8 GiB before the restart at 00:30:18; the restart
  emptied `/private/tmp`. That reclaimed space was scratch evidence of Q205–Q236, and it is gone, not moved.
