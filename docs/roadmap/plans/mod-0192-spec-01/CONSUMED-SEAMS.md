# Consumed-seam GAP register

| GAP | Frozen/mock capability now | Missing producer or integration capability | Owner and gate |
|---|---|---|---|
| GAP-192-DEMAND-VERSION | Frozen `DEMAND` 1.0.0 example can return Published `planId` for `itemId`; a declared fixture can map opaque ID/version/checksum for isolated tests. | No query by exact plan ID + version, version in `DemandPlanRef`, authoritative checksum/capture time or event version. | Shared DEMAND/CT single writer with MOD-0190; C192-01. Live version acceptance BLOCKED. |
| GAP-192-CONSTRAINT-ADAPTER | `SANDOP-CAPACITY` v1 accepts opaque `constraintId/source/sourceVersion` and owned delta. | No versioned source lookup, resource identity/scope/UoM/capacity semantics. | Supply-constraint producer/CT; C192-02. Live constraint claim BLOCKED. |
| GAP-192-EVALUATOR | POST/GET shapes show 202 Accepted and later Completed example. | No calculation oracle, worker/queue/trigger, retry, deadline, restart or failure-policy contract. | MOD-0192 owner for policy; integration owner for shared execution; C192-03/04/06. Terminal acceptance BLOCKED. |
| GAP-192-EVENT-BUS | Three frozen Capacity event schemas and correlation fields. | Exact publisher/outbox adapter and delivery acknowledgment in this service; no shared registration authority. | Platform/Event Bus + one integration owner; C192-07. Event delivery acceptance BLOCKED. |
| GAP-192-SHARED-COMPOSITION | Disjoint `CapacityPlans/**` feature root is proposed. | Program.cs, project/DI, permission seed and gateway are shared with 0190/other modules. | Single integration owner with hash-bound patch; C192-07. No module-lane writes. |
| GAP-192-SEQUENCE | Bounded 0183–0185 acceptances exist; frozen contracts/mocks exist. | Exact Returns/Claims CT disposition for downstream sequencing and owner pack promotion. | CT/owner; C192-08. Runtime GO BLOCKED. |

`DEMAND` `DemandPlanPublishedEvent` is not an exact-version oracle: its published shape has `planId/itemId/period` and no version/checksum. A fixture that supplies those values is test data, not a new producer contract. No new 0192-specific DEMAND contract is proposed.
