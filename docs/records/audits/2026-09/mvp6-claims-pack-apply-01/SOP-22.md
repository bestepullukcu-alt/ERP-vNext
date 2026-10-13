# SOP §22 — MVP6-CLAIMS-PACK-APPLY-01 (CT queue Q38)

**Verdict: APPLIED — MOD-0187 shared pack = `762ab5337af85b965216d41a212217db3681af3a31bbd1514544b58dbfc7786b`, `status: ready-for-dev`.** Single pack-writer lane. Only the pack file and this evidence folder were written.

- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched at start and end).
- Start 2026-09-26T01:33:29+03:00 · End: see final report (Europe/Istanbul).
- Authority: `docs/records/decisions/2026-09/mvp6-claims-pack-signoff-owner-decision-01.md`, SHA-256 `365de5be6fc670a8522dd173f4fbc4d15d72324ece4856519ac9e0424928827b` (MVP6-CLAIMS-PACK-SIGNOFF-OWNER-DECISION-01, approved, covers Q37 and Q31).

## Steps

| Step | Check | Result |
|---|---|---|
| 0 | Repo identity; decision hash prefix `365de5be6fc6` | PASS |
| 1 | `mvp6-ui-pack-revisions-01/SHA256SUMS` (run in its folder, relative paths) | PASS 6/6; the sums file itself `6b7d770b…5d62` = decision `bound_to` |
| 1 | `mvp6-pack-alignment-02-claims/SHA256SUMS` | PASS 5/5 |
| 1 | Alignment patch `00dee2b1fd202f1adc25ca4d3c2aeae532e1bf8d944aca0c68a7fe0417ca5c59` | PASS = decision |
| 1 | UI revision patch `5644479a77db172adf9d24f5a7031eed82c4eb82067d326403a37058270dae92` | PASS = decision |
| 1 | `claims/SIGN-OFF-DECISION.md` `c55ab9aa…35de2` | PASS = decision `bound_to` |
| 1 | Pack preimage `a342054cddcd88a9fae67f21b51ca90eadcdf355e6119c77c9578bae744b1c1f` | PASS |
| 2 | Rehearsal in a scratch Git repo outside the repository: alignment → `f0e4d3bd6af5c16a0d0904721019bfb33f10818a6f9baffd92d3f50740bb1baf`; UI → `762ab5337af85b965216d41a212217db3681af3a31bbd1514544b58dbfc7786b` | PASS; `status: ready-for-dev` |
| 3 | Snapshot of pack (`a342054c…`) and `git --no-optional-locks status --porcelain` (360 rows), taken outside the repo | taken |
| 3 | `git apply --include=<pack>` alignment patch, then UI patch (no `--index`, no fuzz option) | PASS; `f0e4d3bd…` then `762ab533…786b` |
| 3 | Post-check: pack hash, `status: ready-for-dev`, porcelain vs snapshot | PASS. Pack `762ab533…786b`; porcelain unchanged before this folder (the pack was already ` M`); no stray `.orig`/`.rej`; mode unchanged; no `.git/index.lock` |
| 4 | Restore from snapshot | NOT NEEDED |

The final porcelain comparison (after writing this folder) is in the final report: the only new line is this folder.

## Observations for CT

1. **Decision timestamp:** the decision record's front matter has `decided_at_local: 2026-09-26T01:37+03:00` but `decision_source` says the record was written at 01:32. The CT dispatch reached this lane at 01:33. The record hash matched the dispatch, so the lane proceeded; the 01:37 value looks like a typo, not a later decision. The record was not edited.
2. **Unlink warning:** `git apply` printed "unable to unlink … Operation not permitted" for the pack. This session's folder does not allow deletes, so Git rewrote the file in place. The resulting bytes match the target hash exactly and no leftover file exists.
3. **Carried open items** (from the decision; not acted on): approved amount absent from list data, transition modal fit, field icons in a shared test file, date-time with offset, and generic DataTable verifier vs approved OUT rows. These are open items for the UI writer and integration owner.

## Not done / not authorized

No UI code, gateway, permission, navigation, localization, contract, other pack, `.antigravity`, product code, registry or tracker change. No commit, push or stash.

## Evidence

- `COMMANDS.tsv`: every command and its result.
- `ARTIFACTS.sha256`: this folder's files plus the final pack and every input hash, repo-root-relative.
