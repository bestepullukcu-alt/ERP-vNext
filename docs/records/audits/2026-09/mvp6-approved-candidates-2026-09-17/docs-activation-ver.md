# SOP §22 — DocsPathGuard activation independent verification

**Verdict: PASS — exact candidate plus activation in disposable current snapshot only.** Real repository guard/policy/authority not applied. No runtime or canonical publication authority inferred.

Date2026-09-17; real branch feature/mvp6-logistics, HEAD4a8d4d4b339528a88e6220fb8402e5a2c771136c.

## Authorization and integrity
Actual current user message approves exact original patch33b26113d5caf74c42a862dd5679e49b57ca2db5f59fc91f6024a69fb5b81d4f and payload33971262cd8265684e38768814cd0728c264a3c314a017e60614e7f070e2d261, requests real decision/activation preparation and independent verification. Root decision record faithfully records that authorization; it is not synthetic. This verification does not extend permission to apply in real repository.

Original patch hash independently matches. Activation patch SHA256 cc5ee0ed074af41aa568e84b8fc28c4503b62deb7fe4a443f67933c914c82c34; decision SHA256 f2cc8213583f10ba6e7c69e86e5d478ad88fe825ca3bcee8ac2c78c1cb1ca607; activated manifest SHA2562b847fc20dd30adece4f03af44761d2af3b9d8d46533c4d84c230006facb88bc.

Raw canonicalTargets and sealedInputs arrays byte-identical to approved candidate. Independently recomputed payload matches decision. All17sealed input hashes and2canonical hashes match current actual files. Both patches apply cleanly to independent current snapshot. Actual decision copied from current repository, not manufactured by verifier.

## Executed tests
Fresh disposable build/test:53 total, **50 PASS /3 FAIL /0 skipped**. Of these, all35 AuthorityFixture tests PASS (one positive34negative); normal NoCodeFilePointsIntoDocsOutsideTheFiveFolders PASS. Remaining original architecture tests15PASS/3FAIL. Failures reproduce deferred external guards:
- MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun (Platform DB010).
- JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew.
- JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew (HCM/Talent).

First sandboxed VSTest could not bind localhost IPC and aborted. Authorized disposable-only retry completed; final actual TRX is results/full-authorized.trx. Initial abort is retained, not counted as test evidence.

Negative tests reject unknown/unregistered paths, mutated history, invalid/missing pins, missing canonical/provenance/decision, payload mutation, synthetic production authority, unsafe paths/symlinks, malformed records and vacuous input/presence cases. No service runtime tests needed or claimed.

## Independent source review
Activation changes only status+decision reference. Original candidate changes only policy, guard and new authority record. Scanner suffix (Hits/NestedDocsParents/CodeFiles/Directories/RepoRoot) byte-identical; constants and presence checks preserved. No wildcard exclusion, canonical relocation, historical rewrite or bypass introduced. Hash-pinned approval proves integrity; human authority derives from actual user message, not approvedBy string alone.

## No-change / limits
Real repository14,417 files before/after hash-identical; zero changed/new/removed within non-generated baseline. Branch/HEAD/status unchanged. .git and build/cache/vendor generated directories excluded from content inventory; git status separately captured. No real source modifications, commits, pushes or stashes. Canonical and17sealed files unchanged. Repository overall gate remains BLOCKED by3external failures even if this proposed repair is subsequently applied and confirmed. CT acceptance and real application separate.

Evidence: independent-integrity.json, test-results.json, repository-no-change.json, source-review.txt, full.log, full-authorized.log, results/full-authorized.trx. Full disposable source snapshot retained under repo/.
