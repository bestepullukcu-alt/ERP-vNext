---
id: MOD-0187
name: Claims Management
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A spec-only draft. MOD-0183 dependency does not authorize runtime code or shared integration work."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0187 — Claims Management

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0187 --name "Claims Management"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** `status: draft`; no runtime implementation, scaffold, commit or dispatch is authorized.

## 1. Module Summary

MOD-0187 owns logistics claims raised against an existing MOD-0183 shipment, with evidence references, claimed and
approved decimal amounts and an auditable resolution lifecycle. It does not own finance settlement or shipment facts.

## 2. Ownership and Boundaries

**Owns:** claim identity/number, shipment/carrier references, reason/evidence, claimed/approved amount and lifecycle.

**Consumes:** MOD-0183 shipment/POD; optional MOD-0184 carrier reference; **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)**.

**Does not own:** shipment, POD, carrier, warehouse trigger, INVENTORY, Accounts Payable/Receivable or payment SoR.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `Claim` | Logistics claim identity, amount, evidence and status |
| Commands | Create and transition claim |
| Queries | List/get claims by shipment/status |
| Events | Opened/investigating/approved/rejected/settled/closed/withdrawn |
| Permissions | Claims read/create/investigate/decide/settle |

## 4. Entity Fields

`Claim` has server UUID, server-resolved TenantId/LegalEntityId, unique `ClaimNumber`, required `ShipmentId`, optional
`CarrierId`, reason, positive decimal-string `ClaimedAmount`, ISO-4217 currency, evidence refs, nullable approved amount,
resolution code, status/version and audit/soft-delete fields.

## 5. Repo Scope

If later approved, only `services/Diten.SupplyChainService/**/Features/Claims/**`, matching tests, generated API
reference and this pack may change. `docs/analysis/contracts/shipment-bundle.openapi.yaml` is frozen/read-only
implementation authority.

## 6. Protected Paths

- `.antigravity/**`, gateway/shared registrations, archive/frontend shell files.
- MOD-0183/MOD-0184 persistence/features and every other SupplyChain feature.
- Finance/payment services and all foreign-domain data stores.
- Frozen and central Warehouse/Supplier contracts.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| MOD-0183 Shipment/POD | Required before runtime | Claim must reference an existing eligible shipment |
| MOD-0184 Carrier | Optional reference | No Carrier mutation or local copy |
| `SHIPMENT-BUNDLE` v1 | Frozen | Claim schema/lifecycle/event/mock authority |
| Warehouse trigger | Central GAP | **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)** |

## 8. Runtime Constraints

Prospective amounts use decimal strings, never float. Mutations are tenant/LE scoped, idempotent and carry UUID
correlation unchanged into events. Settlement state records a logistics outcome reference and does not execute payment.
This draft grants no runtime authority.

## 9. Layout & Shell Contract

`shell: none`; no UI, form, localization or DataTable.

## 10. Backend File Convention

Prospective root: `Features/Claims/` with standard Commands, Queries, separated handlers, Validators and
`ClaimModels.cs`. Evidence is stored as immutable references, not binary content.

## 11. Frontend File Contract

Not applicable; a future claims UI requires an approved pack revision.

## 12. Validation Rules

- Shipment must resolve in the same tenant/LE; carrier reference, if supplied, must match/relate to the shipment.
- Claimed amount is positive decimal; currency is three uppercase letters.
- Approved amount is required only for Approved and cannot exceed claimed amount.
- Lifecycle follows Open→Investigating|Withdrawn; Investigating→Approved|Rejected; Approved→Settled;
  Rejected→Closed; Settled→Closed.
- Tenant/LE payload fields are rejected; mutation headers are mandatory.

## 13. Failure Path to Verify

Unknown shipment, mismatched carrier, invalid amount/currency, approved amount overflow, invalid transition and
idempotency mismatch return deterministic errors with no partial write/event; cross-scope IDs return 404.

## 14. Authorization Convention

Prospective permissions: `supplychain.claims.read`, `.create`, `.investigate`, `.decide`, `.settle`.

## 15. Gateway / API Routing Decision

Use frozen `SHIPMENT-BUNDLE` `/claims**`; only a separate integration-agent WP may add gateway routes.

## 16. Acceptance Criteria

- [ ] No runtime implementation exists while status is `draft`.
- [ ] Future payloads, responses, errors and events match frozen v1.
- [ ] Shipment/POD is consumed through MOD-0183 contract/reference only.
- [ ] Amount precision and lifecycle transitions are deterministic and atomic.
- [ ] Settled status does not create a payment/AP/AR record.
- [ ] Correlation ID remains unchanged through all claim events.

## 17. Test Expectations

DCP-002, OpenAPI/mock, MOD-0183 dependency, decimal/currency validation, lifecycle, idempotency, tenant isolation,
RBAC, evidence reference, atomic outbox and no-finance-SoR architecture tests.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] MOD-0183 dependency and frozen contract authority are explicit.
- [x] Backend-only slice and ownership boundaries are bounded.
- [ ] MOD-0183 executable verification is available.
- [ ] Owner approval changed status from `draft`.

## 19. Implementation Notes

**ASSUMPTION:** v1 claim is a logistics operational record; settlement is status plus an external reference. Financial
posting, insurer/carrier EDI and recovery accounting are follow-up integrations.

## 20. Follow-up Items

- Verify MOD-0183 contract/runtime before MOD-0187 dispatch.
- **GAP: merkez CT WAREHOUSE-SHIPMENT-TRIGGER üretmeli.** No local central contract is allowed.
- Separate integration WP links claim outcome to finance or supplier performance without duplicating their SoR.
