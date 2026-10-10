# MVP6 Supplier seam ownership disposition

## Verdict

MOD-0147 and MOD-0148 have valid Blueprint identities, but their durable domain/service ownership is **OPEN**. The MVP-6 brief assigns both to the delivery lane; it does not amend the Supply Chain Execution domain boundary or DCP-009 membership.

Both packs therefore remain `draft`. This record does not promote either pack or authorize runtime work.

## Exact authority comparison

| Source | Exact statement | Effect |
|---|---|---|
| `WP-MVP6-logistics.md` | Lists `SupplierPerformance(0147)` and `SupplierPortal(0148)` under MVP-6 `OWNED`; lists `SUPPLIER` as a consumed MVP-2 seam | Establishes delivery-lane intent and the consumer/producer split |
| MOD-0147 and MOD-0148 draft packs | Set `domain: supply-chain-execution`, `service: Diten.SupplyChainService`, but explicitly label this as an assumption pending central reconciliation | Proposed placement only; both ready-for-dev gates remain open |
| Supply Chain Execution `domain-config.md` | Reserves Inventory/Warehouse/Planning/Manufacturing and Logistics MOD-0183…0187; it does not list MOD-0147/0148 | Does not grant durable domain ownership for either Supplier module |
| DCP-009 | `canonical_modules` and member boundary omit MOD-0147/0148; OD-4 front-loads the consumed MOD-0140 Supplier contract | Does not transfer 0147/0148 ownership into DCP-009 |
| `supplier.openapi.yaml` | `x-owner: MOD-0140`, `x-front-loaded-for: [MOD-0147, MOD-0148]` | MOD-0140 remains the Supplier identity contract owner |
| `supplier-performance.openapi.yaml` | Joint `x-owner: [MOD-0147, MOD-0148]` | Gives the two modules their existing shared performance/portal wire surface; it does not settle domain placement or consumed seams |

## DCP-002 result

Fresh fail-closed checks passed:

```text
OK  MOD-0147: proven against Blueprint/registry.
OK  MOD-0148: proven against Blueprint/registry.
```

The exact Blueprint names are `Supplier Performance & Risk` and `Supplier Portal`. The current registry has no explicit MOD-0147 or MOD-0148 row; its role in these checks is collision detection. Identity is therefore PASS, while owner-domain attribution remains unresolved.

## Single accountable owner

Use one accountable decision coordinator for the common seam: **Central Control Tower Supplier Seam Owner**.

This role coordinates and records one decision set. It does not take ownership of Supplier master data or authentication:

- MOD-0140 Supplier contract owner must concur on Supplier identity/status/eligibility semantics.
- Platform authentication/security owner must concur on authenticated actor→supplier binding semantics.
- MOD-0147 and MOD-0148 owners give separate design-consumer acceptance after the common decision is exact.

No module may fill these gaps locally. A shared fixture is test evidence only and cannot become the binding authority.

## Disposition

- Supplier master and base identity remain solely MOD-0140-owned.
- Actor binding remains a platform authentication/security seam.
- MOD-0147 may own only evaluations, scorecards and module-specific SupplierRisk lifecycle after the common gate.
- MOD-0148 may own only portal access projection and supplier-scoped submission lifecycle after the common gate.
- Any decision to place both modules in Supply Chain Execution must be recorded in domain/DCP/registry authorities by their proper owner; this plan proposes the decision but does not make those changes.
