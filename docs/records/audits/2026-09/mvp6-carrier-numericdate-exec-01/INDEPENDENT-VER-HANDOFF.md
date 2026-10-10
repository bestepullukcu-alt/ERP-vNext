# MVP6-CARRIER-NUMERICDATE-EXEC-VER-01

Role: different agent; strict source no-write.

## Exact inputs

- Owner decision SHA-256: `395c3746db4c88aac94964b447c112181c2508573fde04d895bb412cbb19f923`
- Patch SHA-256: `180143bccc08ad3316eb89f79a3e9386da2ce881b9a4b02f00a725b0be27ef4b`
- Final 22-path manifest SHA-256: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`
- Final source archive SHA-256: `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`
- DEV evidence archive SHA-256: `5f8c30998f063a5eee535dc66d32393a767474e2a6e031fa40da13e5f1b2033f`

## Required verification

1. Extract `final-source.tar.gz` to a unique disposable directory and verify
   the 22-path and 3,333-entry manifests. Confirm only the approved two MDM
   paths differ from the predecessor.
2. Use `/Users/natig/.dotnet/dotnet` SDK 8.0.417/runtime 8.0.23 with no major
   roll-forward. Fresh build and run the validator regression.
3. Use a new DB-010 replica set and unused ports, never 27017. Independently
   reproduce the full raw signed payload matrix: duplicate/missing/malformed
   claims and audiences, reverse duplicate order, string/fraction/out-of-range
   NumericDate, configured skew/lifetime, `exp <= iat`, signature/caller/scope.
4. For every rejection prove HTTP 401 and zero scoped
   `mdm_legal_entities` repository reads. Do not use normalized `Claim.Value`
   as raw JSON type evidence.
5. Reproduce real Auth login and refresh through Platform and MDM. Require one
   authoritative `legal_entity_id` on both redacted tokens and fresh refresh
   re-resolution. Diagnostic service tokens are not Auth acceptance.
6. Preserve F-01 CLOSED separately from F-02. Inspect the retained 48/49 first
   attempt and the 49/49 successor; do not silently discard failed evidence.
7. Archive no secret or usable bearer token. Record source→build→binary→process,
   raw HTTP/Mongo evidence, cleanup, and repository no-change.

Do not edit source, Carrier UI, gateway, permissions, contracts, guard, or Git.
Return F-01 and F-02 independently; do not claim CT/full-module acceptance.
