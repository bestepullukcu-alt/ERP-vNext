# Q230 — Review burden per commit

GIT-002 §3 requires two reviews before every commit: the staged **file list** (`git diff --cached --name-only`) and
the staged **content, line by line** (`git diff --cached`). This file says what that costs. It is an estimate from
the working tree, not from a staged index: nothing was staged.

How the numbers were made:

- New (untracked) text file → every line counts as an added line.
- Modified tracked file → added and removed lines from a line comparison of the `HEAD` blob with the working file
  (`git cat-file blob`; `git diff` was not run).
- Binary file (archives, PNG, some logs) → counted, not readable; `git diff --cached` shows only "Binary files differ".

## 1. Per commit

| # | Commit | Files | Modified / new | Text lines to read (+ / −) | Binary files | MB | > 1 MB |
|---:|---|---:|---|---:|---:|---:|---:|
| 1 | `C01` | 57 | 4 / 53 | +26,406 / −166 | 0 | 3.4 | 1 |
| 2 | `S1` | 37 | 0 / 37 | +1,240 / −0 | 0 | 0.1 | 0 |
| 3 | `S2` | 46 | 0 / 46 | +1,835 / −0 | 0 | 0.1 | 0 |
| 4 | `S3` | 24 | 10 / 14 | +577 / −7 | 0 | 0.1 | 0 |
| 5 | `S5` | 46 | 0 / 46 | +1,228 / −0 | 0 | 0.1 | 0 |
| 6 | `S6` | 47 | 0 / 47 | +1,450 / −0 | 0 | 0.1 | 0 |
| 7 | `S7` | 38 | 0 / 38 | +520 / −0 | 0 | 0.1 | 0 |
| 8 | `S8` | 45 | 0 / 45 | +1,834 / −0 | 0 | 0.1 | 0 |
| 9 | `S9` | 40 | 26 / 14 | +1,368 / −35 | 0 | 0.3 | 0 |
| 10 | `S10` | 40 | 9 / 31 | +1,145 / −2 | 0 | 0.5 | 0 |
| 11 | `S11` | 14 | 13 / 1 | +109 / −42 | 0 | 0.2 | 0 |
| 12 | `A1` | 4 | 3 / 1 | +355 / −7 | 0 | 0.1 | 0 |
| 13 | `K1` | 22 | 22 / 0 | +3,184 / −224 | 0 | 1.3 | 0 |
| 14 | `D1` | 49 | 0 / 49 | +1,658 / −0 | 0 | 0.2 | 0 |
| 15 | `R1a` | 445 | 0 / 445 | +41,805 / −0 | 23 | 7.7 | 2 |
| 16 | `R1b` | 381 | 0 / 381 | +32,041 / −0 | 13 | 7.1 | 1 |
| 17 | `R2a` | 446 | 0 / 446 | +118,647 / −0 | 18 | 53.3 | 12 |
| 18 | `R2b` | 79 | 0 / 79 | +1,670 / −0 | 0 | 0.2 | 0 |
| 19 | `R3` | 186 | 0 / 186 | +28,749 / −0 | 2 | 2.4 | 0 |
| 20 | `R4a` | 376 | 0 / 376 | +53,843 / −0 | 47 | 12.1 | 2 |
| 21 | `R4b` | 448 | 0 / 448 | +28,660 / −0 | 6 | 4.6 | 0 |
| 22 | `R4c` | 281 | 0 / 281 | +62,134 / −0 | 13 | 14.8 | 3 |
| 23 | `R5a` | 427 | 0 / 427 | +65,989 / −0 | 22 | 11.3 | 3 |
| 24 | `R5b` | 237 | 0 / 237 | +78,115 / −0 | 11 | 11.6 | 6 |
| 25 | `R6` | 232 | 0 / 232 | +39,579 / −0 | 14 | 6.9 | 2 |
| 26 | `R7a` | 136 | 0 / 136 | +39,397 / −0 | 11 | 11.6 | 4 |
| 27 | `R7b` | 455 | 0 / 455 | +62,638 / −0 | 91 | 27.0 | 3 |
| 28 | `R7c` | 376 | 0 / 376 | +51,879 / −0 | 42 | 15.9 | 3 |
| 29 | `R7d` | 327 | 0 / 327 | +96,564 / −0 | 38 | 19.0 | 5 |
| 30 | `R7e` | 432 | 0 / 432 | +95,800 / −0 | 102 | 29.1 | 6 |
| 31 | `R7f` | 275 | 0 / 275 | +101,012 / −0 | 56 | 24.2 | 7 |
| 32 | `R8a` | 299 | 0 / 299 | +95,935 / −0 | 16 | 17.2 | 6 |
| 33 | `R8b` | 244 | 0 / 244 | +69,115 / −0 | 80 | 24.4 | 5 |
| 34 | `L1a` | 450 | 1 / 449 | +29,433 / −0 | 0 | 3.2 | 0 |
| 35 | `L1b` | 332 | 0 / 332 | +15,614 / −0 | 7 | 2.2 | 0 |
| 36 | `T1` | 40 | 0 / 40 | +3,378 / −0 | 0 | 0.2 | 0 |
| 37 | `G1` | 10 | 0 / 10 | +969 / −0 | 0 | 0.2 | 0 |
| | **Total committable** | **7423** | | **+1,255,875 / −483** | **612** | **312.7** | **71** |

