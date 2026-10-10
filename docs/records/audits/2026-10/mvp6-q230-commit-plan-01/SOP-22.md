# Q230 — Commit plan for the uncommitted MVP6 working tree · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q230 · `AL-CT-COMMIT-PLAN` (INS) · required E1 (static) — **reached: E1**. A plan only. |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` (mode **worktree-read-only**; no fixes, no staging, no commit) → Phase B `documentation-writer` (this folder only) |
| Scope | The 665 porcelain entries of `feature/mvp6-logistics`; GIT-002 §2, §3, §4, §6 |
| Read first | `AGENTS.md`, `read-only-auditor.md`, `documentation-writer.md` (read in full earlier in this session; hashes re-checked, unchanged) · `git-safety.md` (GIT-002, whole file) · `git-backup-policy.md` (GIT-001) — read for this WP |
| Session note | The prompt asks for its own session. This run shares a session with Q201–Q220. |
| Start / End (Europe/Istanbul) | 2026-10-02 22:59:51 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        PLAN PREPARED. Not executable until the owner re-decides OD-Q03a. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      665 entries = 89 " M" + 576 "??" + 0 staged, at start and at end (see ARTIFACTS.sha256)
                      No .git/index.lock.
Git commands run:     status --porcelain (and -uall) · log --oneline -20 · rev-parse · rev-list --count ·
                      remote -v · check-ignore · cat-file blob. No add, commit, stash, reset, clean, checkout,
                      push, fetch, gh. `git diff` was not run in any form.
