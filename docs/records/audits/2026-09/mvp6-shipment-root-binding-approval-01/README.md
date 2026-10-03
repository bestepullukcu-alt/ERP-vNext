# MVP6 Shipment Root Guard Binding Approval — Decision Record

## Decision

The user-approved decision record is bound to the exact decision payload supplied for this approval. The decision file is:

`docs/records/decisions/2026-09/mvp6-shipment-root-docs-path-owner-binding-01.json`

Decision SHA-256: `fe06837bf77c70dacd3525a3b05a9168bb2965cd5bfc3ba3927efa90892a2307`

The approved target/seal payload is preserved byte-for-byte as supplied:
`6a3f1aef23058a4cc9b4c4718e43b089483e8e0ab4c05deb22c44df3047004e4`.

## Exact activation candidate

`activation.patch` is the exact authority activation diff. SHA-256:
`d743d0f1485eef7a0442c4c4cf06ae3f532e16d790166736d82584a27dd787c3`.

The candidate points the authority decision to the new decision file and binds the proposed Shipment-BUNDLE 2.1.0 target hash `91d505c900ce214262fd35ecb6bad9f005b4bbc42f1680d9db7cc1932bf0fbc9`. The canonical contract remains unchanged by this task.

## Gate result

Production-mode `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders` was run with the required test-runner permission:

- Exit: `1`
- Result: **FAIL**
- 36 tests discovered; 35 passed, 1 failed.
- Failure is the existing unconsumed `docs/analysis` references in historical JSON records (`mod-0185-dev-02/03 changed-files.json` and `mod-0186-prep-02/input-hashes.json`).

The guard therefore did not pass. The canonical `docs/reference/architecture/docs-path-authority.json` was not changed, and the activation candidate was not applied. The decision record is recorded; authority activation remains blocked until the exact guard findings are dispositioned through the existing owner process and the final canonical target hash is present.

## Scope and protections

No runtime, migration/backfill, pack promotion, DEV GO, commit, push, or stash was performed. Target/seal payload was not altered. The previous canonical publication approval is not treated as proof that the 2.1.0 bytes are currently canonical; current contract SHA remains `93c696e2fba13dbc8fbfcf2cd1ae0ae0bd93cd9d0935b3ee743229e810163571`.
