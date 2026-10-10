# HELD DEV prompt
Work Package ID: MVP6-MOD0187-DEV-FINAL-01 (proposed)
Prompt ID: MOD0187-DEV-FINAL-v1.0
Agent Lane ID: AL-MVP6-CLAIMS-DEV-FINAL01
Agent Lane Type: DEV
Target Agent / Entry Point: @orchestrator + /add-module, approved bounded backend only
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

## NE / Objective
Implement only queryClaims, createClaim and transitionClaim after gates are released. Use the 47-path allowance and final controlling annex; no unapproved business default. No implementation is authorized now.

## NEDEN
Turn accepted candidate semantics into actual HTTP/JWT and persistent behavior; historical model PASS cannot establish runtime correctness.

## NASIL / Preconditions and ownership
Read AGENTS, SOP, pack, exact inputs and Phase1.5 exception dispositions. Require explicit runtime approval, approved pack and assigned isolated checkout; stop on missing gate. Re-run DCP-002, branch/HEAD/dirty preflight and exact hashes. Do not discard concurrent dirty files. Confirm all existing path contents before modifying. Integration owner separately supplies approved Program.cs patch; Claims writer cannot edit it. Reuse existing assembly scanning and four pipeline behaviors. A newly required shared path is a scope GAP, not implicit permission.

Implement final Claims precedence: auth parser → trusted claims/coarse grant → headers/query → media/body → target grant → receipt → scoped references/aggregate → root → business/lifecycle → atomic commit. In a found receipt compare saved root BEFORE lexical fingerprint: wrong root plus changed valid payload gives409 CLAIM_CORRELATION_MISMATCH; same root/key changed valid payload409 IDEMPOTENCY_KEY_REUSED; identical original result replays without writes or reference rereads, after current grant checks. Lifecycle422 INVALID_CLAIM_TRANSITION. Preserve Claims-specific optional scope headers and permission rules; do not copy Returns rules.

Create inherits authoritative Shipment root; missing/null/empty503, malformed502, mismatch409 per exact annex. Mock-first work must label real producer uptake unproven. Never derive root from trace/identity, infer carrier, write Shipment, or create stock. Four separate aggregate/receipt/audit/outbox write stages must be observable; each precommit injected failure reaches its own stage and aborts all writes. Postcommit response loss and unknown commit are not zero-write assertions; recover original result by same-key receipt lookup. No worker/publisher; outbox remains Pending.

## DOĞRULA / Acceptance and validation plan
Every RT-R01–RT-R30 in runtime-acceptance-R01-R30.md is mandatory and unexecuted at preparation time. Use exact mapped test paths and raw observations, not assertions repeating expected literals. Fresh restore/build in isolated checkout; bind source→build→binary SHA256→actual process command/PID→HTTP requests/results. Real JWT grants, tenant/LE isolation, soft-delete, 49 lifecycle pairs/7allowed edges, lexical amount/time, zero approval, self-approval grants, duplicates, receipt scope and root precedence must be measured. DB-010 isolated test Mongo/replica set on separately assigned port, never operational27017; measure all four collections, real concurrent races, unique collisions, each write-stage fault, unknown commit and two-process restart. HTTP composition awaits separate integration lease. Historical model tests are reference-only. Producer mock evidence does not establish live producer/root uptake.

## YAPMA
No canonical/guard/pack promotion, finance posting, new endpoint, policy-code change, migration/backfill, broad shared cleanup, commit/push/stash or actual checkout application. No speculative default when final annex is silent.

## Output contract / failure protocol
Deliver exact source patch and changed-file SHA256 manifest; commands/exits, source/binary/process/request lineage, test/fault/concurrency/restart raw evidence, RT-R01–30 outcome matrix and SOP§22. Explicit writer-complete then independent VER. Missing approval/hash mismatch/overlap/extra path: stop dependent work and report exact GAP. New product failure: report it; do not enlarge scope. DEV PASS is not CT acceptance or rollout authorization.
