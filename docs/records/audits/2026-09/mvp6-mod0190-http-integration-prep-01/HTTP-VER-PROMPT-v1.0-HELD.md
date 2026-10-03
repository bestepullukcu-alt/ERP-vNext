# HELD — MOD-0190 composed HTTP independent VER, v1.0

Work Package ID: MVP6-MOD0190-HTTP-VER-01; Agent Lane Type: VER; Target Agent: read-only-auditor /read-only-audit; Risk: HIGH; Profile: C.
Precondition: completed DEV handoff, exact source/Program.cs manifest, writer-complete and no active source writer. Branch/HEAD/dirty baseline and source match required.

NE: Independently verify the bounded composed MOD-0190 direct-service HTTP slice.
NEDEN: A DEV PASS and direct Mongo core VER do not establish HTTP/JWT/restart acceptance.
NASIL: Use a separate hash-identical disposable source copy, fresh .NET 8 build, own DB-010 replica set and port. Bind source→binary→process/PID→actual HTTP/DB records. Reproduce six operations, JWT denial/permission, tenant/LE and no cross-scope access, lifecycle and exact-key replay/changed payload, correlation precedence, atomic Pending effects, rollback/unknown commit and restart durability. Check Claims/Returns/Shipment existing composition and regression behavior. Treat test-only DEMAND fixtures and absent live Workflow/publisher honestly.
YAPMA: No source fixes, shared/gateway/contract/guard edits, operational DB, commit/push/stash or CT acceptance.
DOĞRULA: SOP §22 independent verdict, exact matrix, source/protected hash no-change, raw commands/TRX/HTTP/DB and process cleanup. Separate achieved E3/E4 from missing E5/G5; findings require a separate DEV rework.
