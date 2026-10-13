# SOP §22 — MVP6-ROOT-GUARD-DISPOSITION-RECOVERY-01

## Verdict

**DISPOSITION CANDIDATE READY FOR OWNER REVIEW; canonical activation NO-GO.** The prior temporary package was unavailable and was not treated as evidence. This package was regenerated from current repository inputs.

## Fresh input classification

The three exact guard offenders were re-read and classified as `historical-data`:

- `mod-0185-dev-02/changed-files.json` — immutable DEV-02 source/test hash manifest used by its handoff/audit records; no executable consumer.
- `mod-0185-dev-03/changed-files.json` — immutable DEV-03 source/test/failpoint manifest used by its handoff/audit records; no executable consumer.
- `mod-0186-prep-02/input-hashes.json` — immutable PREP-02 input snapshot referenced by README, preservation and manifest; no executable consumer.

Exact current hashes are recorded in `manifest.json`. Historical files were not edited.

## Authority candidate

The candidate appends exactly three historical seals to the current 17-seal authority. The first 17 `sealedInputs` entries compare byte-identically with the current authority. No wildcard, scanner exclusion, guard change, or historical rewrite is present.

- Raw target/seal payload SHA-256: `568cc89adf9efeb0f0a042d7af67ed2784727bb4974d1e8c4ad21e6caa75f555`
- UNAPPROVED decision candidate SHA-256: `03559627ef357bb0969038618dd7cdaaea7f1b1ae00f825b8d313acbaf6d8c04`
- Authority candidate SHA-256: `c39d01c4f19fd8a18a40cb86788c8a34c609cf44c8fe0127bfa988fee86e4c6b`
- Exact authority diff SHA-256: `95f82f86a413fc5f1b687de1ad04aa18ae1700fdc48121c4951106c89dbdedfa`

The supplied prior approved payload `6a3f1aef…7004e4` is not silently reused: the new three-seal payload is different and requires a new owner decision.

## Disposable guard verification

A disposable full checkout was restored and the real `DocsPathGuardTests` production entrypoint was run with socket permission. Result: **34 passed, 2 failed**.

1. `NoCodeFilePointsIntoDocsOutsideTheFiveFolders` — assertion at `DocsPathGuardTests.cs:209` requires authority `status == "APPROVED"`; candidate is `UNAPPROVED`. This is an intentional fail-closed approval gate, not proof that the three seals are structurally invalid.
2. `AuthorityFixture_ReadsExactFilesAndProvenance` — assertion at `DocsPathGuardTests.cs:242` checks the isolated synthetic fixture’s provenance line; its fixture does not contain this recovery package’s new provenance record. This is fixture setup mismatch, separate from the production authority candidate.

The test run therefore does not establish activation PASS. It does establish that an unapproved candidate is rejected by the production reader. The full log is in the disposable recovery workspace under `/private/tmp/mvp6-root-guard-disposition-recovery-01/guard-final.log`.

## Post-approval plan

After an owner binds the exact raw payload and decision hash:

1. Recompute the decision file SHA-256 and authority `decision.sha256`.
2. Re-run the production reader against the exact canonical target and all 20 seals.
3. Run the complete DocsPathGuard suite, including positive and negative wrong-hash/path, missing decision, changed-history and unapproved-status cases.
4. Only after all guards pass may the single activation writer apply the authority diff. Canonical publication remains a separate precondition; no runtime or migration action is implied.

## No-change

Only this audit directory was written in the repository. Canonical contract, canonical authority, guard code, three historical inputs and git state were not modified.
