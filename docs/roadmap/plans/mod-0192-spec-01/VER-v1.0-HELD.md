# HELD independent VER prompt — MVP6-MOD0192-VER-01 / v1.0

**State: HELD. Dispatch only after an authorized DEV handoff.** Target agent: `testing-agent`, independent VER lane `AL-MVP6-MOD0192-VER-01`; risk HIGH; profile A. This draft does not authorize runtime work.

> WP: MVP6-MOD0192-VER-01 · Prompt v1.0 · HELD  
> Target: the exact DEV patch/source manifest and binary from the selected bounded CapacityPlans slice.  
> Branch/HEAD/worktree: record fresh measured values at handoff; drafting baseline is `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, dirty.  
> Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md`; currently `draft`, must be owner-promoted before DEV.  
> Owned validation output: lane-specific audit/evidence directory; source checkout read-only. Do not share DEV process, port or DB. Parallel-safe with MOD-0190 VER only on isolated source and DB scopes.

NE: Independently verify the DEV's exact CapacityPlans implementation against the frozen SANDOP-CAPACITY v1 contract and the recorded C192 owner decisions, with distinct fixture/live labels.

NASIL: Read AGENTS.md, CT SOP, promoted pack, owner decisions, DEV manifest and A192 matrix. Verify source hash and that changed product files are only authorized CapacityPlans paths plus separately approved integration-owner diff. Fresh disposable rebuild; trace source→binary→process→raw signed HTTP/JWT→scoped Mongo/outbox. Use fixed DB-010 test database with per-case tenant+LE, independent port and process. Validate create/get/scenario/evaluation, no foreign DEMAND storage, exact replay/conflict/permission/error envelopes, race/CAS, fault rollback, restart and event identity. Compare all six operations and three event schemas to exact frozen contract. If evaluator policy is not approved, verdict its terminal behavior OPEN, not PASS. Treat mock provenance and live producer verification separately.

YAPMA: No source fix, Program.cs/shared/DEMAND/gateway/pack/registry/canonical/guard edit, no secret/token archive, commit/push/stash, pack promotion, E5/G5 or downstream GO.

OUTPUT: Independent SOP §22 verdict and A192 row matrix; exact input/output hashes, build/binary/PID/config/exit transcripts, raw HTTP/DB/index/outbox evidence, no-change proof and specific blockers. If DEV source, contract or owner scope differs, fail closed with exact mismatch and leave the row BLOCKED.
