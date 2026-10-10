# MVP6-DOCS-PATH-APPLY-01 / v1.0 — SOP §22 application report

Date: 2026-09-17. Lane AL-MVP6-DOCS-DEV01; testing-agent; Profile B; Risk HIGH; Evidence E2.

## Agent Verdict / authority

**PASS for the exact authorized application and affected DocsPathGuard checks.**
**Full repository architecture gate remains BLOCKED:50 PASS/3 FAIL. Independent VER pending.**
Agent PASS is not CT acceptance, MOD0184 closure or MOD0185 DEV GO.

The prior handoff explicitly authorized candidate preparation only. Actual application authority is the
user's current MVP6-DOCS-PATH-APPLY-01 instruction: apply the exact policy patch plus activation within
four owned paths. This is a new explicit application instruction, not inferred candidate consent.
Existing real owner decision was read/hash-verified and left unchanged:
`docs/records/decisions/2026-09/mvp6-docs-path-owner-approval-01.json`.
Kind DOCS_PATH_OWNER_DECISION; status APPROVED; ID MVP6-DOCS-PATH-OWNER-APPROVAL-01;
payload33971262cd8265684e38768814cd0728c264a3c314a017e60614e7f070e2d261.
Decision SHA256f2cc8213583f10ba6e7c69e86e5d478ad88fe825ca3bcee8ac2c78c1cb1ca607.
No synthetic approval was installed. Machine checks bind scope/record integrity; the user instruction
and existing owner record supply authority. No new waiver or owner decision was created.

## Branch / HEAD / worktree

Repository `/Users/natig/Projects/ERP-vNext-recovery`; branch `feature/mvp6-logistics`;
HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Staged empty.
Fresh preflight captured14,423 tracked/untracked existing files, dirty inventory and SHA256 baseline.
Pre-existing dirty canonical Shipment, MOD0184–0187 packs, Program.cs and untracked implementation,
approval, candidate and evidence files were expected inputs, not reverted or rewritten.
One shared writer in this WP; no parallel source writer delegated. No branch switch, stash, staging,
commit or push. Scope comparison found no unrelated concurrent change.

Temporary measurements/logs/TRX: `/private/tmp/mvp6-docs-path-apply-01-43qjifyp/`.
These artifacts exist locally; this report persists their hashes/results but does not claim the temporary
files were archived into the repository. Independent VER must reproduce, not rely solely on temporary files.

## Exact applied inputs and changed-file inventory

1. Original patch: `/private/tmp/mvp6-docs-path-candidate-01-nwap8hb0/package/candidate.patch`.
   SHA256 `33b26113d5caf74c42a862dd5679e49b57ca2db5f59fc91f6024a69fb5b81d4f`.
2. Activation: `docs/records/audits/2026-09/mvp6-approved-candidates-2026-09-17/docs-activation.patch`.
   SHA256 `cc5ee0ed074af41aa568e84b8fc28c4503b62deb7fe4a443f67933c914c82c34`.

Both hashes matched before mutation. Both patches checked/applied sequentially in a disposable preflight;
then each `git apply --check` passed immediately before its real application. No conflict, fallback,
manual edit or conflict resolution. All three resulting files match the preflight bytes exactly.

| Change | Exact path | Applied SHA256 |
|---|---|---|
| MODIFIED | `.antigravity/rules/docs-organization.md` | `c4e0d1e8e1c38f6dbe76bec846daf6090972aa4746eb2e298d58b40858eb372d` |
| NEW | `docs/reference/architecture/docs-path-authority.json` | `2b847fc20dd30adece4f03af44761d2af3b9d8d46533c4d84c230006facb88bc` |
| MODIFIED | `tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs` | `f6b6f3c6427e254e1c1ebf6834225c18041e1bdec6ac3607008238fee6e14b1b` |
| NEW | `docs/records/audits/2026-09/mvp6-docs-path-apply-01.md` | This report; self-hash excluded |

This four-file list is the complete WP inventory, not the full dirty tree. Build outputs under ignored
bin/obj are derived test artifacts; evidence output goes to the temporary directory above.

## Contract/control flow, failures and behavior

Owner decision + exact payload → validated authority → exact canonical/provenance/source SHA checks →
existing full code-path scan and presence checks → consumed disposition verification.
Five-folder rules, scan extensions, traversal and regexes remain; no wildcard exclusion or blanket records
skip.17 sealed inputs are distinguished as active tools versus historical data; existing source bytes and
historical recorded contract hashes remain unchanged. Canonical targets are verified as current files.
Missing/null/wrong SHA, missing target/provenance, changed history, unknown path/tool, malformed approval,
synthetic approval at normal entry, duplicate/unknown metadata, unsafe paths and empty-input scans fail closed.
The manifest is APPROVED through the real decision binding; candidate test-only approvals remain isolated.

## Reproduced build/test commands and measured results

The first command compiled the updated project in the real checkout. VSTest then aborted due to sandbox
localhost socket denial (exit1); this aborted run is not test PASS or a source failure. Subsequent runs used
approved local test communication permission and the newly built real-checkout binary, with no source edits.

```sh
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --no-restore --filter 'FullyQualifiedName~AuthorityFixture' --logger 'trx;LogFileName=fixtures.trx' --results-directory /private/tmp/mvp6-docs-path-apply-01-43qjifyp/results --verbosity minimal
```

