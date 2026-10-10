---
id: MOD-0184
name: Carrier Management
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: ready-for-dev
status_note: "Bounded backend E4 remains CT-accepted under sections 29-30 and ready-for-dev; tenant UI scope owner-approved 2026-10-04 (section 32), UI DEV released to R-4a, independent UI VER open."
owner: supply-chain-execution / control-tower
branch: feature/mvp6-logistics
started: 2026-09-15
target: 2026-09-30
form_field_count: 4
---

# MOD-0184 — Carrier Management

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0184 --name "Carrier Management"`
> returned `OK` again on 2026-09-16 against Blueprint 8.1 and the module ID registry.
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
| Queries | List carriers with optional status filter; no public by-ID query |
| API | Exactly GET/POST `/carriers` and POST `/carriers/{carrierId}/status`; see §21 |
| Permissions | `supplychain.carriers.read`, `.create`, `.status.change` |

## 4. Entity Fields

`Carrier` has server-minted UUID `Id`, server-resolved `TenantId` and request-borne, MDM-validated `LegalEntityId`, tenant+LE unique `CarrierCode`,
required `DisplayName`, one or more `SupportedModes`, optional `ExternalReference`, `Status`, version, soft-delete and
audit fields. Tenant/legal-entity identifiers are never accepted from command payloads.

## 5. Repo Scope

The exact prospective allowlist is §23; no broad service wildcard is authorized. Shared composition exceptions in §23 require explicit separate approval. This PREP lane writes only this pack and the three preparation documents listed in §27.
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
pipeline conventions. All three operations require UUID `X-Correlation-Id`; mutations additionally require a string `Idempotency-Key` of length 1–128 (not a UUID requirement). Retired is terminal. No
implementation or service scaffold is authorized by this draft.

## 9. Layout & Shell Contract

`shell: none`; no frontend, Razor, DataTable, menu or localization surface is in this slice.

## 10. Backend File Convention

Prospective feature root is `Features/Carriers/` with separate Commands, Queries, Handlers/CommandHandlers,
Handlers/QueryHandlers, Validators and `CarrierModels.cs`, following the repository module-pack standard.

## 11. Frontend File Contract

Not applicable. A UI requires a separately approved pack revision with tenant shell, exact fields and golden reference.

## 12. Validation Rules

- Frozen `carrierCode` and `displayName` are non-null strings with minLength 1. Trimming/case normalization is not frozen; see GAP-184-03. Tenant+LE uniqueness is the pack business rule.
- `SupportedModes` is non-empty and limited to frozen contract enum values.
- Active↔Suspended and terminal Retired are frozen. Retirement entry and same-state semantics require GAP-184-02 disposition; do not infer them from this draft.
- Mutation headers are required and TenantId/LegalEntityId body fields are rejected.

## 13. Failure Path to Verify

Prospective test cases and unresolved response mappings are in §§22/24. `INVALID_CARRIER_TRANSITION` is a proposed pack code, not a frozen example. Unknown/cross-scope status target maps to 404 `CARRIER_NOT_FOUND`; global Conflict defines `IDEMPOTENCY_KEY_REUSED`, but status POST does not reference 409.

## 14. Authorization Convention

Server permissions are `supplychain.carriers.read`, `.create`, `.status.change`. Shared permission registration is
integrator-owned and outside this draft.

## 15. Gateway / API Routing Decision

Desired family is the frozen `SHIPMENT-BUNDLE` carrier surface. Only `integration-agent` may modify gateway routes.

## 16. Acceptance Criteria

- [x] Runtime remains absent while status is `draft`. — SUPERSEDED, and the old wording is kept so the change is visible: this pack is no longer `draft`. Its status is `ready-for-dev`, owner-approved 2026-10-04 (see `status_note`), and the Carrier runtime was merged to main in PR #134 (`a814bad0f`). The line is ticked as satisfied-then-superseded, not as a runtime that is still absent.
- [ ] Future implementation matches all Carrier schemas, examples, headers and deterministic errors in v1.
- [x] Carrier status and idempotency tests prove no duplicate write/audit; no Carrier event is invented (the frozen event enum excludes Carrier). — measured live 2026-10-11: the same idempotency key returned the same carrierId with `idempotentReplay=true` and exactly one `carriers` document; the database has no carrier outbox collection at all, so no Carrier event is emitted.
- [x] Cross-tenant/cross-LE access returns 404. — measured live 2026-10-11: `GET /carriers` with a foreign legal entity answers 404 `CARRIER_NOT_FOUND`; the shipment-side cross-tenant and cross-LE cases were measured with a real second tenant in `docs/records/audits/2026-10/mvp6-g5-logistics-golden-flow-01/`.
- [x] MOD-0185 consumes carrier by contract/reference without internal database/type sharing. — measured 2026-10-11: `LoadReferenceReader` resolves the carrier over HTTP through `api/shipment-bundle/carriers` and validates the frozen wire shape; there is no project reference, shared type or shared collection between the two modules. A live load create against a real carrier succeeded.

## 17. Test Expectations

DCP-002 verification, frozen OpenAPI syntax/reference validation, future contract/mock tests, lifecycle matrix tests,
tenant isolation, RBAC, idempotency and Mongo DB-010 architecture guards.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002.
- [x] Owned boundary and frozen `SHIPMENT-BUNDLE` v1 are identified.
- [x] Backend-only initial slice and protected paths are explicit.
- [x] Owner approval to change `draft` to `ready-for-dev` is absent. — SUPERSEDED: approval was given 2026-10-04 and the pack's `status` is `ready-for-dev`. Wording kept so the transition is visible rather than silently rewritten.
- [x] Runtime dispatch is authorized. — Carrier runtime was dispatched, reviewed and merged in PR #134 (`a814bad0f`).

## 19. Implementation Notes

**ASSUMPTION:** carrier contracts and supported modes are managed as reference fields; pricing, tendering and
settlement remain outside MOD-0184. This default narrows scope and does not create a Supplier substitute.

## 20. Follow-up Items

- Approve this pack before MOD-0184 runtime work.
- MOD-0185 remains dependency-gated on a verified MOD-0184 contract/runtime seam.
- Historical **GAP: merkez CT WAREHOUSE-SHIPMENT-TRIGGER üretmeli** is a frozen bundle marker, not evidence that all Warehouse contracts are absent: WAREHOUSE-OUTBOUND exists, while Lane 1 retains correlation/LE/cursor/live-ingress GAPs. No Carrier Warehouse call or local trigger contract is authorized.
- Gateway/permission registration requires a separate integration WP.


## 21. Frozen Carrier parity — measured 2026-09-16 / PREP v1.0

Authority: `docs/analysis/contracts/shipment-bundle.openapi.yaml`, OpenAPI 3.1.0,
info.version 1.0.0, x-contract-version v1, x-status FROZEN. Base URL `/api/shipment-bundle`.
This section refines the original draft; unresolved proposals below are not contract facts.

| Operation ID | HTTP path after base URL | Input | Declared responses |
|---|---|---|---|
| queryCarriers | GET `/carriers` | Required correlation header; optional `status` enum | 200 CarrierListResponse only |
| createCarrier | POST `/carriers` | Both headers; required CreateCarrierCommand body | 201 CarrierResponse; 409 Conflict; 422 Unprocessable |
| changeCarrierStatus | POST `/carriers/{carrierId}/status` | UUID path ID; both headers; required ChangeCarrierStatusCommand | 200 CarrierResponse; 404 NotFound; 422 Unprocessable |

No GET-by-ID, PUT, PATCH, DELETE, bulk, lookup, validation, pagination, search or sorting operation
exists for Carrier. `resolve carrier reference` from the earlier draft does not authorize a route.
Do not copy Golden CRUD endpoints. GET only exposes the optional status filter; `total` counts scoped results.
No automatic Warehouse ingress, Inventory call or Supplier client is needed for these operations.

| Schema.field | Required / null | Exact frozen constraint |
|---|---|---|
| Create.carrierCode | required / non-null | string, minLength 1; no maxLength/pattern/case/trim rule |
| Create.displayName | required / non-null | string, minLength 1; no maxLength/trim rule |
| Create.supportedModes | required / non-null | array, minItems 1; items Road/Air/Sea/Rail/Parcel; duplicates not forbidden |
| Create.externalReference | optional / null allowed | string or null; empty string allowed; no UUID/foreign-key meaning |
| Change.targetStatus | required / non-null | Active/Suspended/Retired |
| Change.reasonCode | required / non-null | string; no minLength, enum, maxLength or lookup constraint (empty permitted by schema) |
| Summary.carrierId | required / non-null | UUID string |
| Summary.carrierCode, displayName | required / non-null | string; no response minLength |
| Summary.status | required / non-null | CarrierStatus |
| Summary.supportedModes | required / non-null | array of TransportMode; no response minItems |
| Response.carrierId, carrierCode, status | required / non-null | UUID string, string, CarrierStatus |
| Response.idempotentReplay | required / non-null | boolean |
| Response.contractVersion | required / non-null | string const v1 |
| List.items, total, contractVersion | required / non-null | Summary array; integer (no minimum); const v1 |

Both command objects have `additionalProperties:false`; tenantId/legalEntityId/supplierId/version and all
unknown body fields must be rejected. Response objects do not prohibit extra properties, but this slice
returns only declared fields. Summary has no externalReference/version/audit fields; mutation response has
no displayName/supportedModes. Required is not equivalent to nonempty: do not silently tighten reasonCode.

CorrelationId is required on GET as well as POST: string format UUID, no explicit nonzero rule.
IdempotencyKey is a string with minLength 1/maxLength 128, no trim or whitespace-only exclusion.
Error shape is `{error:{code,message,correlationId,details?},contractVersion:"v1"}`; correlationId is UUID.
Frozen global examples provide CARRIER_NOT_FOUND and IDEMPOTENCY_KEY_REUSED; there is no
Carrier duplicate-code or invalid-transition example. Header/schema 400, authentication 401/403,
status replay conflict 409 and infrastructure 5xx are not declared for these Carrier operations.
They are GAPs, not permission to invent external semantics.

Lifecycle: description says Active↔Suspended and Retired terminal; creation example says Active.
No client version, ETag or If-Match exists. Contract introduction explicitly chooses idempotent command
plus current-state checking instead of client optimistic concurrency. Internal compare-and-swap/transaction
is still required to make that checking atomic. LifecycleEventEnvelope excludes Carrier in both
aggregateType and eventType; no CarrierCreated/CarrierStatusChanged event or shared event transport is authorized.

## 22. ASSUMPTION / GAP decisions for owner approval

GAP-184-01 is SATISFIED for preparation only by the Lane 1 report below; the runtime release remains HELD. All other decisions remain OPEN unless a versioned owner disposition is attached. CT/contract owner
must distinguish normative clarification from an actual contract amendment; this lane edits neither contract.

| ID / owner | Evidence / missing decision | Concrete proposal for review; not approved |
|---|---|---|
| GAP-184-01 / central CT | Lane 1 report `docs/records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md` arrived during PREP; bounded MOD-0183 E4 ACCEPTED, preparation prerequisite sufficient | SATISFIED for PREP only. Runtime remains CLOSED pending separate approval/DoR/Phase 1.5; gateway/ingress/E5/G5 excluded; architecture remains 15 PASS / 3 deferred external FAIL |
| GAP-184-02 / business + contract owner | Active example; terminal Retired but no complete transition table | Create Active; Active→Suspended/Retired, Suspended→Active/Retired; same-state under a new key and every transition from Retired return 422; exact replay succeeds before state validation |
| GAP-184-03 / business + contract owner | Unique code is a pack rule; frozen schema lacks normalization and duplicate code | Preserve exact code/name text, ordinal case-sensitive code uniqueness per tenant+LE including retired/deleted records; no code reuse. Proposed 409 CARRIER_CODE_CONFLICT. Explicitly approve alternative trim/case rules before DEV |
| GAP-184-04 / contract + security owner | Missing per-operation errors; invalid/missing correlation cannot echo a valid UUID; existing middleware rejects Guid.Empty and trims keys | Define 400/401/403/409/5xx matrix for each operation and correlation fallback. Proposed INVALID_REQUEST for schema/auth, 422 INVALID_CARRIER_TRANSITION; no new frozen claim. Decide nil UUID, whitespace key, auth/header precedence and mismatch response |
| GAP-184-05 / contract owner | idempotentReplay exists; create replay status, scope, canonicalization, retention and different-correlation replay undefined | Persist key scoped by tenant+LE+operation+target ID (create uses fixed target); fingerprint validated payload preserving whitespace/mode order/duplicates; absent and null externalReference equivalent. Exclude correlation from fingerprint; replay original result with replay=true (201 create/200 status); preserve original audit correlation and echo current request correlation in response header. No TTL initially; changed payload 409 IDEMPOTENCY_KEY_REUSED. Approve status 409 mapping |
| GAP-184-06 / service/security owner + central CT | Bundle JWT scope vs global header rule; shared middleware uses ShipmentScope and SHIPMENT_NOT_FOUND for the whole bundle | Carrier-specific context middleware using validated tenant_id/legal_entity_id/sub plus matching scope headers; no caller-selected scope. Explicitly approve common composition exceptions §23; leave Shipment middleware bytes intact |
| GAP-184-07 / service/data owner | No Carrier schema/index/audit atomicity design approved | Approve §24 transaction, unique indexes, L3 persistence and specific repository exception; approve operational 5xx mapping via GAP-184-04 |
| GAP-184-08 / integration owner | No Carrier gateway route found in gateway ocelot files; shared permission registration held | Bounded direct-service E4 with real permission claims; central gateway/catalog provisioning remains a separate integration WP. No claim of end-user/E5 delivery |

ASSUMPTION-184-A: `externalReference` is opaque external text, not Supplier identity. Supplier SoR is
MOD-0140 (`supplier.openapi.yaml`: x-owner MOD-0140, front-loaded for MOD-0147/0148). Tax, contact,
bank, qualification, payment terms and Supplier status remain there. No Supplier entity/cache/table/seed/client,
no inferred Carrier↔Supplier join, and no invented supplierId field. A future relation needs a separate approved contract.

ASSUMPTION-184-B: Warehouse trigger is a bundle-level unresolved consumption marker, not a runtime
prerequisite or call for Carrier create/list/status. MOD-0183 acceptance is an execution sequencing gate,
not permission to mutate Shipments. MOD-0185 must later consume a published seam; by-ID lookup is absent
and any consumer need for it must go back to the contract owner.

## 23. Exact prospective ownership and shared composition request

**This is a proposed DEV allowlist, not current authorization.** Prefix `S` below means exactly
`services/Diten.SupplyChainService/src/Diten.SupplyChainService` (concatenate suffix literally).
A listed feature directory owns only new Carrier files under that exact directory; no parent wildcard.
The current PREP lane cannot write any path in this section.

| Layer | Exact owned directory / files | Planned objects |
|---|---|---|
| Domain | `S.Domain/Features/Carriers/` | Carrier.cs, CarrierScope.cs, CarrierStatus.cs, TransportMode.cs, CarrierLifecycle.cs, CarrierAuditEntry.cs, CarrierMutationResult.cs, ICarrierRepository.cs |
| Application | `S.Application/Features/Carriers/` | Commands/CreateCarrierCommand.cs, Commands/ChangeCarrierStatusCommand.cs; Queries/GetCarrierListQuery.cs; Handlers/CommandHandlers/CreateCarrierHandler.cs, ChangeCarrierStatusHandler.cs; Handlers/QueryHandlers/GetCarrierListHandler.cs; Validators/CreateCarrierValidator.cs, ChangeCarrierStatusValidator.cs, GetCarrierListValidator.cs; CarrierModels.cs, CarrierRequestContext.cs, CarrierRequestFingerprint.cs |
| API | `S.Api/Features/Carriers/` | CarriersController.cs, CarrierContextMiddleware.cs, CarrierContractError.cs; exact three frozen routes |
| Persistence | `S.Persistence/Features/Carriers/` | CarrierRepository.cs, CarrierSchema.cs, CarrierPersistenceRegistration.cs, ICarrierCommitProbe.cs, NoOpCarrierCommitProbe.cs; collections carriers/carrier_idempotency/carrier_audit |
| Infrastructure | `S.Infrastructure/Features/Carriers/` | CarrierPermissions.cs, CarrierPermissionAttribute.cs; no external transport/client |
| Tests | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Carriers/` | CarrierContractTests.cs, CarrierLifecycleTests.cs, CarrierIsolationTests.cs, CarrierReplayTests.cs, CarrierAtomicityTests.cs, CarrierConcurrencyTests.cs |
| HTTP/restart probes | `services/Diten.SupplyChainService/tests/carriers/` | runtime_probe.py, restart_probe.py, verify_evidence.py; no edits to MOD-0183 probes |
| Evidence | `docs/records/audits/2026-09/mod-0184-dev-01/` | new immutable run artifacts + manifest; VER uses a separate directory |

