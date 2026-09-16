---
id: MOD-0184
name: Carrier Management
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Phase A spec-only draft. This pack does not authorize runtime code, service scaffold, shared-seam edits, commit, or dispatch."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0184 — Carrier Management

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0184 --name "Carrier Management"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and the module ID registry.
>
> **Execution gate:** `status: draft`; this document defines a prospective backend/contract initial slice only.
> Runtime implementation is forbidden until a separate owner approval changes the pack to `approved` or `ready-for-dev`.

## 1. Module Summary

MOD-0184 owns the tenant/legal-entity scoped carrier master used by MOD-0185 load planning and MOD-0183 shipment
references. Phase A freezes its external API in `SHIPMENT-BUNDLE` v1; it does not create runtime code or UI.

## 2. Ownership and Boundaries

**Owns:** carrier identity/code, display name, supported transport modes and Active/Suspended/Retired lifecycle.

**Consumes:** Data Contract Registry identifiers and `SHIPMENT-BUNDLE` v1. MVP-6 also records
**CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)**, but MOD-0184 neither defines nor freezes it.

**Does not own:** shipments/POD, routes/loads, warehouse tasks/triggers, inventory, supplier master, claims or returns.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `Carrier` | Carrier code, name, modes and lifecycle |
| Commands | Create carrier; change carrier status |
| Queries | Filter/list carriers; resolve carrier reference |
| API | `SHIPMENT-BUNDLE` v1 `/carriers**` |
| Permissions | `supplychain.carriers.read`, `.create`, `.status.change` |

## 4. Entity Fields

`Carrier` has server-minted UUID `Id`, server-resolved `TenantId`/`LegalEntityId`, tenant+LE unique `CarrierCode`,
required `DisplayName`, one or more `SupportedModes`, optional `ExternalReference`, `Status`, version, soft-delete and
audit fields. Tenant/legal-entity identifiers are never accepted from command payloads.

## 5. Repo Scope

If later approved, the initial slice may write only MOD-0184 feature paths under
`services/Diten.SupplyChainService/**/Features/Carriers/**`, matching tests, generated API reference and this pack.
`docs/analysis/contracts/shipment-bundle.openapi.yaml` is frozen authority and read-only during implementation.

## 6. Protected Paths

- `.antigravity/**`, gateway `ocelot.json`, archive UI and shared shell/registration files.
- Every other domain service and every non-MOD-0184 SupplyChain feature.
- All frozen contracts, including `SHIPMENT-BUNDLE` v1, after Phase A.
- Central Warehouse/Supplier contracts; no local schema or mock contract may be invented.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SHIPMENT-BUNDLE` v1 | Frozen | Carrier API authority and mock source |
| Data Contract Registry | Backbone | Opaque reference validation only |
| MOD-0183 | Optional consumer | Carrier must not mutate shipment state |
| Warehouse trigger | Central GAP | **CONSUMES: WAREHOUSE-SHIPMENT-TRIGGER (merkez üretecek)** |

## 8. Runtime Constraints

Prospective runtime is backend-only in `Diten.SupplyChainService`, Mongo tenant-first, JWT/RBAC, CQRS and repo
pipeline conventions. Mutations require UUID `X-Correlation-Id` and `Idempotency-Key`. Retired is terminal. No
implementation or service scaffold is authorized by this draft.

## 9. Layout & Shell Contract

`shell: none`; no frontend, Razor, DataTable, menu or localization surface is in this slice.

## 10. Backend File Convention

Prospective feature root is `Features/Carriers/` with separate Commands, Queries, Handlers/CommandHandlers,
Handlers/QueryHandlers, Validators and `CarrierModels.cs`, following the repository module-pack standard.

## 11. Frontend File Contract

Not applicable. A UI requires a separately approved pack revision with tenant shell, exact fields and golden reference.

## 12. Validation Rules

- `CarrierCode` and `DisplayName` are trimmed/non-empty; code is tenant+LE unique.
- `SupportedModes` is non-empty and limited to frozen contract enum values.
- Only Active↔Suspended and Active/Suspended→Retired transitions are accepted; Retired cannot reopen.
- Mutation headers are required and TenantId/LegalEntityId body fields are rejected.

## 13. Failure Path to Verify

Duplicate code returns deterministic conflict; invalid status returns `INVALID_CARRIER_TRANSITION`; unknown or
cross-scope carrier returns 404; reused idempotency key with different payload returns `IDEMPOTENCY_KEY_REUSED`.

## 14. Authorization Convention

Server permissions are `supplychain.carriers.read`, `.create`, `.status.change`. Shared permission registration is
integrator-owned and outside this draft.

## 15. Gateway / API Routing Decision

Desired family is the frozen `SHIPMENT-BUNDLE` carrier surface. Only `integration-agent` may modify gateway routes.

## 16. Acceptance Criteria

- [ ] Runtime remains absent while status is `draft`.
- [ ] Future implementation matches all Carrier schemas, examples, headers and deterministic errors in v1.
- [ ] Carrier status and idempotency tests prove no duplicate write/event.
- [ ] Cross-tenant/cross-LE access returns 404.
- [ ] MOD-0185 consumes carrier by contract/reference without internal database/type sharing.

## 17. Test Expectations

DCP-002 verification, frozen OpenAPI syntax/reference validation, future contract/mock tests, lifecycle matrix tests,
tenant isolation, RBAC, idempotency and Mongo DB-010 architecture guards.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] Owned boundary and frozen `SHIPMENT-BUNDLE` v1 are identified.
- [x] Backend-only initial slice and protected paths are explicit.
- [ ] Owner approval to change `draft` to `ready-for-dev` is absent.
- [ ] Runtime dispatch is authorized.

## 19. Implementation Notes

**ASSUMPTION:** carrier contracts and supported modes are managed as reference fields; pricing, tendering and
settlement remain outside MOD-0184. This default narrows scope and does not create a Supplier substitute.

## 20. Follow-up Items

- Approve this pack before MOD-0184 runtime work.
- MOD-0185 remains dependency-gated on a verified MOD-0184 contract/runtime seam.
- **GAP: merkez CT WAREHOUSE-SHIPMENT-TRIGGER üretmeli.** This pack must not define it.
- Gateway/permission registration requires a separate integration WP.
