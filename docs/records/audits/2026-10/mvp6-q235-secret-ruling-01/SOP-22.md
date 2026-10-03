# Q235 — Ruling material for the exposed signing secret in untracked archives · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q235 · `AL-CT-SECRET-RULING` (INS) · required E1 — **reached: E1** (static; nothing built, started, edited or moved) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `security-agent` → Phase B `read-only-auditor` (mode **strict**, no fixes) → Phase C `documentation-writer` (this folder only) |
| §17.4 note | "§17.4's security-agent row is shaped for endpoint authorization review; three of its four fields are n/a for a leaked-credential audit. CT named them rather than leaving them blank." |
| Read first | `AGENTS.md` and the two agent files (unchanged, same sha256 as earlier today) · `security-agent.md` · `security-jwt.md` · `configuration-safety.md` · Q230 `MUST-NOT-COMMIT.tsv`, `SOP-22.md` |
| Start / End (Europe/Istanbul) | 2026-10-02 23:16:05 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        RULING MATERIAL DELIVERED. No decision taken. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 89 " M" · 576 "??" · 0 staged = 665 at start and at end.
                      No .git/index.lock. No git diff, no fetch, no ls-remote, no git write, no gh.
Changed files:        this record folder only. Scratch: /private/tmp/q235/ (scripts and JSON without the value).
The secret value:     never printed, never written — not in this folder, not in scratch, not in a command line.
                      It was held in process memory only. The folder was checked for it before closing (§5).