The Domain interface is a proposed REPO-001 specific-repository exception: a single atomic carrier mutation
must write entity + replay record + audit with scope enforcement. Generic CRUD alone does not express that
transaction. No changes to existing generic/shared repositories; all reads/writes filter tenant+LE and IsDeleted=false.

**Shared composition — explicit extra owner approval required before DEV:**

| Existing file (full prefix S as above) | Minimal requested change / reason | Regression boundary |
|---|---|---|
| `S.Api/Program.cs` | Register CarrierRequestContext and Carrier persistence extension; run Carrier middleware for exact Carrier family while running existing ShipmentContextMiddleware for other bundle paths; Carrier-only invalid-model response mapping if necessary | All Shipment auth, scope, errors, startup, outbox and source-intake behavior unchanged; no health claim rewrite |

A CarrierPersistenceRegistration extension under the owned Persistence folder reuses the existing IMongoClient /
IMongoDatabase, registers CarrierRepository and CarrierSchema; no second MongoClient or standalone fallback.
Application assembly scanning already discovers MediatR handlers and validators, so
`S.Application/DependencyInjection.cs` requires no edit. Existing Persistence DependencyInjection,
ShipmentSchema, RequestContext, ShipmentScope, CustomBaseController, ContractError, permission attribute,
ShipmentContextMiddleware and all Shipment/SourceIntake files are read-only. Controller derives from
CustomBaseController but maps Carrier-specific internal Response<T> to frozen wire data/errors without adding
an outer `data` envelope or invoking Shipment context for errors. No changed project reference is assumed;
if needed, stop and version this allowlist. Shared Program.cs is MOD-0183 baseline source: this proposed
exception does not override the user's current no-MOD-0183-source rule.

