---
id: MOD-0183
name: Shipment Tracking & POD
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
status_note: "Phase A spec approved by owner on 2026-09-15; first executable slice is backend/contract only. WAREHOUSE-OUTBOUND v1 is present; adapter compatibility and integrated acceptance remain separately gated."
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

- [x] `Diten.SupplyChainService` builds with the mandated five layers, four pipeline behaviors and base controller. — measured 2026-10-11: five layers (Api/Application/Domain/Infrastructure/Persistence), four behaviors registered (Validation, Logging, ExceptionHandling, Performance; `DependencyInjection.cs:35-38`), `CustomBaseController` used by six of seven controllers — the exception is `SandopPlansController`, in the uncomposed MOD-0190. Solution builds with 0 warnings, 0 errors.
- [x] Create shipment persists exactly one tenant/legal-entity scoped shipment and one lifecycle/outbox entry. — measured 2026-10-11: one document in each of `sce_shipments`, `sce_shipment_history`, `sce_shipment_outbox`, `sce_shipment_audit`, `sce_shipment_receipts`. docs/records/audits/2026-10/mvp6-g5-logistics-golden-flow-01/
- [x] Repeating the same create/transition idempotency key returns the same result without a second write/event. — measured 2026-10-11: 201 then 200, same shipmentId, `idempotentReplay=true`, one database record. Carrier create behaves the same (one record, replay true).
- [x] Allowed transitions match `SHIPMENT-BUNDLE` v1 and invalid transitions fail without partial state. — measured 2026-10-11: Planned 200, Delivered 422, Dispatched 200, Planned 422, InTransit 200; four accepted transitions left four lifecycle entries and the two rejected ones wrote nothing.
- [x] Capturing POD transitions the shipment to Delivered atomically and emits the specified events once. — measured 2026-10-11: shipment reaches `Delivered`, one `Delivered` lifecycle entry, and `ShipmentDelivered` and `PodCaptured` are two distinct events emitted once each (six state changes, six outbox events).
- [x] A command's UUID correlation ID is preserved across shipment lifecycle and POD events. — measured 2026-10-11: one correlation found in 36 documents across all five modules, including the outbox envelopes. Field name differs per module: Shipments `CorrelationId`, Loads/Returns/Claims `CorrelationRoot`.
- [x] Cross-tenant/cross-LE reads and mutations return 404; missing permissions return 403. — measured 2026-10-11 with a real second tenant and its own MDM legal entity: own read 200, cross-tenant read 404, cross-LE read 404, cross-tenant write 404, token without `supplychain.shipments.read` 403.
- [x] Inventory data is read only through `INVENTORY-BUNDLE` v1 mock/client; no stock collection or balance exists locally. — measured 2026-10-11: no local stock/balance class and no such Mongo collection; `inventoryReferenceId` is only ever validated as an opaque string and never dereferenced; the one `IInventoryReadClient` lives in the uncomposed SourceIntake and is not registered in `Program.cs`.
- [ ] Warehouse intake uses the recorded v1 map; incompatible correlation, missing trusted scope and source drift are explicit blocked/reconciliation states, never fabricated shipments.
- [x] Contract examples pass a live mock smoke test and implementation payloads match the frozen OpenAPI. — measured 2026-10-11: 19 live responses validated against `SHIPMENT-BUNDLE` 3.2.1 (JSON Schema 2020-12), zero schema violations, error paths included. The run found two operations returning an undeclared 404; the contract was patched to 3.2.1 and re-validated clean.
- [ ] G5 evidence plan covers shipment→carrier/load→POD→return/claim, source reconciliation and regression; module — the CHAIN half is done (measured 2026-10-11: ten steps, all pass, docs/records/audits/2026-10/mvp6-g5-logistics-golden-flow-01/). SOURCE RECONCILIATION is still open: it is the warehouse-intake path, which has no producer until MOD-0178 exists, so this box stays unticked.
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
