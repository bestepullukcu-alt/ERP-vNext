# MVP6-SANDOP-PUBLICATION-GATE-CLOSE-01 — SOP §22 handoff

**Verdict: guard disposition ACTIVATED; annex replacement APPLIED; MOD-0190 and MOD-0192 design-consumer consent REPINNED.** This closes the SANDOP DocsPathGuard publication hold only. The full architecture suite is **53 PASS / 3 FAIL**, with the three existing failures listed below; no waiver is claimed.

## Authority and baseline

- Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; staged index empty. The checkout was already dirty. The task preserved that work, made no branch switch, commit, push or stash, and used one writer.
- The user's explicit 2026-09-22 message approved policy patch `1a77c6da9c188c3d9d06413ddd108545accaa90a45124923d12e84fc0cf22527`, authority payload `c74f046033584e59209c074cd76b29834a0fae8f8f3fb58f9728e410af28d5f5`, provenance `dc7aa123eef262014ded3bc90c325b92df71aaaf7ed668cfa732a903fe8f9f57`, annex patch `5d66663f0983e0022f5d8f1b1aaaf6e28148c417be29217f54b48edfc82204b0` and target annex `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. The same message separately repinned MOD-0190 and MOD-0192 design-consumer consent. Earlier publication authority was already exercised; the YAML patch was not replayed.
- The measured preimages were policy `1fa5ce16bd2afe743d71b52888745e01d66d398199ef968174a6633b7a77e90c`, guard source `7f0814fa61ebb653d15e56e7c395c6a3949da7e63e11d90819e49fd9046004f3`, authority `768757bb1fb9e39a324960e25c0e3943adb4bd6231d3dd50bf098915274406fb`, annex `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442`, and published YAML `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`. These hashes were rechecked immediately before application.

## Exact application and result

| Changed canonical/policy path | Final SHA-256 | Scope |
|---|---|---|
| `.antigravity/rules/docs-organization.md` | `0eaff863249654ff2dbe1990ebdcae0f610b0f3ee2bf31f197d5d8b5a744e2a9` | Exact historical-tool wording from the approved policy patch. |
| `tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs` | `da5ec0cc0f180d7237bdf599c7d71eb1fb96962ed14496427a1ea789b18c5050` | Only `.py` historical-tool kind/empty-target enforcement from the same patch. Scanner and production reader remain enabled. |
| `docs/records/decisions/2026-09/mvp6-sandop-publication-guard-owner-decision-01.json` | `e618a1f06ebca5028157a90fab8b7d799c6fde187802003de5056e21c8476654` | Real `DOCS_PATH_OWNER_DECISION`, approved by the user in this session, bound to the exact payload. |
| `docs/reference/architecture/docs-path-authority.json` | `063e0cde7c436cf0926101ac9a300b9b1e6147dd5bea5c7dbfd100e65a868fe9` | Approved status and decision-file hash; previous 22 seals and both canonicalTargets preserved; six JSON historical-data and six Python historical-tool seals appended. |
| `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` | `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda` | Exact status/authority wording replacement after the guard gate passed. Normative operation and executor rules preserved. |

The authority's raw target/seal payload matches the approved `c74f0460…28d5f5` hash. All 34 sealed input bytes and provenance bytes matched their pins; all 12 new entries have empty targets. The two preexisting canonicalTargets are unchanged, and no SANDOP target was added. `historical-tool` adds no script execution authority. The live annex is byte-identical to the candidate target; the published YAML remains `9543e3f2…ff3`.

## Verification and evidence

| Gate | Command / result | Raw evidence |
|---|---|---|
| Candidate inputs and scope | All 12 new path/hash/provenance lines verified; previous 22 seals and two targets unchanged; exact payload hash verified. `git apply --check` passed for both approved patches. | [Prior candidate](../mvp6-sandop-publication-guard-disposition-01/SOP-22.md), [annex validation](../mvp6-sandop-annex-status-disposition-01/VALIDATION.md). |
| Disposable production-mode reader and full DocsPathGuard | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --filter FullyQualifiedName~DocsPathGuardTests` on a hash-bound disposable checkout: **39/39 PASS**. Its decision kind was real `DOCS_PATH_OWNER_DECISION`, not a synthetic fixture decision. | [Disposable TRX](disposable-guard.trx). |
| Actual checkout production-mode reader and full DocsPathGuard, before annex application | Same filter with fresh build: **39/39 PASS**. Includes one production scan, one positive fixture and 37 negative fixture cases (wrong hash/path, changed history, missing decision, synthetic production, scan presence and others). The earlier candidate's six specific historical-tool fixture controls remain hash-bound prior evidence, not a substitute for this production run. | [Checkout guard TRX](checkout-guard.trx). |
| Full architecture suite after annex replacement | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --no-build`: **53 PASS / 3 FAIL / 56 total**. DocsPathGuard passed in this run. | [Architecture TRX](checkout-architecture.trx). |
| Preservation | The hash-bound disposable snapshot was compared with the checkout across **12,821** copied non-generated files, excluding only the four approved preexisting-file edits; no unexpected drift. Actual policy/guard/authority match the disposable production-pass copy byte for byte; annex matches the approved target candidate. `git diff --check` passed on the two policy files. | Final hashes in [checksums](SHA256SUMS). |

The first sandboxed VSTest attempt aborted before executing tests because localhost socket binding returned `SocketException (13)`. The permitted retry ran the tests and produced the result above; the abort is not counted as a product or guard failure.

The three full-suite failures remain outside this work package: `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` identifies Platform test files `PpmAuditRetentionPolicySeedMongoTests.cs` and `DisposableStandaloneMongo.cs`; `JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew` and `JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew` identify HCM/Talent `Program.cs`. No fix or waiver was applied.

## Boundary, follow-up and inventory

MOD-0190/0192 consent is recorded separately in [CONSENT-REPIN.md](CONSENT-REPIN.md). This handoff establishes approved annex bytes and guard activation. It does not prove real DEMAND producer validation, runtime consumer uptake, pack promotion, Phase 1.5, Program.cs composition, migration, rollout, E5/G5 or full architecture PASS. The canonical YAML was neither edited nor republished in this task.

Changed paths owned by this task are the five files in the table above plus this `SOP-22.md`, `CONSENT-REPIN.md`, `SHA256SUMS`, and the three linked TRX files. Historical candidate/VER packages were not rewritten. This writer is complete and every tool-launched test session exited. OS-wide process inspection was unavailable (`ps: operation not permitted`).
