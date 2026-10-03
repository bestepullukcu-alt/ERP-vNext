# DEV dispatch v2.0 — PACK-ACTIVATE-01

Status: GO for isolated47path implementation; runtime not started by activation.
Controlling metadata: branch feature/mvp6-logistics; source HEAD 4a8d4d4b339528a88e6220fb8402e5a2c771136c; isolated filesystem checkout /private/tmp/mvp6-claims-pack-activate-jp3cr5qd/checkout; NOT a registered Git worktree. Dirty current bytes are preserved in checkout-input-manifest.tsv, not HEAD-only. Pack final SHA256 d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0.
Actual owner grant: owner-authority.md; publication: mvp6-final-publication-execution-01/SOP-22.md; canonical target pins in REPORT.md. Exact47path allowance: owned-paths.txt. Protected: all other files incl Program.cs/canonical/guard/other packs/Returns. Evidence may be written under this checkout's lane-specific evidence/claims-dev directory; no common checkout runtime writes.
Phase1.5 design checked; current published target supersedes proposal wording. Historical HELD/start-condition text quoted below is reference only: no reapproval of closed owner/publication gates. Consumer uptake is acceptance output. Program.cs composition remains separately authorized integration work, not a consumer grant. Current task performs no runtime. No Git mutation required; do not infer a branch switch or shared checkout permission.
At dispatch rehash inputs/pack and stop on unreviewed drift. VER creates its own isolated copy from delivered DEV baseline/patch after writer-complete and records exact path, source/binary/process hashes.

Work Package ID: MVP6-MOD0187-DEV-FINAL-01
Prompt ID: MOD0187-DEV-FINAL-v2.0
Prompt Version: 2.0
Capability Block: MVP6 Logistics Claims
Module: MOD-0187
Build Sequence: published target → activated pack/Phase1.5 → DEV → independent VER
Build Lane: isolated Claims backend
Agent Lane ID: AL-MVP6-CLAIMS-DEV-FINAL01
Agent Lane Type: DEV
Target Agent / Entry Point: orchestrator / add-module
Risk Class: HIGH
Dirty baseline: baseline.txt + checkout-input-manifest.tsv
Depends On: verified owner grant + activated exact pack; VER depends on writer-complete; shared composition separately gated
Parallel-Safe With: MOD0186 isolated disjoint writer
Integration Order: single integration owner applies separately authorized composition after writer handoffs
Authority Sources: AGENTS.md; domain-config; activated MOD0187 pack; owner-authority.md; REPORT.md publication pins
Golden-Flow Profile: B; no UI
Persistence: L3 fourcollection atomic transaction; model PASS does not establish actual Mongo behavior
Acceptance: exact30rows runtime-acceptance-R01-R30.md, all future/unexecuted

## NE / Objective
Implement only queryClaims, createClaim and transitionClaim within the released isolated scope. Use the 47-path allowance and final controlling annex; no unapproved business default. The actual conditional owner grant now authorizes this isolated implementation.

## NEDEN
Turn accepted candidate semantics into actual HTTP/JWT and persistent behavior; historical model PASS cannot establish runtime correctness.

## NASIL / Preconditions and ownership
Read AGENTS, SOP, pack, exact inputs and Phase1.5 exception dispositions. Verify owner-authority.md, activated pack hash and assigned isolated checkout; stop on drift. Re-run DCP-002, branch/HEAD/dirty preflight and exact hashes. Do not discard concurrent dirty files. Confirm all existing path contents before modifying. Integration owner separately supplies approved Program.cs patch; Claims writer cannot edit it. Reuse existing assembly scanning and four pipeline behaviors. A newly required shared path is a scope GAP, not implicit permission.

Implement final Claims precedence: auth parser → trusted claims/coarse grant → headers/query → media/body → target grant → receipt → scoped references/aggregate → root → business/lifecycle → atomic commit. In a found receipt compare saved root BEFORE lexical fingerprint: wrong root plus changed valid payload gives409 CLAIM_CORRELATION_MISMATCH; same root/key changed valid payload409 IDEMPOTENCY_KEY_REUSED; identical original result replays without writes or reference rereads, after current grant checks. Lifecycle422 INVALID_CLAIM_TRANSITION. Preserve Claims-specific optional scope headers and permission rules; do not copy Returns rules.

Create inherits authoritative Shipment root; missing/null/empty503, malformed502, mismatch409 per exact annex. Mock-first work must label real producer uptake unproven. Never derive root from trace/identity, infer carrier, write Shipment, or create stock. Four separate aggregate/receipt/audit/outbox write stages must be observable; each precommit injected failure reaches its own stage and aborts all writes. Postcommit response loss and unknown commit are not zero-write assertions; recover original result by same-key receipt lookup. No worker/publisher; outbox remains Pending.

## DOĞRULA / Acceptance and validation plan
Every RT-R01–RT-R30 in runtime-acceptance-R01-R30.md is mandatory and unexecuted at preparation time. Use exact mapped test paths and raw observations, not assertions repeating expected literals. Fresh restore/build in isolated checkout; bind source→build→binary SHA256→actual process command/PID→HTTP requests/results. Real JWT grants, tenant/LE isolation, soft-delete, 49 lifecycle pairs/7allowed edges, lexical amount/time, zero approval, self-approval grants, duplicates, receipt scope and root precedence must be measured. DB-010 isolated test Mongo/replica set on separately assigned port, never operational27017; measure all four collections, real concurrent races, unique collisions, each write-stage fault, unknown commit and two-process restart. HTTP composition awaits separate integration lease. Historical model tests are reference-only. Producer mock evidence does not establish live producer/root uptake.

## YAPMA
No canonical/guard/pack promotion, finance posting, new endpoint, policy-code change, migration/backfill, broad shared cleanup, commit/push/stash or actual checkout application. No speculative default when final annex is silent.

## Output contract / failure protocol
Deliver exact source patch and changed-file SHA256 manifest; commands/exits, source/binary/process/request lineage, test/fault/concurrency/restart raw evidence, RT-R01–30 outcome matrix and SOP§22. Explicit writer-complete then independent VER. Missing approval/hash mismatch/overlap/extra path: stop dependent work and report exact GAP. New product failure: report it; do not enlarge scope. DEV PASS is not CT acceptance or rollout authorization.
