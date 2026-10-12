# HELD independent VER prompt
Work Package ID: MVP6-MOD0187-VER-FINAL-01 (proposed)
Prompt ID: MOD0187-VER-FINAL-v1.0
Agent Lane ID: AL-MVP6-CLAIMS-VER-FINAL01
Agent Lane Type: VER
Target Agent / Entry Point: read-only-auditor / read-only-audit, independent testing-agent checks
Prompt Version: 1.0
Status: HELD — preparation only; no dispatch authority
Capability Block: MVP6 Logistics / Claims bounded backend
Module: MOD-0187 Claims Management
Build Sequence: publication → approved pack/Phase1.5 → runtime DEV → independent VER → CT disposition
Build Lane: Claims isolated backend
Risk Class: HIGH
Target Branch: feature/mvp6-logistics
Expected Base HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c
Repository: /Users/natig/Projects/ERP-vNext-recovery
Worktree: current INS repository above; isolated INS filesystem snapshot /private/tmp/mvp6-mod0187-final-pack-kk_tm6ed/checkout is NOT a registered Git worktree. CT must assign an isolated runtime worktree and record its absolute path before dispatch. No Git worktree creation is authorized by this prompt while HELD.
Dirty-worktree baseline: baseline.txt SHA256 9c8c44e678f2158b871a3d8460301b603f00762f68ea72c0016f564cfc17b5f2; preserve existing dirty content. Fresh dispatch baseline and changed-input disposition mandatory.
Depends On: exact final publication; explicit pack/47-path/Phase1.5 approval and runtime release; separately approved integration-owner composition for HTTP verification.
Parallel-Safe With: MOD0186-FINAL-PACK-DELTA-01 and disjoint Claims/Returns feature paths; shared Program.cs integration serialized.
Integration Order: publication → approved pack/Phase1.5 → runtime DEV; composition supplied by integration owner → independent VER.
Authority Sources:
- Identity authority: docs/System Capability & Implementation Blueprint - master 8.1.xlsx; execution/registries/module-id-registry.md; fresh dcp-002.txt.
- DCP: execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md; domain DCP-009 authority referenced by domain-config.
- Domain Config: execution/domains/supply-chain-execution/domain-config.md.
- Module Pack: execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md plus proposed-pack.patch ONLY after separate approval/application.
- Related inspection/ADR: authority-and-contract.md, phase-1.5-and-integration.md, runtime-acceptance-R01-R30.md, inputs.sha256; real user D187 candidate-design approval does not grant runtime.
Allowed Paths: exact 47 entries in prospective-owned-paths.txt SHA256 eebaf0ac75206a664c168c072411224dc22e4f9b295cf70ad79031eb72e99517; future runtime scope proposal, no broad directory allowance. This INS owns only docs/roadmap/plans/mod-0187-final-pack-delta-01.
Protected Paths: every path outside that manifest, especially Program.cs, other modules, .antigravity, shared entity/serializer/repository/DI, canonical contracts/annexes, guard, registry, gateway, packs and financial posting. Evidence output directory must be assigned in dispatch; no unrestricted repo writes.
Golden-Flow Profile: B (backend); INS review Profile C. shell:none; form fields:0; golden_reference:none; UI/DataTable/modal/localization N/A.
Exact contract: proposed SHIPMENT-BUNDLE 3.0.0 / wire v1 YAML 5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c; Claims annex 16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63; root annex 7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af. Resolve exact paths via authority-and-contract.md, verify bytes, never substitute old root or current2.0.0.
Persistence level: L3; four scoped collections claims/claims_receipts/claims_audit/claims_outbox in one transaction, immutable original receipt/audit, Pending outbox without worker.
Consistency expectation: tenant+LE scoped atomic writes and internal CAS; HTTP reference observation is not distributed atomicity. Exact strings preserve decimals/time fingerprints; no rounding/backfill/finance writes.

## NE / NEDEN
After DEV writer-complete, independently verify exact delivered Claims patch against approved contract and runtime acceptance. Verifier must not be the implementation author. Never turn declaration/model evidence into Mongo, concurrency or runtime uptake PASS.

## NASIL / Preconditions
Require released scope, exact approved composition artifact and DEV handoff hashes. Use a separate disposable copy of exact source baseline, apply delivered patch without editing product/tests, compare resultant hashes and changed-path allowance. Do not run historical scripts in place. Fresh restore/build, binary/process lineage and fresh executions are required for runtime claims. Inspection-only paths remain read-only; output only to CT-assigned VER evidence directory/disposable environment. Registered worktree path/branch must be recorded at dispatch, not fabricated from this INS filesystem snapshot.

## DOĞRULA / Acceptance and validation plan
Execute all RT-R01–30 mapped tests with actual HTTP/JWT and DB-010 isolated Mongo, not operational27017. Verify source-owned isolation/soft-delete and real reference mock wire requests. Explicitly prove original-result replay, changed valid payload409 IDEMPOTENCY_KEY_REUSED and wrong-root precedence409 CLAIM_CORRELATION_MISMATCH; receipt actor/grant behavior, lexical differences, amounts and all49source×target pairs. Independently inject failure after each distinct aggregate/receipt/audit/outbox write; prove stage reached and rollback with four collection measurements. Test real contention/CAS/unique indexes and receipt resolution after response loss/unknown commit. Deliberate disposable mutants must fail targeted assertions, particularly fingerprint omission/root precedence/early-return failpoint conflation. Keep mutant edits outside product deliverable, never fix source. Two-process restart must use recorded binaries and fresh process command evidence. No worker/publisher registration, Pending outbox, financial/stock writes0. Real Shipment producer uptake remains separately measured or explicitly missing; mock PASS is not uptake.

## YAPMA / failure protocol
Do not fix source, tests, canonical, guard, pack or Program.cs; no approval or scope expansion. Failure produces exact path:line, observed/expected result and closure test for separate rework. Missing runtime environment/evidence yields PARTIAL, never inferred PASS. Check unchanged protected inputs against dispatch hashes and separate concurrent unrelated drift.

## Output contract
SOP§22 PASS/REWORK/PARTIAL within exact evidence bounds, per-RT matrix, command/exits/raw outputs, exact source/patch/build/binary/process/input/output hashes and immutable evidence archive. Compare independent results with DEV without adopting DEV PASS. Publication, consumer consent, CT acceptance and deployment/data-provenance gates remain separate.
