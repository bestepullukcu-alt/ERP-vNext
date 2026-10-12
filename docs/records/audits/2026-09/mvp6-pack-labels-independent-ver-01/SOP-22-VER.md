# SOP-22 VER — pack heading-label applies (Q75) and its two records (Q76)

- **Work package:** MVP6-WP-LABELS-IVER-01 · Prompt Q76 v1.0 · Lane AL-MVP6-LABELS-IVER-01 (VER, independent: this lane did not write or apply the patches).
- **Repo / base:** `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched; no `.git/index.lock`).
- **Window (Istanbul):** start 2026-09-26 15:10 · end 2026-09-26T15:13+0300.
- **Method:** read-only; every git command ran with `GIT_OPTIONAL_LOCKS=0`. The patches were reproduced only in throwaway git folders in the VM's `/tmp/q76`. Every command and exit code is in `COMMANDS.tsv`.
- **Subject:** proposal `docs/roadmap/plans/mvp6-pack-heading-labels-01/` (SHA256SUMS `39ae6f62…cde8`, SIGN-OFF-DECISION.md `f177ac35…922c`); decision record `mvp6-pack-heading-labels-owner-decision-01.md` (`6f5a5151…a4ba`); verdict record `mvp6-ct-verdicts-q72-q73-2026-09-26.md` (`5f2a91a4…bf30`); writer evidence `mvp6-pack-labels-apply-01/`; the 8 targets DCP-009, MOD-0183…0187, MOD-0190, MOD-0192.

## 1. Verdict table

| Step | Verdict | Evidence |
|---|---|---|
| 0 Preflight | PASS | HEAD/branch match; start porcelain 437 lines (VM home) |
| 1 Evidence, proposal, decision record | **PASS** | Writer ARTIFACTS **6/6 OK**; proposal SHA256SUMS **13/13**. The decision record's single quote block (3 682 chars) is a **byte-identical** substring of SIGN-OFF-DECISION.md, inside "## Exact decision text", the only option text. The record binds SIGN-OFF-DECISION.md and SHA256SUMS with the hashes on disk. The decision text names all 8 patches with their exact before/after hashes = BASE-HASHES.tsv. The patch file hashes are bound through SHA256SUMS (13/13; each = BASE-HASHES `patch_sha256`). The verdict record's 4 bindings (including this lane's Q72 SOP-22-VER.md `5d8465c4…` and ARTIFACTS `bf7c0359…`) all equal disk. |
| 2 Reproduction per target | **PASS 8/8** | `/tmp/q76/repro.py`: for each target, live sha256 = BASE-HASHES after; `git apply -R` rc 0 → **exact before-hash**; `git apply` rc 0 → **`cmp` byte-identical to live**. |
| 3 Only heading/blank/Approved lines; correct approving records | **PASS** | `git diff --no-index -U0` before→live per file: the removed lines are only the 11 old headings, and the added lines are **11 headings + 11 blank + 11 `Approved:` + 0 other**. Old and new headings equal HEADINGS.tsv. Each `Approved:` path exists and its sha256 equals HEADINGS.tsv. Each record is the approving one, because the labelled heading was introduced by a patch that record signed (see §2). |
| 4 No NOT APPROVED heading; no other file changed | **PASS** | grep of the 8 files: 0 headings with "NOT APPROVED" / "proposal —" / "proposed draft delta", and 0 occurrences of "NOT APPROVED" at all. Files newer than 14:59 in `execution/`, `docs/analysis/`, `.antigravity/`, `services/`, `frontend/`, `gateway/`, `tests/`, `scripts/`, `docs/guides/`, `docs/reference/`: **exactly the 8 targets**. In records/roadmap: the 2 records, 5 evidence files, CT-QUEUE and MILESTONE-EVENTS (BLOCKERS untouched, as stated), plus `docs/roadmap/plans/scm-planning-analysis-01/`, created 15:03:48–15:03:55 by the parallel Q74 lane (LANE 1), after the writer's final snapshot at 15:01 and not in its delta. The writer's st-before → st-after porcelain delta = its 2 records + evidence folder. No `.orig`/`.rej`/`~`. |
| 5 Secret scan | **PASS** | Installed `scripts/evidence-kit/k08_redact_scan.py` (= v1.2 proposal file) in pattern mode on a /tmp copy of 17 files (5 evidence, 2 records, 2 ledgers, 8 targets) → **result PASS**. Keyword grep: the only matches are the writer's own "Secret scan" description lines. |
| **Overall** | **PASS** | No finding above INFO. |

## 2. Approving record per heading (step 3 detail)

| Target §  | New heading | Approved: record (sha256) | Why it is the approving record |
|---|---|---|---|
| DCP-009 §21 | Follow-up — Supply Chain module self-registration foundation | self-registration-patches-signoff (`7707329e…`) | added by `mvp6-self-registration-patches-01/patches/01-DCP-009-…patch`, signed there |
| MOD-0183 §22 | Self-registration | same (`7707329e…`) | patch 02, signed there |
| MOD-0184 §31 | Self-registration | same | patch 03 |
| MOD-0185 §29 | Self-registration — after Loads UI approval | same | patch 04 (the record keeps it inactive until Loads UI, and the heading keeps that wording) |
| MOD-0186 §31 | Accepted bounded scope binding | returns-pack-signoff (`649269c3…`) | added by `mvp6-pack-alignment-03-returns/alignment.patch`, approved there |
| MOD-0186 §33 | Self-registration | self-registration-patch05-signoff (`626055e4…`) | added by `05-MOD-0186-self-registration-rebased.patch`, signed there (the original patch 05 was superseded) |
| MOD-0187 §31 | Accepted bounded scope binding | claims-pack-signoff (`365de5be…`) | added by `mvp6-pack-alignment-02-claims/proposed-pack.patch`, approved there |
| MOD-0187 §33 | Self-registration | self-registration-patches-signoff | patch 06 |
| MOD-0190 §22 | Accepted bounded scope binding | sop-pack-promotion-q27 (`bcc3f8e5…`) | added by the Q27 patch (verified in Q72) |
| MOD-0192 §21 | Published 2.0.0 binding and bounded executor acceptance | capacity-pack-promotion-q28 (`7436a5c6…`) | added by the Q28 patch |
| MOD-0192 §22 | Accepted bounded scope binding | same | added by the Q28 patch |

## 3. Notes

| Id | Severity | Note |
|---|---|---|
| N1 | INFO | The decision text binds each patch by path and before/after hashes; the patch-file hashes are bound only indirectly (record → SHA256SUMS → patches), which verifies. |
| N2 | INFO | The writer's ARTIFACTS deliberately excludes the 8 targets and the ledgers (A-01), so later approved patches do not break it. Their after-hashes are in BASE-HASHES/HASHES.tsv and were verified here. |
| N3 | INFO | The verdict text itself comes from the CT instruction, not a repository file, so it cannot be byte-checked. Its hash bindings all verify. |

## 4. ASSUMPTIONS (no-question policy)

1. "Correct approving record" = the record that signed the patch which introduced that heading. Verified by locating the `+##` line in the signed patch.
2. "No other file changed by Q75" uses modification times since 14:59:00 plus the writer's own porcelain snapshots in the same VM (`/tmp/q75/st-before.txt`, `st-after.txt`). The targets and ledgers were already modified before Q75, so they appear by modification time, not in the porcelain delta.

## 5. Close-out

- `GIT_OPTIONAL_LOCKS=0 git status --porcelain` vs the 15:10 start snapshot: only this lane's folder `docs/records/audits/2026-09/mvp6-pack-labels-independent-ver-01/` is new; no other entry changed; HEAD `4a8d4d4b…1136c`; no `.git/index.lock`.
- Artifacts: `SOP-22-VER.md`, `COMMANDS.tsv`, `ARTIFACTS.sha256` (repo-root paths). **Uncommitted** (owner decision Q03a; chat lane).
- Agent PASS ≠ CT ACCEPTED — returning to CT.
