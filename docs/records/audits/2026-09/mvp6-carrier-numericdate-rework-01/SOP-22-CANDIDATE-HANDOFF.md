# SOP §22 — MVP6-CARRIER-NUMERICDATE-REWORK-01

## Verdict

**CANDIDATE READY / APPLICATION BLOCKED BY NEW EXACT-HASH AUTHORITY.** F-01 remains technically **CLOSED**. The two-file candidate closes the reproduced F-02 JSON-type defect in disposable E2 evidence, but it was not applied to an integration checkout or accepted as runtime/CT evidence. A new owner decision bound to the hashes below is required before isolated application and independent VER.

## Authority and controlling policy

- Predecessor owner decision: `mvp6-carrier-auth-token-findings-close-01/OWNER-DECISION-TEXT.md`, SHA-256 `5c9d67fcfc2c59b657423e4ae188b01321b09a2fac94253c687868e82c32acb3`.
- Controlling independent finding: `mvp6-carrier-auth-token-rework-ver-02/SOP-22-VER.md`, SHA-256 `81372008d538a19c6a5d5d1bd41b9dd5f7ee00e4f8f04d90f4c7eac84b2657e2`.
- The predecessor decision authorized patch `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`; it does not authorize this new diff. No authority was carried forward.
- The controlling temporal policy keeps exactly one `iat`, `nbf`, and `exp`; `iat == nbf`; configured skew/lifetime rules; and the prior signed 64-bit seconds representation. The candidate adds raw JSON Number proof and preserves the existing Int64 range rather than introducing a new clock policy.

## Exact candidate

- Patch: `180143bccc08ad3316eb89f79a3e9386da2ce881b9a4b02f00a725b0be27ef4b`
- Delta manifest: `aed4ddd904ee78120ca47c23b0a65b328c7ae88771bda62183f4f72777b6f172`
- Successor 22-path manifest: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`
- Build-source manifest: `516ba828bed602e4355a5cc6871501cbb3b3c72d60473fa7ec73d135bbadf911` (3,333 entries)
- Candidate source archive: `f96427458902b8dd0eaefb2254b35245f3e7131cb9d5797c41dcd453fc1519a7`
- Evidence archive: `9849c1882fecd32f50ea453476c6b3b958aa87594d08c372484020be4013f60b`

Only two MDM-owned paths differ; see `DELTA-MANIFEST.tsv`. Repository product source was not edited.

## Change and security boundary

The candidate keeps the framework's signature/issuer/audience/lifetime validation before custom inspection. In the resulting validator, exact claim cardinality is still checked, then the already validated token's raw payload is parsed. Each time member must occur exactly once, be JSON Number, and satisfy `TryGetInt64` (candidate source lines 114–138). Digit-only strings, fractional numbers, and numbers outside the inherited Int64 range fail closed. No identity is derived from an unsigned payload.

Tests construct the exact signed JSON representation instead of inferring type from `Claim.Value`. String cases cover all three members; fractional and range cases preserve the inherited numeric boundary.

## RED → GREEN and runtime evidence

| Check | Result | Evidence |
|---|---:|---|
| Baseline validator + three new string tests | 28 PASS / 3 FAIL | `evidence.tar.gz::tests/red/numericdate-red.trx` |
| Candidate validator regression | 33/33 PASS | `evidence.tar.gz::tests/green/numericdate-green.trx` |
| Candidate wire/profiler matrix | 49/49 PASS | `evidence.tar.gz::runtime/candidate-token-results.json` |
| String `iat`, `nbf`, `exp` | each HTTP 401, zero scoped reads | same runtime result |
| F-01 duplicate/cardinality | preserved across identical/conflicting/reversed cases | same runtime result |
| Valid/skew/lifetime cases | expected 200/401 and scoped read counts | same runtime result |
| Native runtime | SDK 8.0.417, runtime 8.0.23 | `evidence.tar.gz::runtime/dotnet-info.txt` |
| DocsPathGuard | 39/39 PASS | `evidence.tar.gz::architecture/carrier-numericdate-docspath.trx` |
| Candidate binary | `74d05fe16255cecbf7daae5c9dcaaf51c905aee6abb8dae150fcab644e64eea7` | `source-binary-process.tsv` |

The first wire capture produced 45/49 due profiler timing inconsistencies and is not PASS; its disposition is preserved in `ATTEMPT-DISPOSITION.md`. A 150 ms profiler flush produced the controlling 49/49 result. Lane-owned API 18659 and Mongo 37784 were closed; operational 27017 was not used.

## Disposition

- **F-01:** CLOSED, preserved by source review, 33/33 unit regression, and the 49-case wire matrix.
- **F-02:** candidate technical behavior CLOSED at E2; production/integration status remains OPEN until exact owner approval, application, and different-agent VER.
- Auth login/refresh and Carrier E2E were not rerun in this candidate-only lane. They remain downstream of independent VER.
- No Carrier UI, gateway, permission, rollout, commit, push, or stash occurred.

## Next gate

Approve the exact text in `OWNER-DECISION-TEXT.md`, then execute `INDEPENDENT-VER-PROMPT.md` with a source writer and a different verifier. Candidate PASS is not runtime or CT acceptance.
