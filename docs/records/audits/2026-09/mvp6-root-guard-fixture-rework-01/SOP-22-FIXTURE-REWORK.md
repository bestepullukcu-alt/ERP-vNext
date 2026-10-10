# SOP §22 — MVP6-ROOT-GUARD-FIXTURE-REWORK-01

## Scope

Disposable fixture-only rework for `AuthorityFixture_ReadsExactFilesAndProvenance`. Production reader, scan scope, hash checks and authority status behavior were not changed.

## Exact failure and correction

The prior fixture failure occurred at `DocsPathGuardTests.cs:242` inside `ReadAuthority`: the synthetic `AuthorityFixture` copied paths from the current 17-seal authority but did not add the three recovery historical manifests/provenance records. The fixture's `provenanceLine` lookup therefore read the wrong line and failed its exact path/hash assertion.

The narrow candidate patch adds, inside `AuthorityFixture` construction only:

- the three exact recovery manifest paths;
- computed source SHA-256 values;
- the recovery provenance file and computed provenance SHA-256;
- exact provenance lines 3, 4 and 5;
- `historical-data` seals with empty targets.

No production `ReadAuthority`, `VerifyRoot`, scan regex, status requirement, or hash validation was relaxed. The old 17 seals remain unchanged.

Patch SHA-256: `7aed354f4feddd108ec373bc547ed1f5a9e0e8c0153d3495060d9369f87778e2`.

## Disposable verification

A full disposable checkout used the patch and ran the real DocsPathGuard test filter with the required socket permission:

- **35 passed, 1 failed**.
- `AuthorityFixture_ReadsExactFilesAndProvenance`: PASS after the patch.
- All invalid-evidence theory cases: PASS, including wrong hashes, missing provenance, wrong provenance line, changed history, unknown path, wildcard, symlink, duplicate and unapproved cases.
- `NoCodeFilePointsIntoDocsOutsideTheFiveFolders`: remains FAIL because the disposable checkout contains existing/new unclassified JSON references, including the recovery candidate authority and other pre-existing uptake records. This is a production disposition issue, not a fixture defect.

The production `UNAPPROVED` authority rejection remains fail-closed and was not “fixed.”

## Guard-scope impact

The candidate fixture patch itself is a test-only `.cs` diff. The persistent audit outputs are Markdown/patch/log/manifest records. No new authority JSON or path inventory was added to the repository by this rework. The pre-existing recovery candidate JSON remains outside this task and is listed by the production scan; it must be handled by its own owner disposition.

## Gates

- Fixture defect: **CLOSED in disposable candidate**.
- Production activation: **BLOCKED**; real approval and a clean full DocsPathGuard run remain required.
- Canonical authority/contract/guard implementation: unchanged.
- Historical inputs and 17 seals: unchanged.
- Commit/push/stash: none.