No frontend, gateway, registry, DCP, shared permission catalog, source adapter, service README, frozen schema,
other service, or `.antigravity` edit is included. Central tracker changes are a CT handoff dependency, not a DEV write.
Single writer for Program.cs must be assigned by CT; no parallel lane touching it is safe.

## 24. Acceptance and failure matrix (future DEV; gated by §22)

Persistence target L3: durable Mongo replica-set transaction, restart/cold-process reload, audit retained.
Proposed indexes: unique `(TenantId,LegalEntityId,CarrierCode)` with ordinal/simple collation; unique
`(TenantId,LegalEntityId,Operation,TargetId,IdempotencyKey)` on replay records; scoped audit lookup.
Retired is not soft delete. Base IsDeleted/DeletedAt persist; deleted fixtures are excluded without exposing delete API.
All actor/time/audit/scope fields are server-set; internal Version is never a request or response field.
EntityBase reuse is read-only. reasonCode belongs in lifecycle audit, not Supplier master.

| AC | Test / expected result |
|---|---|
| C01 surface | Exactly three operations and declared input/output property names; no CRUD copy endpoints; validate every frozen example and local $ref; plain wire responses, internal Response<T> |
| C02 schema | Table §21 exhaustive missing/null/type/unknown-property cases; empty code/name and empty modes rejected; null externalReference accepted; duplicate modes, empty reasonCode and absent optional reference treated exactly per approved decisions; no invented max lengths |
| C03 scope | Two tenants × two LEs with overlapping codes; list/status/replay/audit filter independently; unknown/cross-tenant/cross-LE status target 404 CARRIER_NOT_FOUND, no mutation/no data leak. Spoofed body/query scope rejected; header/claim mismatch fail-closed per GAP-184-04/06 |
| C04 RBAC | Anonymous 401; valid actor without operation permission 403; read cannot create/change, create cannot read/change; grants only supplychain.carriers.read/create/status.change. Require real JWT validation, deny missing/duplicate/malformed tenant/LE/actor claims |
| C05 unique | Same-scope simultaneous creates with distinct keys yield one carrier and one success audit; loser gets approved 409; same exact code in different tenant or LE succeeds. Repeat for retired/deleted-code policy |
| C06 lifecycle | Test all 9 source→target pairs against approved GAP-184-02 table; valid mutation increments internal version and one audit; invalid transition makes no durable changes; reason and actor retained |
| C07 replay | Same key/payload including after process restart returns same carrierId/code/original resulting status with replay=true, no second audit/write; replay an older success after later status changes without overwriting current state. Different payload under same scope/key fails approved 409; test separate targets/operations/scopes and correlation policy |
| C08 concurrent state | Barrier-synchronized distinct-key transitions use atomic current-state/version predicate inside transaction; retry re-reads latest state and revalidates. Outcome must equal a permitted serial history, never blind overwrite or reopening Retired; no client version requirement |
| C09 atomic | Inject failures after entity write, after replay insert, after audit insert and before commit: rollback all three. After commit/before HTTP response loss: retry returns durable original result. Duplicate-key/transaction conflicts resolve through replay/uniqueness logic, never success with missing audit |
| C10 durability | Restart service after successful create/status; GET list and scoped DB evidence retain values and version/audit/replay. Mongo unavailable, transaction unsupported or index creation failure fails closed (no in-memory fallback/no 2xx); response mapping awaits GAP-184-04 |
| C11 correlation/audit | Supplied valid UUID carried unchanged in audit and errors; no generated Carrier lifecycle event. Replay does not rewrite original audit; capture exact headers/body and approved missing/invalid/new-correlation cases |
| C12 protection | Hash manifest proves frozen contracts and MOD-0183 source/evidence untouched except separately authorized exact Program.cs hunk; no Supplier master, shared catalog, ingress, Inventory write, gateway or UI changes |

