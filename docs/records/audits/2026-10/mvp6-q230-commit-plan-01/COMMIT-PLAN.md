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

### 1. `C01` — docs(scm): publish MVP6 contracts and annexes with the docs-path guard authority

57 files · 3.4 MB · 0 binary · 1 over 1 MB · 4 modified tracked, 53 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/C01.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/C01.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/C01.txt
```

Message (`messages/C01.txt`):

```text
docs(scm): publish MVP6 contracts and annexes with the docs-path guard authority

Contracts (2 changed YAMLs, 8 annexes), docs-path-authority.json, DocsPathGuardTests.cs, the docs-organization.md candidate rule and the sealed inputs the authority pins by SHA-256. Same file set as C01 of the Q69 prep plan.
Work packages: Q69 (plan), Q62/Q72-Q76 publication WPs as recorded in the folders.
57 files from pathspec/C01.txt (Q230 commit plan).
```

### 2. `S1` — feat(supply-chain): add MOD-0184 Carrier bounded source

37 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 37 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S1.txt
```

Message (`messages/S1.txt`):

```text
feat(supply-chain): add MOD-0184 Carrier bounded source

Carrier feature source, tests and probes as they stand in the working tree.
Work packages: MOD-0184 WPs; Q202a for files it wrote.
37 files from pathspec/S1.txt (Q230 commit plan).
```

### 3. `S2` — feat(supply-chain): add MOD-0185 Loads bounded source

46 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 46 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S2.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S2.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S2.txt
```

Message (`messages/S2.txt`):

```text
feat(supply-chain): add MOD-0185 Loads bounded source

Loads feature source, tests and probes. The three Loads probes are the Q117 versions (no static secret).
Work packages: MOD-0185 WPs; Q117; Q202a.
46 files from pathspec/S2.txt (Q230 commit plan).
```

### 4. `S3` — feat(supply-chain): add MOD-0183 shipment root read, module registration and service composition

24 files · 0.1 MB · 0 binary · 0 over 1 MB · 10 modified tracked, 14 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S3.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S3.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S3.txt
```

Message (`messages/S3.txt`):

```text
feat(supply-chain): add MOD-0183 shipment root read, module registration and service composition

Shipment root read, ModuleRegistration, Program.cs (Carrier + Load composition only), .csproj, appsettings, shared test setup.
Work packages: MOD-0183 WPs; Q202a; Program.cs is the held pre-integration composition (Q209 open).
24 files from pathspec/S3.txt (Q230 commit plan).
```

### 5. `S5` — feat(supply-chain): add MOD-0186 Returns bounded core (not composed)

46 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 46 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S5.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S5.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S5.txt
```

Message (`messages/S5.txt`):

```text
feat(supply-chain): add MOD-0186 Returns bounded core (not composed)

Returns source, tests and probes. Not registered in Program.cs.
Work packages: Q202a; verified by Q210.
46 files from pathspec/S5.txt (Q230 commit plan).
```

### 6. `S6` — feat(supply-chain): add MOD-0187 Claims bounded core (not composed)

47 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 47 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S6.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S6.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S6.txt
```

Message (`messages/S6.txt`):

```text
feat(supply-chain): add MOD-0187 Claims bounded core (not composed)

Claims source, tests and probes. Not registered in Program.cs.
Work packages: Q202a; verified by Q211.
47 files from pathspec/S6.txt (Q230 commit plan).
```

### 7. `S7` — feat(supply-chain): add MOD-0190 S&OP bounded core (not composed)

38 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 38 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S7.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S7.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S7.txt
```

Message (`messages/S7.txt`):

```text
feat(supply-chain): add MOD-0190 S&OP bounded core (not composed)

S&OP source and tests (the 38 owned paths). Not registered in Program.cs.
Work packages: Q202a; verified by Q212.
38 files from pathspec/S7.txt (Q230 commit plan).
```

### 8. `S8` — feat(supply-chain): add MOD-0192 Capacity bounded core (not composed)

45 files · 0.1 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 45 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S8.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S8.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S8.txt
```

Message (`messages/S8.txt`):

```text
feat(supply-chain): add MOD-0192 Capacity bounded core (not composed)

Capacity source and tests. Not registered in Program.cs.
Work packages: Q202a; verified by Q213.
45 files from pathspec/S8.txt (Q230 commit plan).
```