Changed files:        this record folder only
Decisions:            none taken
Out-of-scope changes: none. No backup was created.
```

## 1. Result in one table

| | Files | Where |
|---|---:|---|
| Porcelain entries classified | 665 of 665 | `ENTRY-CLASSIFICATION.tsv` |
| Files behind those entries | 7,634 | `FILE-ASSIGNMENT.tsv` (one row per file) |
| Assigned to a commit | 7,423 in **37 commits** | `COMMIT-PLAN.md`, `pathspec/`, `messages/` |
| Must not commit (excluded or held) | 211 | `MUST-NOT-COMMIT.tsv` |
| Unclassified | **0** | `UNCLASSIFIED.tsv` (header only) |

The 37 pathspec files are disjoint and together list exactly the 7,423 files.

## 2. Groups — the prompt's six, refined

| Prompt group | In this plan | Commits | Files | Why refined |
|---|---|---|---:|---|
| G1 pilot ledgers | `G1` — the whole `mvp6-process-pilot-01/` folder | 1 | 10 | The two ledgers sit in one untracked folder with 8 sibling ledger and prompt files. Committed last. |
| G2 CT record folders | `R8a`, `R8b` (October) | 2 | 543 | Split by folder to stay under 450 files per commit. Per-WP commits would be 22 commits for no gain; the pathspec files are sorted by folder, so a WP can still be lifted out. |
| — (not in the prompt) | `R1a`…`R7f` (September records), `D1` (decisions) | 18 | 5,588 | **The tree holds all of September's records too, not only today's.** 355 of the 665 entries are September record folders. They cannot be left out of a plan for "all 665". |
| G3 Q202a source | `S1`–`S11` | 10 | 377 | 277 of these were written by Q202a; the other 100 are product files that were already in the tree (the Q69 plan's product commits, or byte-equal to the Q131 layer). Q202a wrote 291 classified files in all; the rest of them are packs (`K1`) and files in other groups. Split by module so each diff is readable. |
| G4 packs and governance | `K1`, `A1`, `C01` | 3 | 83 | `C01` is separate and first: the docs-path guard authority pins files by hash. |
| — | `L1a`, `L1b`, `T1` (plans, backlog, guides, evidence kit) | 3 | 822 | Roadmap folders and the kit are neither records nor packs. |
| G5 must not commit | EXCLUDE-X1, EXCLUDE-X2 | — | 10 | Owner decision OD-Q03b |
| — | HOLD-H1, HOLD-D3, HOLD-D6, HOLD-F02 | — | 201 | Owner decisions OD-Q03b, D3, D6 — and one hold that needs a ruling (F-Q230-2) |
| G6 unclassified | — | — | 0 | Ten files had no rule at first; each was then placed by a ledger row or a prior record (`FILE-ASSIGNMENT.tsv`, column `reason`) |

Rules are applied in a fixed order by `tools/plan.py`; the reason column of every row names the rule and, where one
exists, the record.

## 3. GIT-002 checks

| Rule | Finding |
|---|---|
| §1 branch | The branch is `feature/mvp6-logistics`, **not `main`** — confirmed. Its name does not follow `AGENTS.md` §9 (`feature/{domain}/{module-id}-{slug}`); the owner recorded that deviation once (OD-Q03c = A). |
| §1 / §5 push | **No commit in this plan needs a push.** `origin/feature/mvp6-logistics` is the upstream and is level with HEAD (0 ahead, 0 behind, from local refs; no fetch was run). After the commits the branch would be 37 ahead, locally. Push stays a separate gate. |
| §2 dirty tree | The tree is dirty by design; 665 is the known state. |
| §3 staging | Explicit file paths only, through `--pathspec-from-file`. No `-A`, no `.`, no folder, no glob. |
| §3 two reviews | Possible in full for 18 commits. **Not realistic line by line for the 19 record commits** (about 1.16 million lines). See `REVIEW-BURDEN.md`. |
| §4 commit gate | Not opened. **OD-Q03a = "no commit for now" is still the recorded owner decision.** |
| §6 destructive commands / backup | No commit in this plan is destructive, so §6 does not require a new backup. The Q198 archive exists; it was taken at 15:34–15:48 and so does **not** contain Q202a's source or any record written after it. |

## 4. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q230-1** | 🔴 Blocker | The recorded owner decision is still **OD-Q03a = C, no commit**. Q03b and Q03c "take effect only when Q03a is re-decided". The plan cannot run until the owner re-decides. | `mvp6-commit-strategy-owner-decision-q03a-01.md`; `mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md:53`; `mvp6-q147-owner-decision-pack-01/DECISION-PACK.md:70` |
| **F-Q230-2** | 🔴 Blocker for 16 files | **16 untracked source archives contain the exposed signing-secret literal (Q101-F02).** The loose probe files were fixed by Q117; the archives still hold the old ones (member sha256 matched). The Q69 plan had marked all 16 "commit". OD-Q03b holds `runtime_probe.py` but does not mention archives. Held here; the owner must rule. | `MUST-NOT-COMMIT.tsv` (HOLD-F02); `mvp6-q117-test-guard-fixes-01/OVERLAY-MANIFEST.tsv:8-10`; `DECISION-PACK.md:71` |
| **F-Q230-3** | 🟠 High | **Seven test-output files and two stray files are not git-ignored** and would be staged by any folder-level add: 5 `.trx` under the SupplyChain test project, 2 files under the architecture tests' `TestResults/`, `rework-results.json`, `.claude/settings.local.json.bak-20260926`. Decision D4 said to add `TestResults/` to `.gitignore`; that has not been done. | `MUST-NOT-COMMIT.tsv` (EXCLUDE-X1, X2); `.gitignore`; `mvp6-q101-fix-decisions-owner-decision-01.md:17` |
| **F-Q230-4** | 🟠 High | GIT-002 §3's line-by-line review cannot be done for the record commits: about 1.16 million text lines and 612 binary files. The rule has no exception. Whether hash verification plus a secret scan may stand in is an owner decision. | `REVIEW-BURDEN.md` |
| **F-Q230-5** | 🟠 High | The tree holds far more than today. 7,634 files: 5,791 are September records never committed, 543 are October records, 377 are product source. "If this tree is lost" covers three weeks of records, not one day. | `FILE-ASSIGNMENT.tsv` |
| **F-Q230-6** | 🟡 Medium | The exclusion refresh that OD-Q03b requires "before any commit" is only partly done here. This WP re-applied the recorded classes and added the F02 archive match and a narrow token-pattern scan (1 hit, already in HOLD-H1). It did **not** re-run the evidence kit's full K08 scan over the roughly 3,500 files added since the Q69 snapshot. | OD-Q03b; `tools/collect.py` |
| **F-Q230-7** | 🟡 Medium | 45 porcelain entries are folders that contain at least one excluded or held file. For those, staging the folder is unsafe; only the pathspec files are safe. | `ENTRY-CLASSIFICATION.tsv`, last column |
| **F-Q230-8** | 🟡 Medium | Holding a file breaks its folder's own checksum file in git: the folder's `SHA256SUMS` names a file that is not there. Two held archives (`BC-SOURCE.tar.gz`, `successor-source.tar.gz`) are BASE-STACK v2 sub-layer sources. | `COMMIT-PLAN.md` §2 |
| **F-Q230-9** | 🟡 Medium | The snapshot is already moving. Sibling lanes write record folders and append to `CT-QUEUE.tsv`. The ledger commit is placed last and a trailing commit is foreseen; the status must be re-read before staging. | `COMMIT-PLAN.md` §0 |
| **F-Q230-10** | 🟡 Medium | Intermediate source commits were not checked for buildability. Only the state after `S11` is the tree Q208 built. | `COMMIT-PLAN.md` §3 |
| **F-Q230-11** | ⚪ Low | `.claude/settings.local.json` is a tracked file with a machine-local change (it also holds the deny rule that blocked Q220). It is excluded per OD-Q03b; it will keep showing as modified. | `MUST-NOT-COMMIT.tsv` |
| **F-Q230-12** | ⚪ Low | October's record set includes records of work that CT did not accept or that is blocked (Q189, Q88b, Q65b verdicts; Q220). They are committed as records; a commit is not an acceptance (K17). | `COMMIT-PLAN.md`, `R8a`, `R8b` |
| **F-Q230-13** | ⚪ Info | An earlier commit plan exists and was reused, not replaced: Q69, `docs/roadmap/plans/mvp6-commit-plan-01/` (snapshot 26 Sep, 4,082 files, 14 commits). All 4,513 of its listed paths are still present. This plan keeps its families and its first commit, and extends them to today's 7,634 files. | `tools/plan.py`, rule 3 |
| **F-Q230-14** | ⚪ Info | No file is over 100 MB (largest 7.7 MB); 71 files are over 1 MB. | `REVIEW-BURDEN.md` |

## 5. Known gaps

- Secret detection is limited to: the three known F02 file hashes, loose and inside `.tar`, `.tar.gz`, `.zip`
  archives (members named `*_probe.py`, not nested archives), and six token patterns. It is not a full scan.
- Classification of September record folders uses folder-name keywords, as the Q69 plan did. A folder with an
  unusual name lands in `R6` (cross-module).
- Line counts for modified files come from a line comparison with the `HEAD` blob; they are estimates of what
  `git diff --cached` would show, not its output.
- Nothing was staged, so no command block in `COMMIT-PLAN.md` has been executed.

## 6. Refused / not done

- No `git add`, commit, stash, reset, clean, checkout or push. No `git diff`. No fetch. No backup created.
- No file outside this folder was written. No hold was resolved; no `.gitignore` line was added.

## 7. Files

`SOP-22.md` · `ENTRY-CLASSIFICATION.tsv` (665 rows) · `MUST-NOT-COMMIT.tsv` (211 rows) · `COMMIT-PLAN.md` ·
`REVIEW-BURDEN.md` · `ARTIFACTS.sha256` — required by the WP.
Supporting: `FILE-ASSIGNMENT.tsv` (7,634 rows) · `UNCLASSIFIED.tsv` (empty) · `pathspec/` (37 files) ·
`messages/` (37 files) · `tools/` (`collect.py`, `plan.py`) · `COMMIT-PLAN.head.md`, `COMMIT-BLOCKS.md`,
`BURDEN-TABLE.md` (the generated parts `COMMIT-PLAN.md` and `REVIEW-BURDEN.md` are assembled from).

Return to CT; CT decides.
