---
id: MOD-0185
name: Routing & Load Planning
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A spec-only draft. Dependency on MOD-0184 is not an authorization for runtime code."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0185 — Routing & Load Planning

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0185 --name "Routing & Load Planning"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** `status: draft`; no runtime code, scaffold or dispatch is authorized.

## 1. Module Summary

MOD-0185 owns route stops and load-plan lifecycle that groups MOD-0183 shipments under a MOD-0184 carrier.
Its first prospective slice is backend/contract only and conforms to `SHIPMENT-BUNDLE` v1.

## 2. Ownership and Boundaries

**Owns:** load identity/number, carrier and shipment references, transport mode, ordered stops, planned departure and
Draft→Planned→Tendered→Accepted→Dispatched→Completed lifecycle.

**Consumes:** MOD-0184 carrier; MOD-0183 shipment references; **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)**.

**Does not own:** Carrier/Shipment aggregates, warehouse pick/pack/trigger, inventory truth, POD, return or claim.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `LoadPlan` | Shipment grouping, carrier, mode and current lifecycle |
| `LoadStop` | Ordered pickup/delivery/return reference |
| Commands | Create load and transition lifecycle |
| Queries | Filter/list and retrieve load plans |
| Events | Load created/planned/tendered/accepted/dispatched/completed/cancelled |

## 4. Entity Fields

`LoadPlan` carries server UUID, server-resolved TenantId/LegalEntityId, unique `LoadNumber`, required `CarrierId`,
one or more `ShipmentIds`, `Mode`, UTC `PlannedDepartAt`, at least two ordered stops, status/version and audit fields.
`LoadStop` contains positive unique sequence, opaque location reference and Pickup/Delivery/Return action.

## 5. Repo Scope

If approved later, only `services/Diten.SupplyChainService/**/Features/Loads/**`, matching tests, generated API
reference and this pack are writable. `docs/analysis/contracts/shipment-bundle.openapi.yaml` remains read-only
frozen authority.

## 6. Protected Paths

- `.antigravity/**`, gateway routes, shared registrations, archive/frontend shells.
- Carrier and Shipment persistence/features and every other domain service.
- Frozen contracts and central Warehouse/Supplier contracts.
- Any inventory balance, reservation or warehouse trigger implementation.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0184 Carrier | Required before runtime | Carrier must exist and be Active; contract/reference only |
| MOD-0183 Shipment | Required reference | Eligible shipment states only; no shipment SoR mutation |
| `SHIPMENT-BUNDLE` v1 | Frozen | Load schema, transitions, event and mock authority |
| Warehouse trigger | Central GAP | **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)** |

## 8. Runtime Constraints

Prospective runtime is tenant/legal-entity scoped, idempotent and event-backed. UUID correlation propagates unchanged
from command to load lifecycle events. Terminal Completed/Cancelled loads cannot transition. This draft grants no
runtime code authority.

## 9. Layout & Shell Contract

`shell: none`; no frontend or DataTable work.

## 10. Backend File Convention

Prospective root: `Features/Loads/` with the standard Commands, Queries, separated handler folders, Validators and
`LoadModels.cs`. Repository and outbox logic must preserve tenant predicates and atomic transitions.

## 11. Frontend File Contract

Not applicable; any planning UI requires a separate approved revision and golden reference decision.

## 12. Validation Rules

- Carrier reference is required and must resolve to Active MOD-0184 carrier.
- Shipment IDs are unique/non-empty and resolve through MOD-0183 without internal DB sharing.
- Stop sequence is unique, contiguous and starts at 1; at least one Pickup and one Delivery.
- Lifecycle accepts only the transition matrix frozen in `SHIPMENT-BUNDLE` v1.
- All mutations require correlation/idempotency headers; tenant/legal entity never comes from payload.

## 13. Failure Path to Verify

Unknown/inactive carrier, duplicate shipment assignment, invalid stop order and invalid lifecycle return deterministic
errors with correlation UUID and no partial writes/events; cross-scope resources return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.loads.read`, `.create`, `.transition`. Shared registration is integrator-owned.

## 15. Gateway / API Routing Decision

Use the `SHIPMENT-BUNDLE` `/loads**` surface. Gateway changes belong only to a later integration-agent WP.

## 16. Acceptance Criteria

- [ ] No runtime code exists while this pack is `draft`.
- [ ] Future implementation consumes verified MOD-0184 and MOD-0183 seams by contract.
- [ ] Load commands/responses/events match frozen v1 examples and schemas.
- [ ] Invalid transitions and duplicate assignment are atomic failures.
- [ ] Correlation UUID is preserved from command through all load events.

## 17. Test Expectations

DCP-002 PASS, OpenAPI/mock validation, route/stop invariants, lifecycle matrix, idempotency, tenant isolation, RBAC,
outbox atomicity and dependency contract tests with MOD-0184/MOD-0183 mocks.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] Frozen contract and MOD-0184/MOD-0183 dependency edges are explicit.
- [x] Backend-only slice and paths are bounded.
- [ ] MOD-0184 executable dependency gate is satisfied.
- [ ] Owner changed this draft to `ready-for-dev`.

## 19. Implementation Notes

**ASSUMPTION:** route optimization is deterministic ordered-stop planning in v1; maps, pricing, tender marketplace and
external optimization engines are follow-ups. This avoids inventing an external carrier/warehouse contract.

## 20. Follow-up Items

- Wait for MOD-0184 approval and executable seam before MOD-0185 runtime dispatch.
- **GAP: merkez CT WAREHOUSE-SHIPMENT-TRIGGER üretmeli.** Do not define or freeze it here.
- Separate integration WP assigns load reference into Shipment and wires gateway/events.