### 9. `S9` — feat(platform): add service-to-service scope resolution and lane-configurable test Mongo URIs

40 files · 0.3 MB · 0 binary · 0 over 1 MB · 26 modified tracked, 14 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S9.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S9.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S9.txt
```

Message (`messages/S9.txt`):

```text
feat(platform): add service-to-service scope resolution and lane-configurable test Mongo URIs

Auth, MDM, Platform, HCM, TEP and CRM service files from the Auth-22, A12-360, Q117, Q121 and Q131 layers.
Work packages: Q202a (BASE L2/L3, Q117, Q121, Q131).
40 files from pathspec/S9.txt (Q230 commit plan).
```

### 10. `S10` — feat(web): add Supply Chain shipment UI, shared resources and gateway route tests

40 files · 0.5 MB · 0 binary · 0 over 1 MB · 9 modified tracked, 31 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S10.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S10.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S10.txt
```

Message (`messages/S10.txt`):

```text
feat(web): add Supply Chain shipment UI, shared resources and gateway route tests

frontend/ and gateway test files written by Q202a. ocelot.json itself is not in this commit (held seam).
Work packages: Q202a.
40 files from pathspec/S10.txt (Q230 commit plan).
```

### 11. `S11` — chore(repo): apply BASE-STACK v2 root files: AGENTS.md, scripts, test-env helper

14 files · 0.2 MB · 0 binary · 0 over 1 MB · 13 modified tracked, 1 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S11.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/S11.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/S11.txt
```

Message (`messages/S11.txt`):

```text
chore(repo): apply BASE-STACK v2 root files: AGENTS.md, scripts, test-env helper

AGENTS.md, smoke/watch scripts, scripts/test-env/mvp6-test-mongo-env.sh.
Work packages: Q202a; Q131.
14 files from pathspec/S11.txt (Q230 commit plan).
```

### 12. `A1` — docs(governance): apply SOP v2.5 r2, dispatch-wp workflow and the pre-Mac UI checklist patch

4 files · 0.1 MB · 0 binary · 0 over 1 MB · 3 modified tracked, 1 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/A1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/A1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/A1.txt
```

Message (`messages/A1.txt`):

```text
docs(governance): apply SOP v2.5 r2, dispatch-wp workflow and the pre-Mac UI checklist patch

.antigravity/ agent and workflow edits plus the SOP text.
Work packages: Q144/Q145, Q167.
4 files from pathspec/A1.txt (Q230 commit plan).
```

### 13. `K1` — docs(scm): update supply-chain module packs, DCP-009 and CRM packs to the applied state

22 files · 1.3 MB · 0 binary · 0 over 1 MB · 22 modified tracked, 0 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/K1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/K1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/K1.txt
```

Message (`messages/K1.txt`):

```text
docs(scm): update supply-chain module packs, DCP-009 and CRM packs to the applied state

Module packs MOD-0183…0192, DCP-009 and the commercial-suite packs written by Q202a.
Work packages: pack-apply WPs; Q202a.
22 files from pathspec/K1.txt (Q230 commit plan).
```

### 14. `D1` — docs(records): add MVP6 owner decision records (2026-09)

49 files · 0.2 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 49 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/D1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/D1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/D1.txt
```

Message (`messages/D1.txt`):

```text
docs(records): add MVP6 owner decision records (2026-09)

Owner decision records.
Work packages: as named in each file.
49 files from pathspec/D1.txt (Q230 commit plan).
```

### 15. `R1a` — docs(records): add MVP6 records: Shipment / MOD-0183 (part a)

445 files · 7.7 MB · 23 binary · 2 over 1 MB · 0 modified tracked, 445 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R1a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R1a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R1a.txt
```

Message (`messages/R1a.txt`):

```text
docs(records): add MVP6 records: Shipment / MOD-0183 (part a)

26 folders/items, first '(files at month level)', last 'mvp6-shipment-line-accessibility-independent-ver-01'.
Work packages: as named in each folder.
445 files from pathspec/R1a.txt (Q230 commit plan).
```

### 16. `R1b` — docs(records): add MVP6 records: Shipment / MOD-0183 (part b)

381 files · 7.1 MB · 13 binary · 1 over 1 MB · 0 modified tracked, 381 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R1b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R1b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R1b.txt
```

