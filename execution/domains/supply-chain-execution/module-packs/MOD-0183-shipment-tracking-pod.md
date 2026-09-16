---
id: MOD-0183
name: Shipment Tracking & POD
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: ready-for-dev
status_note: "Phase A spec approved by owner on 2026-09-15; first executable slice is backend/contract only. WAREHOUSE-OUTBOUND v1 is present; adapter compatibility and integrated acceptance remain separately gated."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 0
---

# MOD-0183 — Shipment Tracking & POD

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0183 --name "Shipment Tracking & POD"`
> returned `OK` on 2026-09-15. The canonical source is Blueprint 8.1 and the module ID registry.
>
> **Phase A owner decision:** MVP-6 is the first contract-first lane. Approval of this pack authorizes Phase B to
> scaffold `Diten.SupplyChainService` on port 5061 and implement the bounded MOD-0183 backend slice. It does not
> authorize runtime work for MOD-0184/0185/0186/0187/0190/0192/0147/0148.
>
> **ASSUMPTION:** the first slice is backend-only (`shell: none`). UI shape and form-field count are intentionally
> not invented; any tenant UI requires an approved follow-up or pack revision with an explicit Slim/Compact decision.

## 1. Module Summary

MOD-0183 is the system of record for a shipment and its proof of delivery. It owns shipment identity, shipment
lifecycle, delivery confirmation, exceptions, immutable lifecycle history, idempotent commands, audit evidence and
the correlation chain emitted through the Event Bus. It is the first writer in the MVP-6 logistics sequence.

The first Phase B slice provides a contract-faithful backend vertical slice and the initial
`Diten.SupplyChainService` scaffold. It is developed against published mocks and does not wait for another service
implementation. Central consumer contracts are present. Unresolved compatibility gates affect their adapters and G5 integration evidence; see the readiness record linked below.

## 2. Ownership and Boundaries

**Owns:**

- Shipment aggregate and server-minted shipment number.
- Shipment line references; product and stock facts remain external references.
- Shipment lifecycle and append-only lifecycle entries.
- Proof of delivery, including delivery time, receiver, evidence references and exception note.
- Shipment lifecycle event payloads in `SHIPMENT-BUNDLE`.
- Correlation and causation propagation for its own HTTP commands and events.

**Consumes:**

- `INVENTORY-BUNDLE` v1 frozen read surface for availability/balance/transaction reconciliation.
- Event Bus transport/envelope from Diten Building Blocks.
- `WAREHOUSE-OUTBOUND` v1, centrally owned by MOD-0178: read-only outbound list/detail and `OutboundShipmentReadyEvent`.
- `SUPPLIER` v1 was inspected: MOD-0140 owns it; it serves MOD-0147/0148 and is not a MOD-0183 runtime dependency.
- Product and Location identifiers as opaque references through their frozen contracts when validation is needed.

**Does not own:**

- Warehouse task, pick, pack or shipment-ready trigger semantics.
- Inventory balance, reservation or movement truth; no shadow stock.
- Carrier master, routing/load, reverse logistics, claims or supplier master.
- Gateway global route files, permission registry or shared platform registrations.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `Shipment` | Stable shipment identity, source references, route endpoints and current lifecycle state |
| `ShipmentLine` | Opaque item/SKU, quantity and warehouse-dispatch line references; no stock balance |
| `ShipmentLifecycleEntry` | Append-only transition history with correlation/causation and actor evidence |
| `ProofOfDelivery` | Delivery confirmation bound one-to-one to a delivered shipment |
| `ShipmentException` | Shipment-owned exception record; claims remain MOD-0187 |
| Commands | Create, assign carrier/load references, dispatch, record exception, capture POD, cancel |
| Queries | List shipments, get shipment, get lifecycle, get POD, reconcile source references |
| API | `/api/shipment-bundle/shipments/**` from `SHIPMENT-BUNDLE` v1 |
| Events | Shipment created/dispatched/in-transit/exception/delivered/cancelled and POD captured v1 |
| Permissions | `supplychain.shipments.read`, `.create`, `.dispatch`, `.pod.capture`, `.cancel`, `.reconcile` |

## 4. Entity Fields

### Shipment

| Field | Rule |
|---|---|
| `Id` | Server-minted stable UUID |
| `TenantId` | Required; server context only, never request body |
| `LegalEntityId` | Required; server context/header policy only, never request body |
| `ShipmentNumber` | Server-minted, tenant/legal-entity unique |
| `WarehouseReferenceId` | Required opaque Location warehouse reference; map `OutboundShipment.warehouseId`, never `outboundId` |
| `SourceModule` / `SourceType` / `SourceDocumentId` | Required frozen create fields; Warehouse mapping is `MOD-0178` / `WAREHOUSE_OUTBOUND` / `outboundId` |
| `SourceSystem` / `ExternalRef` | Optional DEC-INV-19 reconciliation hooks |
| `CarrierId` | Optional MOD-0184 reference |
| `LoadPlanId` | Optional internal MOD-0185 reference; wire projection is `loadId`; no new assignment API |
| `ShipToReference` | Required opaque delivery destination reference |
| `Lines` | At least one `ShipmentLine`; quantity uses the frozen v1 decimal-string schema |
| `PlannedShipAt` / `PlannedDeliverAt` | UTC date-time; planned delivery cannot precede planned shipment |
| `Status` | `Draft`, `Planned`, `Dispatched`, `InTransit`, `Delivered`, `Exception`, `Closed`, `Cancelled` |
| `IsDeleted` / audit fields | Repo-standard soft delete and audit fields |

### ProofOfDelivery

| Field | Rule |
|---|---|
| `Id` | Server-minted stable UUID |
| `ShipmentId` | Required; one active POD per shipment |
| `ReceivedAt` | Required UTC date-time |
| `RecipientName` | Required, trimmed |
| `EvidenceReferenceIds` | At least one immutable document/evidence reference; binary content not stored here |
| `Note` | Optional, max 1000 |
| `CorrelationId` | Required UUID propagated from command through emitted events |

Indexes are tenant-first: `(TenantId, LegalEntityId, ShipmentNumber)` unique; `(TenantId, LegalEntityId, Status,
PlannedDeliverAt)`; `(TenantId, LegalEntityId, WarehouseReferenceId)`; lifecycle entries by
`(TenantId, ShipmentId, Sequence)` unique; POD by `(TenantId, ShipmentId)` unique among non-deleted records.

## 5. Repo Scope

Phase B may write only:

- `services/Diten.SupplyChainService/**` for the initial five-layer service scaffold and MOD-0183 slice.
- `services/Diten.SupplyChainService/src/**/Features/Shipments/**`.
- `services/Diten.SupplyChainService/tests/**/Shipments/**` and service-level architecture/contract tests.
- `docs/reference/architecture/api/**` for generated MOD-0183 API reference.
- `docs/records/audits/**` for verification evidence.
- This module pack and applicable backlog/seam records required by closure.

The frozen file `docs/analysis/contracts/shipment-bundle.openapi.yaml` is implementation authority. Phase B must not
edit it; any additive contract revision is a separately versioned, single-writer Phase A change.

## 6. Protected Paths

- `.antigravity/**`.
- `gateway/Diten.ApiGateway/**/ocelot.json`; only `integration-agent` may change routes in a separate WP.
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and shared shell files.
- Every other domain service and every non-MOD-0183 feature in `Diten.SupplyChainService`.
- Existing eight frozen Wave-0 contracts and central Warehouse/Supplier contracts.
- MOD-0184/0185/0186/0187/0190/0192/0147/0148 runtime paths.

## 7. Dependencies

| Dependency | State for Phase B | Rule |
|---|---|---|
| `SHIPMENT-BUNDLE` v1 | SATISFIED when Phase A validation passes | Owned frozen contract |
| `INVENTORY-BUNDLE` v1 | SATISFIED | Consume via generated/manual contract client or mock; reads only in this slice |
| Event Bus | SATISFIED as platform foundation | Use outbox/event envelope; preserve correlation UUID |
| Location/Product contracts | SATISFIED | Opaque IDs; validation through frozen mock where invoked |
| `WAREHOUSE-OUTBOUND` v1 | PRESENT / FROZEN; compatibility PARTIAL | GET list/detail + ready event; mapping and GAP-0183-01/02/03 in readiness record |
| `SUPPLIER` v1 | PRESENT / FROZEN; N/A for this slice | Supplier identity belongs to MOD-0140; no Shipment dependency |
| MOD-0184 Carrier | OPEN | Optional reference only; no Carrier aggregate here |
| MOD-0185 Load | OPEN | Optional reference only; no LoadPlan aggregate here |

The missing-contract claim is resolved. The inbound adapter remains disabled for untrusted tenant/legal-entity
context or non-UUID/mismatched correlation; no local contract substitute, scope guessing or silent correlation
replacement is allowed. The approved HTTP core is independent of this blocked automatic intake.

## 8. Runtime Constraints

- Service: `Diten.SupplyChainService`, port 5061, five layers + CQRS/MediatR.
- MongoDB tenant/legal-entity scoped collections; cross-tenant and cross-LE lookup returns 404.
- `TenantId` and `LegalEntityId` never come from mutation payloads.
- State transitions are append-only history; a delivered/cancelled shipment is not edited back to an earlier state.
- Commands require `Idempotency-Key` and UUID `X-Correlation-Id` exactly as frozen contract v1 specifies.
- Duplicate idempotency key returns the prior successful result without a second transition/event.
- State transition + outbox event are atomic or use an explicit compensating/partial marker; silent partial success is forbidden.
- Inventory is consumed by API/mock only; no database/internal-type sharing and no second balance.
- **Explicit pack exception to the default response-envelope rule:** the externally observable
  `/api/shipment-bundle` wire response is the unwrapped OpenAPI v1 schema, matching the eight Wave-0 mock contracts.
  Application handlers may use `Response<T>` internally, but the contract adapter unwraps `Data`; it must not add
  `data/isSuccessful/statusCode/errors` fields to this frozen wire surface. This exception is required for exact
  contract/mock compatibility and is limited to this contract route.

## 9. Layout & Shell Contract

`shell: none`. Phase B creates no Razor UI, menu, DataTable, localization surface or frontend route.

## 10. Backend File Convention

- Five projects/layers: Api, Application, Domain, Persistence, Infrastructure.
- Each command/query/handler/validator is in its own file.
- Feature root: `Features/Shipments/Commands`, `Queries`, `Handlers/CommandHandlers`,
  `Handlers/QueryHandlers`, `Validators`, and `ShipmentModels.cs`.
- Four pipeline behaviors and `CustomBaseController` are installed before endpoint implementation.
- Specific repositories are allowed for append-only lifecycle/idempotency/outbox atomicity; no generic repository shortcut
  may weaken tenant, legal-entity or current-state transition predicates.

## 11. Frontend File Contract

Not applicable in the first slice. A future UI requires a pack revision/follow-up with tenant shell, exact form fields,
DataTable decision and Slim/Compact reference.

## 12. Validation Rules

| Field/Header | Required | Rule | Persistence/pre-check |
|---|---|---|---|
| `Idempotency-Key` | State changes | Trimmed, max 128 | `(TenantId, LegalEntityId, operation, IdempotencyKey)` unique replay record |
| `X-Correlation-Id` | Every operation | UUID, non-empty | Persist on lifecycle/outbox records for mutations |
| `WarehouseReferenceId` | Create | Non-empty opaque reference | Warehouse mapping uses `warehouseId`; source identity separately uses `outboundId` |
| `ShipToReference` | Create | Non-empty opaque reference | — |
| `Lines` | Create | At least one; fields and decimal-string quantity match v1 schema | Product refs validate when mock available |
| `PlannedShipAt` | Create | UTC | Must be before optional planned delivery |
| `TargetStatus` / `OccurredAt` | Transitions | Frozen transition matrix + UTC timestamp | Current state is checked atomically |
| `ReceivedAt` | POD | UTC, not before dispatch | Shipment must be Dispatched or InTransit |
| `RecipientName` | POD | Trimmed, non-empty | — |
| `EvidenceReferenceIds` | POD | At least one | Reference only; no binary upload |

## 13. Failure Path to Verify

- Duplicate idempotency key replays the first result and creates no second lifecycle/outbox record.
- Invalid lifecycle transition returns `INVALID_SHIPMENT_TRANSITION` with correlation ID and no write.
- A concurrent or repeated transition is resolved by atomic current-state validation plus idempotency; invalid state returns `INVALID_SHIPMENT_TRANSITION` without a write.
- Unknown/cross-tenant/cross-LE shipment returns 404 without existence leakage.
- Duplicate POD returns 409 `POD_ALREADY_CAPTURED`.
- Missing/invalid UUID correlation returns 400 and publishes no event.
- Unavailable Warehouse detail, incompatible correlation or untrusted tenant/LE context keeps automatic intake blocked and emits no fabricated shipment.
- Inventory reconciliation failure is explicit and never mutates inventory or shipment state silently.

## 14. Authorization Convention

Actor type: tenant user/service actor. Server-side permissions:

- Read: `supplychain.shipments.read`.
- Create: `supplychain.shipments.create`.
- Dispatch/transitions: `supplychain.shipments.dispatch`.
- POD: `supplychain.shipments.pod.capture`.
- Cancel: `supplychain.shipments.cancel`.
- Reconcile: `supplychain.shipments.reconcile`.

Permission definition/seed is a shared seam and must be handled by its single owner/integration WP. Phase B may use
the approved names but must not edit shared permission registries outside an explicitly authorized integration task.

## 15. Gateway / API Routing Decision

The canonical contract family is `/api/shipment-bundle/**` to service port 5061, exactly matching the OpenAPI
`servers.url` plus its paths. The Phase B developer does not edit
`ocelot.json`. A separate `integration-agent` WP adds the route after controller endpoints exist and validates
`Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id` and `Idempotency-Key` propagation.

## 16. Acceptance Criteria

- [ ] `Diten.SupplyChainService` builds with the mandated five layers, four pipeline behaviors and base controller.
- [ ] Create shipment persists exactly one tenant/legal-entity scoped shipment and one lifecycle/outbox entry.
- [ ] Repeating the same create/transition idempotency key returns the same result without a second write/event.
- [ ] Allowed transitions match `SHIPMENT-BUNDLE` v1 and invalid transitions fail without partial state.
- [ ] Capturing POD transitions the shipment to Delivered atomically and emits the specified events once.
- [ ] A command's UUID correlation ID is preserved across shipment lifecycle and POD events.
- [ ] Cross-tenant/cross-LE reads and mutations return 404; missing permissions return 403.
- [ ] Inventory data is read only through `INVENTORY-BUNDLE` v1 mock/client; no stock collection or balance exists locally.
- [ ] Warehouse intake uses the recorded v1 map; incompatible correlation, missing trusted scope and source drift are explicit blocked/reconciliation states, never fabricated shipments.
- [ ] Contract examples pass a live mock smoke test and implementation payloads match the frozen OpenAPI.
- [ ] G5 evidence plan covers shipment→carrier/load→POD→return/claim, source reconciliation and regression; module
      completion remains open until downstream modules and central contracts pass E5 integration.

## 17. Test Expectations

- DCP-002 identity verifier PASS.
- OpenAPI syntax/reference validation and Prism mock startup/smoke for `SHIPMENT-BUNDLE`.
- Domain transition matrix unit tests.
- Handler tests for tenant/legal-entity isolation, permission denial, idempotency, concurrent state transition and atomic outbox.
- API contract tests for every success and declared error response.
- Mongo integration tests use isolated test database naming per DB-010.
- Runtime E4 for MOD-0183 state changes; G5 requires later E5 cross-module integration.
- Architecture tests include the new service and verify no competing stock SoR or direct foreign database access.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] DCP-009 and Supply Chain domain boundary were read.
- [x] Owned contract is versioned, validated and frozen as `SHIPMENT-BUNDLE` v1.
- [x] Backend-only first slice selected; no UI/form pattern invented.
- [x] Service/port, entity base, owned objects and repository paths are explicit.
- [x] Commands, queries, event/correlation behavior and error codes are frozen in the contract.
- [x] Tenant/legal-entity, RBAC, idempotency, concurrency, audit and outbox gates are explicit.
- [x] At least four failure paths and measurable acceptance criteria are defined.
- [x] Gateway ownership remains with `integration-agent`.
- [x] Central `WAREHOUSE-OUTBOUND` is present; field mapping, adapter compatibility gaps and fail-closed boundary are recorded.
- [ ] All Warehouse automatic-intake compatibility gates pass (GAP-0183-01/02/03 remain open).
- [x] User explicitly authorized `ready-for-dev` for Phase A on 2026-09-15.

## 19. Implementation Notes

- **ASSUMPTION:** the Phase B runtime naming root is `Shipments`; this is derived from the canonical module name and
  contract base path and may not broaden into Carrier/Load/Reverse/Claims features.
- **ASSUMPTION:** the target date `2026-09-30` is a planning default required by the module-pack schema; it is not a
  commitment to promote the eight draft packs or complete all MVP-6 integration by that date.
- **ASSUMPTION:** UUID `X-Correlation-Id` is mandatory on every frozen HTTP operation and maps losslessly to the Event
  Bus `Guid CorrelationId` for lifecycle events.
- **ASSUMPTION:** warehouse-originated creation is an adapter into the owned create-shipment command, using central
  `WAREHOUSE-OUTBOUND` v1. Provenance snapshots remain internal and cannot extend the frozen create DTO.
- This pack supersedes the earlier DCP-009 note that named MOD-0173 as the only possible initial service-scaffold
  trigger, based on the owner's explicit 2026-09-15 instruction that MOD-0183 is the first contract-first lane.

## 20. Follow-up Items

- Central CT review: resolve correlation, trusted LE event context and strict OpenAPI example compatibility gaps
  recorded in `docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md`; contract publication is no longer missing.
- MOD-0184 pack must freeze Carrier before MOD-0185 Routing/Load becomes executable.
- MOD-0186 and MOD-0187 become executable after Shipment lifecycle v1 is frozen and verified.
- Separate integration WP for gateway route and permission/shared registration seams.
- Separate UI follow-up after actor journeys and form fields are approved.
- G5 E5 integration after all MVP-6 modules and central consumer contracts are available.

## 21. Readiness reconciliation — 2026-09-15

See [field mapping, assumptions, SOP §17 prompt, DoR and Phase 1.5 checklist](../../../../docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md).
The user explicitly approved the reconciled MOD-0183 scope and Phase 1.5 checklist in this session. That approval
is recorded; it is not implementation or acceptance evidence. The subsequent CT request invokes independent
acceptance before advancing to MOD-0184. No runtime implementation is present at this inspection.

Exact initial HTTP surface: `queryShipments`, `createShipment`, `getShipment`, `transitionShipment`,
`captureProofOfDelivery`. Lifecycle/POD/history/source-reconciliation queries without a frozen standalone route
are internal/read-model responsibilities, not authorization to invent endpoints. Carrier/load assignment commands
listed conceptually in §3 are not implemented without an approved frozen assignment surface. `loadId`/`carrierId`
remain nullable read-model references. Shipment event production is in scope; the bundle webhook documents consumer
intake and does not authorize a new public Shipment event-ingest endpoint.

**Status:** core planning/approval recorded; automatic Warehouse intake NOT READY; runtime acceptance NOT READY.
All §16 implementation acceptance boxes remain open. MOD-0184 remains draft and sequence-gated.
