# MVP6-GUARD-COMPLETE-DISPOSITION-01 — approval validation / SOP §22

Verdict: APPROVAL RECORDED; ACTIVATION BLOCKED.

User authorized exactly five added historical-data inputs on 2026-09-20; older candidate prose saying six is inaccurate and was not rewritten. Approved payload.txt, decision-candidate and authority-final.patch hashes match the exact user message. Existing 17 seals plus all five added source/provenance hashes verified. All 22 source hashes rechecked after testing.

Real decision record: docs/records/decisions/2026-09/mvp6-guard-complete-disposition-owner-approval-01.json
Decision-file SHA256: cd399b2179aada749738121a1863065830c938cf055e82450cd2d7dec5a309df
Approved authority working artifact SHA256: c8264d10849680e6e08da38d61be9a06b82798fcecf172f4805c45035f6bb740
The resulting decision FILE hash is bound in the disposable authority; canonical authority is not activated.

## Exact blockers
1. Production ReadAuthority line222 hashes raw canonicalTargets + newline + raw sealedInputs to b1dd34f588e360b92041275f12b6f5b1f996b7fb1ef3a207cb3f3c91e30364c1. User-approved standalone payload hash is 634208abae660a3a3669cc4c9a5a59105605419a2b1c71eae50c7c16eaa42202. Thus approved candidate bundle is internally inconsistent. No consent transferred to the reader hash.
2. ReadAuthority line236 requires every seal path to start docs/records/. Added input-manifest.json and baseline.json live under docs/roadmap/plans/mod-0183-root-uptake-recovery-01/. Synthetic fixture recalculates its own payload then exposes this independent failure. Production reader stopped before scan inventory; full scan cleanliness is NOT established.
3. Fixture patch 7aed354f4feddd108ec373bc547ed1f5a9e0e8c0153d3495060d9369f87778e2 has only prior disposable preparation authorization (2026-09-20 09:09:18 UTC user message); no real-checkout application grant found. Not applied. It also unconditionally adds three paths already present in the 22-seal candidate, so it cannot be assumed compatible with that candidate without new review.

## Tests
Fresh disposable checkout with exact approved decision binding, unchanged guard source, no fixture-fix applied. Initial VSTest run aborted on sandbox socket permission; subsequent permitted local test run completed: 34 PASS, 2 FAIL, 36 total, exit1. Failing tests: NoCodeFilePointsIntoDocsOutsideTheFiveFolders (payload), AuthorityFixture_ReadsExactFilesAndProvenance (path prefix). Existing negative cases passed, but these do not offset failed positive/production gates.
Evidence: /private/tmp/mvp6-guard-approved-validation-uxmm7pw2/guard-authorized.log and checkout/tests/architecture/TenantArchitecture.ArchitectureTests/TestResults/approved-guard.trx.

## Archive and continuation
Recovery working authority artifact has not been moved. Relocation permission is preserved, but relocation alone cannot resolve the two reader failures; no partial move/reference rewrite performed. Before eventual move, verify durable non-code archival copy hash, then update working references atomically while preserving historical inputs.
Required: reconcile raw serialization with the approved payload or present a precise changed payload for additional consent; separately resolve the exact roadmap seal policy mismatch with a reviewable owner disposition (no blanket exclusion, no historical rewrite); review fixture applicability/authorization against final seals. Current approvals remain valid for their exact bytes. No new business/runtime consent requested.

Only new owner decision and this audit record were written in real checkout; candidate binding and tests are disposable. Canonical YAML/authority, guard source, historical inputs, runtime, pack and git were not modified by this task. No activation/publication/runtime/migration/promotion/commit/push occurred.