Message (`messages/R1b.txt`):

```text
docs(records): add MVP6 records: Shipment / MOD-0183 (part b)

21 folders/items, first 'mvp6-shipment-line-accessibility-rework-01', last 'mvp6-shipment-ui-presentation-ver-01'.
Work packages: as named in each folder.
381 files from pathspec/R1b.txt (Q230 commit plan).
```

### 17. `R2a` — docs(records): add MVP6 records: Carrier / MOD-0184 (part a)

446 files · 53.3 MB · 18 binary · 12 over 1 MB · 0 modified tracked, 446 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R2a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R2a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R2a.txt
```

Message (`messages/R2a.txt`):

```text
docs(records): add MVP6 records: Carrier / MOD-0184 (part a)

30 folders/items, first '(files at month level)', last 'mvp6-carrier-ui-quality-close-01'.
Work packages: as named in each folder.
446 files from pathspec/R2a.txt (Q230 commit plan).
```

### 18. `R2b` — docs(records): add MVP6 records: Carrier / MOD-0184 (part b)

79 files · 0.2 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 79 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R2b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R2b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R2b.txt
```

Message (`messages/R2b.txt`):

```text
docs(records): add MVP6 records: Carrier / MOD-0184 (part b)

4 folders/items, first 'mvp6-carrier-ui-ver-01', last 'mvp6-shipment-carrier-predecessor-pin-01'.
Work packages: as named in each folder.
79 files from pathspec/R2b.txt (Q230 commit plan).
```

### 19. `R3` — docs(records): add MVP6 records: Loads / MOD-0185

186 files · 2.4 MB · 2 binary · 0 over 1 MB · 0 modified tracked, 186 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R3.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R3.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R3.txt
```

Message (`messages/R3.txt`):

```text
docs(records): add MVP6 records: Loads / MOD-0185

23 folders/items, first '(files at month level)', last 'mvp6-mod0185-r2-candidate-handoff'.
Work packages: as named in each folder.
186 files from pathspec/R3.txt (Q230 commit plan).
```

### 20. `R4a` — docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part a)

376 files · 12.1 MB · 47 binary · 2 over 1 MB · 0 modified tracked, 376 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R4a.txt
```

Message (`messages/R4a.txt`):

```text
docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part a)

18 folders/items, first 'mod-0186-prep-02', last 'mvp6-mod0186-r01-ct-close-01'.
Work packages: as named in each folder.
376 files from pathspec/R4a.txt (Q230 commit plan).
```

### 21. `R4b` — docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part b)

448 files · 4.6 MB · 6 binary · 0 over 1 MB · 0 modified tracked, 448 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R4b.txt
```

Message (`messages/R4b.txt`):

```text
docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part b)

10 folders/items, first 'mvp6-mod0186-r01-independent-ver-01', last 'mvp6-mod0187-e4-rework-scope-01'.
Work packages: as named in each folder.
448 files from pathspec/R4b.txt (Q230 commit plan).
```

### 22. `R4c` — docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part c)

281 files · 14.8 MB · 13 binary · 3 over 1 MB · 0 modified tracked, 281 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4c.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R4c.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R4c.txt
```

Message (`messages/R4c.txt`):

```text
docs(records): add MVP6 records: Returns / MOD-0186 and Claims / MOD-0187 (part c)

15 folders/items, first 'mvp6-mod0187-http-composition-01', last 'mvp6-returns-ui-draft-02'.
Work packages: as named in each folder.
281 files from pathspec/R4c.txt (Q230 commit plan).
```

### 23. `R5a` — docs(records): add MVP6 records: S&OP / Capacity (MOD-0190/0192) and BC successor (part a)

427 files · 11.3 MB · 22 binary · 3 over 1 MB · 0 modified tracked, 427 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R5a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R5a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R5a.txt
```

Message (`messages/R5a.txt`):

```text
docs(records): add MVP6 records: S&OP / Capacity (MOD-0190/0192) and BC successor (part a)

