---
id: MOD-0028-FU01
name: Documentation Management Backend Contract Foundation
parent: MOD-0028
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0028-fu01-backend-contract-foundation
started: 2026-06-15
target: 2026-06-22
form_field_count: 0
---

# MOD-0028-FU01 - Documentation Management Backend Contract Foundation

## 1. Module Summary

MOD-0028-FU01 is a narrow, backend-only follow-up to `MOD-0028 Documentation Management`. It responds to the
`MOD0028-P0-INSPECT-V230-ALIGNMENT` Wave 1 verdict of **BLOCKED** by defining the first safe contract foundation
for `Diten.Platform`. It does not authorize full MOD-0028 implementation.

The Wave 1 blockers addressed or bounded by this pack are:

- no `api/v1/document-management` route/controller family;
- no MOD-0028 runtime permission mapping;
- no response-envelope `reason_code` and body-level `correlation_id` contract;
- parent-pack base-type drift from the live `TenantScopedEntity` convention;
- no MOD-0028 feature-flag, audit/correlation, NL-01, or deferred-scope foundation;
- an existing MOD-0220 validation adapter whose availability and exact consumer contract remain unconfirmed;
- no Gateway coverage for `api/v1/document-management`.

### Approval Scope

- The user approved the FU01 backend contract foundation scope.
- Approval is limited to FU01 only; full MOD-0028 implementation remains prohibited.
- No business aggregate, CRUD, frontend UI, company provisioning, template, exception, or local-node implementation
  is approved.
- FU02, QMS folder baseline import, and TenantShell UI are not approved by this pack.
- Implementation may start only for the exact FU01 scope and must stop/report when a controlled gate below cannot
  be satisfied inside the authorized repo scope.

## 2. Ownership and Boundaries

### In scope

- MOD-0028 API route-family and thin-controller conventions under `api/v1/document-management`.
- One real, read-only contract-discovery action at `GET api/v1/document-management/contract`; fake success,
  empty business-list, and placeholder CRUD actions are prohibited.
- Backward-compatible `Response<T>` support for controlled `reason_code` and response-body `correlation_id`.
- Tenant-owned persistence base decision using live `TenantScopedEntity` conventions.
- Minimum FU01 permission registration, alias, policy, and enforcement strategy.
- Registration and lookup strategy for the seven MOD-0028 feature flags.
- MOD-0220 LegalEntity validation seam confirmation, without company provisioning.
- MOD-0021 audit emit pattern, correlation propagation, and future mutation hook conventions.
- Gateway route requirements and a separate integration-agent handoff when routing is required.
- Focused contract, security, compatibility, and configuration tests.

### Explicitly out of scope

- Full MOD-0028 implementation or business CRUD.
- Baseline Catalog UI or any TenantShell governance UI.
- Company provisioning, reconciliation, or provisioning jobs.
- Collection tree workspace or local-node management.
- Collection-definition, baseline-release, corporate-root, or collection-instance persistence and workflows.
- Template master, template version, template variant, drift, or rebase implementation.
- Exception request, approval, decision, queue, closure, or expiry implementation.
- MOD-0029 controlled-document lifecycle.
- MOD-0030 retention or legal-hold enforcement.
- MOD-0031 evidence-pack assembly/export.
- Binary upload/download or repository implementation.
- Runtime activation of `POSITION` or `PERSON` scope.
- Frontend permission gates, navigation, pages, JavaScript, localization, or DataTables.

## 3. Owned Objects

FU01 owns no MOD-0028 business aggregate. It owns only the following backend foundation contracts:

- `DocumentManagementRoutes` or the repository-equivalent route constant/convention.
- `DocumentManagementReasonCodes` for controlled MOD-0028 failures.
- `DocumentManagementFeatureFlags` registration and lookup contract.
- A module-local response model for the selected read-only contract-discovery action.
- Minimum permission mapping records for the contract permission and five future-compatibility permissions.
- Contract tests proving route, envelope, authorization, feature-flag, tenant, audit, and correlation behavior.

