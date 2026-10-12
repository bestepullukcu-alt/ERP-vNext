# MVP6-SANDOP-CAPACITY-PUBLICATION-01 — SOP §22

**Publication disposition: exact canonical two-file patch APPLIED; repository architecture gate FAIL (52 PASS / 4 FAIL).** The owner's current message separately grants MOD-0190 consent, MOD-0192 consent, and single-writer publication authority for the exact final-pack-01 hashes. It explicitly withholds producer/runtime uptake, pack promotion, Phase 1.5, DEV GO, Program.cs, migration, rollout, E5/G5 and commit/push. The [preceding authority reconciliation](../mvp6-sandop-capacity-final-authority-close-01/SOP-22.md) binds the original `2.0.0 / wire v1` preparation decision and the owner's no-repository-external-consumer clarification. These are distinct decisions; the predecessor's UNAPPROVED draft was not used as authorization.

## Baseline, authority and exact result

| Item | Before / authority | After / measured |
|---|---|---|
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; 185 preexisting dirty status entries, staged empty | Same branch and HEAD; staged empty, unrelated dirty work preserved |
| Canonical YAML | `docs/analysis/contracts/sandop-capacity.openapi.yaml` SHA-256 `c255e92923ba91714cec8daf229262101b5d45648b7f714a03ab402811db683c` | SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3` |
| Canonical annex | Absent | `docs/analysis/contracts/sandop-capacity-semantics-v2.0.0.md` SHA-256 `e2599fd9b8cc7cf7e4a39b4d5d1bccb3fa985c156fec6c6082f5227334770442` |
| Authorized patch | [final-pack-01/publication-proposed.patch](../mvp6-sandop-capacity-final-pack-01/publication-proposed.patch) SHA-256 `0ebc6a6fa6a867170b1d32545295e88676d74fb47794879a671c9f6c0375bd04` | `git apply --check` and `git apply` exit 0 in disposable and real checkout; exact two-file result |
| Protected DEMAND | SHA-256 `3c77262e411976e311bcf9b65be131e2035fd18a33215d24075f87209f87bb9d` | Same SHA-256 |

The user message in this task is the release authority for all three separate clauses. Both design consumers consented to the exact YAML and annex; the same message authorizes the exact patch after recorded baseline verification. It grants no runtime implementation. The artifact/baseline mismatch stop condition was checked immediately before real `git apply`; the target annex path did not already exist. No other contract or shared file was changed by this publication writer.

**Post-publication content conflict:** The exact authorized annex [lines 1–3](../../../../analysis/contracts/sandop-capacity-semantics-v2.0.0.md) still labels itself “proposed final publication artifact (UNAPPROVED)” and states that its bytes are not canonical, final wire dispositions remain unapproved, and no publication follows. Those assertions are stale after this actual owner decision and exact application. The applied bytes are canonical, but the annex's own release-status narrative contradicts that fact. The exact-hash authorization does not permit silently editing the annex; any corrected bytes would have a new hash and need an explicit artifact/consumer/publication disposition. This conflict prevents a clean documentary release verdict and must be resolved before downstream pack/DEV gates. The substantive candidate semantics and the source of this wording are retained for review.

## Verification and raw evidence

1. Pinned final-pack-01 hashes and independent [final VER](../mvp6-sandop-capacity-final-ver-01/SOP-22.md) were rechecked. Its exact final YAML had full OpenAPI 3.1 meta-schema and full-spec-validator **0 errors**, plus refs/examples and mutation checks. That validation is inherited at the identical published YAML SHA-256, not misreported as a new full-validator run; the prior offline validator dependency is no longer available locally.
2. In `/private/tmp/mvp6-sandop-publication-01-23H7y3`, `git apply --unsafe-paths --check` and `git apply --unsafe-paths` each exited 0 against an exact baseline copy. The resulting YAML and annex were byte-identical to authorized final-pack-01 artifacts. Fresh post-apply checks found `openapi: 3.1.0`, `info.version: 2.0.0`, `x-status: FROZEN`, `x-contract-version: v1`, exact annex link, **12 operations**, **204 local ref encounters** and **98 response example schema checks**. This is E1/E2 static contract evidence, not 98 distinct cases or runtime proof. [Raw archive](raw-evidence.tar.gz) SHA-256 `ed42a33a082f579dd3350b0e9c1b14f1b70c68691910604a357ca5bb799f5756` contains disposable results and TRX files.
3. On the real checkout, exact patch applied and both resulting SHA-256 values matched; scoped `git diff --check` exited 0. The current target YAML is the published canonical source at `2.0.0 / wire v1`, while the consumer packs/mocks have **not** been repinned here and no runtime uptake is claimed.
4. Fresh DocsPathGuard run: **38 PASS / 1 FAIL (39 tests)**. `NoCodeFilePointsIntoDocsOutsideTheFiveFolders` reports preexisting references to `docs/analysis` in historical candidate/VER JSON/Python files and `mvp6-mod0186-http-composition-01/inputs.json`. The published YAML and annex are absent from its offender list; neither adds an offending code-side path. The full architecture run initially aborted because the sandbox denied the test-host socket; an escalated retry executed **56 tests: 52 PASS / 4 FAIL**. The four failures are this DocsPathGuard scan, `JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew`, `JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew` (HCM/Talent `Program.cs`), and `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` (two Platform test files). No waiver or PASS is inferred. The exact failure messages and paths are in the TRX archive.

## SOP §22 fields and boundaries

| Field | Result |
|---|---|
| Agent verdict / golden flow | Exact contract publication applied at owner-authorized hashes. Technical contract content-bound E1/E2 verification passes; full repository architecture gate does not. |
| Changed-file inventory | Only canonical `sandop-capacity.openapi.yaml`, new canonical `sandop-capacity-semantics-v2.0.0.md`, and this new audit directory (`SOP-22.md`, `MANIFEST.tsv`, `raw-evidence.tar.gz`, `SHA256SUMS`). Existing final-pack, VER, authority reconciliation and other dirty sources remain untouched. |
| Subflows / failure paths | Baseline drift, changed patch or occupied annex path would have stopped application. No runtime failure path was exercised. |
| Tests | Disposable apply/byte equality and targeted post-apply refs/examples PASS; DocsPathGuard 38/1 FAIL; architecture 52/4 FAIL. Full OAS validation is inherited from identical independently verified bytes. |
| Persistence / RBAC / tenant / audit / observability | No runtime evidence or DB action. Not assessed. |
| Migration / rollback | No migration/backfill. Exact preimage remains in canonical baseline and the disposable copy; if a future authorized rollback is needed, restore only these two paths after hash verification and preserve all unrelated dirty work. No automatic rollback was performed. |
| Decisions | Three explicit owner clauses consumed at their exact hashes. No guard expansion, pack promotion or runtime authorization. |
| Blockers / known gaps | Annex lines 1–3 contradict actual publication and need an exact owner disposition; repository architecture gate is red for four listed failures. Consumer repin/uptake, Phase 1.5, actual producer/runtime and E5/G5 remain separate. Guard violations require their own disposition, not an implicit waiver. |
| Out-of-scope changes | None by this writer; no commit, push or stash. |

The release's **canonical bytes are present**, but the annex status narrative is contradictory and the repository-wide test gate is not green. Do not use this report to claim MOD-0190/0192 readiness or full release acceptance.