Tests must measure persisted counts and record content, not only HTTP status or in-memory mocks.
Unique-index enforcement is authoritative; a pre-check is insufficient. No TTL eviction is assumed for replay.
Rollback: stop Carrier exposure, revert only approved Carrier composition registration, preserve Carrier data/audit;
no destructive migration or dropping shared database. Future cleanup requires separate owner approval.
Observability: scoped operation, outcome, correlation, replay/conflict/transaction-failure signals; no tokens,
connection strings or raw externalReference/payload logging. No external performance SLO is invented;
record list size and timings, and escalate pagination/volume needs instead of adding query parameters.

Future commands: service `dotnet build services/Diten.SupplyChainService/Diten.SupplyChainService.sln`,
`dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj`,
`dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`. Use DB-010 test database naming and
isolated fixtures; retain existing MOD-0183 regression suite. Historical architecture 15 PASS / 3 external FAIL
is not a new result or waiver; report fresh failures with owner/baseline comparison. E2+E3+E4 required,
E5/gateway integration remains HELD. Frontend build/browser/RESX/DataTable verifier are N/A for shell:none.

## 25. Phase 1.5 — proposed architectural approval table

This is a filled design review, not an assertion that code exists or Phase 1.5 has been approved.

| # | Check | Plan / disposition |
|---|---|---|
| 1 | All entity fields | Plan: Id/TenantId/base audit/soft-delete/version + LegalEntityId, CarrierCode, DisplayName, SupportedModes, ExternalReference, Status; actor fields server-set; reasonCode and correlation in durable audit. Evet (design), code absent |
| 2 | Global field names | Plan: retain exact frozen carrierCode/displayName/supportedModes/externalReference/targetStatus/reasonCode; no local names. Evet |
| 3 | Isolation/soft delete | Plan: §23 CarrierRepository.cs applies tenant+LE+IsDeleted=false and atomic mutations; unique/replay indexes §24. Evet (proposal); GAP-184-07 open |
| 4 | Entity base | Plan: Domain/Features/Carriers/Carrier.cs inherits existing Domain/Common/EntityBase.cs; tenant owned, no GlobalEntity. Evet |
| 5 | CQRS | Plan: exact command/query/handler/validator paths §23; separate files; four existing pipeline behaviors reused. Evet |
| 6 | Golden decision | N/A: shell:none, golden_reference:none, form_field_count:0, no DataTable. Slim backend naming inspected; no copied CRUD surface |
| 7 | Compact sections | N/A: no UI/Create/Edit/Details/_Form |
| 8 | Required parity | Plan: §21 validators/DTOs match required/null exactly; create three required + nullable optional externalReference, status two required. Web/Razor/progress N/A. GAP-184-03/04 prohibit hidden tightening |
| 9 | Platform lookup | Yok: no PSS lookup. Frozen transport/status enums only; reasonCode has no lookup contract; Supplier boundary preserved |

