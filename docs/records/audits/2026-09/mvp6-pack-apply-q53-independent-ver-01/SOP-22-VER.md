# SOP §22 — Independent verification of Q53 (rebased self-registration patch 05 → MOD-0186)

**Verdict: PASS on all five checks.** Queue Q54. Verifier: MVP6 Lane-3 (read-only auditor); I did not write this apply.
Repo `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, all Git commands with
`GIT_OPTIONAL_LOCKS=0`. The rehearsal ran in a temporary Git repo outside the repository. The only files written are the ones in this folder.
Start 2026-09-26T09:49:08+03:00 · end: see the final report (Europe/Istanbul).

The authority is `docs/records/decisions/2026-09/mvp6-self-registration-patch05-signoff-owner-decision-01.md`, sha256 `626055e4…4744`, which is the value the writer cites.
It is bound to source `SIGN-OFF-DECISION.md` `0a526059…05d4` (verified). Every command and raw result is in [COMMANDS.tsv](COMMANDS.tsv).

| # | Check | Verdict | Evidence (own commands) |
|---|---|---|---|
| 1 | Evidence checksums | **PASS** | Writer `mvp6-pack-apply-q53-01/ARTIFACTS.sha256` 7/7 OK (repo root). Rebase folder `SHA256SUMS` 3/3 OK. Patch `7adf06ad…c6fd` = record. |
| 2 | MOD-0186 = approved after-hash | **PASS** | `a762305ec789f0b2547d5205135a0ff70b64e83f4a63b839a985432a46d8a552` |
| 3 | Reverse-apply in a temp copy → exactly the before-hash | **PASS** | `git apply -R --check` then `git apply -R` exit 0 → `6c8fbe28f0b058cacf08aaf3941e82845b0842db3f8b8f295a753e746a65a5a0` (the approved before-hash). Forward re-apply → `a762305e…a552`, byte-identical to the live file (`cmp`). |
| 4 | Only §33 added | **PASS** | `diff <reconstructed before> <live>`: a single hunk `787a788,864`, 0 lines removed, 77 lines appended after the last line of the before-text (§32 was its last section). Exactly one new heading: `## 33. Self-registration …`. The 77 appended lines are identical (`cmp`) to the patch's `+` lines. The pack now has 33 level-2 sections, the last being §32 then §33. |
| 5 | No other file changed by this step | **PASS** | `git diff --name-only` = 16 files, identical to the 16 files enumerated in the Q49 VER and accepted by the CT disposition q46-q47-q49 ("Tracked diff now 16 files"). `git status -- execution/` shows only the six already-accepted targets, with no untracked file. Files modified since the decision (09:44): MOD-0186 at 09:45:38 (the apply). The decision record, `CT-QUEUE.tsv` and `MILESTONE-EVENTS.tsv` were all written at 09:44:10, before the apply; they are CT's own records. The writer evidence follows at 09:46. No `.git/index.lock`. |

## Notes (no verdict change)

- **Method correction, recorded rather than hidden.** In check 4 my first comparison of patch lines vs diff lines used `grep '^+[^+]'`. That drops blank added lines from the patch side only, so the hashes differed. Re-run with `grep '^+' | grep -v '^+++ '`, the lines are identical (77 = 77). Both rows are in COMMANDS.tsv.
- **Stale heading suffix.** §33 keeps the signed heading suffix "(PATCH PROPOSAL — NOT APPROVED until owner sign-off)", the same as the other applied sections. It is already queued for correction as Q50 (CT disposition O4).
- **Status unchanged:** `status: ready-for-dev`.

## Not done

No apply, edit outside this folder, commit, push, stash or index operation. CT decides.