## 2. What that means

| Block | Commits | Files | Lines to read | Honest reading |
|---|---|---:|---:|---|
| Source | `S1`–`S11` | 377 | about 11,300 | **Readable line by line.** At 300–500 lines an hour for careful code review: roughly 25–40 hours. This is the block where a line-by-line review has real value. |
| Contracts + guard | `C01` | 57 | about 26,400 | Half of it is one 2.6 MB generated JSON (`baseline.json`) and sealed inputs pinned by hash. The contracts and annexes themselves are a few thousand lines and should be read. |
| Governance, packs, decisions | `A1` `K1` `D1` | 75 | about 5,200 | Readable. 2–4 hours. |
| Plans, kit, ledgers | `L1a` `L1b` `T1` `G1` | 832 | about 49,400 | Text. Readable only by sampling; a full read is days. |
| **Records** | `R1a` … `R8b` (19 commits) | 6,082 | **about 1,163,600**, plus 612 binary files | **A line-by-line read is not realistic**: at 500 lines an hour it is over 2,000 hours. |

So the plan can meet GIT-002 §3 in full for 18 of the 37 commits, and cannot meet its second review literally for
the 19 record commits.

## 3. What can stand in for the second review on record commits — an owner decision, not mine

The Q69 prep plan met the same wall and proposed, for record commits, "name + stat review" instead of the full
diff (`docs/roadmap/plans/mvp6-commit-plan-01/COMMIT-SEQUENCE.md`, gate G3). That plan was accepted as
preparation only; the gate itself was never approved, and GIT-002 §3 has no exception for records.

If the owner wants one, these checks are mechanical and cover what a human read would look for in evidence files:

1. Name list equals the pathspec file exactly (review 1, unchanged).
2. Every folder's own `SHA256SUMS` / `ARTIFACTS.sha256` verifies for the staged files.
3. A secret scan of the staged content (the evidence kit's K08 pattern scan plus the exact-value scan for F02).
4. Size check: the staged files over 1 MB equal the expected list (71 in total; `docs-organization.md` K7).
5. No path outside `docs/records/` in a record commit.

Until the owner decides, the plan's default is the rule as written: read the diff, or do not commit.

## 4. Other costs worth knowing before starting

- **37 commits.** Each needs its own stage, two reviews and an explicit go-ahead.
- **312.7 MB** enters history, 612 binary files, 71 files over 1 MB (largest 7.7 MB). Nothing is near GitHub's
  100 MB per-file limit, but git stores every later version of a binary in full.
- **A push later sends all of it**, including anything committed by mistake. History is not rewritten in this
  repo (decision pack Q147 `:71`), which is why the holds come first.
