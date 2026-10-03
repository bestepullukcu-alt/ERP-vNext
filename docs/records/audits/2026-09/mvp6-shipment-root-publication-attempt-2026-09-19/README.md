# SOP §22 — exact root publication / activation attempt

## Authority disposition

The repository user explicitly granted in Agent Lane-3:
- Compatibility consent for Shipment / Loads / Returns / Claims against final YAML
  91d505c900ce214262fd35ecb6bad9f005b4bbc42f1680d9db7cc1932bf0fbc9, with the reconciled report limitations.
  Returns/Claims consent is draft-design compatibility only. Producer runtime uptake and Loads multi-root decision remain open.
- Publication approval for patch 0ef69539bc35a7be2b7e97f87558690262138d55711e9394ae09e5bc33b64b72
  from verified2.0.0 baseline to that exact2.1.0/FROZEN YAML.
- Conditional activation approval for patch c6f07fa7e3672af1a0b36e54bb0873dd97d6f7c5ebd92bdc41ced6a762956d23
  and decision artifact ae62a22c9740b04d110572aa3c3911580fac36aaf3e7c5264d44ce4dd42c7089,
  subject to final canonical hash and historical-seal verification.
No runtime/Program.cs, migration/backfill, rollout, Phase1.5, pack promotion, DEV GO or commit/push authority.
This is a record of actual user consent, not another unsigned approval template.

## Verdict

Compatibility consent: RECORDED, exact scope and limitations above.
Publication authority: APPROVED; application DEFERRED to avoid canonical/guard inconsistency.
Guard activation: REWORK/BLOCKED due to three measurable defects in the exact approved package.
No canonical or guard activation performed. Existing2.0.0 and its valid approved binding remain coherent.
Publication consent is not revoked; it need not be requested again.

## Exact findings

1. Candidate authority decision.sha256 is f8adaf5e778db42c9684e8bc3614954512dffeb81af53c9c14a2b65f8a69b700,
   but actual referenced decision bytes hash to ae62a22c9740b04d110572aa3c3911580fac36aaf3e7c5264d44ce4dd42c7089.
   File hash and raw target/seal payload hash are different bindings; substituting one for the other is invalid.
2. Decision includes unknown scope member. Real guard uses JsonUnmappedMemberHandling.Disallow and rejects it.
3. Decision.payloadSha256 is f8adaf5e778db42c9684e8bc3614954512dffeb81af53c9c14a2b65f8a69b700;
   actual canonicalTargets.GetRawText()+newline+sealedInputs.GetRawText() hash is
   6a3f1aef23058a4cc9b4c4718e43b089483e8e0ab4c05deb22c44df3047004e4.

The package is intentionally CANDIDATE/UNAPPROVED, so merely applying it also fails the production approval gate.
Owner stamping alone would not repair these three defects. No mismatched approval was silently installed.
Previous final VER's summary does not establish successful real ReadAuthority validation after activation;
these directly reproduced guard failures take precedence for this attempt. Historical VER is preserved unchanged.

## Reviewable correction — still UNAPPROVED

The corrected proposal removes scope, binds the exact unchanged target/seal arrays with the computed payload,
and pins the corrected decision artifact's file hash. It stays CANDIDATE/UNAPPROVED with approvedBy:null.
No canonicalTargets or sealedInputs content/format was changed from the reviewed candidate.

Corrected decision artifact: aff2ed273e172880508d5176a6029760c9297c9becf7a888a2590b4273bb250d
Corrected candidate authority: 8436f34008df73528d6e61587fa3c553c4684302692ad242d2e26b65c88c6dac
Corrected candidate activation patch: e679c5ad6a07a7d7f6d725fd625127bb6fd06ad8d9768ed8f479a4d3e653a071
Actual raw payload: 6a3f1aef23058a4cc9b4c4718e43b089483e8e0ab4c05deb22c44df3047004e4

Artifacts: /private/tmp/mvp6-root-publish-95jnzdjq/corrected-unapproved/
To proceed, obtain explicit disposition authorizing this corrected binding and its real approval-record stamping;
then derive actual decision file hash, rerun normal guard before/after, and publish the already-approved YAML together
with a coherent approved binding. Do not re-request unrelated design/consumer/publication consent.

## Measured validation and limits

All11 original manifest entries matched. Both exact patches applied successfully in disposable before-copies.
Publication output equals approved final YAML hash. AST delta is exactly version2.0.0→2.1.0,
getShipment example.lifecycleCorrelationId and ShipmentDetail property/description; Returns/Claims operations,
replay codes, shared schemas/webhook/required lists remain unchanged. All17 seals and provenance hashes unchanged.

Fresh compiled probe uses byte-identical DocsPathGuardTests.cs and invokes its real ReadAuthority method:
- Original candidate rejected via normal production-mode reader.
- Isolated synthetic wrapper with scope member rejected.
- Isolated synthetic wrapper with scope removed but old payload hash rejected.
- Corrected raw payload accepted ONLY via syntheticFixture:true with SYNTHETIC_TEST_ONLY kind.
- The same synthetic decision rejected via production-mode reader.
All5 diagnostic expectations met. This is binding diagnostic E2, not independent VER or a production gate PASS.
The probe is not the full NoCodeFilePointsIntoDocsOutsideTheFiveFolders repository scan.
No runtime/schema consumer uptake, service build/run or operational DB work performed.

## Remaining SOP fields

Branch / HEAD: feature/mvp6-logistics / 4a8d4d4b339528a88e6220fb8402e5a2c771136c.
Worktree: preexisting dirty; before hashes/status/index captured under temporary evidence.
Changed files by this task: this new audit README only in repository; diagnostic/correction artifacts in temp.
Golden/contract flow: exact owner approval→manifest/baseline→patch application in temp→actual guard rejection→bounded correction.
Sub-flows/failure paths: file-vs-payload hash, unknown property, raw payload mismatch, synthetic-production rejection.
Persistence: no DB/data writes. Security: authority integrity diagnostics only, no runtime JWT/RBAC result.
Audit/evidence: binding-diagnostics.json, per-case logs, source/binary hashes, corrected artifacts and manifests in temp.
Observability: command/build/probe outputs retained. Build succeeded; vulnerability audit not claimed (NuGetAudit=false).
Migration/rollback: no rollout or canonical change, hence no data rollback; retain current coherent2.0.0/binding.
Decisions/blockers: corrected exact activation disposition required; runtime uptake/multi-root/legacy/consumer rollout remain separate.
Known gaps: external consumer inventory beyond user's named scope not inferred; draft consent is not runtime acceptance.
Out-of-scope changes by this task: none. No commit/push/stash or changes to runtime/Program.cs/guard code/policy/old approvals.