Shared Program.cs composition, wire-envelope adaptation and specific-repository exception are explicit
approval items in addition to the nine-row table. `/add-module` Phase 1.5 requires explicit user approval
before Phase 2; this PREP does not request or infer that approval prematurely.

## 26. Definition of Ready / dispatch disposition

| SOP §8.1 item | Result | Evidence / remaining gate |
|---|---|---|
| Canonical identity | PASS | DCP-002 exit 0, 2026-09-16; existing MOD-0184, no new/FU ID |
| Owner / owned objects | PASS | §§2/21/23; Supplier excluded |
| Approved module pack | FAIL | status remains draft |
| DCP boundary | PASS bounded | DCP-009; historical status drift is central-owned |
| Required contracts unambiguous | FAIL | GAP-184-02 through 06 |
| Dependencies satisfied/waived | PARTIAL / HELD | GAP-184-01 preparation satisfied by Lane 1; runtime release and GAP-184-08 remain HELD; no waiver |
| Allowed/protected paths | PROPOSED | §23 exact roots + Program.cs exception awaiting approval |
| Repository/branch/base HEAD | PASS snapshot | /Users/natig/Projects/ERP-vNext-recovery; feature/mvp6-logistics; 4a8d4d4b339528a88e6220fb8402e5a2c771136c |
| Dirty baseline | PASS snapshot | Existing untracked continuation plan and continuation-review directory preserved; new Lane 1 report appeared during PREP and was read without editing. Re-enumerate at dispatch |
| Profile and contract flow | PASS design | B backend/contract: caller→validation/auth→handler→atomic persist/audit/replay→response |
| Measurable acceptance | PASS design | C01–C12; external error decisions remain unresolved |
| Persistence/security/evidence | PROPOSED | L3, E4, JWT/RBAC, scoped DB evidence; GAP-184-07 |
| Parallel safety | HELD | No shared-file writer assigned; Program.cs cannot overlap with other DEV/INT lanes |
| Risk / conditional gates | PASS design | HIGH (scope/auth/shared composition/atomic storage); migration, rollback and observability §24; UI/finance/export N/A |
| Entry point and agent inputs | PASS prepared | Versioned DEV/VER documents §27; future DEV @orchestrator /add-module |
| Paste-ready measured prompt | HELD | v1.0 prepared with measured HEAD; must reissue version after decisions/base/implementation evidence change |
| Phase 1.5 approval | HELD | §25 filled; no user approval claimed |