FU01 must not introduce `CollectionDefinition`, `BaselineRelease`, `CorporateDocumentationRoot`,
`CollectionInstance`, or any other parent-pack business entity.

## 4. Entity Fields

No business entity is created by FU01.

For later MOD-0028 persisted entities, the confirmed live Platform convention is:

| Concern | FU01 decision |
|---|---|
| Base type | `Diten.Platform.Common.Persistence.TenantScopedEntity` or an explicitly confirmed canonical equivalent |
| TenantId | Inherited/server-resolved; never accepted in request DTOs, forms, routes, or query parameters |
| Soft delete | `IsDeleted` is inherited; `DeletedAt` remains mandatory on governed entities because the live common base does not currently provide it |
| Concurrency | Inherited technical `Version`; business version fields must use semantic names |
| Audit actor | Server-resolved from current actor when a later business entity requires `CreatedBy`/`UpdatedBy` |
| Indexing | Every future repository/index begins with `TenantId`; soft-deleted rows are excluded from active uniqueness |

FU01 does not modify `Diten.Platform.Common` to add `DeletedAt`, does not create a module-specific base class, and
does not persist a contract-discovery response.

## 5. Repo Scope

### Authorized future implementation scope after promotion

- `services/Diten.Platform/src/Diten.Platform.API/**` for route/controller and controlled response metadata wiring.
- `services/Diten.Platform/src/Diten.Platform.Application/**` for contract models, permission constants,
  feature-flag interfaces, reason codes, and audit metadata conventions.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` for configuration binding, DI, approved alias
  registration, and MOD-0220 adapter reuse/confirmation.
- `services/Diten.Platform/tests/**` for focused FU01 contract and security tests.

### Separately governed integration scope

- `gateway/Diten.ApiGateway/**/ocelot.json` only through an explicit `integration-agent` task after the backend
  route contract is fixed.
- Permission seed changes outside `Diten.Platform` only through MOD-0018/security ownership approval.

No frontend path is in scope.

## 6. Protected Paths

- `.antigravity/**`
- `frontend/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` except through the separate integration-agent task
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` except through a separately approved MOD-0018/security-owned permission task
- `services/Diten.MdmService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- MOD-0029, MOD-0030, and MOD-0031 implementation files
- Audit store/query internals, workflow engine internals, retention engine internals, evidence export, and binary
  repository internals
- Parent pack `MOD-0028-document-management.md` unless a separate governance reconciliation explicitly authorizes
  its update

## 7. Dependencies

| Dependency | FU01 usage |
|---|---|
| MOD-0018 | Approves lowercase canonical permission keys, uppercase spec aliases, seed ownership, and effective mapping |
| MOD-0021 | Provides `AuditBehavior`, `IAuditService`, audit outbox, and event persistence; FU01 defines consumption hooks only |
| MOD-0028 parent | Supplies ownership, API family, failure semantics, and later business-wave boundaries |
| MOD-0032 / Gateway | Owns route hardening and any Ocelot integration task |
| MOD-0220 | Supplies LegalEntity lookup-validation semantics; FU01 confirms the existing consumer seam only |
| Platform Common | Supplies `TenantScopedEntity`, tenant context, tenant repository filtering, and correlation middleware |

The current `ILegalEntityReferenceValidator` / `MdmLegalEntityReferenceValidator` pattern is the preferred
consumer seam. FU01 must verify matching GUID, active/referenceable state, tenant-header propagation, bearer
forwarding, cancellation preservation, and fail-closed behavior before declaring it reusable for MOD-0028.

## 8. Runtime Constraints

- Persistence remains MongoDB with tenant isolation on every future MOD-0028 record.
- `TenantScopedEntity` is the FU01 base decision; the parent pack's provisional `BaseEntity` wording does not
  authorize use of `Diten.Platform.Domain.Common.BaseEntity`.
- No client-controlled `TenantId`, technical `Version`, audit actor, or correlation identity is accepted.
- Correlation header propagation alone is insufficient: every FU01 API envelope includes body-level
  `correlation_id`.
- Controlled failures include stable `reason_code`; internal exceptions and stack traces are never returned.
- Cross-tenant detail behavior is 404 non-leakage; unauthorized mutations are 403; restricted lists omit rows.
- The route/controller foundation must not return fake success, a fabricated readiness state, or an empty business
  collection that implies implementation exists.
- The selected contract-discovery action derives its result from live configuration and registered capabilities,
  is read-only, performs no database write or MOD-0220 call, and exposes no secrets or dependency identifiers.
- `POSITION` and `PERSON` remain disabled and no background process may create records for them.
- No company provisioning, reconciliation, or business persistence is allowed in FU01.

Feature flags and required defaults:

| Key | Default |
|---|---|
| `mod0028.corporate_root.enabled` | on |
| `mod0028.company_provisioning.enabled` | on |
| `mod0028.manual_local_nodes.enabled` | on |
| `mod0028.exceptions.enabled` | off until live API and UI exist |
| `mod0028.position_scope.enabled` | off |
| `mod0028.person_scope.enabled` | off |
| `mod0028.workflow_integration.enabled` | off until MOD-0023 integration is approved |

Feature-flag provider decision:

- Reuse the existing Platform configuration/options pattern discovered during implementation.
- If no general feature-flag provider exists, FU01 uses minimum typed options plus configuration binding; it does
  not introduce a wider feature-management framework.
- Keys and defaults are centralized in `DocumentManagementFeatureFlags` and typed options. Scattered string
  literals are prohibited.
- Missing configuration resolves to the defaults above; invalid configuration fails startup validation rather
  than silently enabling a deferred feature.

## 9. Layout & Shell Contract

- `shell: none` is intentional because FU01 contains no frontend surface.
- No `.cshtml`, controller view action, menu item, navigation entry, or JavaScript file is authorized.
- The parent module remains tenant-facing and must use `Layout = "_LayoutTenantShell";` in later UI waves.
- PlatformAdminShell remains limited to entitlement, enablement, bootstrap, operations/support, and read-only
  diagnostics; FU01 adds none of those pages.

## 10. Backend File Convention

FU01 follows the live Diten.Platform CQRS shape when an action is implemented:

```text
Features/DocumentManagementContract/
|-- Queries/
|-- Handlers/
|   `-- QueryHandlers/
|-- Validators/
`-- DocumentManagementContractModels.cs
```

- Query types are sealed records.
- Handlers are sealed classes without `QueryHandler` suffix.
- Controllers inherit `CustomBaseController`, remain thin, and dispatch through MediatR.
- No raw `HttpClient` is used in handlers.
- No repository or business command is introduced by FU01.
- FU01 implements exactly one callable action: `GET api/v1/document-management/contract`.
- The action requires `platform.document-management.contract.view`, returns real registered contract state, and
  must not masquerade as business readiness.
- It returns no business collection, performs no persistence, and does not call MOD-0220.

Contract response fields:

| Field | Required value/behavior |
|---|---|
| `module_id` | `MOD-0028-FU01` |
| `module_name` | `Documentation Management Backend Contract Foundation` |
| `parent_module_id` | `MOD-0028` |
| `api_family` | `api/v1/document-management` |
| `contract_version` | Explicit FU01 contract version |
| `active_scopes` | `CORPORATE`, `COMPANY` |
| `deferred_scopes` | `POSITION`, `PERSON` |
| `feature_flags` | Summary derived from live typed options, without secrets |
| `permissions_required_for_fu01` | Contract permission plus five foundation permissions |
| `mod0220_validator_registered` | `true` or `false` based on DI registration inspection only |
| `audit_correlation_ready` | `true` only when the required runtime seams are registered |
| `business_capability_status` | `FOUNDATION_ONLY` |
| `warning` | `This endpoint does not indicate full MOD-0028 business readiness.` |

The response uses the Platform `Response<T>` envelope and includes body-level `correlation_id`.

## 11. Frontend File Contract

No frontend files are in scope. `golden_reference: none` and `form_field_count: 0` are intentional.

Future MOD-0028 governance UI remains governed by the parent pack's Compact/TenantShell contract and requires a
separate approved follow-up.

## 12. Validation Rules

| Contract input | Required | Rule | Failure |
|---|---|---|---|
| Correlation header | No | Accept only the existing safe character/length policy; otherwise generate server-side | Controlled response still contains generated `correlation_id` |
| Reason code | Controlled failure only | Non-empty stable uppercase code from approved catalog; never raw exception text | Contract test failure |
| Feature flag key | Yes for lookup | Exact registered key; no dynamic arbitrary-key lookup | `FEATURE_FLAG_UNKNOWN` |
| Scope request | Conditional | Only `CORPORATE` and `COMPANY` may be runtime-active | `FEATURE_DISABLED` for `POSITION`/`PERSON` |
| LegalEntityId | Seam test only | Non-empty GUID; response ID must match; ACTIVE and referenceable | Fail closed without persistence |
| TenantId | Never client input | Resolved from tenant context only | Request contract rejected/test fails |

The contract endpoint has no request body, business identifier, tenant override, or database input.

## 13. Failure Path to Verify

- **Unknown route or unimplemented business action:** 404; never fabricated success.
- **Controlled validation failure:** 400 with `reason_code`, `correlation_id`, and no stack trace.
- **Missing permission:** 403 `PERM_DENIED`; no handler side effect and no success audit event.
- **Cross-tenant detail identifier:** 404 `NOT_FOUND_NON_LEAKAGE`; no restricted identifier in response or logs.
- **Disabled POSITION/PERSON scope:** 400 `FEATURE_DISABLED`; no entity/job is created.
- **MOD-0220 unavailable, malformed, mismatched, inactive, or non-referenceable:** fail closed with the approved
  dependency reason code; caller cancellation remains cancellation.
- **Stale technical version in a later mutation contract:** 409 `CONFLICT`; silent overwrite is prohibited.
- **Audit enqueue failure for a future critical mutation:** use the existing MOD-0021 critical-category policy;
  FU01 tests the hook contract but performs no business mutation.

## 14. Authorization Convention

- Policy: `[Authorize]` for any tenant-facing MOD-0028 API controller.
- Actor type: `tenant_user`.
- Runtime canonical format: PKS-001 lowercase dotted keys under
  `platform.document-management.{resource}.{action}`.
- Spec keys remain traceable aliases only if MOD-0018/security approves directional canonical-to-alias mapping.
- Backend attributes and future frontend gates must use the same lowercase effective key.

Minimum FU01 permission subset:

| Spec key | Selected runtime canonical key | FU01 status |
|---|---|---|
| FU01 contract foundation | `platform.document-management.contract.view` | selected and enforced by FU01 |
| `MOD0028.COLLECTION_DEFINITION.LIST` | `platform.document-management.collection-definitions.list` | approval required |
| `MOD0028.COLLECTION_DEFINITION.VIEW` | `platform.document-management.collection-definitions.view` | approval required |
| `MOD0028.BASELINE_RELEASE.LIST` | `platform.document-management.baseline-releases.list` | approval required |
| `MOD0028.CORPORATE_ROOT.INITIALIZE` | `platform.document-management.corporate-root.initialize` | approval required |
| `MOD0028.COLLECTION_INSTANCE.VIEW` | `platform.document-management.collection-instances.view` | approval required |

Permission strategy:

- MOD-0018/security approves the lowercase keys before runtime use.
- Permission seeds are added only in the canonical security-owned seed location through a separately authorized
  security task when that location is outside FU01 repo scope.
- The contract endpoint uses `[HasPermission("platform.document-management.contract.view")]`.
- The other five keys are seed/registry/alias foundation only because FU01 contains no corresponding business
  endpoints.
- If uppercase spec keys must remain operational, the approved mapping direction is spec key to lowercase runtime
  key. The runtime alias implementation must preserve the repository's canonical-to-alias lookup convention while
  producing that effective resolution; reverse grants and dynamic aliases are prohibited.
- Any future frontend implementation uses the same lowercase effective keys.
- FU01 may not claim a permission as `confirmed` until seed, backend policy/attribute, alias behavior, and focused
  tests agree. The other 21 parent permissions remain `missing` and outside FU01.
- Missing contract permission returns 403 with `PERM_DENIED` and body/header correlation parity.

## 15. Gateway / API Routing Decision

Decision: a Gateway route is required before any browser or frontend consumer can call the MOD-0028 API family.

- Required upstream/downstream family: `/api/v1/document-management` and
  `/api/v1/document-management/{everything}`.
- Required methods cover the approved backend actions and `OPTIONS`; FU01 itself authorizes at most GET for the
  selected contract-discovery action.
- `gateway/Diten.ApiGateway/**/ocelot.json` remains protected and is changed only by a separate integration-agent
  task after backend routes are fixed.
- Gateway acceptance includes root and catch-all routing, correlation-header preservation, authorization-header
  forwarding, and controlled 404 behavior.
- Frontend must use Gateway port `5000` or a same-origin proxy and must never call `5057` directly.

After the backend contract endpoint is complete, a separate integration-agent task must add and verify:

- `/api/v1/document-management`;
- `/api/v1/document-management/{everything}`;
- `GET` and `OPTIONS`;
- Authorization header forwarding;
- correlation header forwarding;
- controlled 404 behavior for unknown paths.

## 16. Acceptance Criteria

- [x] FU01 is `status: approved` for the exact backend contract foundation scope only.
- [ ] Scope remains limited to backend contract foundation; no full MOD-0028 implementation is introduced.
- [ ] No frontend governance UI, view, JavaScript, navigation, or localization file is included.
- [ ] No business CRUD, business aggregate, repository, seed data, or fake success endpoint is included.
- [ ] Any approved callable action uses the `api/v1/document-management` family, thin controller,
  `CustomBaseController`, and MediatR.
- [ ] `GET api/v1/document-management/contract` is the only FU01 endpoint and returns the defined live foundation
  response with `business_capability_status: FOUNDATION_ONLY` and the required warning.
- [ ] The contract endpoint performs no database write, returns no business collection, and makes no MOD-0220 call.
- [ ] `Response<T>` compatibility strategy supplies body-level `reason_code` and `correlation_id` without breaking
  existing consumers; global serialization impact is covered by regression tests.
- [ ] Controlled error responses include `reason_code`.
- [ ] Every FU01 response includes body-level `correlation_id`; response header and body values are identical.
- [ ] Internal stack traces and exception details are absent from controlled responses.
- [ ] Future tenant-owned MOD-0028 entities use `TenantScopedEntity` or a confirmed canonical equivalent;
  `TenantId` is never client-controlled.
- [ ] Soft-delete, `DeletedAt`, tenant-first indexes, and technical concurrency requirements are recorded and tested
  when the first persisted entity is introduced.
- [ ] The six-permission FU01 subset has an approved seed/alias/policy mapping; backend and future frontend use the
  same effective lowercase key.
- [ ] The contract endpoint enforces
  `[HasPermission("platform.document-management.contract.view")]`; a missing permission returns 403
  `PERM_DENIED`.
- [ ] The remaining 21 parent permissions stay explicitly outside FU01.
- [ ] All seven feature flags are registered with the specified defaults; POSITION and PERSON are demonstrably off.
- [ ] `MdmLegalEntityReferenceValidator` reuse is confirmed by contract evidence or remains an explicit blocker.
- [ ] The contract endpoint reports only whether the MOD-0220 validator is registered; it does not validate a Legal
  Entity or disclose dependency details.
- [ ] MOD-0220 failure behavior is fail-closed and caller cancellation is preserved.
- [ ] Audit emit and correlation hook points are defined for future mutations without implementing the full event
  catalog.
- [ ] Controlled failures expose a traceable correlation ID in both response header and body.
- [ ] Gateway route need is explicit and any route change is assigned to a separate integration-agent task.
- [ ] Direct full MOD-0028 coding remains prohibited.

## 17. Test Expectations

- Build `services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj`.
- Run the relevant Diten.Platform application, API contract, authorization, audit, and infrastructure tests.
- Response serialization regression tests for successful and failed existing endpoints after optional envelope fields
  are introduced.
- Controlled error tests for `reason_code`, body/header `correlation_id`, and stack-trace absence.
- Route/controller reflection or integration tests for the exact approved route family.
- Permission tests for all six FU01 keys, including canonical match, approved alias match, missing claim, and no
  reverse/dynamic alias behavior.
- Feature-flag registration/default tests, including POSITION/PERSON disabled behavior.
- Tenant contract tests proving no request DTO exposes `TenantId`.
- NL-01 tests for 404 detail non-leakage and 403 mutation denial when the first semantic action exists.
- MOD-0220 adapter tests for success, non-success, malformed JSON, ID mismatch, inactive/non-referenceable result,
  tenant-header propagation, bearer forwarding, timeout/network failure, and caller cancellation.
- Audit/correlation seam tests proving future auditable commands must provide metadata and propagate correlation.
- Gateway route smoke through port `5000` only after the separate integration-agent task.
- `git diff --check` and protected-path verification.

No frontend build, DataTable verifier, RESX parity, or browser UI smoke is required because FU01 has no frontend.

## 18. Ready-for-dev Checklist

- [x] User reviews this pack and explicitly approves only FU01 backend-foundation scope.
- [ ] Parent reference `MOD-0028` and follow-up identity `MOD-0028-FU01` are recorded in the registry.
- [ ] Registry/DCP-002 preflight is run when the required tooling is available. If unavailable, report it without
  expanding scope or inventing identity data. **CONTROLLED GATE**
- [ ] The Enterprise Architect records the parent MOD-0028 canonical-name/alias decision. This remains a parent-level
  governance issue and does not authorize full MOD-0028; FU01 foundation may proceed only while the accepted
  `MOD-0028-FU01` identity remains unambiguous. **CONTROLLED GATE**
- [x] `entity_base: TenantScopedEntity` is accepted as the live Platform tenant convention; FU01 creates no
  persisted entity, and parent-pack drift must be reconciled before a later persistence wave.
- [x] FU01 selects `GET api/v1/document-management/contract` with an exact read-only, foundation-only response
  contract; fake business readiness and persistence are prohibited.
- [x] Response strategy selected: backward-compatible optional `ReasonCode` and `CorrelationId` members on Platform
  `Response<T>`, subject to global-impact approval and regression tests.
- [x] Six-key permission subset selected, including
  `platform.document-management.contract.view` for the enforced endpoint.
- [x] Feature-flag strategy selected: existing configuration/options pattern, or minimum typed options binding when
  no provider exists; centralized constants and fail-safe defaults are mandatory.
- [x] MOD-0220 seam selected: existing `ILegalEntityReferenceValidator` / `MdmLegalEntityReferenceValidator`;
  endpoint reports DI registration only and performs no validation call.
- [x] Gateway route work is deferred to a separate integration-agent task after the backend endpoint exists.
- [ ] MOD-0018/security approval is confirmed for the six lowercase keys and any uppercase spec aliases. FU01 may
  implement only locally authorized Platform constants/attributes and must stop/report if seed ownership requires a
  protected security-owned path. **CONTROLLED GATE**
- [ ] Permission seed ownership and the separate security task, if required, are identified. **CONTROLLED GATE**
- [ ] Backward-compatible global `Response<T>` optional fields pass existing serialization regression tests. Stop
  implementation and report a separate response-envelope follow-up if compatibility breaks. **CONTROLLED GATE**
- [ ] MOD-0220 owner confirmation is recorded when required by governance. FU01 remains limited to DI
  registration/status reporting and performs no company provisioning or real LegalEntity business operation.
  **CONTROLLED GATE**
- [ ] Existing `MdmLegalEntityReferenceValidator` registration/reusability is confirmed or the exact gap is reported;
  no company behavior is added. **CONTROLLED GATE**
- [ ] Implementation inspection records the existing configuration/options registration location, or FU01 uses the
  minimum safe typed-options pattern inside allowed scope. **CONTROLLED GATE**
- [ ] Gateway integration-agent task is created after the callable backend endpoint exists; `ocelot.json` remains
  outside FU01. **CONTROLLED GATE**
- [x] FU01 test matrix and protected paths are accepted for the approved narrow scope.
- [x] User promoted the pack to `approved` before runtime implementation.

## 19. Implementation Notes

- The selected response strategy is to add optional `ReasonCode` and `CorrelationId` members to the existing
  Platform `Response<T>` envelope while preserving current fields and success serialization. Success responses may
  leave `ReasonCode` null; every FU01 response sets `CorrelationId`, and controlled failures require both fields.
  Because this affects every Platform API response, implementation requires explicit compatibility approval and
  regression coverage. If inspection shows unacceptable global risk, implementation stops and opens a separate
  Platform response-envelope follow-up rather than introducing an incompatible FU01 workaround.
- Correlation middleware already resolves and returns the correlation header. FU01 must source body-level
  correlation from the same request-scoped context, never from client payloads.
- The live Platform common persistence model uses `TenantScopedEntity : BaseEntity`; it includes `TenantId`,
  `IsDeleted`, and technical `Version`, but not `DeletedAt`. FU01 records this gap without modifying Platform.Common.
- `MdmLegalEntityReferenceValidator` already uses a typed client, bearer forwarding, tenant propagation, active and
  referenceable checks, mismatch rejection, fail-closed mapping, and cancellation preservation. Company
  provisioning remains outside FU01 even if the seam is confirmed. The contract endpoint checks registration only;
  adapter behavior remains covered by separate unit/integration tests.
- The route family is not proof that business capability exists. No endpoint may return an empty list or generic
  success solely to make Gateway smoke pass.
- Audit event catalog completion belongs to later semantic waves. FU01 only fixes the pattern and mandatory metadata
  contract that later mutation commands must implement.
- Parent pack status does not expand FU01 authority. This follow-up is executable only within its approved backend
  contract foundation boundaries and controlled stop/report gates.

### Approved Implementation Handoff

- Next executable action: the orchestrator may implement FU01 only.
- Allowed endpoint: `GET api/v1/document-management/contract`.
- Allowed permission: `platform.document-management.contract.view`.
- Allowed response change: backward-compatible optional `ReasonCode` and `CorrelationId` members.
- Allowed feature-flag foundation: centralized constants and typed options/configuration only.
- Allowed MOD-0220 behavior: DI registration/status reporting only; no remote validation call from the endpoint.
- Gateway `ocelot.json` remains out of scope and requires a separate integration-agent task.
- Frontend remains out of scope.
- Full MOD-0028, FU02, QMS folder baseline import, business CRUD, company provisioning, and TenantShell UI remain out
  of scope.

## 20. Follow-up Items

1. **FU02 or bounded Wave 3 - Corporate governance core:** collection definitions, corporate root, baseline releases,
   immutable manifest, persistence, and semantic permissions.
2. **Company adoption follow-up:** MOD-0220-bound collection instances, provisioning, reconciliation, and jobs.
3. **TenantShell UI follow-up:** baseline catalog, instantiation, provisioning status, and company tree viewer.
4. **Local governance follow-up:** local nodes and exception request/detail/queue/expiry.
5. **Template governance follow-up:** masters, immutable versions, variants, drift, and rebase.
6. **Release inspection follow-up:** complete audit catalog, NL-01 matrix, accessibility, observability, security,
   Gateway, and release gates.

Each follow-up requires its own approved or ready-for-dev scope. FU01 does not authorize any later wave.
