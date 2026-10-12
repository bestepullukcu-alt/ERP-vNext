# Q230 — Commit plan for `feature/mvp6-logistics` (PLAN ONLY — nothing staged, nothing committed)

Snapshot: `git status --porcelain` at 2026-10-02 22:59:51 +03, HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`:
665 entries = 89 modified + 576 untracked + 0 staged, which expand to **7,634 files**.

- **7,423 files** are assigned to **37 commits** below.
- **211 files** are excluded or held and are in **no** pathspec file (`MUST-NOT-COMMIT.tsv`).
- 0 files are unclassified.

## 0. Before anything is staged — three gates that are not this plan's to open

1. **An owner decision currently says "no commit".** OD-Q03a = C, "no commit for now"
   (`docs/records/decisions/2026-09/mvp6-commit-strategy-owner-decision-q03a-01.md`), confirmed still in force on
   27 Sep (`…/mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md:53`). This plan can only be executed after
   the owner re-decides Q03a. GIT-002 §4 requires the explicit request in any case.
2. **Holds need an owner answer first** — section 2 below. In particular the 16 archives that carry the exposed
   signing-secret literal.
3. **The snapshot ages.** Other lanes are writing record folders and appending to the ledgers. Re-run
   `git status --porcelain` before staging; anything not in a pathspec file goes into a trailing commit after
   being classified by the same rules, never into an earlier one.

## 1. How staging is done — explicit paths only

Every commit is staged with `git add --pathspec-from-file=<file>`, where the file lists **exact file paths**, one
per line: no directory, no glob, no `-A`, no `.`. This is the mechanism the earlier prep plan (Q69,
`docs/roadmap/plans/mvp6-commit-plan-01/`) used; CT accepted that plan as preparation.

Why not `git add <folder>`: 45 porcelain entries are folders that contain at least one excluded or held file.
Adding the folder would stage it.

Per commit, in this order (GIT-002 §3):

1. stage from the pathspec file;
2. review 1 — the staged name list must equal the pathspec file exactly (the `diff` prints nothing);
3. review 2 — `git diff --cached`, read in full;
4. if either review shows anything unexpected: stop, and do not commit;
5. commit with the prepared message file, only on the owner's explicit request.

No push is part of this plan. Push is a separate gate (GIT-002 §5).

## 2. What is not committed (`MUST-NOT-COMMIT.tsv`, 211 files)

None of these is git-ignored. Each one appears in `git status`, so a folder-level `git add` would stage it.

| Class | Files | What | Decided by |
|---|---:|---|---|
| EXCLUDE-X1 | 8 | `rework-results.json` at the repo root; 7 files in two `TestResults/` folders (5 `.trx` under the SupplyChain test project, 2 under the architecture tests) | OD-Q03b (X1 + TestResults excluded); D4 |
| EXCLUDE-X2 | 2 | `.claude/settings.local.json` (tracked, machine-local change) and `.claude/settings.local.json.bak-20260926` | OD-Q03b (X2). The `.bak` copy is not named in the decision; same class by content |
| HOLD-H1 | 4 | Secret-review hold: a bearer/JWT literal (`restart.env` + its archive) and a secret assignment (`start-api.sh` + its archive) | OD-Q03b |
| HOLD-D3 | 3 | Unrecorded archives held from commit | D3 (`mvp6-q101-fix-decisions-owner-decision-01.md:16`). D3 names 6 archives; the other 3 are in HOLD-F02 |
| HOLD-D6 | 178 | Files under five open `overlay/` folders in `docs/records/audits/2026-09/*-draft-01/` | D6 (`…:19`) |
| **HOLD-F02** | **16** | **Source archives that contain the pre-Q117 Loads probe files with the exposed signing-secret literal** | **Needs an owner ruling — see below** |

**HOLD-F02 is the finding that matters.** Q101-F02 (HIGH) is a static signing secret in three Loads probe files.
Q117 removed it from the loose files; the working-tree copies are the fixed versions. But 16 `.tar.gz` archives
under `docs/records/audits/2026-09/` and `docs/roadmap/plans/` still contain the old files — matched here by the
sha256 of the archive members (`e5ee0aed…`, `8f6cc448…`, `296c150a…`). The Q69 plan classed all 16 as "S1 —
commit", before F02 was raised. OD-Q03b holds `runtime_probe.py` "until F02 is fixed" and commits "the other
S1"; it does not say what happens to archives that contain the same value. The decision pack states the rule
plainly: "No history rewrite is allowed, so a secret must never enter a commit." So they are held here.

The owner's options, none taken by this plan: keep them out of git (records then cite them by hash only); commit
them after confirming the value is dead; or commit redacted copies.

Two side effects of any hold:

- A record folder's own `SHA256SUMS` lists the held file, so that folder's checksum file will not fully verify
  from git alone.
- Two of the HOLD-F02 archives are also layer sources other records cite
  (`BC-SOURCE.tar.gz` and `successor-source.tar.gz` are BASE-STACK v2 sub-layers).

## 3. Order, and why

| Step | Commits | Why here |
|---|---|---|
| 1 | `C01` | **Must be first.** `docs-path-authority.json` pins sealed inputs by SHA-256, and the docs-path guard reads it. Committing the authority, the guard test, the contracts and the pinned inputs together keeps the guard consistent at every later commit (Q69 plan, C01). |
| 2 | `S1` `S2` `S3` | Product source that was in the tree before today. `S3` holds `Program.cs`, which registers Carrier and Loads, so it follows `S1` and `S2`. |
| 3 | `S5` `S6` `S7` `S8` | The four module cores Q202a wrote. Nothing registers them yet, so their order among themselves is free. They need `S3` (project files, module registration). |
| 4 | `S9` `S10` `S11` | Other services, frontend and gateway tests, root files. Independent of each other. |
| 5 | `A1` `K1` `D1` | Governance, packs, owner decisions. Text only. |
| 6 | `R1a` … `R7f` | September records by module family, then the Q-series. Records cite source and decisions, so they follow. |
| 7 | `R8a` `R8b` | October records, including today's 11 accepted WPs. |
| 8 | `L1a` `L1b` `T1` | Plans, backlog, guides; evidence kit. |
| 9 | `G1` | **Last.** The ledgers are appended to by other lanes and point at everything else. |
| 10 | trailing | Whatever appeared after the snapshot — including this record folder. |

Dependencies that are real: `C01` before everything; `S1`, `S2` before `S3`; `S3` before `S5`–`S8`. Everything
else is ordered for readability.

**Not verified:** that the tree builds after each intermediate source commit. Only the final state (all of
`S1`–`S11`) is the tree Q208 built. Build once after `S11`, not after each.

## 4. Commits

Message style follows the repo's history (`git log --oneline -20`): `type(scope): subject`, for example
`feat(supply-chain): implement MOD-0183 shipment core` and `docs(scm): front-load MVP-6 consumed seams …`.