Overall: **PREPARED FOR REVIEW; NOT READY; DEV DISPATCH HELD; VER DISPATCH HELD.**
No module promotion, implementation acceptance, independent verification or CT acceptance is claimed.

## 27. Preparation artifacts / approval scope

- This pack (only existing file modified).
- `docs/roadmap/plans/mod-0184-dev-01-prompt-v1.0.md` (future DEV, HELD).
- `docs/roadmap/plans/mod-0184-ver-01-prompt-v1.0.md` (independent future VER, HELD).
- `docs/records/audits/2026-09/mod-0184-prep-01-approval-v1.0.md` (readiness/approval packet).

Approval must explicitly dispose GAP-184-01…08, accept the bounded API and L3 design, authorize any
shared Program.cs exception with a single writer, approve Phase 1.5, and separately authorize pack promotion
and a versioned runtime dispatch. Merely accepting this preparation report does none of those automatically.

## 28. Runtime gate review v1.0 — current disposition

The current delegated CT gate review is recorded in
[mod-0184-runtime-gate-review-v1.0.md](../../../../docs/records/audits/2026-09/mod-0184-runtime-gate-review-v1.0.md).
This section supersedes the all-open decision status in §§22/25/26, not the preserved PREP proposals.

- GAP-184-02/03/06/07/08: APPROVED as bounded design decisions, with exact semantics and limits in the report.
- GAP-184-04/05: BLOCKED pending contract/security-owner disposition of operation errors, header/trace
  precedence and status-operation409/cross-correlation replay response semantics. No contract amendment inferred.
- §23 exact owned paths and minimal Program.cs composition boundary accepted as design; **no runtime write
  grant** while the gate is blocked. All other existing source remains protected.
- Phase1.5: checks1/2/3/4/5/9 design approved,6/7 N/A,8 blocked; overall HELD. Replay consistency also gated by05.
- DoR: identity/ownership/design scope retained; approved pack and unambiguous contract gates fail;
  dispatch and writer assignment remain HELD. Fresh baseline is in the report; remeasure at future dispatch.
- Status remains **draft**. **DEV NO-GO**. HELD v1.0 prompts preserved; no READY replacement issued.

ASSUMPTION-184-C/D in the report distinguish delegated local design approval from contract-owner authority
and an omitted response from a proven breaking change. Existing ASSUMPTION-184-A/B remain unchanged.

## 29. Effective owner approval and DEV release — 2026-09-17

This section supersedes draft/HELD/proposed status statements in §§1–28 for the bounded
Carrier slice only. Authority: explicit user approvals and external no-additional-consumers
attestation, recorded in docs/records/audits/2026-09/mod-0184-release-and-dev-go-2026-09-17.md.
Canonical SHIPMENT-BUNDLE1.1.0 plus carrier-semantics-v1.1.0.md now govern errors, precedence,
UUID/key validity, exact replay and audit. Old1.0.0 references are historical baseline only.
GAP184-04/05 are resolved by approved publication and verified test-consumer uptake.
Phase1.5 table APPROVED; DoR release APPROVED with fresh dispatch baseline. §§23/24 exact
owned paths, specific atomic repository, wire adaptation and minimal Program.cs exception
are authorized. Sole writer AL-MVP6-MOD0184-DEV02; no concurrent composition writer.
Replay may return historical success receipt before deleted-target lookup exactly as annex;
fresh reads/status exclude deleted targets. No code/name trimming; preserve contract strings.
DEV v2.0 replaces HELD v1.0 for execution. VER begins only after DEV handoff.
Module acceptance remains pending; C01–C12 require fresh runtime proof. No scope beyond Carrier.

## 30. Bounded CT acceptance — 2026-09-18

CT ACCEPTED for the §29 bounded Carrier slice; C01–C12 supported by content-bound independent
E2/E3/E4 evidence. Decision and matrix: docs/records/audits/2026-09/
mvp6-mod0184-ct-accept-02-2026-09-18/README.md and independent-review.md.
This supersedes pending bounded-acceptance statements only. Full-module status is not done;
gateway/shared permissions,UI,E5/G5 and existing external architecture failures remain outside
this acceptance. MOD0185 reference dependency may cite this decision; no downstream DEV grant.
Historical sections and test evidence remain intact.

## 31. Self-registration

Approved: `docs/records/decisions/2026-09/mvp6-self-registration-patches-signoff-owner-decision-01.md`

**Authority:** `docs/records/decisions/2026-09/mvp6-self-registration-design-owner-decision-01.md` (D1–D5 = A; pack preparation only). **Design:** [`mvp6-self-registration-prep-01`](../../../../docs/roadmap/plans/mvp6-self-registration-prep-01/MANIFESTS.md) (MANIFESTS.md, NAV-L10N-KEYS.tsv, TEST-PLAN.md).
**Foundation:** DCP-009 §21 (interface, hosted service, options, `PlatformRegistration` settings via the existing `X-Internal-Api-Key`, project reference and `Program.cs` lines — single integration owner).
This section specifies; it authorizes no code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, and no commit, push or stash.

