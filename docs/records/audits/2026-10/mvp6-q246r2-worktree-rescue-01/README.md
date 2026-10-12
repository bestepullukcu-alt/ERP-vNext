# Worktree dirty-file rescue — 2026-10-03 (WP Q246-R2)

**Nothing here is committed.** The folder is untracked. No ref was created, nothing was pushed.

## What this folder is

A backup of the uncommitted files in the six Codex worktrees of this repository
(`~/.codex/worktrees/<name>/ERP-vNext-recovery`). Q244 found 341 uncommitted status entries there that
exist in no commit and no backup. Each worktree's files are in one archive, at repo-relative paths.

All six worktrees are at HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (tip of `feature/mvp6-logistics`).
Modified tracked files are stored as whole files (their content after the change), not as diffs.
To see a change, unpack an archive over a checkout of that commit in a scratch folder.

| File | Content |
|---|---|
| `<name>.tar.gz` × 6 | the kept files of that worktree |
| `INVENTORY.tsv` | one row per kept file: worktree, path, bytes, git status code (1,702 rows) |
| `EXCLUDED.tsv` | one row per excluded file: worktree, path, bytes, git status code, pattern a–f (200 rows) |
| `HEADS.tsv` | one row per worktree: path, branch, HEAD, dirty counts, kept and excluded totals |
| `ARTIFACTS.sha256` | sha256 of the six archives and the three tables |

Totals: 1,702 files kept, 13,335,977 bytes; six archives, 2,815,862 bytes on disk.
200 files excluded, 238,504,331 bytes.

## What was left out, by rule

Exclusion is by path pattern only (CT ruling in Q246-R2). No excluded file was opened by this WP.

| Pattern | Rule | Files | Bytes |
|---|---|---:|---:|
| a | basename `*_probe.py` | 60 | 300,529 |
| b | path contains `/tests/loads/` | 6 | 17,634 |
| c | path contains `/mongo-data/` | 84 | 212,183,600 |
| d | basename ends `.tar`, `.tar.gz`, `.tgz`, `.zip` | 20 | 8,640,807 |
| e | path contains `/TestResults/` | 30 | 17,361,761 |
| f | path contains `/bin/`, `/obj/`, `/node_modules/` | 0 | 0 |

A file matching more than one pattern is listed under the first that matches, in the order a–f.
Patterns a and b exist because probe scripts under `tests/loads` carry the exposed value of the Q235 ruling.
The excluded files remain only in the worktrees.

## Commands that regenerate it

For each worktree `<name>` with root `W=~/.codex/worktrees/<name>/ERP-vNext-recovery`:

```bash
export GIT_OPTIONAL_LOCKS=0 COPYFILE_DISABLE=1
git -C "$W" status --short | wc -l                      # dirty count (HEADS.tsv)
git -C "$W" rev-parse --abbrev-ref HEAD; git -C "$W" rev-parse HEAD
git -C "$W" status --porcelain=v1 -z -uall              # expanded file set: every dirty file, untracked folders opened
# apply patterns a–f to each path -> keep list, one path per line, in <name>.keep.list
tar -czf <name>.tar.gz -C "$W" -T <name>.keep.list
```

`-C` comes before `-T`: macOS tar applies `-C` only to the names that follow it.
The keep list for a worktree is the `path` column of its rows in `INVENTORY.tsv`.

Checks run after writing:

```bash
shasum -a 256 -c ARTIFACTS.sha256            # all nine OK
tar -tzf <name>.tar.gz | wc -l               # equals the worktree's kept-file count in HEADS.tsv
```

Each archive member was also read back and its sha256 compared with the source file: 0 mismatches in 1,702.

## Limits

- This is a point-in-time copy (2026-10-03 01:09 +03). Later edits in the worktrees are not in it.
- It sits on the same disk as the worktrees. It protects against loss of `~/.codex/worktrees`, not against loss of the disk.
- Archives sit inside the main working tree; they add one untracked entry to its status.
