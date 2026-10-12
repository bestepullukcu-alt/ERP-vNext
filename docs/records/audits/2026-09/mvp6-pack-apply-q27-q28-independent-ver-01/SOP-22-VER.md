# SOP-22 VER — Q27/Q28 pack apply and the 8 records of Q71 (Q72)

- **Work package:** MVP6-WP-PACK-APPLY-Q27-Q28-IVER-01 · Prompt Q72 v1.0 · Lane AL-MVP6-Q27Q28-IVER-01 (VER, independent: this lane did not apply the patches or write the records).
- **Repo / base:** `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched; no `.git/index.lock`).
- **Window (Istanbul):** start 2026-09-26 14:50 · end 2026-09-26T14:55+0300.
- **Method:** read-only; every git command ran with `GIT_OPTIONAL_LOCKS=0`. Patch reproduction ran only in throwaway git folders in the VM's `/tmp/q72`, never in the repo. Every command and exit code is in `COMMANDS.tsv`.
- **Subject:** Q71 writer evidence `docs/records/audits/2026-09/mvp6-pack-apply-q27-q28-01/` (ARTIFACTS.sha256 `643d…` listed there). The 8 records are 1 CT-verdict record + 7 owner-decision records (Q03a, Q27, Q28, DN-01, DN-02, PC-02/03/04/28, Loads PH15), as listed in the writer's README.

## 1. Verdict table

| Step | Verdict | Evidence |
|---|---|---|
| 0 Preflight | PASS | HEAD/branch match; start porcelain 431 lines (VM home) |
| 1 Evidence and hashes | **PASS** | Writer ARTIFACTS `sha256sum -c` → **17/17 OK**. decision-prep-02 SHA256SUMS 12/12. Patch sha256 Q27 `116b7c47…923e` and Q28 `7d9b587e…b92f` equal Q27.md, Q28.md, both records, HASHES.tsv and the files on disk. Before `637690f3…6877` / `edd550b8…69f7` and after `8403d8f4…ea40` / `de81a0e2…946c` are identical in packages, records and HASHES.tsv, and the live packs equal the after hashes. |
| 2 Reproduction in /tmp | **PASS** | For each item: copy the live pack into a throwaway repo; `git apply --check -R` + `git apply -R` → rc 0 → **exact before-hash**; `git apply` forward → rc 0 → **`cmp` byte-identical to the live pack**. |
| 3 Only approved lines; status; no other file | **PASS** | `git diff --no-index` (reconstructed before → live) has exactly the patch's +/- lines (Q27 +70/−45, Q28 +77/−47); a first difflib count differed only by blank-line alignment. Both packs: `status: ready-for-dev` (line 9). Files newer than 14:41 in `execution/`, `docs/analysis/`, `.antigravity/`, `services/`, `frontend/`, `gateway/`, `tests/`, `scripts/`, `docs/guides/`, `docs/reference/`: **only MOD-0190 and MOD-0192**. In `docs/records`/`docs/roadmap`: exactly the 8 records, 3 ledgers and 5 evidence files. The writer's own st-before → st-after porcelain delta = the same paths; st-after = my 14:50 start snapshot. No `.orig`/`.rej`/`~` files. |
| 4 Records quote sources exactly | **PASS** (notes N1–N3) | Script `/tmp/q72/records.py`: **23 cited path+sha256 pairs, 0 mismatch**; 4 more in the verdict-record table, 0 mismatch. **10 of 12 quote blocks are byte-identical substrings of the cited source**, each inside its "Exact decision text" section: Q27 and Q28 (in Q27.md/Q28.md **and** PROMOTION-DECISION.md), DN-01, DN-02, PC-02, PC-03, PC-04, PC-28, PH15, Q03a (prepared text, marked NOT approved). The other 2: the F-1 line is a byte-identical *line* of `mvp6-loads-uptake-prep-01/README.md` (`ce59aecd…`) with a `> ` marker added, as the writer's A-06 says; the CT verdict text has no repository source (N2). All 7 decision records carry `decided_at_local`, `decided_by`, `approvedBy`, `bound_to`, `recorded_by`. The verdict record is not an owner decision and has no approvedBy (appropriate). |
| 5 Secret scan | **PASS** | Installed `scripts/evidence-kit/k08_redact_scan.py` (byte-identical to the v1.2 proposal file) in pattern mode on a /tmp copy of all 18 writer files (8 records, 2 packs, 3 ledgers, 5 evidence files) → 18 units, **result PASS**. Keyword grep: the only matches are the writer's own "Secret scan" row mentions. |
| **Overall** | **PASS** | The pack applies are exact and reproducible, and nothing outside the approved scope changed. One MEDIUM item (N1) needs CT confirmation; it does not affect the pack applies. |

## 2. Notes and findings

| Id | Severity | Finding |
|---|---|---|
| N1 | MEDIUM (CT to confirm) | **Q03a option-label mismatch.** The record states "C — no commit for now", but in the cited source `mvp6-commit-plan-01/DECISION-TEXT.md` (`57aa584d…`), row C is "One commit per WP folder (~260 commits)". The record says this openly (writer ASSUMPTION A-01) and quotes the prepared text as NOT approved. Nothing in the repository shows what the owner was shown, so VER cannot verify which meaning the owner chose. CT should confirm that the option presented was "no commit for now". This does not affect Q27/Q28. |
| N2 | LOW | The verdict record's quote ("CT verdicts (CT conversation, ~14:36): …", 548 chars) comes from the CT instruction to Q71, which is not a repository file (grep of docs/records and docs/roadmap: none), so it cannot be verified byte for byte. Its four checksum bindings all match disk and re-check: Q68 8/8 (from its folder), Q69 20/20 (from the repo root), Q70 12/12 (from its folder). |
| N3 | INFO | The Q24a binding `mvp6-evidence-kit-install-01/ARTIFACTS.sha256` (`768cd323…` = disk) now re-checks **34/36**, not the 36/36 the record states. The 2 failures are `CT-QUEUE.tsv` and `MILESTONE-EVENTS.tsv`, which Q71 itself edited at 14:47:54. Their pinned hashes are exactly the writer's "before" values (`dbe4623b…`, `e3d7ddd6…`), so 36/36 was true at recording time (14:45:53). The Q24a evidence pins living ledgers, so it will drift again. |
| N4 | INFO | `decided_at_local` is a window (~14:36–14:48), not the minute of each answer (writer A-02). Acceptable, because CT supplied only the window. |
| N5 | INFO | Both applied packs still carry proposal-style heading labels (MOD-0190 §22, MOD-0192 §21/§22). These are part of the approved result bytes (writer A-07) and queued as Q73. |

## 3. ASSUMPTIONS (no-question policy)

1. "The 8 records" = the writer's list (1 verdict + 7 decisions). The evidence-kit v1.2 adoption record (13:44) predates Q71 and is not in scope.
2. For "no other file changed", I used file modification times since 14:41:00 plus the writer's own porcelain snapshots (`/tmp/q71/st-before.txt` / `st-after.txt`, readable in the same VM). The writer's pre-copies in `/tmp/q71/pre/` were not readable by this user; the reconstructed before-files in step 2 stand in for them.
3. The two quotes that are not blocks (N2 and the F-1 line) are judged by the writer's stated method: F-1 as a byte-identical source line, and the verdict text as CT-instruction text.

## 4. Close-out

- `GIT_OPTIONAL_LOCKS=0 git status --porcelain` vs the 14:50 start snapshot: two new untracked entries.
  1. `docs/records/audits/2026-09/mvp6-pack-apply-q27-q28-independent-ver-01/` — this lane.
  2. `docs/roadmap/plans/mvp6-pack-heading-labels-01/` — **not written by this lane**: created 2026-09-26 14:53:34 +03:00, during this run, by the parallel Q73 lane (LANE 3, heading-label proposal, declared parallel-safe); no command in `COMMANDS.tsv` names that path.
  No other entry changed; HEAD `4a8d4d4b…1136c`; no `.git/index.lock`.
- Artifacts: `SOP-22-VER.md`, `COMMANDS.tsv`, `ARTIFACTS.sha256` (repo-root paths). **Uncommitted** (owner decision Q03a; chat lane).
- Agent PASS ≠ CT ACCEPTED — returning to CT.