43 folders/items, first 'mvp6-bc-integration-successor-01', last 'mvp6-mod0192-hosted-evidence-ver-01'.
Work packages: as named in each folder.
427 files from pathspec/R5a.txt (Q230 commit plan).
```

### 24. `R5b` — docs(records): add MVP6 records: S&OP / Capacity (MOD-0190/0192) and BC successor (part b)

237 files · 11.6 MB · 11 binary · 6 over 1 MB · 0 modified tracked, 237 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R5b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R5b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R5b.txt
```

Message (`messages/R5b.txt`):

```text
docs(records): add MVP6 records: S&OP / Capacity (MOD-0190/0192) and BC successor (part b)

26 folders/items, first 'mvp6-mod0192-http-process-dev-01', last 'mvp6-sandop-publication-guard-disposition-01'.
Work packages: as named in each folder.
237 files from pathspec/R5b.txt (Q230 commit plan).
```

### 25. `R6` — docs(records): add MVP6 records: cross-module, publication/guard and integration

232 files · 6.9 MB · 14 binary · 2 over 1 MB · 0 modified tracked, 232 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R6.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R6.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R6.txt
```

Message (`messages/R6.txt`):

```text
docs(records): add MVP6 records: cross-module, publication/guard and integration

33 folders/items, first '(files at month level)', last 'mvp6-three-scope-application-01'.
Work packages: as named in each folder.
232 files from pathspec/R6.txt (Q230 commit plan).
```

### 26. `R7a` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part a)

136 files · 11.6 MB · 11 binary · 4 over 1 MB · 0 modified tracked, 136 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7a.txt
```

Message (`messages/R7a.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part a)

12 folders/items, first '(files at month level)', last 'mvp6-q121c-supplychain-rerun-01'.
Work packages: Q101, Q101B, Q103, Q114, Q117, Q119, Q121, Q121B, Q121C.
136 files from pathspec/R7a.txt (Q230 commit plan).
```

### 27. `R7b` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part b)

455 files · 27.0 MB · 91 binary · 3 over 1 MB · 0 modified tracked, 455 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7b.txt
```

Message (`messages/R7b.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part b)

1 folders/items, first 'mvp6-q122-ver-claims-v3-01', last 'mvp6-q122-ver-claims-v3-01'.
Work packages: Q122.
455 files from pathspec/R7b.txt (Q230 commit plan).
```

### 28. `R7c` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part c)

376 files · 15.9 MB · 42 binary · 3 over 1 MB · 0 modified tracked, 376 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7c.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7c.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7c.txt
```

Message (`messages/R7c.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part c)

28 folders/items, first 'mvp6-q126-apply-01', last 'mvp6-q24b-kit-validation-01'.
Work packages: Q24B, Q126, Q128, Q129, Q131A, Q139, Q141, Q142, Q144, Q145, Q152, Q154, Q162, Q163, Q164, Q165, Q167, Q168, Q170, Q171, Q172, Q173, Q174, Q176, Q177, Q179, Q180, Q184.
376 files from pathspec/R7c.txt (Q230 commit plan).
```

### 29. `R7d` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part d)

327 files · 19.0 MB · 38 binary · 5 over 1 MB · 0 modified tracked, 327 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7d.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7d.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7d.txt
```

Message (`messages/R7d.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part d)

1 folders/items, first 'mvp6-q64b-claims-build-01', last 'mvp6-q64b-claims-build-01'.
Work packages: Q64B.
327 files from pathspec/R7d.txt (Q230 commit plan).
```

### 30. `R7e` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part e)

432 files · 29.1 MB · 102 binary · 6 over 1 MB · 0 modified tracked, 432 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7e.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7e.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7e.txt
```

