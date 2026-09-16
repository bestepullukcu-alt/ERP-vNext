---
id: MOD-0186
name: Reverse Logistics
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A spec-only draft. MOD-0183 dependency and frozen contract do not authorize runtime code."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0186 — Reverse Logistics

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0186 --name "Reverse Logistics"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** this is a `draft` backend/contract spec; runtime implementation is not authorized.

## 1. Module Summary

MOD-0186 owns Return/RMA identity and lifecycle for items returned from an existing MOD-0183 shipment. It references
frozen INVENTORY transactions but never stores or adjusts a shadow stock balance.

## 2. Ownership and Boundaries

**Owns:** Return/RMA number, reason, shipment-line quantities, evidence references, disposition and reverse lifecycle.

**Consumes:** MOD-0183 shipment/POD; frozen INVENTORY movement references; **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)**.

**Does not own:** shipment source, warehouse receiving trigger, inventory movement/balance, Supplier, carrier or claim.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `ReturnOrder` / `RMA` | Reverse transaction identity and status |
| `ReturnLine` | Original shipment-line reference and return quantity |
| Commands | Create return and transition lifecycle |
| Queries | List/get returns by shipment/status |
| Events | Requested/authorized/rejected/in-transit/received/dispositioned/closed/cancelled |

## 4. Entity Fields

The aggregate has server UUID, server-resolved TenantId/LegalEntityId, unique `RmaNumber`, required `ShipmentId`,
reason, non-empty return lines, evidence references, status/version and audit fields. Received/dispositioned states may
carry an opaque `InventoryTransactionReferenceId`; it is a reconciliation reference, never a local balance.

## 5. Repo Scope

If separately approved, only `services/Diten.SupplyChainService/**/Features/Returns/**`, matching tests, generated
API reference and this pack are writable. `docs/analysis/contracts/shipment-bundle.openapi.yaml` remains the
read-only frozen implementation authority.

## 6. Protected Paths

- `.antigravity/**`, gateway, shared registrations, archive/frontend shells.
- MOD-0183 Shipment/POD runtime paths and all other SupplyChain features.
- INVENTORY persistence/contracts and every other domain service.
- Central Warehouse and Supplier contracts; never define their schema locally.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0183 Shipment | Required before runtime | Return references an existing eligible shipment/line |
| INVENTORY v1 | Frozen consumed | Read/reference and movement reconciliation only |
| `SHIPMENT-BUNDLE` v1 | Frozen | Reverse API/lifecycle/mock authority |
| Warehouse trigger | Central GAP | **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)** |

## 8. Runtime Constraints

Prospective mutations are tenant/LE scoped, idempotent and correlation-preserving. Receipt does not itself mint or
modify INVENTORY movement truth. Invalid/terminal lifecycle transitions produce no event or partial write. Draft
status forbids runtime implementation.

## 9. Layout & Shell Contract

`shell: none`; backend/contract only.

## 10. Backend File Convention

Prospective root is `Features/Returns/` with standard Commands, Queries, separated handler folders, Validators and
`ReturnModels.cs`; outbox and idempotency persistence stay tenant-first.

## 11. Frontend File Contract

Not applicable; no UI/form shape is invented.

## 12. Validation Rules

- Shipment and shipment line must resolve through MOD-0183 and remain in the same tenant/LE.
- Return quantity is positive and cannot exceed shipped quantity net of existing active returns.
- Lifecycle follows Requested→Authorized|Rejected; Authorized→InTransit|Cancelled; InTransit→Received;
  Received→Dispositioned; Dispositioned→Closed.
- INVENTORY reference is opaque and required only when the frozen lifecycle/schema requires it.

## 13. Failure Path to Verify

Unknown shipment/line, excess quantity, duplicate active return, invalid transition and invalid inventory reference
return deterministic errors with unchanged state; cross-tenant/cross-LE resources return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.returns.read`, `.create`, `.authorize`, `.receive`, `.disposition`.

## 15. Gateway / API Routing Decision

Use frozen `SHIPMENT-BUNDLE` `/returns**`; route publication is a separate integration-agent task.

## 16. Acceptance Criteria

- [ ] Runtime remains absent while `draft`.
- [ ] Future code consumes MOD-0183/INVENTORY strictly by frozen contract/mock.
- [ ] Return quantity invariants and lifecycle are atomic and deterministic.
- [ ] No stock collection, projection or movement SoR exists in MOD-0186.
- [ ] Correlation ID remains unchanged through reverse lifecycle events.

## 17. Test Expectations

DCP-002, OpenAPI/mock, shipment dependency, quantity invariant, lifecycle, idempotency, tenant isolation, RBAC,
outbox and explicit no-shadow-balance architecture tests.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] MOD-0183 and INVENTORY consumption boundaries are explicit.
- [x] Frozen v1 contract defines reverse lifecycle and mock examples.
- [ ] MOD-0183 dependency has executable verified evidence.
- [ ] Owner approval changed status from `draft`.

## 19. Implementation Notes

**ASSUMPTION:** v1 supports customer return against shipment lines; supplier-return and financial credit settlement
are outside scope. Warehouse receipt and INVENTORY posting remain external consumed seams.

## 20. Follow-up Items

- Verify MOD-0183 before runtime dispatch.
- **GAP: merkez CT WAREHOUSE-SHIPMENT-TRIGGER üretmeli.** Reverse receiving adapter must wait for its central schema.
- Separate integration evidence must reconcile received return with frozen INVENTORY movement.
