# SOP §22 — MVP6-PACK-APPLY-Q46-Q47-01 (CT queue Q46, Q47; Q48 prep)

**Verdict: APPLIED — Q46 (Returns alignment + UI revision) and Q47 (self-registration 01, 02, 03, 04, 06) all hash-verified; patch 05 rebased and checked, NOT applied.** Single pack writer (Lane-2).

- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched at start and end).
- Start 2026-09-26T09:25:00+03:00 · apply 09:25:55 · end in final report (Europe/Istanbul).
- Authority: see [AUTHORITY.md](AUTHORITY.md) (records A `649269c3…`, B `7707329e…`).

## Steps

| Step | Result |
|---|---|
| Pre-write verification: packages 5/5 and 11/11; 7 patch hashes; 6 target before-hashes | PASS; nothing written before this passed |
| Rehearsal on a scratch copy outside the repository | PASS; all 7 after-hashes reproduced |
| Step 1 (A, Q46): MOD-0186 `07a8a015…` → alignment → `f4396e8a…` → UI revision → `6c8fbe28…a5a0` | PASS |
| Step 2 (B, Q47): DCP-009 → `e346043d…`; MOD-0183 → `8e269efa…`; MOD-0184 → `346288ab…`; MOD-0185 (patch 04, inactive section §29) → `45b5dd33…`; MOD-0187 → `31cb35c3…` | PASS; patch 05 not applied |
| Restore from pre-copy | NOT NEEDED |
| Step 3 (rebase, Q48 prep): `05-MOD-0186-self-registration-rebased.patch` in `docs/roadmap/plans/mvp6-self-registration-patch05-rebase-01/` | Only change is §30 → §33 plus hunk position; `git apply --check` on the live file exit 0; expected after `a762305ec789f0b2547d5205135a0ff70b64e83f4a63b839a985432a46d8a552`; NOT APPLIED; original package unchanged |

Full hashes: [HASHES.tsv](HASHES.tsv). Commands: [COMMANDS.tsv](COMMANDS.tsv).

## Observations for CT

1. **Stale headings in signed text:** each applied self-registration section keeps its signed heading "(PATCH PROPOSAL — NOT APPROVED until owner sign-off)": MOD-0183 §22, MOD-0184 §31, MOD-0185 §29, MOD-0187 §33 and DCP-009 §21. The signed bytes were applied exactly; the sign-off decision record is the approval evidence. Removing the label would be an administrative correction that needs its own patch and hash.
2. **Unlink warnings:** `git apply` printed "unable to unlink … Operation not permitted" for each target. This session's folder does not allow deletes, so Git rewrote each file in place. Every after-hash matches and no leftover file exists.
3. **Status:** MOD-0186 moved from `draft` to `ready-for-dev` as approved in A. No other status changed.
4. **The rebased patch keeps the historical "Base note"** paragraph verbatim. It already states that the section becomes §33 after the Returns sign-off.

## Changed files

The six target pack/DCP files, this evidence folder and the new plan folder `mvp6-self-registration-patch05-rebase-01/`. Nothing else; no add, commit, push or stash.
