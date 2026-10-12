# Disposable reconstruction and validation

1. Verify each artifact in `INPUTS.tsv` by SHA-256.
2. Extract the 379-row MOD-0190 archive and the disjoint 43-row Capacity archive.
3. Verify every extracted file against its source manifest.
4. Apply `PROGRAM-COMPOSITION.patch`, then
   `X07-LATER-READ-EVIDENCE-TEST.patch` with `git apply --check` followed by
   disposable application.
5. Serialize `path<TAB>sha256<LF>` bytewise-sorted with one header row. The
   result must be 422 rows, 74,155 bytes and SHA-256 `cdc6228…`.
6. Verify the three C preimages from `SOURCE-MANIFEST.tsv`.
7. Apply `PROPOSED-UPTAKE.patch` with `git apply --check` followed by
   disposable application.
8. Verify the three target hashes, serialize the successor manifest and check
   SHA-256 `dec28b6…`.
9. Verify the preservation entries and compare the 43 Capacity paths with the
   isolated C successor. Accept only the documented B hosted-test supersession.

The disposable workspace is
`/private/tmp/mvp6-bc-integration-successor-01-agvc8muo`. It is supporting
workspace, not the durable evidence source. The durable manifests and report in
this directory are sufficient to reproduce the same source selection from the
permanent input archives.

