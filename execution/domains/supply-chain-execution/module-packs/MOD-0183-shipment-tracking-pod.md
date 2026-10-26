---
id: MOD-0183
name: Shipment Tracking & POD
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
status_note: "Phase A spec approved by owner on 2026-09-15; first executable slice is backend/contract only. WAREHOUSE-OUTBOUND v1 is present; adapter compatibility and integrated acceptance remain separately gated. §23 (WP-P1 r3, Inventory stock issue at dispatch) is an unapproved draft revision: its slices DL-1 and DL-2 may not start before its CT-APR-02 review and a CT-APR-01 row."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 13
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
- **The sales stock issue request (W-4, owner decision 2026-10-21; revision §23):** MOD-0183 is the single writer of
  `GOODS_ISSUE_SALES`, posted through the frozen INVENTORY `postMovement` when a shipment becomes `Dispatched`, and keeps the
  resulting movement references. Inventory remains the balance owner (below).

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
| `LegalEntityId` | Required; arrives WITH THE REQUEST (query) and the service validates it against MDM for tenant ownership and Active state, fail-closed (R-2, SHIPMENT-BUNDLE 3.2.0); header policy on the service seam, never request body |
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
- The tenant UI that §9 and §11 bind, under `frontend/Diten.Web/` and only these paths: `Controllers/SupplyChainShipmentsController.cs`
  (§9 Routes), `Views/SupplyChain/Shipments/**` (§11 Screens), `Models/SupplyChain/Shipments/ShipmentViewModels.cs` (§11 form
  fields), `wwwroot/assets/js/SupplyChain/Shipments/**` (§11 open item 4) and
  `Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (§9 Localization surface). Not granted
  here: the shared shell and layout files (§6) and `SharedResource.*.resx`, whose navigation values the integration owner adds (§22).
- `docs/reference/architecture/api/**` for generated MOD-0183 API reference.
- `docs/records/audits/**` for verification evidence.
- This module pack and applicable backlog/seam records required by closure.

The frozen file `docs/analysis/contracts/shipment-bundle.openapi.yaml` is implementation authority. Phase B must not
edit it; any additive contract revision is a separately versioned, single-writer Phase A change.

**§23 DL-1 exception (WP-P1 r3, MOD-0183 review F-6).** The 3.4.0 contract change and its code must land in one change,
because `ContractErrorCodeParityTests` pins the contract's codes to `ContractErrorCodes`. DL-1 therefore also writes, and
only these: `docs/analysis/contracts/shipment-bundle.openapi.yaml` (3.3.0 → 3.4.0, exactly
`docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.4.0.patch` as approved; single writer: the MOD-0183 contract
writer, MVP-6 CT), `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Common/ContractErrorCodes.cs`,
`services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Shipments/ContractErrorCodeParityTests.cs`,
`services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Common/ShipmentBundleContract.cs`, and the return
reader's line whitelist `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs`
with its test under `services/Diten.SupplyChainService/tests/**/Returns/**` (accepting the 3.4.0 line fields only; no MOD-0186
behaviour change). Inventory's `ShipmentBundleHttpReader` does not read lines and needs no change.

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
| `INVENTORY-BUNDLE` v1 | SATISFIED (1.5.0) | Consume via contract client or mock; reads, and — §23 only — `postMovement` (`GOODS_ISSUE_SALES`; `REVERSAL` per §23.4, decided in WP-P1 r3) |
| `PRODUCT-MASTER` 1.1 | DRAFT (`1.1.0-draft.16`) | a later MOD-0183 revision after the contract is frozen (G6); not a slice of this version |
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

### 8.1 Required observability signals

> Added by C-04, 2026-10-03. **Nothing here is new policy.** These signals were already named in
> `docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md` §8 ("Conditional gates and
> defaults", the `Observability:` line), but that record is a plan, and AGENTS.md §1 makes the module
> pack the authority. SOP §18.0 gate row 13 therefore could not be closed: the requirement existed in
> a document that does not bind. Lifting it here makes it contractual. The wording is the readiness
> record's; only the per-signal implementation status is new.

SOP §18.0 row 13 is satisfied for this module when, and only when, all four exist:

| # | signal | required form | status measured 2026-10-03 |
|---|---|---|---|
| O-1 | Correlation trace | one correlation ID spanning web, gateway and service **in the logs** for a single user action | **NOT MET end to end — service half fixed, re-checked 2026-10-03 by Q312.** Q264 measured live against all five services: Web **0** occurrences of an ID it issued itself, Gateway **2** (as `RequestId`), SupplyChain **0**; echoing is not logging. Since then the SupplyChain service writes the ID on handler, hosting and middleware-rejection lines (`CorrelationIdEnricher` plus an explicit output template, `Program.cs:36-37`; Q270 measured live: 8, 6 and 2 lines for its three requests, 0, 0 and 0 with the change reverted), and logs the ID it generated when the inbound one is invalid (Q280, `CorrelationIdEnricher.cs:15`). Web has not been re-measured since Q264, so one ID in all three logs is still unproven. Evidence: `docs/records/audits/2026-10/mvp6-r03-r07-runtime-01/CORRELATION.tsv` |
| O-2 | Operation latency | measured and reported **p95**. ASSUMPTION A9 stands: the existing 500 ms threshold is an initial observation threshold, **not** an acceptance SLA, and no SLA may be invented from it | **partial** — the histogram `shipment.operation.duration` (ms, tagged by request type, `ShipmentTelemetry.cs:20`) is recorded for every MediatR operation (`PerformanceBehavior.cs:14`), so p95 is readable by an out-of-process listener such as `dotnet-counters`; no exporter is registered in the service and no p95 has been measured or reported. The 500 ms warning remains (A9) |
| O-3 | Intake, replay, drift and denial counts | four counters, each incremented on the matching named behaviour in §13 | **present in code — 4 of 4, re-measured 2026-10-03 by Q323.** Defined in `ShipmentTelemetry.cs:24-31`: intake (`IntakeBlocked`, incremented at `WarehouseIntakeCoordinator.cs:78` for the intake-blocked causes of §13), replay (`IdempotentReplays`, `ShipmentRepository.cs:142`), denial (`ScopeDenials`, `ShipmentContextMiddleware.cs:29`) and drift (`SourceDrifts`, `shipment.intake.drift`, built by Q315 against the §13 drift line, incremented at `WarehouseIntakeCoordinator.cs:87` for both `SOURCE_DRIFT` sites `:38` and `:54`; it counts every detection, not every business event, CT ruling 2026-10-03). Drift tests: `ShipmentTelemetryTests.cs:98-170`. No exporter; not measured live. The earlier status, "absent — 0 files contain `Meter(`, `Counter<` or `ActivitySource`", predates Q265 |
| O-4 | Outbox pending, retry and dead-letter counts | three counters over the outbox collection's own states | **present in code, re-measured 2026-10-03 by Q312** — `OutboxPending` (`ShipmentRepository.cs:143`), `OutboxRetries` and `OutboxDeadLetters` (`ShipmentOutboxStore.cs:38,43`), defined in `ShipmentTelemetry.cs:34-39`. No exporter; not measured live |

**How O-2, O-3 and O-4 are read** (Q314, applying CT ruling 3 of 2026-10-03). All eight instruments sit on one
`System.Diagnostics.Metrics` meter, `Diten.SupplyChainService.Shipments`, version `1.0` (`ShipmentTelemetry.cs:15-17`):
`shipment.operation.duration` (histogram, ms), `shipment.intake.blocked`, `shipment.idempotency.replays`,
`shipment.scope.denials`, `shipment.intake.drift`, `shipment.outbox.pending`, `shipment.outbox.retries` and
`shipment.outbox.dead_letters` (`:20-39`). The service registers **no exporter**: `AddOpenTelemetry`, `AddPrometheus`, `AddMeter`, `MeterListener` and
`UseOpenTelemetry` occur 0 times in `src/`. An operator reads the meter out of process, with no code change:
`dotnet-counters monitor --process-id <pid> --counters Diten.SupplyChainService.Shipments`, which shows each counter and
the histogram's percentiles, p95 included. That reading path is what makes these rows falsifiable, and with it the
counters satisfy their O-3 and O-4 rows as module deliverables; O-3 is four of four since Q315. An exporter is a
**deployment** concern, recorded separately as ledger Q316, and is not a gate for this module. No reading has been
taken live yet.

Two constraints carry over verbatim from the readiness record and bind here:

- Event names are exactly the frozen Shipment/POD event names.
- **A retry is not a second business event** and must not be counted or published as one.

Anything not in this table is explicitly **not** required for row 13. A module that adds further
telemetry is not thereby closer to closing the gate, and a reviewer may not demand a signal that is
absent from this table.

### 8.2 Warehouse provenance snapshot — obligations

> Added by Q314, 2026-10-03, applying CT rulings 1 and 2 on Q313. The readiness record says "restrict snapshot
> access" (`docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md:177`), but that record is a plan and does
> not bind (AGENTS.md §1); until now this pack said only that provenance snapshots "remain internal" (§19). The
> obligations below bind. The measurements are Q313's, re-checked by Q314 against the code.

What is stored today:

- The full canonical Warehouse outbound detail, including `shipTo` with the recipient `name` and the address fields
  (`warehouse-outbound.openapi.yaml:80-84,113`), is persisted in three collections:
  `sce_shipment_source_intents` (the `IntentJson` of every intake that reaches preparation, successful ones included —
  `SourceIntakeStore.cs:29`), `sce_shipment_source_links` (every committed intake — `ShipmentRepository.cs:127`) and
  `sce_shipment_source_evidence` (6 of the 11 evidence-writing sites store the full snapshot, not only drift —
  `WarehouseIntakeCoordinator.cs:33,38,40,42,51,54`).
- The Warehouse read schema is open: `additionalProperties` occurs 0 times in
  `docs/analysis/contracts/warehouse-outbound.openapi.yaml`, so any field the producer adds is persisted with the
  snapshot, unclassified.
- Recipient, address, note and evidence references are confidential PII
  (`docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md:176`).

Obligations:

1. **Snapshot access is restricted.** No query, API, export, report or log may read or return a stored Warehouse
   snapshot from any of the three collections without an approved revision of this pack. Today nothing in `src/` reads
   `sce_shipment_source_evidence`; it has a writer (`SourceIntakeStore.cs:44`) and an index (`ShipmentSchema.cs:20`) only.
2. **Intake is not wired to production** until obligation 3 has landed: no host registration of
   `WarehouseIntakeCoordinator`, its Warehouse read client or a poller. This costs nothing today —
   `WarehouseIntakeCoordinator` has no production caller (no reference in `src/` outside its own file).
3. **Target shape**, across all three collections: keep the snapshot hash (drift detection depends on it), drop
   `shipTo`, and persist only whitelisted declared fields. This must land **before any production row exists** in any
   of the three collections.
4. **No retention period may be set.** The readiness record says retention "follows shared evidence policy, no invented
   TTL" (`:181`), but that policy is named and defined nowhere — a search of `docs`, `execution`, `.antigravity` and
   `AGENTS.md` finds it only in the readiness record — and its candidate owners are not live: MOD-0029 Controlled
   Documents is `planned` (`execution/registries/module-id-registry.md:110`) and MOD-0031 Evidence Linking is
   `review / planned` (`:114`). Retention therefore cannot be written today and stays an open owner/Platform decision.

## 9. Layout & Shell Contract

**Shell: tenant.** Reconciled with §22 on 2026-10-03 by Q320, on the owner's decision of that day to reconcile.
The first slice was backend-only, and this section then read: "`shell: none`. Phase B creates no Razor UI, menu,
DataTable, localization surface or frontend route." That slice is over. §22, whose authority is the owner decision
`docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A), declares this
module's three tenant pages, and the tree holds them. Each bound the old sentence set still holds, in this form:

- **Shell.** The three page views render inside the tenant shell: `Layout = "_LayoutTenantShell"` at
  `Index.cshtml:7`, `Create.cshtml:8` and `Details.cshtml:8`. Shared shell files stay protected (§6); this module
  does not edit them.
- **Routes.** Exactly the three §22 RoutePaths, served by `SupplyChainShipmentsController.cs:39-49`. No other frontend
  route is added without a revision of this pack; §22 test M-04 checks the set.
- **Menu.** No hand-written menu or sidebar entry. Navigation comes only from the §22 manifest page `SHIPMENTS` (the one
  navigation-visible page) and its navigation keys, which ship with the UI and never ahead of it (§22 Ship rule, D4 = A).
- **DataTable.** The list uses the shared v2 DataTable contract: `data-dt-standard="v2"` at `_DataTable.cshtml:4`
  (AGENTS.md DataTable rule).
- **Localization surface.** Tenant module, seven languages: the view resource `ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`
  holds 63 keys in each of the seven. Hard-coded user text is not added.
- **Denied access.** Each page renders only the shared `_AccessDenied` partial when the key it needs is missing
  (`Index.cshtml:9`, `Create.cshtml:10`, `Details.cshtml:11`), inside the shell and without redirect; Q264 measured
  this live and found UAS-001 satisfied on all three pages
  (`docs/records/audits/2026-10/mvp6-r03-r07-runtime-01/R03-DENIED.tsv`).

The UI is **not** complete. Reconciling the contract makes its open defects closable; it does not close them (§11,
open items).

## 10. Backend File Convention

- Five projects/layers: Api, Application, Domain, Persistence, Infrastructure.
- Each command/query/handler/validator is in its own file.
- Feature root: `Features/Shipments/Commands`, `Queries`, `Handlers/CommandHandlers`,
  `Handlers/QueryHandlers`, `Validators`, and `ShipmentModels.cs`.
- Four pipeline behaviors and `CustomBaseController` are installed before endpoint implementation.
- Specific repositories are allowed for append-only lifecycle/idempotency/outbox atomicity; no generic repository shortcut
  may weaken tenant, legal-entity or current-state transition predicates.

## 11. Frontend File Contract

Reconciled with §22 on 2026-10-03 by Q320. The first slice said: "Not applicable in the first slice. A future UI
requires a pack revision/follow-up with tenant shell, exact form fields, DataTable decision and Slim/Compact
reference." This section is that revision. Tenant shell and DataTable are answered in §9; the rest follows. All paths
are under `frontend/Diten.Web/`.

**Screens.** Taken from §22's page table, each matched to its view under `Views/SupplyChain/Shipments/`:

| §22 PageCode | PageType | View | Partials it composes |
|---|---|---|---|
| `SHIPMENTS` | List | `Index.cshtml` | `_Filter`, `_DataTable`, `_IndexL10n` |
| `SHIPMENT_CREATE` | Detail | `Create.cshtml` | `_Form`, `_IndexL10n` |
| `SHIPMENT_DETAILS` | Detail | `Details.cshtml` | `_TransitionOffcanvas`, `_PodOffcanvas`, `_IndexL10n` |

The folder's other files are not pages: seven partials (the six above plus `_LineEditor.cshtml`, a one-line comment
composed by no view — the line template lives in `_Form.cshtml`) and `ShipmentsIndex.cs`, the localizer's marker type.

**Exact form fields.** Method: every `input`, `select` and `textarea` in the form's view, counted once; the repeatable
line template is counted once per field, the way MOD-0186 counts its lines. The view models
(`Models/SupplyChain/Shipments/ShipmentViewModels.cs`) carry the same fields.

| Form | View | Fields | Count | View model |
|---|---|---|---|---|
| Create | `_Form.cshtml` | header: `sourceModule`, `sourceType`, `sourceDocumentId`, `warehouseReferenceId`, `shipToReference`, `plannedShipAt`, `plannedDeliverAt`; each line: `lineNumber`, `itemId`, `skuId`, `quantity`, `uomId`, `inventoryReferenceId` | 7 + 6 per line = 13 | `CreateShipmentViewModel` (7, plus the `Lines` container) and `CreateShipmentLineViewModel` (6), `:5-25` |
| Change Status | `_TransitionOffcanvas.cshtml` | `targetStatus`, `occurredAt`, `reasonCode`, `note` | 4 | `TransitionShipmentViewModel`, `:27-35` |
| Capture POD | `_PodOffcanvas.cshtml` | `recipientName`, `receivedAt`, `evidenceReferenceIds`, `note` | 4 | `CaptureShipmentPodViewModel`, `:37-43` |
| List filter | `_Filter.cshtml` | `status`, `sourceDocumentId` | 2 | none — query parameters |

**Golden reference: Compact.** The Create form has 13 fields, more than 8, and AGENTS.md binds more than 8 to
`GoldenReferenceCompact`: separate `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` and `_Form.cshtml`, with no
create/edit offcanvas on the list. The design record agrees: the list's "Create Shipment" button is navigation, "as in
GoldenCompact" (`docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md:35`), and the Compact reference treats
Add-New, Edit, Quick View and Details as navigation, not actions
(`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/ModuleRegistration/GoldenCompactManifestProvider.cs:6-11`).
Compact therefore binds: full-page Create and Details, a shared `_Form`, and list navigation that is never a manifest
action. Two declared differences from the Compact file set: there is no `Edit.cshtml`, because the frozen contract has
no shipment update operation (§21 lists its five); and Details carries two offcanvas command forms, Change Status and
Capture POD, which are state transitions, not edit forms.

**Open items** — none is closed by this reconciliation:

1. **Navigation keys.** §22 tracks them as "0/7 present today" for each key. Measured 2026-10-03: all three keys
   (`Nav.Domain.SUPPLYCHAINEXECUTION`, `Nav.Module.SHIPMENTTRACKINGPOD`, `Nav.Page.SHIPMENTS`) have non-empty values in
   all seven `Resources/SharedResource.{lang}.resx` files of the working tree — 21 of 21 — but none is in the committed
   tree, and no l10n review is recorded. They ship with the UI (§22 Ship rule).
2. **UX defects.** Q231 recorded four DEFECT rows: a 403 or a failed load on the list drawn as an empty table plus a
   toast, and no client check on the Change Status and Capture POD forms
   (`docs/records/audits/2026-10/mvp6-q231-mod0183-18-0-gap-01/UX-STATES.tsv`). Q264 could not measure 15 view-state rows
   without a user holding the keys (`docs/records/audits/2026-10/mvp6-r03-r07-runtime-01/UX-STATES.tsv`).
3. **Note length.** The UI allows 2000 characters (`ShipmentViewModels.cs:34,42`; `maxlength="2000"` in both offcanvas
   forms) and the server allows 1000 (§4, `ShipmentNoteRules.cs:15`).
4. **Status names are not localized.** The eight shipment status names are hard-coded English in `_Filter.cshtml:4`
   and shown raw in the list badge (`wwwroot/assets/js/SupplyChain/Shipments/index.js:16-19`), in all seven languages.

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

**Waiver (owner, 2026-10-03; recorded by Q334 from ledger row Q255).** SOP §18.0's shared-evidence-service requirement is **waived for MOD-0183 POD evidence** only, because this row prescribes references and no binding to an evidence service. Owner: the repository owner. **Expiry: MVP-6 closure.** The waiver does not survive into MVP-7 and must be re-decided there. Gate row 10 (Audit / Evidence) is therefore **CLOSED UNDER WAIVER, not MET**. A measurement records it that way while MVP-6 is open, and without a new owner decision measures the row on its merits after MVP-6 closure. The audit half of row 10 is not waived, and neither is any other row, module or standard. Record: `docs/records/decisions/2026-10/mvp6-shipment-pod-evidence-row10-waiver-owner-decision-01.md`.

## 13. Failure Path to Verify

- Duplicate idempotency key replays the first result and creates no second lifecycle/outbox record.
- Invalid lifecycle transition returns `INVALID_SHIPMENT_TRANSITION` with correlation ID and no write.
- A concurrent or repeated transition is resolved by atomic current-state validation plus idempotency; invalid state returns `INVALID_SHIPMENT_TRANSITION` without a write.
- Unknown/cross-tenant/cross-LE shipment returns 404 without existence leakage.
- Duplicate POD returns 409 `POD_ALREADY_CAPTURED`.
- Missing/invalid UUID correlation returns 400 and publishes no event.
- Unavailable Warehouse detail or incompatible correlation keeps automatic intake blocked and emits no fabricated shipment; an untrusted tenant/LE context never reaches intake — `TrustedSourceContext.FromValidatedPrincipal` throws `UnauthorizedAccessException` to the caller before the coordinator runs, so it leaves no intake result code, no evidence row and no counter increment, and emits no shipment.
- A Warehouse source whose canonical snapshot differs from the hash already recorded for the same source identity, on a later or concurrent intake, ends in the in-process intake result `Drift` / `SOURCE_DRIFT` (no HTTP response exists; see §13.1), records one `Drift` evidence row per detection, and creates or changes no shipment, lifecycle, audit, outbox or receipt record and publishes no event.
- Inventory reconciliation failure is explicit and never mutates inventory or shipment state silently.

### 13.1 Intake-internal result codes — not wire codes

The frozen contract governs the wire. These codes are the Warehouse intake coordinator's in-process result
(`SourceIntakeResult.State` / `.Error`); the coordinator has no HTTP route and none of these reaches a client.
**Do not add them to `shipment-bundle.openapi.yaml`.** They are declared here so that no intake code exists
undeclared. Source identity is tenant, legal entity, `MOD-0178`, `WAREHOUSE_OUTBOUND` and `outboundId`.
Every row writes one row to `sce_shipment_source_evidence` with its state, except the pass-through rows from the commit.
"Counted" means the O-3 intake counter (`IntakeBlocked`) increments.

| Code | State | Emitted at (`WarehouseIntakeCoordinator.cs`) | Trigger | Counted |
|---|---|---|---|---|
| `INVALID_UPSTREAM_CORRELATION` | Quarantined | `:23` | an upstream correlation is supplied that is not a non-empty UUID | yes |
| `CONFLICTING_CORRELATION` | Quarantined | `:28` | the supplied correlations (caller, upstream, envelope) include an empty UUID or disagree | yes |
| `DEPENDENCY_HTTP_<status>`, `SCHEMA_INCOMPATIBLE`, `DEPENDENCY_TIMEOUT`, `DEPENDENCY_UNAVAILABLE` | Blocked | `:30`, passed through from `ReadOnlySourceClients.cs:62-72` | the Warehouse detail read fails or does not match the frozen read schema | yes |
| `SOURCE_ID_MISMATCH` | Blocked | `:33` | the detail returned carries a different `outboundId` than requested | no |
| `SOURCE_DRIFT` | Drift | `:38`, `:54` | see the drift line in §13 | no — counted instead by the O-3 drift counter `SourceDrifts`, once per detection (`:87`, Q315) |
| `SOURCE_NOT_READY` | Blocked | `:40` | the Warehouse status is not `ReadyToShip` | no |
| `EMPTY_SOURCE_LINES` | Blocked | `:42` | the Warehouse detail has no lines | no |
| `SHIPMENT_MAPPING_INCOMPATIBLE` | Blocked | `:51` | the mapped shipment fails `CreateShipmentValidator` | no |
| `CONFLICTING_PERSISTED_ROOT` | Quarantined | `:56` | the prepared intent's correlation root differs from the supplied correlation | yes |
| `COMMIT_FAILED` | Blocked | `:66` | the shipment transaction throws; the pending intent is kept for a later retry | no |
| `SHIPMENT_NOT_FOUND`, `IDEMPOTENCY_KEY_REUSED`, `INVALID_REQUEST` | Blocked | `:61`, passed through from `ShipmentRepository.cs:63,69,70,73` | the commit itself returns an error; no evidence row is written | no |

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

- [ ] `Diten.SupplyChainService` builds with the mandated five layers, four pipeline behaviors and base controller. — **⚠ PARTIAL** (build measured (Q335); layers/behaviors/base controller structural, no test; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Create shipment persists exactly one tenant/legal-entity scoped shipment and one lifecycle/outbox entry. — **✅ MET** (`ShipmentTests.EveryForbiddenTransitionLeavesPersistenceUnchanged`, `FailureBeforeCommitRollsBack…`; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Repeating the same create/transition idempotency key returns the same result without a second write/event. — **✅ MET** (create/POD replay `ShipmentTests.GoldenFlow_Replay…`; transition replay `SameKeyTransitionReplayIsOneWrite…` (2026-10-19); `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Allowed transitions match `SHIPMENT-BUNDLE` v1 and invalid transitions fail without partial state. — **✅ MET** (`ShipmentTests.EveryForbiddenTransitionLeavesPersistenceUnchanged` (8×8), `AllowedBranches…`; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Capturing POD transitions the shipment to Delivered atomically and emits the specified events once. — **✅ MET** (`ShipmentTests.AllowedBranchesAndPodFailureHaveAtomicHistory`; G5 run: POD 201 → Delivered, PodCaptured once; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] A command's UUID correlation ID is preserved across shipment lifecycle and POD events. — **✅ MET** (`ShipmentTests.GoldenFlow_…`; G5 run: 6/6 shipment events carry the root; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Cross-tenant/cross-LE reads and mutations return 404; missing permissions return 403. — **✅ MET** (reads `Scope_Rbac_Schema_AndCorrelationFailClosed`; mutations `CrossScopeMutationsAre404AndWriteNothingAnywhere` (2026-10-19); 403 `CancelPermissionIsRequired…`; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [ ] Inventory data is read only through `INVENTORY-BUNDLE` v1 mock/client; no stock collection or balance exists locally. — **⚠ PARTIAL** (client tested (`SourceClientTests.InventoryReadOnly…`) but not composed; no absent-stock guard; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [ ] Warehouse intake uses the recorded v1 map; incompatible correlation, missing trusted scope and source drift are explicit blocked/reconciliation states, never fabricated shipments. — **⚠ PARTIAL** (component level (`SourceIntakeTests`); coordinator not composed; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] Contract examples pass a live mock smoke test and implementation payloads match the frozen OpenAPI. — **✅ MET** (all 247 contract examples validate (`ShipmentBundleContractExampleTests`); live success+error answers validate (`ShipmentTests.Live_responses_match_the_frozen_contract`) — 2026-10-19; `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
- [x] G5 evidence plan covers shipment→carrier/load→POD→return/claim, source reconciliation and regression; module — **✅ MET** (`mvp6-g5-e2e-chain-01` — full chain through the gateway, reconciled (2026-10-19); `docs/records/audits/2026-10/mvp6-pack16-evidence-audit-01/REPORT.md`)
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
- Observability tests for the four signals in §8.1: one test per counter in O-3 and O-4 asserting it moves on the
  named behaviour and does **not** move on a retry, and one test asserting a p95 is reported for O-2. A test that
  only asserts the counter exists does not satisfy this (SOP §24.2 vacuity rule — see Q247 for a worked example of
  a test in this module that asserted nothing and still counted as green).

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] DCP-009 and Supply Chain domain boundary were read.
- [x] Owned contract is versioned, validated and frozen as `SHIPMENT-BUNDLE` v1.
- [x] Backend-only first slice selected for Phase A; the UI that followed is reconciled in §9 and §11 (Q320): tenant shell, GoldenReferenceCompact, no pattern invented.
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

## 22. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; pack preparation only). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md).
**Foundation:** DCP-009 §21 (interface, hosted service, options, `PlatformRegistration` settings via the existing `X-Internal-Api-Key`, project reference and `Program.cs` lines — single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `shipment-tracking-pod` / `ShipmentTrackingPod` / `Shipment Tracking & POD` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, seed-once) | 390 / `bx-package` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/ShipmentTrackingPodManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/ShipmentTrackingPodManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath (verbatim) | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `SHIPMENTS` | Shipments | `/SupplyChain/Shipments` | `supplychain.shipments.read` | — | **true** | List | 10 |
| `SHIPMENT_CREATE` | Create Shipment | `/SupplyChain/Shipments/Create` | `supplychain.shipments.create` | `SHIPMENTS` | false | Detail | 11 |
| `SHIPMENT_DETAILS` | Shipment Details | `/SupplyChain/Shipments/Details/{shipmentId:guid}` | `supplychain.shipments.read` | `SHIPMENTS` | false | Detail | 12 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `SHIPMENT_CREATE` | `SAVE` | Save | `supplychain.shipments.create` | Toolbar | no |
| `SHIPMENT_DETAILS` | `CHANGE_STATUS` | Change Status | `supplychain.shipments.dispatch` | Toolbar | no |
| `SHIPMENT_DETAILS` | `CANCEL` | Cancel Shipment | `supplychain.shipments.cancel` | Toolbar | **yes** |
| `SHIPMENT_DETAILS` | `CAPTURE_POD` | Capture POD | `supplychain.shipments.pod.capture` | Toolbar | no |

All keys are existing constants in `Diten.SupplyChainService.Infrastructure/Authorization/ShipmentPermissions.cs`. Not modeled: the list row's details link (navigation); the list page's "Create Shipment" button (navigation, deliberately not an action — see below); and `supplychain.shipments.reconcile` (API-only, no UI route or button). The list's create button (`frontend/Diten.Web/Views/SupplyChain/Shipments/Index.cshtml:17-18`, `asp-action="CreatePage"`, shown only with `supplychain.shipments.create`) opens the `SHIPMENT_CREATE` page, which is already registered above with that same permission; the creating operation is that page's `SAVE` action. This follows the Compact pattern the design chose ("the list's 'Create Shipment' button is navigation, as in GoldenCompact", `docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md:35`) and the reference provider it names, which treats Add-New, Edit and Details as navigation, not actions (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Api/ModuleRegistration/GoldenCompactManifestProvider.cs:10-12`). MOD-0184…0187 §22 each do model a list-page `CREATE` Toolbar action; for MOD-0186/0187 that follows from GoldenReferenceSlim, where create runs in an offcanvas on the list page itself (AGENTS.md §6 Golden Reference table). Route truth today: `SupplyChainShipmentsController` (`[Route("SupplyChain/Shipments")]`) in the A12 successor archive `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` (`7b6a0d1a…`).

### Navigation keys (NAV-L10N-KEYS.tsv; 0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Domain.SUPPLYCHAINEXECUTION` | en, tr, fr, es, zh, ar, ru | first SupplyChain provider to ship (with the DCP-009 §21 foundation) |
| `Nav.Module.SHIPMENTTRACKINGPOD` | en, tr, fr, es, zh, ar, ru | this provider |
| `Nav.Page.SHIPMENTS` | en, tr, fr, es, zh, ar, ru | this provider |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `ShipmentPermissions` |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: `supplychain.shipments.reconcile` (API-only) |
| M-04 | Manifest RoutePaths = the frontend view-route set of `SupplyChainShipmentsController`, counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target (Q14/Q15), never ahead of it. If Shipment is the first SupplyChain module to ship, the DCP-009 §21 foundation and `Nav.Domain.SUPPLYCHAINEXECUTION` ship with it.

### Open gaps (carried, not solved)

1. The Shipment UI is not in the common checkout: its controller, views and scripts exist in the working tree but are untracked, so none is in HEAD (`git ls-files frontend/Diten.Web/Views/SupplyChain` → 0, measured 2026-10-03 by Q323). Route truth is the archived A12 successor source until integration.
2. The RoutePath keeps the `:guid` constraint verbatim (other manifests use `{id}`); the platform normalizer's handling is confirmed by R-01.
3. The details action bar is a page-level bar; `Toolbar` is the nearest supported placement.
4. `supplychain.shipments.read` is also a UI prerequisite of Returns/Claims create and transition (G-SHIPREAD); the single-key action model cannot express that conjunction.
5. Runtime tests R-02…R-04 need a native executor and the integrated target.

## 23. Inventory stock issue at dispatch

> W-4 — revision draft WP-P1 r3. Owner decisions: W-4, E-1, E-6, E-7 (Beste, 2026-10-21, integration plan §2.1 / §4.1) and G1, G5, G6, G7, G9, G10 (Natig,
> 2026-10-22, `docs/roadmap/plans/mvp6-integration-wp-p1-01/DECISION-DRAFT.md` §3a) and the MVP-1 Control Tower's answers
> to G2, G3 and G4 (2026-10-23, DECISION-DRAFT §3b), **corrected by the MVP-1 CT's CT-APR-02 review of 2026-10-23**
> (`docs/records/reviews/MOD-0183/2026-10-23-febf6e35.md`, UYGUN DEĞİL): per-attempt keys replace the fixed keys (F-1),
> one active attempt per shipment (F-2), the REVERSAL body and the exit from a failed compensation are written out (F-3,
> F-4), the company header comes from the shipment (F-5), `inventoryReferenceId` is unused in v1 (F-10); r3 closes F-3
> (REVERSAL fields as INVENTORY's code requires), F-6 (§5 scope), F-7 (slice table), F-8, F-9, F-10, F-11; **r4** (2026-10-26)
> closes the independent r3 review (`docs/records/reviews/MOD-0183/2026-10-26-2acd1866.md`, KOŞULLU): fencing F-1, external
> reversal F-2, closed code list and declared statuses F-3, `skuLevel` / lot check F-4, text and order F-5. **Draft revision —
> not approved.** Needs a new CT-APR-02 round (MVP-1 CT) and a CT-APR-01 row (high risk) before any code.

### 23.1 What is posted

| Field (INVENTORY 1.5.0 `MovementRequest`) | Value |
|---|---|
| `movementType` | `GOODS_ISSUE_SALES` |
| `itemId`, `skuId`, `quantity`, `uomId` | the shipment line (the whole line quantity; no partial issue of a line) |
| `skuLevel`, `lotId`, `fromLocationId`, `fefoBypassReason` | the shipment line's SHIPMENT-BUNDLE 3.4.0 fields (G1); `fromWarehouseId` = `WarehouseReferenceId` |
| `serialIds` | not sent in v1. A serial-tracked SKU is therefore refused by INVENTORY (`SERIAL_COUNT_MISMATCH`, shown as `INVENTORY_REFUSED`); serial-tracked shipments are out of v1 |
| `fromStockStatus` | `AVAILABLE` |
| `sourceModule` / `sourceType` | `MOD-0183` / `SHIPMENT` |
| `sourceDocumentId` / `sourceLineId` / `documentGroupId` | shipment id / line number / shipment id |
| `transactionDate` / `postingDate` | the validated `occurredAt` of the dispatch command that **opened the attempt**; posting day = UTC day (E-6); stored with the attempt and never changed |
| `idempotencyKey` (body) and `Idempotency-Key` (header) | per line and attempt: `{shipmentId}:{lineNumber}:dispatch:a{n}` (F-1), `n` = the shipment's stored attempt number |
| `X-Legal-Entity-Id` | the shipment's stored, validated `LegalEntityId` — always sent, because INVENTORY requires it when the token has no `legal_entity_id` claim (F-5) |
| `X-Correlation-Id` | the current dispatch command's correlation id, forwarded (E-1) |

The legacy line field `inventoryReferenceId` is **not used in v1** — it is neither sent nor read for the issue (F-10);
consuming an MVP-5 reservation needs `MovementRequest.reservationId`, which the MOD-0173 owner adds (W-5).

Sources of the line stock fields (G1, G10): in a tenant **without** a warehouse module the user enters them on the shipment;
in a tenant with one, MVP-5 picking (WAREHOUSE-OUTBOUND `ReadyToShip`) fills them — that mapping is written when MOD-0178
exists. **Before INVENTORY is called (r4, review F-4):** a line without `skuLevel` → 422 `SHIPMENT_LINE_STOCK_INCOMPLETE`
(`details.lineNumber`), nothing posted — INVENTORY requires `skuLevel`. Whether a line is lot-tracked is **not** decided here
(that needs PRODUCT-MASTER, not frozen): a lot-tracked line without `lotId` is refused by INVENTORY (`LOT_REQUIRED`) and shown
as `INVENTORY_REFUSED` (§23.3), which compensates like any refusal.

### 23.2 Authorization

The caller's token is forwarded. The dispatching role needs `inventory.movements.create` in addition to
`supplychain.shipments.dispatch`, granted through Roles → Permissions (the BL-652 interim path), never through the default
role template. **An INVENTORY 403 is an authorization outcome and is answered as 403 `INVALID_REQUEST`** (the pattern of the
return reader, Q420), never as 503; the log line keeps INVENTORY's code, so `CROSS_LE_FORBIDDEN` (wrong or missing
`X-Legal-Entity-Id`) is told apart from a missing permission: before the first post of an attempt nothing changes; during an attempt the attempt stays
`InProgress` (§23.4) and the shipment stays `Planned`. A service identity comes with BL-652 Option 2.

### 23.3 Refusals and events

- Any INVENTORY 422 → 422 `INVENTORY_REFUSED` with `details.lineNumber` and `details.inventoryCode` (SHIPMENT-BUNDLE 3.4.0);
  the shipment does **not** become `Dispatched`. The Shipments screen shows a localized text per `inventoryCode` (7 languages:
  `Resources/Views/SupplyChain/Shipments/ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`, already in §5 Repo Scope).
- **Closed `inventoryCode` list (r4, review F-3):** the 422 codes of INVENTORY's `InventoryErrorCodes.cs` (at `origin/main`
  when DL-1 starts; today `FEFO_BYPASS_REASON_REQUIRED`, `FEFO_ORDER_VIOLATION`, `INSUFFICIENT_AVAILABLE`,
  `INSUFFICIENT_STOCK`, `ITEM_NOT_ACTIVE`, `LOT_BELOW_MIN_REMAINING_SHELF_LIFE`, `LOT_BLOCKED`, `LOT_EXPIRED`,
  `LOT_NOT_ALLOWED`, `LOT_REQUIRED`, `LOT_RETEST_DUE`, `LOT_SKU_MISMATCH`, `LOT_UNDER_RECALL`, `PRODUCT_MASTER_DATA_INCOMPLETE`,
  `RESERVATION_CONFLICT`, `SERIAL_ALREADY_EXISTS`, `SERIAL_COUNT_MISMATCH`, `SERIAL_LOT_MISMATCH`, `SERIAL_NOT_AVAILABLE`,
  `SKU_ITEM_MISMATCH`, `SKU_LEVEL_NOT_STOCKABLE`, `UNKNOWN_ITEM`, `UNKNOWN_LOCATION`, `UNKNOWN_LOT`,
  `UOM_CONVERSION_MISSING`); each has a text, and a code outside the list shows the generic text key
  `ShipmentsIndex.InventoryRefused.Unknown` with the code itself — never the generic `validationError` text.
- INVENTORY unreachable / 5xx / timeout → 503 `DEPENDENCY_UNAVAILABLE`; before the first post nothing changes, during an
  attempt the attempt stays `InProgress` with the lines already posted (a later user retry resumes it, §23.4).
- **Declared statuses (r4, review F-3):** `transitionShipment` declares 200 / 404 / 409 / 422 / 503 in 3.3.0. §23 also answers
  403 (INVENTORY 403, §23.2) and 500 (INVENTORY 400, §23.4); SHIPMENT-BUNDLE 3.4.0 **adds 403 and 500 to
  `transitionShipment`** (additive), so no status of §23 is undeclared.
- The `ShipmentDispatched` event keeps its frozen shape; it is written only after every line's movement exists (G9). MVP-5
  will use it to mark its outbound record `Shipped`; any later change of its shape is announced to the MVP-1 CT first.

### 23.4 Reservations attempts and compensation

**G2 — reservations.** No INVENTORY change is needed for this slice: INVENTORY consumes an active reservation in the same
posting transaction when the movement's (`sourceModule`, `sourceDocumentId`, `sourceLineId`) equals the reservation's
(MOD-0173, ASSUMPTION-0173-15). MOD-0183 opens no reservation in v1, so its issue consumes none. Matching is an **exact text
comparison within the same SKU** (`"1"` does not match `"001"`), a reservation with an empty `sourceDocumentId` is never
matched, and only a movement that reduces `AVAILABLE` consumes one. A shipment reservation, if MOD-0183 ever opens one,
carries **no `expiresAt`**. Consuming an MVP-5 (MOD-0178) reservation needs `MovementRequest.reservationId` (W-5, MOD-0173
owner). "Release first, then issue" is not used.

**The dispatch attempt (F-1, F-2).** The shipment stores one `DispatchAttempt` at a time:

| Stored | Meaning |
|---|---|
| `n` | attempt number, 1 for the first attempt; it increases **only** when an attempt is closed as `Compensated` (all its posted lines reversed). A completed (`Dispatched`) attempt ends the dispatch; an `InProgress` or `CompensationFailed` attempt keeps its `n` |
| `state` | `InProgress` / `CompensationFailed` / `Compensated` / `Completed`; the wire shows it as `ShipmentDetail.dispatchState` (`None` when there is no open attempt, 3.4.0) |
| `occurredAt`, `transactionDate`, `postingDate` | from the command that opened the attempt; sent unchanged on every retry within the attempt |
| per line | the full §23.1 request body as first sent, the issue `transactionId` and `lotId` when INVENTORY answered, the reversal dates and reversal `transactionId` when compensated |
| `leaseUntil` | the attempt's single-writer lease (request timeout + 30 s), renewed before each INVENTORY call |

- **One active attempt (F-2).** Opening or resuming an attempt is a conditional update on the shipment (`dispatchState` and
  `leaseUntil` checked in the filter). A dispatch command while another holds the lease → 409 `DISPATCH_IN_PROGRESS`
  (3.4.0); `Planned → Cancelled` and POD capture while an attempt is `InProgress` → 409 `DISPATCH_IN_PROGRESS`; any
  transition while `CompensationFailed` other than the exit below → 409 `DISPATCH_COMPENSATION_FAILED`.
- **Posting.** Each line is posted with its attempt key and the stored body. **A retry within the attempt resends the same
  request** (same key, same body, same dates): INVENTORY answers an already-processed request with
  `idempotentReplay: true`, which is success. A refused request is not stored by INVENTORY, so resending it re-runs its rules.
  `IDEMPOTENCY_KEY_REUSED` is an integrity fault (logged; the attempt is blocked like `CompensationFailed`), never a reason to
  post again.
- **Completion.** When every line has an issue `transactionId`, the shipment becomes `Dispatched`, the attempt `Completed`,
  and the frozen `ShipmentDispatched` event is written in the same transaction (with receipt and central audit). MOD-0186
  reads the per-line issue of the **completed** attempt only.
- **Refusal → compensation (G3, F-3).** If a line is refused (422), every posted line of the attempt is reversed. The
  REVERSAL body is fixed:

  | Field | Value |
  |---|---|
  | `movementType` | `REVERSAL` |
  | `itemId`, `skuId`, `skuLevel`, `quantity`, `uomId` | equal to the original issue (stored request of that line) — INVENTORY checks the equality (`ReversalRules.cs:37-56`) |
  | `sourceModule` / `sourceType` | `MOD-0173` / `REVERSAL` |
  | `sourceDocumentId` | the original issue `transactionId` (a UUID) |
  | `documentGroupId` | shipment id |
  | `transactionDate` / `postingDate` | the moment compensation starts (server UTC), stored at the first reversal attempt and resent unchanged |
  | `idempotencyKey` / `Idempotency-Key` | `{shipmentId}:{lineNumber}:reversal:a{n}` |
  | `X-Legal-Entity-Id`, `X-Correlation-Id` | as §23.1 |
  | **not sent** | `lotId`, `serialIds` (`MovementRequestParser.cs:125-126`), every `from*` / `to*` field including the stock statuses (`MovementDirectionRules.cs:109-112`), `fefoBypassReason`, `sourceLineId` — INVENTORY derives lot, location and direction from the original |

  **An INVENTORY 400 (`INVALID_REQUEST`) is never a business refusal (F-3):** it means our request is malformed. On an issue
  post the attempt is compensated as for a refusal and the command answers 500 with the correlation id; on a REVERSAL the
  attempt becomes `CompensationFailed`. Both are logged as integrity faults. A reversed issue does not restore a reservation
  it consumed. When all reversals are done → attempt `Compensated`, `n` increases, `dispatchState = None`, the shipment stays
  `Planned`, and the command answers 422 `INVENTORY_REFUSED` (§23.3).
- **Issue reversed by someone else (r4, review F-2).** Anyone holding `inventory.movements.create` can reverse our issue in
  INVENTORY under another key; our REVERSAL then answers 422 `ALREADY_REVERSED`. That line **counts as reversed** (the stock
  is already back); it is logged as an integrity fault and written to the transition's central-audit record with the
  original `transactionId`. When the other lines are reversed, the attempt is `Compensated` — no `CompensationFailed` loop.
  During a dispatch (not a compensation) a line whose issue was reversed externally is still a posted line; the attempt is
  completed or compensated as usual.
- **Fencing a late writer (r4, review F-1).** The lease carries an id. Every INVENTORY call and every write of a result is
  conditional on the shipment still holding **this attempt `n` and this lease id**. A writer whose lease has expired (a
  paused process) may still get a 201 from INVENTORY for a line; when it then cannot record the `transactionId`, it reverses
  that issue itself with the REVERSAL body of §23.4 and key `{shipmentId}:{lineNumber}:reversal:a{n}` (unused for that line,
  which was never recorded in attempt `n`), and logs an integrity fault. So no issue stays unrecorded and unreversed.
- **Order (r4, review F-5).** Lines are posted, and reversed, in the order they are stored on the shipment (create order),
  never sorted by `lineNumber` text.
- **Failed compensation and the exit (F-4).** If a REVERSAL itself fails, the attempt becomes `CompensationFailed`, the
  wire `status` stays `Planned` (the frozen lifecycle has no `Planned → Exception` arrow) and the command answers 409
  `DISPATCH_COMPENSATION_FAILED` with the affected lines. **Exit:** a user sends a transition to `Dispatched` or `Cancelled`.
  The service first resends the attempt's open reversals (same keys, same stored reversal dates). If all succeed, the attempt
  is `Compensated`, `n` increases, and only then is the requested transition carried out — `Cancelled` cancels; `Dispatched`
  opens attempt `n + 1` with this command's `occurredAt`. If a reversal fails again, nothing else happens and the answer is
  409 `DISPATCH_COMPENSATION_FAILED`. The exit is therefore the existing transition command, with no new endpoint (F-4):
  permission `supplychain.shipments.dispatch` for `Dispatched` and `supplychain.shipments.cancel` for `Cancelled`, both plus
  `inventory.movements.create` for the reversals; the Shipments screen shows the `CompensationFailed` label and the two
  existing actions only to a user who holds the permission (UAS-001); the transition's central-audit record (AUD-001) carries
  the reversal `transactionId`s and the outcome.
- **After `Dispatched` (F-11).** `Dispatched → Exception → Cancelled` and every later transition post nothing and reverse
  nothing: the stock issue stands, and goods that come back return through MOD-0186 (§34, `CUSTOMER_RETURN` into
  quarantine).
- **MVP-5 reservations (F-10).** When MVP-5 S2 opens MOD-0178 reservations, the issue must name them
  (`MovementRequest.reservationId`, added by the MOD-0173 owner, W-5); that is a new MOD-0183 revision and a **dependency of
  MVP-5 S2 going live** — until then an MVP-5 reservation stays open and may make the issue fail the AVAILABLE − Reserved
  check.
- **Crash and resume.** Resume happens only on a user action (the caller's token is needed): a dispatch command after the
  lease expired takes the lease and resumes the same attempt. A background job may only flag attempts whose lease expired; it
  posts and reverses nothing.

### 23.5 Acceptance

| AC | Measurable acceptance |
|---|---|
| S23-1 | One-line shipment dispatched → one `GOODS_ISSUE_SALES` with the §23.1 fields and `X-Legal-Entity-Id` = the shipment's LE; AVAILABLE falls by the quantity (MVP-1 live tour "Dispatched → stok düştü") |
| S23-2 | Lot under recall → 422 `INVENTORY_REFUSED` / `LOT_UNDER_RECALL`, shipment not `Dispatched`, no event (live tour "recall altındaki lot → sevkiyat reddedildi") |
| S23-3 | Replaying the dispatch command (same `Idempotency-Key`) posts no second movement and no second event |
| S23-4 | INVENTORY unavailable → 503, no state change before the first post; INVENTORY 403 → 403 `INVALID_REQUEST`, never 503; sabotage: dropping `X-Legal-Entity-Id` makes the test red (INVENTORY `CROSS_LE_FORBIDDEN`) |
| S23-5 | A line without `skuLevel` → 422 `SHIPMENT_LINE_STOCK_INCOMPLETE`, INVENTORY not called; a lot-tracked line without `lotId` → INVENTORY `LOT_REQUIRED` → 422 `INVENTORY_REFUSED`, posted lines compensated |
| S23-6 | Cross-tenant / cross-LE shipment id → 404, no movement |
| S23-7 | Every `inventoryCode`, the module codes `INVENTORY_REFUSED`, `SHIPMENT_LINE_STOCK_INCOMPLETE`, `DISPATCH_IN_PROGRESS`, `DISPATCH_COMPENSATION_FAILED`, `DEPENDENCY_UNAVAILABLE`, and the `dispatchState` labels (`InProgress`, `CompensationFailed`) have a non-empty text in all 7 languages of `ShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` (parity measured by MATCH count); the screen never falls back to the generic `validationError` text for them (F-9) |
| S23-8 | Three-line dispatch, line 2 refused → line 1 reversed with key `…:1:reversal:a1` and the §23.4 body; attempt `Compensated`, `n = 2`; shipment `Planned`; no event |
| S23-9 | Crash between line 1 and line 2, user retry after the lease → line 1 resent with key `…:dispatch:a1` and the stored body answers `idempotentReplay: true`, line 2 posted, `Dispatched` once |
| S23-10 | Compensating REVERSAL fails → `CompensationFailed`, transitions and POD 409 `DISPATCH_COMPENSATION_FAILED`; exit by `Cancelled` and by `Dispatched` both resend the reversals first; `Dispatched` then opens attempt 2 with keys `…:a2` |
| S23-11 | Stored dates are resent within an attempt; a forced date change in a test shows `IDEMPOTENCY_KEY_REUSED` handled as an integrity fault |
| S23-12 | A background job never posts or reverses; it only flags an expired lease |
| S23-18 | **Fencing (review F-1):** a writer whose lease expired posts line 3 after the attempt was compensated → its issue is reversed by itself with `…:3:reversal:a1`, net stock unchanged, integrity fault logged; sabotage: dropping the lease-id condition makes the test red |
| S23-19 | **External reversal (review F-2):** line 1's issue is reversed in INVENTORY by a user, then line 2 is refused → our REVERSAL of line 1 answers `ALREADY_REVERSED`, counted as reversed, attempt `Compensated`, no `CompensationFailed` loop |
| S23-17 | REVERSAL body: only the §23.4 fields are sent; sabotage: adding `lotId` or a `from*` field makes the compensation test red (INVENTORY 400), and a 400 is handled as an integrity fault |
| S23-13 | Compensation retried after a crash → each REVERSAL resent with its `…:reversal:a{n}` key and stored dates → `idempotentReplay: true`, no `ALREADY_REVERSED`, no second reversal |
| S23-14 | **Re-dispatch after compensation (F-1):** after S23-8 the cause is removed and the shipment is dispatched again → three new issues with keys `…:a2`, three new `transactionId`s different from attempt 1, AVAILABLE net −(sum of lines); none answered by replay |
| S23-15 | **One active attempt (F-2):** two concurrent dispatch commands → one proceeds, the other 409 `DISPATCH_IN_PROGRESS`; `Cancelled` and POD during `InProgress` → 409 `DISPATCH_IN_PROGRESS`; never two attempts |
| S23-16 | **Joint scenario with MOD-0186 (R34-8):** partial dispatch → compensation → re-dispatch → partial return → retry, with the movement ids and net stock checked after each step (MOD-0186 §34.3) |

Each rule gets a sabotage proof; the full SupplyChain test assembly runs unfiltered; MDM and INVENTORY consumer tests and the
MVP-1 CT's live tours are the acceptance.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | SHIPMENT-BUNDLE 3.4.0 + çıkış çekirdeği | Ownership and Boundaries, Repo Scope, Dependencies, Inventory stock issue at dispatch, What is posted, Authorization, Refusals and events, Reservations attempts and compensation, Acceptance |
| DL-2 | ret ve durum metinleri | Refusals and events, Acceptance |

DL-1 covers S23-1…S23-6, S23-8…S23-15 and S23-17…S23-19; the 3.4.0 contract patch ships in the same change
(`ContractErrorCodeParityTests`) together with the return reader's acceptance of the 3.4.0 line fields (Repo Scope, §23 DL-1
exception). DL-2 covers the screen texts and labels of S23-7. S23-16 is measured when MOD-0186 DL-1 lands (it needs both
modules). Not slices of this version: the product check (Dependencies, PRODUCT-MASTER row — dispatchable only after
PRODUCT-MASTER is frozen, by a new revision) and MVP-5 reservation consumption (`reservationId`, MOD-0173 owner's W-5 work, a
later MOD-0183 revision). CT-APR-01 note: "only DL-1, DL-2".
