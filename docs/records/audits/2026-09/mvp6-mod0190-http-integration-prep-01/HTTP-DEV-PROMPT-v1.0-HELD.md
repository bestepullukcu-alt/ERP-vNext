# HELD — MOD-0190 composed HTTP DEV, v1.0

Work Package ID: MVP6-MOD0190-HTTP-DEV-01
Prompt ID: MVP6-MOD0190-HTTP-P01
Agent Lane Type: INT/DEV; Target Agent: integration-agent + @orchestrator
Risk: HIGH; Profile: B; Branch: feature/mvp6-logistics; base HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c
Worktree: new registered isolated integration checkout, seeded from the exact 341-entry normal source archive plus 38-entry Sandop source archive; fresh dirty baseline required.
Depends on: real owner transfer/composition approval in AUTHORITY.md. Parallel-safe: no shared writer. Integration order: exact transfer → exact Program.cs patch → HTTP DEV → independent VER.
Allowed production paths: exact 38 paths in transfer-manifest.tsv and Program.cs composition.patch only. Protected: MOD-0192, canonical/guard, gateway, UI, shared permission, other module behavior.

NE: After approval, transfer the exact 38 files and apply only the exact Program.cs patch to a hash-matching registered isolated checkout. Produce direct-service HTTP/JWT evidence.
NEDEN: CORE-REVER-01 verifies direct Mongo behavior, not routed HTTP or process restart.
NASIL: Recheck archive/manifest and all target paths; stop on a conflicting destination. Pin Program.cs baseline/target, JWT configuration, published YAML/annex and pack. Fresh build; DB-010 lane-owned replica set and lane-only API port. Use real JWT middleware and short-lived test JWTs without recording token/secret. Exercise six operations, auth/permission, tenant/LE, exact key, replay/current vs original correlation, lifecycle, receipt/fingerprint, rollback/unknown commit, Pending outbox and same-binary process restart. DEMAND remains exact scoped fixture; Workflow and delivery remain absent. Record source→binary→PID→raw HTTP/DB evidence and cleanup.
YAPMA: Do not change business core, MOD-0192, contract/guard, gateway, shared permissions, auth validation, publisher or operational DB. No commit/push/stash.
DOĞRULA: Execute relevant Sandop and same-host regressions, six-operation HTTP acceptance and negative/failure paths. Preserve historical 147/152 full-suite and 15/18 architecture non-PASS until fresh results exist. SOP §22, complete hashes, TRX/raw evidence, exact scope and writer-complete.
