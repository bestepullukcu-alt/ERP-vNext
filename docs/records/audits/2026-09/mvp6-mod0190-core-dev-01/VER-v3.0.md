# MVP6-MOD0190-CORE-VER-01 — independent dispatch v3.0

## SOP §17 metadata

Work Package ID: MVP6-MOD0190-CORE-VER-01  
Prompt ID: MVP6-MOD0190-CORE-VERIFY-P01  
Prompt Version: 3.0  
Capability Block: MVP-6 Supply Chain Execution; Module: MOD-0190  
Build Lane: AL-MVP6-MOD0190-VER01; Type VER; Target independent `read-only-auditor /read-only-audit`; Risk HIGH; Profile C  
Expected HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; source checkout `/Users/natig/.codex/worktrees/mvp6-mod0190-core/ERP-vNext-recovery`; read only after DEV writer-complete and source manifest match. Use a new disposable copy and a separate DB-010 replica set/port. Parallel-safe with MOD-0192 VER only in separate environments.  
Authority: promoted pack `6a57769c…`, exact published YAML `9543e3f2…`, annex `eb1df138…`, actual owner approval, DEV handoff/manifests and 38-path allowlist. Source/protected boundaries identical to DEV.

## NE

Independently verify the bounded six-operation core and identify exact unproven criteria.

## NEDEN

Developer test PASS is not independent acceptance; composed HTTP remains a separate integration-owned gate.

## NASIL

Verify transfer, all 38 source hashes, promoted pack, frozen hashes, binary provenance and writer-complete. Rebuild hash-identical source in a disposable checkout. Use independent tenant/LE fixtures and an isolated replica set. Reproduce lifecycle, exact-key replay, current response/original audit correlation where observable, permission/scope, duplicate race, transactional rollback, known failure versus unresolved commit, Pending outbox and persistence across client/process restart. Distinguish direct repository, model, HTTP/JWT, and test-injection evidence. Never infer live DEMAND or Event Bus delivery from fixtures.

## YAPMA

No repository/source/pack/contract/guard/shared change, no operational 27017, no commit/push/stash, no CT ACCEPTED or E5/G5 claim. If source drift, writer activity or missing manifest exists, stop and report exact issue.

## DOĞRULA

SOP §22 independent verdict, per-pack acceptance matrix, commands/TRX/raw state and no-change proof. Report the architecture 15/3 baseline as FAIL and assess only the MOD-0190 impact; do not waive external findings. HTTP/JWT remains PENDING absent separately authorized Program.cs/permission composition.
