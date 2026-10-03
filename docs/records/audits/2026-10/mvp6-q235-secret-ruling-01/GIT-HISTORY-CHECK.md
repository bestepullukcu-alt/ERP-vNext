# Q235 — Has the value reached git?

## Answer

**It is NOT in any commit, branch, tag, stash or remote-tracking ref.**
**It IS in the local git object database** — and in two git-ignored backup bundles. This was not known before.

## 1. What was searched

Read-only plumbing only: `cat-file --batch --batch-all-objects`, `rev-list --objects`, `for-each-ref`, `ls-tree`,
`log --find-object`, `bundle list-heads`, `config --get`. No fetch, no `ls-remote`, no write.

| Scope | Size | Objects containing the value |
|---|---:|---:|
| Every object in `.git` (reachable or not) | 76,537 (39,234 blobs · 35,184 trees · 2,119 commits) | **8 blobs** |

## 2. The eight blobs

| Blob (git id, first 12) | What | Reachable? |
|---|---|---|
| `0871f3f03b83` | pre-fix `runtime_probe.py` (sha256 `e5ee0aed…a4a6`) | yes — see §3 |
| `88933cd517ee` | pre-fix `restart_probe.py` (sha256 `296c150a…5dcf`) | yes |
| `a7cb22f1f255` | pre-fix `failure_probe.py` (sha256 `8f6cc448…5a63`) | yes |
| `4503da3eba80`, `679705be1980`, `6813f5bc79c9`, `868f6563aee0`, `8b1b6b020216` | five other file versions carrying the value | no — unreachable (dangling) objects |

## 3. What reaches the three carrier blobs

| Ref kind | Count | Reaches the carriers? |
|---|---:|---|
| Branches (`refs/heads`) | 37 | **no** |
| Remote-tracking (`refs/remotes`) | 153 | **no** |
| Tags | 0 | — |
| `refs/stash` and its three reflog entries | 1 | **no** |
| `refs/codex/turn-diffs/checkpoints/…` | 9 | **yes — all nine** |

The nine `refs/codex/…` refs point at **tree objects**, not commits. They are checkpoints written by the Codex
desktop tool while it worked in this checkout. Each tree contains
`services/Diten.SupplyChainService/tests/loads/{runtime,restart,failure}_probe.py` in the pre-fix version.

`git log --all --find-object=<blob>` returns no commit for any of the three: no commit has ever contained them.
`HEAD` and `origin/feature/mvp6-logistics` are the same commit (`4a8d4d4b3`) and its tree has no `tests/loads/`
path at all (the folder is untracked).

## 4. Was it pushed?

| Fact | Evidence |
|---|---|
| No remote-tracking ref reaches the blobs | §3 |
| The fetch refspec covers branches only; no push refspec; no mirror | `remote.origin.fetch = +refs/heads/*:refs/remotes/origin/*`; `remote.origin.push` and `remote.origin.mirror` unset |
| A plain `git push` or `git push --all` does not send `refs/codex/*` | follows from the two lines above |
| Whether `refs/codex/*` exists on the remote (`git@github.com:bestepullukcu-alt/ERP-vNext.git`) | **CANNOT DETERMINE here.** It needs a network read, which this strict lane does not do. One command settles it: `git ls-remote origin 'refs/codex/*'` |

## 5. Copies outside the object database (same machine)

| Place | State |
|---|---|
| `.git-backups/ERP-vNext-recovery-20260925-2210.bundle`, `…-20260926-0941.bundle` | git-ignored (`.gitignore:15`). Each lists 217 heads including 9 `refs/codex/…` refs |
| `.git-backups/…-untracked.tar.gz` (two files) | git-ignored. Each holds 56 carrying members across nested archives (`ARCHIVE-INVENTORY.tsv`) |
| `~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz` (Q198) | outside the repo; a full copy of the folder including `.git`, so it contains everything above by construction. Not opened here |
| `~/mvp6-env/**/tests/loads/*_probe.py` | 15 loose copies found in a partial scan |

## 6. Why this matters for the ruling

- "Keep it out of git" is only true for **commits**. The object database already holds the value, reachable
  through nine local refs. A `git gc` will not remove it while those refs exist.
- Anything that copies the repository wholesale — a bundle made with `--all`, a mirror push, a folder backup —
  carries the value. Two bundles and one folder backup already do.
- The ruling therefore changes from "should the value enter git?" to "the value is in the local repository
  already; should it enter a **commit that gets pushed**?"