Executed next, separately; logs respectively fixtures-actual.log,guard-actual.log,architecture-actual.log:

```sh
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~AuthorityFixture' --logger 'trx;LogFileName=fixtures-actual.trx' --results-directory /private/tmp/mvp6-docs-path-apply-01-43qjifyp/results --verbosity minimal
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~NoCodeFilePointsIntoDocsOutsideTheFiveFolders' --logger 'trx;LogFileName=guard-actual.trx' --results-directory /private/tmp/mvp6-docs-path-apply-01-43qjifyp/results --verbosity minimal
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --no-build --no-restore --logger 'trx;LogFileName=architecture-actual.trx' --results-directory /private/tmp/mvp6-docs-path-apply-01-43qjifyp/results --verbosity minimal
```

| Real checkout check | Measured result | Exit |
|---|---|---|
|35 fixtures, including34 negatives and1 positive |35 PASS/0 FAIL/0 skipped |0 |
|Normal DocsPathGuard with real activation |1 PASS/0 FAIL |0 |
|Full architecture suite |53 executed:50 PASS/3 FAIL/0 skipped |1 |
|git diff --check |clean |0 |

The35 fixture tests are included in the full53; do not add them again to the total. These are fresh results,
not the disposable50/3 result copied from the handoff. No service HTTP/Mongo/runtime suite was required or
run for this policy-only change. Evidence level E2; no new runtime acceptance claim.

Exact remaining failures:

1. `TenantArchitecture.ArchitectureTests.MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun`
   — Platform `PpmAuditRetentionPolicySeedMongoTests.cs` and `DisposableStandaloneMongo.cs`.
2. `TenantArchitecture.ArchitectureTests.JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew`
   — HumanCapital and TalentEcosystem API Program.cs.
3. `TenantArchitecture.ArchitectureTests.JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew`
   — same HumanCapital/Talent Program.cs.

These are the previously deferred external failures; no fourth DocsPath failure remains in this execution.
They were not changed, waived or marked PASS. The repository gate remains BLOCKED.

## Integrity / evidence hashes

Applied activated manifest SHA256:
`2b847fc20dd30adece4f03af44761d2af3b9d8d46533c4d84c230006facb88bc`.

Canonical files preserved:

- Shipment YAML: `ba9d85f086dd2bfc150c1818843fa22c5b00b0dba1948a57a3672e2529d9880f`.
- Carrier annex: `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee`.

17/17 sealed source hashes and every referenced provenance hash match. Owner decision and old manifests,
reports, runtime sources, other rules and all packs retain preflight bytes. Of14,423 pre-existing files,
14,421 are unchanged; only the two explicitly owned existing files changed. New files are solely authority
manifest and this report. Preservation details: temporary baseline.json and preservation.json.

Architecture test binary SHA256: `83fb15c0fb0576f5c581c205fe058b3effbc2f2c3718cc1c9be6c6ac9935e7fa`.

- `/private/tmp/mvp6-docs-path-apply-01-43qjifyp/results/architecture-actual.trx` — SHA256 `5c13d3c9049653f1bbb672cdf2a8d9dde7bd71b3399426203458afb24409c045`.
- `/private/tmp/mvp6-docs-path-apply-01-43qjifyp/results/fixtures-actual.trx` — SHA256 `905664a65760c96113e74c19d8819ea8648ad1b49b26ed057005f3eb64c9774a`.
- `/private/tmp/mvp6-docs-path-apply-01-43qjifyp/results/guard-actual.trx` — SHA256 `4324bfd107d3af7846b31745120717ae5402003058060b6662273c4326eddfdd`.

## Independent VER handoff — separate lane, not dispatched/accepted here

Proposed WP MVP6-DOCS-PATH-VER-01; read-only source inspection plus independently reproduced E2 tests.
Use current branch/HEAD and exact applied hashes above. Read AGENTS.md, handoff, real owner decision,
this report, applied manifest, rules and guard. Verify original+activation diff equals current three files;
check decision payload/hash,17 sealed files,2 canonical files and provenance. Inspect all negative cases,
including null/missing hash regressions and no-input presence failure. Rebuild independently and rerun35
fixtures, normal guard and full suite; report real counts and compare named3 external failures.

Preserve all repository source/evidence; output new verification artifacts outside this DEV inventory.
Use a separate disposable copy if strict no-output-in-repository mode is required. Do not run historical
uptake scripts in place because they write old results. No source fixes or new owner decisions in VER;
any defect returns to a separately authorized rework WP. Check fresh dirty/hash baseline before/after.
Return Agent Verdict / Verification Verdict / CT status separately, with commands/TRX/provenance.
Do not declare MOD0184 acceptance or MOD0185 DEV GO.

## Remaining limits / rollback

Independent VER and central disposition remain pending. Persistence and application RBAC behavior are
unchanged; this WP tests documentation-policy enforcement only. Observability is test logs/TRX; no runtime
logging change. No contract migration or data migration. Any rollback requires explicit authorization,
exact reversal of owned changes after checking intervening edits, and preservation of historical approval
records; do not reset the dirty worktree or rewrite the real owner decision. No rollback performed.

Out-of-scope changes: none. Agent PASS applies only to this authorized patch application and E2 evidence.