Message (`messages/R7e.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part e)

3 folders/items, first 'mvp6-q64d-claims-runtime-01', last 'mvp6-q82-ver-ui-pack-190-192'.
Work packages: Q64D, Q65B, Q82.
432 files from pathspec/R7e.txt (Q230 commit plan).
```

### 31. `R7f` — docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part f)

275 files · 24.2 MB · 56 binary · 7 over 1 MB · 0 modified tracked, 275 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7f.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R7f.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R7f.txt
```

Message (`messages/R7f.txt`):

```text
docs(records): add MVP6 records: Q-series work packages, CT verdicts and BASE-STACK (2026-09) (part f)

3 folders/items, first 'mvp6-q84b-sop-mac-01', last 'mvp6-q95-ver-q93-apply'.
Work packages: Q84B, Q87, Q95.
275 files from pathspec/R7f.txt (Q230 commit plan).
```

### 32. `R8a` — docs(records): add MVP6 records: work packages and CT verdicts (2026-10) (part a)

299 files · 17.2 MB · 16 binary · 6 over 1 MB · 0 modified tracked, 299 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R8a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R8a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R8a.txt
```

Message (`messages/R8a.txt`):

```text
docs(records): add MVP6 records: work packages and CT verdicts (2026-10) (part a)

16 folders/items, first '(files at month level)', last 'mvp6-q221-q209-prework-01'.
Work packages: Q185, Q189, Q198, Q201, Q202A, Q205, Q208, Q210, Q211, Q212, Q213, Q214, Q216, Q220, Q221.
299 files from pathspec/R8a.txt (Q230 commit plan).
```

### 33. `R8b` — docs(records): add MVP6 records: work packages and CT verdicts (2026-10) (part b)

244 files · 24.4 MB · 80 binary · 5 over 1 MB · 0 modified tracked, 244 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R8b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/R8b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/R8b.txt
```

Message (`messages/R8b.txt`):

```text
docs(records): add MVP6 records: work packages and CT verdicts (2026-10) (part b)

3 folders/items, first 'mvp6-q88b-capacity-mac-01', last 'mvp6-returns-ui-draft-03'.
Work packages: Q88B.
244 files from pathspec/R8b.txt (Q230 commit plan).
```

### 34. `L1a` — docs(plans): add MVP6 plans, backlog and guides (part a)

450 files · 3.2 MB · 0 binary · 0 over 1 MB · 1 modified tracked, 449 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/L1a.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/L1a.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/L1a.txt
```

Message (`messages/L1a.txt`):

```text
docs(plans): add MVP6 plans, backlog and guides (part a)

37 folders/items, first '(single files)', last 'operations'.
Work packages: as named in each folder.
450 files from pathspec/L1a.txt (Q230 commit plan).
```

### 35. `L1b` — docs(plans): add MVP6 plans, backlog and guides (part b)

332 files · 2.2 MB · 7 binary · 0 over 1 MB · 0 modified tracked, 332 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/L1b.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/L1b.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/L1b.txt
```

Message (`messages/L1b.txt`):

```text
docs(plans): add MVP6 plans, backlog and guides (part b)

37 folders/items, first 'mvp6-loads-guard-binding-prep-01', last 'scm-planning-analysis-01'.
Work packages: as named in each folder.
332 files from pathspec/L1b.txt (Q230 commit plan).
```

### 36. `T1` — chore(tooling): install MVP6 evidence kit v1.2

40 files · 0.2 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 40 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/T1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/T1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/T1.txt
```

Message (`messages/T1.txt`):

```text
chore(tooling): install MVP6 evidence kit v1.2

scripts/evidence-kit/, the kit guide and its install records. Same set as C14 of the Q69 plan.
Work packages: Q24a.
40 files from pathspec/T1.txt (Q230 commit plan).
```

### 37. `G1` — docs(pilot): add MVP6 process-pilot ledgers and lane prompts

10 files · 0.2 MB · 0 binary · 0 over 1 MB · 0 modified tracked, 10 new

```bash
GIT_OPTIONAL_LOCKS=0 git add --pathspec-from-file=docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/G1.txt
```

```bash
git diff --cached --name-only | sort | diff - <(sort docs/records/audits/2026-10/mvp6-q230-commit-plan-01/pathspec/G1.txt)
```

```bash
git diff --cached --stat | tail -1
```

```bash
git diff --cached
```

Only after both reviews pass and the owner explicitly asks for the commit (GIT-002 §4):

```bash
git commit -F docs/records/audits/2026-10/mvp6-q230-commit-plan-01/messages/G1.txt
```

Message (`messages/G1.txt`):

```text
docs(pilot): add MVP6 process-pilot ledgers and lane prompts

CT-QUEUE.tsv, MILESTONE-EVENTS.tsv and the other pilot ledger files. Commit LAST: other lanes append to these files.
Work packages: all pilot WPs.
10 files from pathspec/G1.txt (Q230 commit plan).
```
