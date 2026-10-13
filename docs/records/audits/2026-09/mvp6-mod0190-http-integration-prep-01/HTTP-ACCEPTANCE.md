# Bounded MOD-0190 direct-service HTTP acceptance plan

Environment: a newly registered, isolated integration checkout from the pinned 341-entry normal archive and 38-entry Sandop archive; one integration writer; DB-010 named test DB on a lane-owned replica set (not port 27017); lane-owned API port; .NET 8; ephemeral JWT issuer/audience/key and short-lived tenant/LE/actor/permission claims. Preserve the original source/Program.cs manifest before starting. Record actual request/response bytes, redacting bearer values and secrets.

| Case | Fresh behavior and persisted-state evidence |
|---|---|
| Startup/composition | Fresh build, API binary hash, launch command/PID, controller route discovery; six operations reachable with valid JWT. Existing Claims/Returns/Shipment/Carrier/Loads controllers still resolve. |
| Security | No token → published 401; missing permission → 403; wrong tenant or legal entity → scoped denial without cross-scope read/write. Verify same actor/scope token after renewal at restart. |
| Create/get | Test-only DEMAND exact ID/version/provenance fixture, create response/body/current correlation; GET returns scoped persisted body; fixture mismatch/error follows published YAML/annex. |
| Snapshot/sign-off | Draft→InReview snapshot and permitted sign-off lifecycle; invalid state and wrong snapshot error code/status; list/get under correct scope. No automatic approval/Workflow HTTP. |
| Replay/conflict | Exact Idempotency-Key (no trim) and payload replay preserve original persisted audit/root with current response correlation; changed payload and different root/status follow published precedence; replay makes no new fixture read or state write. |
| Atomicity | For each mutation, check plan/snapshot/sign-off, receipt, audit and outbox tenant/LE-scoped before/after counts. Inject rollback and unknown-commit using isolated Mongo test commands; distinguish no receipt from durable receipt, then verify same-key recovery. All outbox rows remain Pending. |
| Concurrency | Concurrent same-key calls and distinct-key business collision produce one durable effect set and documented conflict/replay results. |
| Restart | Stop only own API process, preserve DB/fixtures, relaunch the **same binary** with same config; read and replay original persisted state; compare before/after collection counts and current/original correlation. |
| Regressions | Targeted Sandop suite, same-host Claims/Returns/Shipment/Carrier/Loads tests and architecture suite; report actual unique counts and failures, without inheriting historical PASS. |

Evidence level sought: E3/E4 for bounded direct-service HTTP. Gateway/live DEMAND/Workflow/Event Bus/operational integration and E5/G5 are later gates.
