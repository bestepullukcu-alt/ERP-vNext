# VER dispatch v2.0 — PACK-ACTIVATE-01

Status: WAITING FOR DEV WRITER-COMPLETE; independent verifier required.
Controlling metadata: branch feature/mvp6-logistics; source HEAD 4a8d4d4b339528a88e6220fb8402e5a2c771136c; isolated filesystem checkout /private/tmp/mvp6-claims-pack-activate-jp3cr5qd/checkout; NOT a registered Git worktree. Dirty current bytes are preserved in checkout-input-manifest.tsv, not HEAD-only. Pack final SHA256 d035d42059141f17edb90bb97183892a3c98c171a16770795ce2557f834ac9b0.
Actual owner grant: owner-authority.md; publication: mvp6-final-publication-execution-01/SOP-22.md; canonical target pins in REPORT.md. Exact47path allowance: owned-paths.txt. Protected: all other files incl Program.cs/canonical/guard/other packs/Returns. Evidence may be written under this checkout's lane-specific evidence/claims-ver directory; no common checkout runtime writes.
Phase1.5 design checked; current published target supersedes proposal wording. Historical HELD/start-condition text quoted below is reference only: no reapproval of closed owner/publication gates. Consumer uptake is acceptance output. Program.cs composition remains separately authorized integration work, not a consumer grant. Current task performs no runtime. No Git mutation required; do not infer a branch switch or shared checkout permission.
At dispatch rehash inputs/pack and stop on unreviewed drift. VER creates its own isolated copy from delivered DEV baseline/patch after writer-complete and records exact path, source/binary/process hashes.

Work Package ID: MVP6-MOD0187-VER-FINAL-01
Prompt ID: MOD0187-VER-FINAL-v2.0
Prompt Version: 2.0
Capability Block: MVP6 Logistics Claims
Module: MOD-0187
Build Sequence: published target → activated pack/Phase1.5 → DEV → independent VER
Build Lane: isolated Claims backend
Agent Lane ID: AL-MVP6-CLAIMS-VER-FINAL01
Agent Lane Type: VER
Target Agent / Entry Point: read-only-auditor / read-only-audit
Risk Class: HIGH
Dirty baseline: baseline.txt + checkout-input-manifest.tsv
Depends On: verified owner grant + activated exact pack; VER depends on writer-complete; shared composition separately gated
Parallel-Safe With: MOD0186 isolated disjoint writer
Integration Order: single integration owner applies separately authorized composition after writer handoffs
Authority Sources: AGENTS.md; domain-config; activated MOD0187 pack; owner-authority.md; REPORT.md publication pins
Golden-Flow Profile: B; no UI
Persistence: L3 fourcollection atomic transaction; model PASS does not establish actual Mongo behavior
Acceptance: exact30rows runtime-acceptance-R01-R30.md, all future/unexecuted

## NE / NEDEN
After DEV writer-complete, independently verify exact delivered Claims patch against approved contract and runtime acceptance. Verifier must not be the implementation author. Never turn declaration/model evidence into Mongo, concurrency or runtime uptake PASS.

## NASIL / Preconditions
Require released scope, DEV handoff hashes; require separately approved composition artifact for HTTP integration checks. Use a separate disposable copy of exact source baseline, apply delivered patch without editing product/tests, compare resultant hashes and changed-path allowance. Do not run historical scripts in place. Fresh restore/build, binary/process lineage and fresh executions are required for runtime claims. Inspection-only paths remain read-only; output only to CT-assigned VER evidence directory/disposable environment. Record actual isolated copy path and source branch/HEAD; do not mislabel a filesystem copy as a registered Git worktree.

## DOĞRULA / Acceptance and validation plan
Execute all RT-R01–30 mapped tests with actual HTTP/JWT and DB-010 isolated Mongo, not operational27017. Verify source-owned isolation/soft-delete and real reference mock wire requests. Explicitly prove original-result replay, changed valid payload409 IDEMPOTENCY_KEY_REUSED and wrong-root precedence409 CLAIM_CORRELATION_MISMATCH; receipt actor/grant behavior, lexical differences, amounts and all49source×target pairs. Independently inject failure after each distinct aggregate/receipt/audit/outbox write; prove stage reached and rollback with four collection measurements. Test real contention/CAS/unique indexes and receipt resolution after response loss/unknown commit. Deliberate disposable mutants must fail targeted assertions, particularly fingerprint omission/root precedence/early-return failpoint conflation. Keep mutant edits outside product deliverable, never fix source. Two-process restart must use recorded binaries and fresh process command evidence. No worker/publisher registration, Pending outbox, financial/stock writes0. Real Shipment producer uptake remains separately measured or explicitly missing; mock PASS is not uptake.

## YAPMA / failure protocol
Do not fix source, tests, canonical, guard, pack or Program.cs; no approval or scope expansion. Failure produces exact path:line, observed/expected result and closure test for separate rework. Missing runtime environment/evidence yields PARTIAL, never inferred PASS. Check unchanged protected inputs against dispatch hashes and separate concurrent unrelated drift.

## Output contract
SOP§22 PASS/REWORK/PARTIAL within exact evidence bounds, per-RT matrix, command/exits/raw outputs, exact source/patch/build/binary/process/input/output hashes and immutable evidence archive. Compare independent results with DEV without adopting DEV PASS. Publication, consumer consent, CT acceptance and deployment/data-provenance gates remain separate.