### Identity (D3 = A)

| Field | Value |
|---|---|
| ModuleCode / ModuleName / DisplayName | `carrier-management` / `CarrierManagement` / `Carrier Management` |
| Domain / Service | `SupplyChainExecution` / `DitenSupplyChainService` |
| ModuleVersion / IsTenantAssignable / IsBaseline | `1.0.0` / true / false |
| SortOrder / Icon (SOFT, seed-once) | 400 / `bx-truck` |
| Provider / tests (proposed paths) | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/CarrierManagementManifestProvider.cs` / `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/CarrierManagementManifestProviderTests.cs` |
| Scope | every RoutePath starts with `/SupplyChain/`, none with `/Platform/` → Tenant |

### Pages

| PageCode | DisplayName | RoutePath | RequiredPermission | Parent | Nav | PageType | Sort |
|---|---|---|---|---|---|---|---|
| `CARRIERS` | Carriers | `/SupplyChain/Carriers` | `supplychain.carriers.read` | — | **true** | List | 10 |

### Actions

| Page | ActionCode | DisplayName | PermissionKey | Placement | Dangerous |
|---|---|---|---|---|---|
| `CARRIERS` | `CREATE` | Create | `supplychain.carriers.create` | Toolbar | no |
| `CARRIERS` | `CHANGE_STATUS` | Change Status | `supplychain.carriers.status.change` | RowAction | no |

All keys are existing constants in `Infrastructure/Features/Carriers/CarrierPermissions.cs`. Route per `mvp6-carrier-ui-scope-01/SCREEN-ROUTE-PERMISSION-MATRIX.tsv`. Identity choices match the unapproved candidate `docs/roadmap/plans/mvp6-carrier-ui-dispatch-close-01/candidates/MODULE-REGISTRATION.patch` (`18001827…`); that candidate and `NAVIGATION-L10N.patch` (`e752b46e…`) remain unapproved and are not adopted by this section.

### Navigation keys (NAV-L10N-KEYS.tsv; 0/7 present today)

| Key | Required languages | Ships with |
|---|---|---|
| `Nav.Domain.SUPPLYCHAINEXECUTION` | en, tr, fr, es, zh, ar, ru | first SupplyChain provider to ship (draft value in the unapproved candidate) |
| `Nav.Module.CARRIERMANAGEMENT` | en, tr, fr, es, zh, ar, ru | this provider (draft value in the unapproved candidate) |
| `Nav.Page.CARRIERS` | en, tr, fr, es, zh, ar, ru | this provider (draft value in the unapproved candidate) |

Values are added to `frontend/Diten.Web/Resources/SharedResource.{lang}.resx` by the integration owner and reviewed by the l10n agent (no empty value, no English placeholder, no key echoing its own name). This pack does not supply or approve values.

### Tests (TEST-PLAN.md §2; all required before the module counts as closed)

| ID | Pass condition |
|---|---|
| M-01 | Identity exactly as above; `IsTenantAssignable` true |
| M-02 | Every manifest `RequiredPermission` / `PermissionKey` is in the reflected `public const string` fields of `CarrierPermissions` |
| M-03 | Every reflected key is in the manifest or on the API-only allow-list with a reason: none expected (every constant is modeled; any addition needs a reason) |
| M-04 | Manifest RoutePaths = the frontend view-route set of the Carrier v2 controller, counts equal (cross-checked by W-01) |
| M-05 | The action table above equals the manifest actions per page (code, key, placement, dangerous flag) |
| M-06 | PageCodes, RoutePaths and ActionCodes (per page) unique, case-insensitive |
| M-07 | Exactly one `IsNavigationVisible` page, with a null parent; every other page has a parent |
| M-08 | No RoutePath starts with `/Platform/` |

Shared guards W-01…W-04 and reconcile-state R-01…R-04 (DCP-009 §21.3) must also be green for this module.

### Ship rule (D4 = A)

The provider, its `AddSingleton<IModuleManifestProvider, …>` line and its navigation keys ship **together with this module's UI** in the integrated target (Q14/Q15), never ahead of it. If Carrier is the first SupplyChain module to ship, the DCP-009 §21 foundation and `Nav.Domain.SUPPLYCHAINEXECUTION` ship with it.

### Open gaps (carried, not solved)

1. The Carrier v2 UI source is not archived in the repository (VER `docs/records/audits/2026-09/mvp6-carrier-ui-ver-01/`, Index `da254157…`); the view-route set is proven only after integration.
2. The candidate's both-direction completeness tests are not yet written (M-02/M-03).
3. Runtime tests R-02…R-04 need a native executor and the integrated target.

## 32. Tenant UI scope — MVP6-CARRIER-UI-SCOPE-01 (approved 2026-10-04)

**Approved** by the owner on 2026-10-04: `docs/records/decisions/2026-10/mvp6-carrier-loads-ui-scope-owner-decision-01.md`. The text below is
the scope package's proposed section (`docs/roadmap/plans/mvp6-carrier-ui-scope-01/PROPOSED-PACK.patch` `93ee76da…7777d`), renumbered from §31
(§31 is now Self-registration) with two recorded changes: the intent sentence in §32.3 (see there) and the gate disposition in §32.5.

This section began as a proposed expansion of the module pack. It does not alter or reopen the bounded backend E4
acceptance in §§29–30. Because UI, gateway and shared permission/catalog work were expressly excluded from that
acceptance, the existing backend readiness remains scoped to §§29–30 and UI dispatch remains HELD pending an exact owner decision,
Phase 1.5 approval, exact pack-delta application and versioned UI dispatch.

### 32.1 Bounded screen and contract surface

The first tenant UI owns only list, create and status change over the published Carrier operations
`queryCarriers`, `createCarrier` and `changeCarrierStatus`. It must not add detail-by-id, edit, delete, bulk,
import/export, lookup, server paging/search/sort, Supplier ownership or a new backend operation. The browser uses
same-origin MVC adapter routes; the adapter's only service egress is Gateway port 5000. It never calls SupplyChain
port 5061 directly.

The create surface has exactly four user fields: required `carrierCode`, required `displayName`, required
`supportedModes`, and optional nullable `externalReference`. Tenant/LE, identity, status, audit and version fields
are server-owned. Therefore `form_field_count: 4`, `shell: tenant` and `golden_reference: slim`. The status row
command's `targetStatus` and `reasonCode` are not create fields. Empty `reasonCode` is valid; no trim, max-length,
case normalization, mode deduplication or other contract tightening is permitted.

### 32.2 UI and permission behavior

- Page route: `/SupplyChain/Carriers`; `data-dt-standard="v2"`; `_LayoutTenantShell.cshtml` consumed unchanged.
- Read: `supplychain.carriers.read`; create: `supplychain.carriers.create`; status:
  `supplychain.carriers.status.change`. Missing action grants remove only that action. Backend authorization remains
  authoritative for every adapter call.
- UAS-001: an authenticated user without read sees only `_AccessDenied`, without title, filter, skeleton, table,
  buttons, toast or redirect. An unauthenticated user uses the standard shell-less 401 surface.
- Seven tenant languages (`en,tr,fr,es,zh,ar,ru`) cover all screen/action/validation/empty/error/replay text.
  Wire enum/error tokens are never translated. Premium SweetAlert2/shared confirmation primitives are used; no
  native alert/confirm.
- The Slim template is deliberately create-only. Because no detail/edit/delete/bulk operation exists, its generic
  checkbox, bulk, edit, delete and by-id quick-view affordances are omitted. The list uses client-side DataTables
  search/order/page; only the optional exact `status` query reaches the backend.

### 32.3 Replay, errors and lifecycle

The UI preserves the normative Carrier annex. One stable idempotency key belongs to one logical create/status
intent. In-flight duplicate submit is suppressed; network loss/500/503 retries the exact body with the same key.
**An intent is one opened create form or status panel, not one payload** (applied 2026-10-04 instead of the scope's "A changed payload is
a new intent and key", per the owner decision above and `cea01354e`/Q403): the key is minted when the form or panel opens and kept across
edits, failures and retries until it closes; only a newly opened form or panel is a new intent. An edit resent while the outcome is unknown
uses the same key, so a committed first attempt answers 409 `IDEMPOTENCY_KEY_REUSED`; the UI then stops, keeps the inputs and never mints a
key to get past it. Measured: `docs/records/audits/2026-10/mvp6-r2-returns-ui-01/evidence/traps-browser.md` §T2 (a per-payload key created
two records from one intent). Replay success uses the original 201/200 body with
`idempotentReplay:true` and the current response correlation header, then reloads the list. It does not infer
current state from a historical receipt.

The UI distinguishes 401/403, safe 404 scope hiding, validation/415, 409 `CARRIER_CODE_CONFLICT`, 409
`IDEMPOTENCY_KEY_REUSED`, 422 `INVALID_CARRIER_TRANSITION`, 500 and 503 without replacing the published status,
code, body or precedence. Active may move to Suspended/Retired; Suspended to Active/Retired; Retired is terminal.
The backend remains authoritative under races. Empty 200 is a normal localized list state, never an auth/error
fallback.

### 32.4 Exact prospective UI-owned paths

The 21 paths in `docs/roadmap/plans/mvp6-carrier-ui-scope-01/OWNED-PATHS.txt` are the complete prospective UI
allowlist. Shared shell, Program.cs, gateway, permission/catalog, module registration and navigation resources are
excluded and protected. The exact gateway route, SupplyChain module/permission provider and navigation localization
are separate single-writer integration changes described in `SHARED-INTEGRATION-HANDOFF.md`.

### 32.5 UI Phase 1.5 and dispatch gates

The UI field/wire mapping, Slim choice, tenant shell, DataTables v2 topology, UAS-001, seven-language resource set,
SweetAlert2 interaction, permission split, acceptance matrix and owned paths are design-complete in
`docs/roadmap/plans/mvp6-carrier-ui-scope-01/`. The remaining gates are:

1. Lane A successor binds and authorizes one immutable source baseline/checkout; the current selection record is
   HELD and is not transfer authority.
2. A single integration owner supplies exact target-bound gateway, permission/catalog/module-registration and
   navigation changes; `_LayoutTenantShell.cshtml` remains unchanged.
3. The owner approves and applies this pack target, closes UI Phase 1.5, and releases a versioned UI DEV prompt.
4. Independent UI VER follows writer-complete and includes composed Gateway 5000 evidence; isolated UI tests do not
   prove gateway integration.

Disposition 2026-10-04: gate 3 closed by the owner decision above; gates 1–2 are answered by the R-4 dispatch (the integrated branch
`feature/mvp6-logistics` is the source baseline; the R-4 lane is the single integration owner; no gateway route is added because C-03's
catch-all already serves the family). **UI DEV released to R-4a. Independent UI VER (gate 4) remains open.**