```

## 1. Answers

| Task | Answer | File |
|---|---|---|
| 1. Inventory | **17 archives, not 16.** Q230's 16 are confirmed by member hash. The 17th is `mvp6-mod0185-fresh-evidence-ver-03/evidence.tar.gz`: three probes inside a nested archive plus two more scripts carrying the value. Two git-ignored backup archives under `.git-backups/` carry it too | `ARCHIVE-INVENTORY.tsv` (55 rows) |
| 2. Nature | Symmetric HMAC-SHA256 JWT signing secret; 47 characters; a descriptive test phrase, not random key material. The service reads its secret from `JwtSettings:Secret` (`Program.cs:25`) | `LIVE-OR-DEAD.md` §1 |
| 3. Live or dead | **DEAD in the repository. CANNOT DETERMINE outside it.** | `LIVE-OR-DEAD.md` |
| 4. Git history | **Not in any commit, branch, tag, stash or remote-tracking ref. But it IS in the local object database**, reachable through nine `refs/codex/…` checkpoint refs, and in two backup bundles | `GIT-HISTORY-CHECK.md` |
| 5. Options | (a) loses 9 stack files from git and leaves 16 checksum files incomplete; (b) is permanent and contradicts two recorded rules; (c) breaks 17 hashes and 178 references | `OPTION-CONSEQUENCES.md` |
| 6. Recommendation | (a) plus custody outside git, a held-artifacts manifest, and the 17th archive added to the hold. Decision owner: the repository owner | `RECOMMENDATION.md` |

## 2. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q235-1** | 🔴 Changes the ruling | **The value is already inside the local git repository.** Three carrier blobs are reachable through nine `refs/codex/turn-diffs/checkpoints/…` refs (tree objects written by the Codex desktop tool); five more carrier blobs are dangling. No commit contains them and no branch, tag, stash or remote-tracking ref reaches them. Whether those refs exist on GitHub cannot be determined without a network read. | `GIT-HISTORY-CHECK.md` §2-§4 |
| **F-Q235-2** | 🔴 Blocker for the commit plan | **A 17th archive carries the value and is not held by Q230:** `docs/records/audits/2026-09/mvp6-mod0185-fresh-evidence-ver-03/evidence.tar.gz` (sha256 `21d8aad3…`). Q230 assigned it to a commit. Its scan did not open nested archives and did not look for the value itself. | `ARCHIVE-INVENTORY.tsv` rows with "NO — not in Q230" |
| **F-Q235-3** | 🟠 High | Five file versions beyond the three known ones carry the value: `mod0185-fresh-recovery.py` and `mod0185-fresh-startup.py` in the 17th archive, plus dangling blobs. Matching by the three known file hashes alone is not enough. | `ARCHIVE-INVENTORY.tsv`; `GIT-HISTORY-CHECK.md` §2 |
| **F-Q235-4** | 🟠 High | "Dead" cannot be established from the repository. Nothing in the tree reads the value and Q117 replaced it with a per-run value, not a new literal. Whether any running service was ever configured with it is unknown here. | `LIVE-OR-DEAD.md` §2-§3 |
| **F-Q235-5** | 🟠 High | Committing as-is contradicts two recorded rules: "a secret must never enter a commit" (`DECISION-PACK.md:71`) and `security-jwt.md:34`. Neither has an exception for dead or test values. | `OPTION-CONSEQUENCES.md` (b) |
| **F-Q235-6** | 🟡 Medium | Without the archives, 9 BASE-STACK v2 files exist nowhere else in the repository — the held and shared-seam files (gateway tests, `ocelot.json`, three `Program.cs`, `ports.md`, the MOD-0183 pack). The other 756 of the 782 members of the two BASE inputs have a byte-equal copy in the tree or in `HEAD`. | `OPTION-CONSEQUENCES.md` (a) |
| **F-Q235-7** | 🟡 Medium | Copies also sit in two git-ignored bundles and two git-ignored tarballs under `.git-backups/`, in the Q198 backup, and as 15 loose files under `~/mvp6-env` (partial scan). | `GIT-HISTORY-CHECK.md` §5 |
| **F-Q235-8** | 🟡 Medium | Redaction breaks 17 archive hashes, 16 checksum files, 178 references in other records, 3 BASE-manifest rows and 3 Q117 preimage checks. | `OPTION-CONSEQUENCES.md` (c) |
| **F-Q235-9** | ⚪ Low (own conduct) | The scan script called `git stash list` once to count stashes. It is read-only, but the prompt says never to run `git stash` in any form. The later checks used `for-each-ref` and `log -g refs/stash` instead. | this record |
| **F-Q235-10** | ⚪ Info | The 16 archives Q230 listed are confirmed exactly: each holds the three pre-fix probe files with the recorded hashes. | `ARCHIVE-INVENTORY.tsv` |

## 3. Method

- The value was obtained in memory from one archive member whose sha256 is a known pre-fix hash, then searched for
  as bytes — not only by file hash.
- Working tree: 26,845 files read (all except `.git/`, `node_modules/`); 412 archives opened, nested up to four
  levels; 105,281 members read.
- Git: every object in the database (76,537), then reachability per ref kind.
- Pins: each archive's sha256 searched in every text record under `docs/`.
- Output of every script was limited to paths, hashes and counts; quoted strings were masked.

## 4. Known gaps

- The remote was not contacted. `refs/codex/*` on GitHub: unknown.
- `~/mvp6-env` was scanned partially (time limit). The Q198 backup and the `.git-backups` bundles were not opened;
  what they contain is inferred from how they were made and from `bundle list-heads`.
- Only this one value was searched. Other secrets inside nested archives were not looked for.
- Encoded forms (base64, hex, compressed streams other than gzip/tar/zip) were not searched.
- The five dangling blobs were not identified by path.

## 5. Refused / not done

- The value is not printed anywhere. Before closing, every file in this folder and in the scratch folder was
  checked in memory for the value: 0 occurrences.
- No archive was edited, deleted, redacted or moved. Nothing was extracted into the repository.
- Nothing was rotated. No `.gitignore` line was added. No ref was deleted. No `git gc`.
- No recommendation was carried out.

## 6. Files

`SOP-22.md` · `ARCHIVE-INVENTORY.tsv` · `LIVE-OR-DEAD.md` · `GIT-HISTORY-CHECK.md` · `OPTION-CONSEQUENCES.md` ·
`RECOMMENDATION.md` · `ARTIFACTS.sha256`

Return to CT; CT decides.
