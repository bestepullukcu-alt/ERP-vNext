---
id: MOD-0290-FU03
name: Product Legal Entity Scope Assignment
domain: master-data-management
service: Diten.MdmService
shell: tenant
golden_reference: slim
entity_base: EntityBase
status: in-progress
owner: product-data-owner / master-data-owner
branch: feature/mdm/mod-0290-product-identity-final-integration
started: 2026-08-26
target: 2026-09-30
form_field_count: 3
parent: MOD-0290
canonical_blueprint: "docs/System Capability & Implementation Blueprint - master 8.1.xlsx::Blueprint_Data!A291:AG291"
---

# MOD-0290-FU03 — Product Legal Entity Scope Assignment

> **Named-step implementation in progress.** Sections 5 A-G, H1a and the default-disabled H1b runtime foundation are
> implemented and verified under their separately approved code-start gates. H1a was also exercised locally and
> left the pilot tenant in `Preparation`; it did not classify products or activate enforcement. H1b supplies the
> tenant writer lease, activation/suspension fences, canonical readiness evidence and operation 14/15 audit-pending
> read-back, but no H1b operational action has been invoked. Real Legal Entity inventory, explicit operational
> parameters, service-token/grant provisioning, live central receipt proof and user acceptance remain separate gates.
> Gateway changes are unnecessary for the implemented nested routes; WorkCenter, Production/Staging
> enablement and push remain outside this pack's current authority. DCP-002 proves this exact FU child against parent
> `MOD-0290`.

## 1. Module Summary

This follow-up defines an explicit tenant-owned Product Legal Entity scope policy. The Phase 1.5-approved architecture
uses one `ProductLegalEntityScopePolicy` aggregate document per Global Product rather than separately mutable policy
and assignment documents. Its embedded periods hold full Legal Entity snapshots.
It answers where a product definition is available for business use; it does not claim ownership, manufacture,
supply, MAH, seller, sponsor, or regulatory roles.

The three product-scope states are distinct and must never be inferred from one another:

- **LegacyUnclassified:** no scope-policy record exists. Absence is not group-wide.
- **GroupWide:** the current embedded period explicitly targets the tenant group and has an empty Legal Entity set.
- **Scoped:** the current embedded period carries a sorted, unique, non-empty full Legal Entity ID snapshot.

Deployment does not activate enforcement. Preparation and Enforced operational states, completeness read-back,
and rollback rules are separate gates. This pack is `in-progress`. MOD-0018-FU21 now supplies the trusted
multi-Legal-Entity provider endpoint and MOD-0018-FU22 supplies the special Auth profile. FU03's B-step durable
persistence and audit-delivery regression are complete; its exact D-step MDM consumer adapter, later C-step API,
named-step code-start and operational rollout remain separately gated.

## 2. Ownership and Boundaries

- **MOD-0290 owns:** the Global Product scope-policy aggregate, embedded period facts, temporal rules, and product-consumer
  evaluation seam.
- **MOD-0220 owns:** Legal Entity identity, lifecycle and same-tenant referenceability. FU03 only consumes its
  existing MDM read contract.
- **MOD-0018-FU21 / Platform-Security owns:** the implemented trusted multi-Legal-Entity candidate resolver and
  delegated-Bearer plus dedicated-S2S internal transport. FU03 may consume it through the exact D-step MDM adapter;
  it cannot modify, reproduce or bypass the Platform mechanism.
- **MOD-0288 owns:** Organization Unit, Position and Position Assignment. Organization Unit targeting is excluded
  from the LegalEntity-only MVP.
- **MOD-0018-FU22 / Auth owns:** the implemented exact six-key catalog/grant profile, Admin-read/Viewer-none defaults,
  dedicated Steward/Auditor/RolloutOperator matrices and source-scoped reconciliation. FU03 owns only its manifest
  declarations and `[HasPermission]` enforcement; it cannot create grants or assign users to responsibility roles.
- **Integration agent owns:** Ocelot routing. This pack makes no Gateway edit.
- **Audit owner owns:** aggregate discriminator, operation vocabulary, durable intent delivery and retention rules.
- **Out of scope:** manufacturer, supplier, seller, sponsor, MAH, Registered Presentation, Market Supply
  Assignment, PV verbatim-intake blocking, WorkCenter submit/approve/reject, and Production enablement.

The Platform System Tenant module-access exception is not a product row-scope bypass. A scope-management
permission authorizes management operations; it does not grant consumer access to every scoped product.

## 3. Owned Objects

Control-tower recommendation, subject to section 18 owner gates:

- `ProductLegalEntityScopePolicy`: one aggregate document per tenant/product containing ordered embedded
  `ScopePeriods`, audit intent/receipt evidence and an `int` expected version.
- `ProductLegalEntityScopePeriod`: embedded history item with `Mode = GroupWide | Scoped` and a complete Legal
  Entity snapshot; it is not independently addressable persistence.
- `ProductLegalEntityScopeRolloutState`: one tenant-scoped operational state with `Preparation`, `Enforced` or
  `FailClosedSuspended`.
- `IProductLegalEntityScopePolicyRepository` and `IProductLegalEntityScopeRolloutStateRepository`: authoritative
  single-document persistence boundaries.
- `IProductLegalEntityScopeEvaluator`: returns facts plus a reasoned effective decision; it does not persist
  visibility/selectability/mutability/downstream-usage booleans.
- Commands/queries for atomic initial policy, full-set replace, end, policy/history/effective reads, create options,
  completeness and state-gated activation/suspension. There is no assignment-row CRUD.
- API under `/api/global-products/{globalProductId}/legal-entity-scope-policy`.
- Navigation-hidden tenant management page `MasterDataManagement/ProductLegalEntityScopes` only after
  policy, permission and transport gates close.

No nullable target, fake `GMG Group` Legal Entity, `absence = group-wide` fallback, or scope copy into
Revision/GSKU/LSKU/Finished Good is allowed.

## 4. Entity Fields

### Phase 1.5-approved aggregate document and owner-approved numeric bounds

| Field | Type | Required | Rule / source |
|---|---|---:|---|
| `Id` | `Guid` | server | Technical identity. |
| `TenantId` | `Guid` | server | Trusted server tenant; never accepted from payload. |
| `GlobalProductId` | `Guid` | yes | Current-tenant, non-deleted Global Product. |
| `CreationCommandId` | `Guid` | server/request identity | Stable normalized idempotency identity. |
| `ScopePeriods` | embedded list | yes | Ordered, bounded, non-overlapping history; exactly zero or one current period. |
| `AuditIntents` / `AuditReceipts` | embedded evidence | server | Durable intent/receipt facts; not `EntityBase` timestamps. |
| `Version` | `int` | server | Existing `EntityBase` code-truth expected-version type. |
| technical lifecycle fields | inherited | server | `EntityBase` tenancy/soft-delete/created/updated fields only. |

### Embedded `ScopePeriod`

| Field | Type | Required | Rule / source |
|---|---|---:|---|
| `PeriodId` | `Guid` | server | Immutable period identity. |
| `Mode` | `ProductLegalEntityScopeMode` | yes | `GroupWide` or `Scoped`. |
| `LegalEntityIds` | sorted unique `Guid` list | conditional | GroupWide empty; Scoped contains the complete snapshot and at least one ID. |
| `EffectiveFromUtc` | `DateTimeOffset` | yes | Inclusive UTC start. |
| `EffectiveToUtc` | `DateTimeOffset?` | no | Exclusive UTC end; null is open-ended. |
| `CommandId` | `Guid` | yes | Idempotency identity of the create/replace/end fact. |
| `ActorId` | `Guid` | server | Trusted authenticated actor. |
| `CreatedAtUtc` | `DateTimeOffset` | server | Trusted server time. |

Initial/create, full-set replace and end are atomic expected-version updates of one aggregate document. Foundation
uses server time; backdating and future planning are deferred. Unique no-reuse identities are
`(TenantId, GlobalProductId)` and `(TenantId, CreationCommandId)`. Approved hard limits are exactly 200 Legal Entity
IDs per Scoped snapshot, 100 embedded periods per policy document and 1,048,576 serialized BSON bytes per policy
document. Values are never truncated. No tenant may raise these limits and the MVP exposes no tenant-specific limit
configuration. Closed historical Scoped periods may coexist with a current GroupWide period; only
overlapping/current-period ambiguity is invalid.

## 5. Repo Scope

This section is an A-H control-tower allow-list. A later code-start may authorize only one named step at a time.
Every path is exact; directory and wildcard authorization is forbidden.

### A — Domain aggregate and rollout contracts

Planned runtime paths:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductLegalEntityScopeMode.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductLegalEntityScopeRolloutMode.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopePolicy.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopePolicyRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeRolloutStateRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/IProductLegalEntityScopeEvaluator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeEvaluator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeModels.cs`

Planned test path:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeDomainTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeRolloutTests.cs`

### B — Single-document persistence, idempotency and audit delivery

Planned/runtime paths:

- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopePolicyRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/AuditAggregateType.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductAuditOperation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IAuditIntentDeliveryRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/AuditIntentDeliveryRepository.cs`

Planned/existing test paths:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeRolloutMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`

Exact append-only audit vocabulary, frozen against current code truth (`AuditAggregateType` values 1..6 and
`ProductAuditOperation` values 1..10 remain unchanged):

| Enum | Exact additive member | Numeric value |
|---|---|---:|
| `AuditAggregateType` | `ProductLegalEntityScopePolicy` | 7 |
| `AuditAggregateType` | `ProductLegalEntityScopeRolloutState` | 8 |
| `ProductAuditOperation` | `ProductLegalEntityScopePolicyCreated` | 11 |
| `ProductAuditOperation` | `ProductLegalEntityScopePolicyReplaced` | 12 |
| `ProductAuditOperation` | `ProductLegalEntityScopePolicyEnded` | 13 |
| `ProductAuditOperation` | `ProductLegalEntityScopeEnforcementActivated` | 14 |
| `ProductAuditOperation` | `ProductLegalEntityScopeEnforcementSuspended` | 15 |

Values 14 and 15 reserve the H-step vocabulary but grant no H runtime authority. B may only append the frozen enum
members and extend existing discovery/claim/fencing/retry/dead-letter/acknowledgement/compaction handling for the two
new aggregate types. Delivery bookkeeping must not increment the policy or rollout business `Version`.

The recommended write contract is exact replay for the same command and payload, deterministic `409` for payload
drift, and single-document expected-version updates. Persistence may classify only `MongoConnectionException`,
`MongoExecutionTimeoutException` and `MongoWriteConcernException` as ambiguous. On an ambiguous outcome, an exact
tenant/product/command/payload/version reread may recover success; missing or mismatched proof returns non-success
`202 PRODUCT_SCOPE_RECONCILIATION_REQUIRED`. Cancellation, argument, serialization and programming errors
propagate to the global pipeline.

Implemented code truth on 2026-08-27 reserves `65,536` bytes for audit-delivery lifecycle bookkeeping: a business
write must serialize to at most `983,040` bytes and every claim/acknowledgement/compaction mutation remains atomically
guarded by the absolute `1,048,576`-byte document ceiling. Policy create requires exact operation 11 evidence;
replace/end require exact operation 12/13 evidence. Rollout `Preparation` creation is the sole zero-intent bootstrap
because no rollout-created operation is authorized; later rollout writes require exact operation 14/15 evidence.
Same-intent immutable drift is rejected and ambiguous success requires exact persisted business plus audit proof.

### C — Aggregate CQRS, API, state-gated rollout and create options (after D)

Execution order is deliberately `A -> B -> D -> C`. D must first supply the bounded trusted candidate provider and
one-batch local Legal Entity intersection. C cannot introduce a second provider abstraction, an unbounded tenant-wide
scan, per-ID/N+1 validation or an in-memory `Take(200)` workaround.

Planned/runtime paths:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeModels.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/CreateProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/ReplaceProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/EndProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Queries/GetProductLegalEntityScopePolicyQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Queries/GetProductLegalEntityScopeHistoryQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Queries/GetEffectiveProductLegalEntityScopeQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Queries/GetProductLegalEntityScopeCreateOptionsQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Queries/GetProductLegalEntityScopeCompletenessQuery.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/CommandHandlers/CreateProductLegalEntityScopePolicyHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/CommandHandlers/ReplaceProductLegalEntityScopePolicyHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/CommandHandlers/EndProductLegalEntityScopePolicyHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/QueryHandlers/GetProductLegalEntityScopePolicyHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/QueryHandlers/GetProductLegalEntityScopeHistoryHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/QueryHandlers/GetEffectiveProductLegalEntityScopeHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/QueryHandlers/GetProductLegalEntityScopeCreateOptionsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Handlers/QueryHandlers/GetProductLegalEntityScopeCompletenessHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/CreateProductLegalEntityScopePolicyValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/ReplaceProductLegalEntityScopePolicyValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/EndProductLegalEntityScopePolicyValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/GetProductLegalEntityScopePolicyValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/GetProductLegalEntityScopeHistoryValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/GetEffectiveProductLegalEntityScopeValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/GetProductLegalEntityScopeCreateOptionsValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Validators/GetProductLegalEntityScopeCompletenessValidator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopePolicyRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopePolicyRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductLegalEntityScopesController.cs`

Planned test paths:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCommandTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeReconciliationTests.cs`

Activation/suspension commands, handlers, validators and operator invocation are not authorized in C. They remain
an H-step pack amendment after the rollout owner freezes the exact operator surface, fencing and rollback contract.
The reconciled C contract is frozen as follows:

- Create, replace and end are allowed only while a persisted rollout state is `Preparation`; `Enforced` and
  `FailClosedSuspended` return deterministic `409`.
- Missing rollout state is `Uninitialized` and fail-closed for mutation. Read handlers never create or initialize it.
- `GetEffective...` is a management view of rollout/current-period facts, not a consumer allow/deny decision. Actual
  consumer decisions begin in E/F with each resource operation's real permission.
- Completeness is internal/read-only, has no public controller route and is not H activation proof. Its inventory is
  all current-tenant, non-deleted, non-`Retired` Global Products, read with exact bounded inventory/count methods on
  the allow-listed Global Product and policy repositories; paging loops, N+1 and materializing the full tenant are
  forbidden.
- Command identity is one canonical non-empty GUID transported only in the `Idempotency-Key` header. Tenant, actor,
  effective dates, audit facts and technical evidence are server-derived and absent from public bodies.
- Create-options requires the server-side conjunction `configure` + `global-products.read` + `legal-entities.read`.
- Create returns `201`; exact replay returns the same sanitized result/status. Replace and end return `200`; exact
  replay returns the same sanitized result/status. Drift, stale version, temporal conflict and BSON limits return
  `409`; only repository `WriteOutcomeAmbiguous` becomes non-success
  `202 PRODUCT_SCOPE_RECONCILIATION_REQUIRED`.

### D — FU21 trusted multi-Legal-Entity MDM consumer adapter

Provider code truth is complete under MOD-0018-FU21. FU21 implements
`POST /api/internal/v1/access-governance/legal-entity-scope/resolve`, bounded multi-value candidate resolution,
delegated-Bearer validation, a dedicated pair-bound S2S credential, strict parsing, a two-second budget and
deterministic tenant-scope restoration. The user accepted that implementation on 2026-08-26. Dedicated Development
secret provisioning/rotation and live operational smoke remain separate FU21 gates; committed secret/config values
are forbidden.

FU03 may implement only the following MDM consumer paths after a separate exact D-step code-start:

Runtime paths:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeCandidateFacade.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/TrustedLegalEntityScopeProviderOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILegalEntityRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`

Test paths:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/PlatformTrustedLegalEntityScopeProviderClientTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeDelegatedTokenForwardingTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/Authorization/TrustedLegalEntityScopeProviderDependencyInjectionTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCandidateFacadeTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/InMemoryLegalEntityRepository.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityReferenceValidationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LegalEntityMongoRoundTripTests.cs`

Exact consumer contract inherited from FU21:

- Endpoint is exactly `POST /api/internal/v1/access-governance/legal-entity-scope/resolve`.
- Request body contains only exact `module_code` and `permission_key`; tenant ID, user/subject ID and Legal Entity
  IDs are forbidden.
- MDM forwards the delegated Bearer and presents a dedicated rotatable S2S credential. Platform independently
  validates one tenant, one subject, `tenant_user`, permission, service and audience.
- MDM sends exactly one delegated Bearer plus `X-Legal-Entity-Scope-Credential-Id`,
  `X-Legal-Entity-Scope-Credential` and
  `X-Legal-Entity-Scope-Audience: TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE`. The dedicated client rejects multiple/missing
  Bearer values, disables redirects, redacts sensitive headers and never uses a tenant-propagation handler.
- A successful response contains a bounded, sorted, distinct candidate `LegalEntityIds` set plus echoed tenant,
  subject, module, permission and evaluated-at time. `200` with an empty set is authoritative confirmed empty.
- `400`, `401` and `403` remain client/auth failures. An over-bound, duplicate, unsorted or inconsistent Platform
  response is `409` at the provider boundary and maps to fail-closed MDM `503`; Platform unavailable is `503`, the
  two-second budget maps to `504`, and cancellation propagates.
- Memoization is request-scope only; there is no cross-request cache.
- Platform's candidate resolver must not call MDM. MDM locally revalidates every candidate against current-tenant,
  active/referenceable Legal Entity code truth, preventing a Platform-to-MDM callback loop.
- FU21 fixes the provider maximum at 200 IDs and the strict body limit at 1,024 bytes. The collection may be
  authoritatively empty, but every returned ID must be non-empty; MDM rejects over-bound, unsorted, duplicate or
  echoed-context-mismatched payloads before policy evaluation.
- MDM caps the raw provider response at exactly `32,768` bytes before JSON parsing. Declared or streamed overflow,
  non-JSON/HTML, duplicate or additional JSON properties and invalid success/failure envelopes fail closed as `503`.
- `ILegalEntityRepository` may gain only a bounded batch referenceability read for the supplied IDs. The candidate
  facade intersects provider candidates with same-tenant, active, non-deleted MDM facts in one batch; per-ID/N+1
  reads and an unbounded tenant-wide Legal Entity scan are forbidden.
- Provider lookup may be memoized once per request and exact module/permission pair. List/detail/selector handlers
  must reuse the request decision and may not call Platform once per product row.
- JWT Legal Entity claims and a signed scope token are rejected for the foundation. Browser tenant/Legal Entity
  headers remain forbidden.

Exact MDM configuration section: `TrustedLegalEntityScopeProvider`. It contains only `PlatformBaseAddress`,
`Timeout`, `CredentialIdentifier` and `CredentialSecret`; environment names therefore use the exact
`TrustedLegalEntityScopeProvider__*` prefix. Timeout is positive and no greater than two seconds. Audience, module and
the provider's 30 allowed pairs are code-truth constants, not weakenable MDM configuration. No value or secret is
committed. `LegalEntityMongoRoundTripTests` must be converted in its already-listed path from skippable/per-run GUID
database use to the fixed shared database, serialized collection, fresh-tenant isolation and tenant-owned cleanup.

Exact D consumer mapping:

| Provider/client result | MDM result |
|---|---:|
| Valid `200`, including an authoritative empty candidate set | 200 |
| Local or Platform request-contract failure | 400 |
| Missing/invalid delegated Bearer or Platform 401 | 401 |
| Platform permission/service denial | 403 |
| Platform 404/409, malformed/over-bound/echo-mismatched success, missing config or unavailable/unexpected status | 503 |
| Platform/local two-second budget expiry | 504 |
| Caller cancellation | propagate unchanged |

The module is always `product-item-sku-master`. Management read/history/effective-facts/completeness use
`mdm.product-legal-entity-scopes.read`; create/create-options use `mdm.product-legal-entity-scopes.configure`;
replace uses `mdm.product-legal-entity-scopes.replace`; end uses `mdm.product-legal-entity-scopes.end`. A D facade memo
key is exact current tenant + trusted subject + module + permission and lives only for the request scope. Management
permissions never become generic consumer row-access permissions.

Read-only provider code-truth evidence remains owned by FU21 and is not writable under FU03. The MDM adapter must not
reuse the verified GSKU/Market credential, accept tenant/subject/Legal Entity IDs from the browser, weaken the exact
 30-pair provider allow-list, add cross-request caching, or call Platform from inside a Platform callback.

### E — Global Product consumer enforcement

Runtime paths, authorized only after D and preparation completeness gates:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GlobalProductRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGlobalProductSelectorHandler.cs`

Test paths:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductApiMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeConsumerTests.cs`

### F — Descendant and ABB consumer enforcement

No assignment is copied. Effective scope is derived as follows:

- `GSKU -> ProductDefinitionRevision -> GlobalProduct -> scope policy`
- `LSKU -> GSKU -> ProductDefinitionRevision -> GlobalProduct -> scope policy`
- `FinishedGood -> GSKU -> ProductDefinitionRevision -> GlobalProduct -> scope policy`
- `ABB -> GlobalProduct -> scope policy`

Missing, deleted or cross-tenant links deny without disclosure. Exact runtime paths:

The effective decision is mandatory for every list, detail, selector, create-options, create, update and lifecycle
mutation listed in this step. Mutation handlers must evaluate scope before code reservation, ordinal allocation,
reservation consume/binding, audit intent creation or any business write begins. Idempotent replay and reconciliation
read-back must re-evaluate the current trusted scope and cannot return an existing record as a scope bypass. A
missing, deleted or cross-tenant parent anywhere in the derivation chain returns the same non-leaking denial.

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IGskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/ILskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IFinishedGoodRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductDefinitionRevisionRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/GskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LskuRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/FinishedGoodRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskusHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetGskuCreateOptionsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskusHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskuByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetLskuCreateOptionsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodsHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodByIdHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/QueryHandlers/GetFinishedGoodGskuSelectorHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFirstGskuDraftFacadeHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFirstGskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/UpdateGskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateLskuDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/QueryHandlers/GetProductAbbreviationByGlobalProductHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/QueryHandlers/ResolveProductAbbreviationHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/QueryHandlers/GetProductAbbreviationAllocationEvidenceHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/RequestProductAbbreviationAllocationHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/CancelProductAbbreviationAllocationHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/InitiateProductAbbreviationCorrectionHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/ApproveProductAbbreviationAllocationHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/RejectProductAbbreviationAllocationHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/RequestProductAbbreviationRetirementHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/ApproveProductAbbreviationRetirementHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Handlers/CommandHandlers/RejectProductAbbreviationRetirementHandler.cs`

Test paths:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterFacadeTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuRegisterQueryTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodApiContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationApiContractTests.cs`

#### F1 — Product identity lifecycle current-scope hardening

Standing user non-push authorization on 2026-08-30 closes the measured lifecycle bypass. Every public submit/start and
direct-retire handler must resolve the exact lifecycle permission, derive the current Global Product through the
tenant-safe repository chain and evaluate the existing consumer guard before processor entry, audit creation, business
write or replay read-back. Broken/deleted/cross-tenant parent chains and denied decisions use the same non-disclosing
resource `404`; provider/tenant failures retain the existing fail-closed status mapping.

Exact runtime allow-list:

- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartGlobalProductIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartFirstGskuIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartLskuIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Workflow/Handlers/CommandHandlers/StartFinishedGoodIdentityWorkflowHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGlobalProductIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireGskuIdentityPairHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireLskuIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/Handlers/CommandHandlers/RetireFinishedGoodIdentityHandler.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FirstGskuIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodIdentityWorkflowRecoveryRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementRecoveryRunner.cs`

Exact test allow-list:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductIdentityLifecycleScopeEnforcementTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductLifecycleUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductIdentityWorkflowRecoveryWorkerMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodIdentityWorkflowRecoveryRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FirstGskuIdentityLifecycleUnitTests.cs`

Background recovery has no delegated human Bearer and may not assert the stored maker subject through a service-only
bypass. Missing rollout and `Preparation` retain the existing recovery behavior. `Enforced`,
`FailClosedSuspended`, malformed or unreadable rollout state defers without calling a scope-sensitive processor; a
fresh interactive replay with the original actor, delegated Bearer, exact lifecycle permission and current scope may
continue. Tokens and candidate sets are never persisted. Configuration, secrets, data, Production/Staging, commit and
push remain outside this amendment.

Implementation evidence on 2026-08-30: MDM API and test-project Release builds passed with zero errors; the exact
lifecycle/scope/recovery matrix passed **59/59** with zero skipped, and the existing real-Mongo Global Product recovery
worker regression passed **1/1**. The matrix locks all eight exact submit/retire permission-to-scope orderings, all
five background recovery gates, executable `Enforced -> AwaitingMakerReplay` behavior for GSKU retirement and
executable Enforced fail-closed results for the four API recovery runners. `git diff --check` passed. No config,
secret, data, Production/Staging, commit or push operation was performed.

### G — Manifest, permissions, Gateway and Golden Slim UI

MDM manifest paths:

- `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ProductItemSkuMasterManifestProviderTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ModuleRegistration/ModuleRegistrationHostedServiceTests.cs`

Permission/default-role/entitlement runtime allow-list under FU03 remains `none`; MOD-0018-FU22 already owns and
implements the exact special profile. This G step may only declare the nav-hidden MDM page and exact six descriptors;
catalog/grant reconciliation, responsibility-role membership and token refresh remain separate operations. Gateway
runtime allow-list under FU03 is `none`: the existing `/api/global-products/{everything}` GET/POST/OPTIONS route
already reaches MDM `5059`, including the nested policy routes. `ocelot.json` remains protected. `POST .../end` is
selected instead of PATCH. The exact `ModuleCode` is `product-item-sku-master`.

Planned frontend paths:

- `frontend/Diten.Web/Controllers/ProductLegalEntityScopesController.cs`
- `frontend/Diten.Web/Models/ProductLegalEntityScopes/ProductLegalEntityScopeViewModels.cs`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/Index.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/_Filter.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/_DataTable.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/_IndexL10n.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/_CreateEditOffcanvas.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/_DetailsQuickView.cshtml`
- `frontend/Diten.Web/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.cs`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/ProductLegalEntityScopes/index.js`
- `frontend/Diten.Web/wwwroot/assets/js/MasterDataManagement/ProductLegalEntityScopes/index.l10n.js`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.en.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.fr.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.es.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.zh.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.ar.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.ru.resx`
- `frontend/Diten.Web/Resources/Views/MasterDataManagement/ProductLegalEntityScopes/ProductLegalEntityScopesIndex.tr.resx`
- `frontend/Diten.Web/tests/product-legal-entity-scopes.test.js`

### H — Migration, activation and live smoke

The read-only H preflight found a runtime gap: the repository can persist `Preparation`, `Enforced` and
`FailClosedSuspended`, but there is no supported operator entry point for tenant-scoped bootstrap/readiness or a
safe activation/suspension orchestration. H is therefore split into two separately gated steps. This planning
amendment grants no runtime code-start or operational-run authority.

#### H1a — Development rollout inspect and Preparation bootstrap foundation

H1a may add one exact CLI surface, and no internal/public HTTP controller:

- exact argument: `--run-product-legal-entity-scope-operational`;
- default-disabled configuration section: `ProductLegalEntityScope:Operational`;
- environment: `Development` only, rejected before tenant/repository resolution in every other environment;
- the H1a slice itself implements only `Inspect` and `BootstrapPreparation`; the separately gated H1b slice may
  add only the exact `ActivateEnforced` and `SuspendFailClosed` actions frozen below;
- aliases, casing variants and argument-value shortcuts remain unauthorized;
- the runner is explicit one-shot code and never implements `IHostedService` or runs during normal startup.

`Inspect` is read-only. It reports persisted rollout ID/mode/version or `Uninitialized`; reuses
`IGlobalProductRepository.GetProductLegalEntityScopeCompletenessInventoryAsync` for exact eligible/configured counts
and a maximum-200 missing-product diagnostic sample; and uses one dedicated read-only operational projection for
exact descendant-integrity and scope-audit facts. It must not claim `IsActivationReady` while H1b runtime and the
audit-owner central consumer remain open.

The descendant report covers non-deleted records through these exact chains:

- `ProductDefinitionRevision -> GlobalProduct`;
- `GSKU -> ProductDefinitionRevision -> GlobalProduct`;
- `LSKU -> GSKU -> ProductDefinitionRevision -> GlobalProduct`;
- `FinishedGood -> GSKU -> ProductDefinitionRevision -> GlobalProduct`;
- ABB register, allocation-ledger and history entries -> `GlobalProduct`.

Every category returns exact total/orphan counts, at most 200 deterministic orphan IDs and an explicit `HasMore`
flag. Missing, deleted and cross-tenant parents are reported as non-disclosing orphan categories; no foreign-tenant
identity is returned. The dedicated projection is required because existing entity repositories expose CRUD/page or
consumer derivation, not bounded cross-collection integrity counts. It adds no collection, index, migration, write
path or second completeness implementation.

The raw audit report is limited to aggregate types `ProductLegalEntityScopePolicy` and
`ProductLegalEntityScopeRolloutState`. It reports exact Pending/Processing/Delivered/DeadLetter and compacted-receipt
counts plus bounded malformed/unacknowledged samples. It does not decide whether those facts are sufficient for
activation and does not claim, acknowledge, compact or otherwise mutate an audit intent.

`BootstrapPreparation` requires exact non-empty `TenantId`, `ActorId` and `CommandId` supplied through process
configuration. It sets the scoped server tenant before resolving tenant-bound repositories, calls only the existing
rollout repository `CreateAsync`, and requires exact persisted read-back. Missing state creates one `Preparation`
record. Same command plus same actor replays the same business facts. A different command/actor, or an existing
non-Preparation state, fails closed. It never creates or changes a product policy, classifies a product, creates an
audit intent, assigns a role, enables navigation or starts a service.

Exact H1a runtime allow-list:

- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/ProductLegalEntityScopeOperationalOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeOperationalReadinessRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeOperationalReadinessRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`

Exact H1a test allow-list:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalReadinessMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalDependencyInjectionTests.cs`

H1a failure contract:

| Condition | Required result |
|---|---|
| Non-Development environment | `PRODUCT_SCOPE_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED`; no tenant/repository resolution or mutation. |
| Disabled, missing or malformed H1a configuration | `PRODUCT_SCOPE_OPERATIONAL_CONFIGURATION_INVALID`; no mutation. |
| Action other than exact `Inspect` or `BootstrapPreparation` | `PRODUCT_SCOPE_OPERATIONAL_ACTION_NOT_AUTHORIZED`; no mutation. |
| Inspect with no rollout state | Successful read-only report with `Uninitialized`; no implicit bootstrap. |
| Inspect evidence cannot be read consistently | `PRODUCT_SCOPE_OPERATIONAL_READINESS_UNAVAILABLE`; no partial success claim. |
| Existing Preparation with same command and actor | Idempotent replay with exact persisted state/readiness read-back. |
| Existing state has different command/actor or is not Preparation | `PRODUCT_SCOPE_ROLLOUT_BOOTSTRAP_CONFLICT`; no replacement. |
| Ambiguous create with exact persisted replay proof | Success with the original persisted state; no duplicate. |
| Ambiguous create without exact proof | `PRODUCT_SCOPE_RECONCILIATION_REQUIRED`; no false success. |
| Caller cancellation | Propagates unchanged. |

#### H1b — Enforced activation, fail-closed suspension and live acceptance

H1b Phase 1.5 owner decisions, exact allow-list and default-disabled runtime foundation are implemented. Status is
`BLOCKED_OPERATIONAL_PREREQUISITES`: no operation 14/15 invocation, activation/suspension execution, public endpoint,
data migration or live run occurred. The earlier statement that the audit-owner provider and MDM consumer were
missing is superseded by the 2026-08-28 G4 and H1b code-truth evidence below; operational configuration, tenant
grant/credential, real inventory approval and live receipt proof remain separate gates:

1. **Audit acknowledgement:** exact provider prerequisite is `MOD-0021-FU01 Trusted Durable Source Audit Intent
   Ingestion`; exact MDM consumer prerequisite is the base MOD-0290 named G4 step `Trusted Durable Audit Intent
   Delivery Consumer Foundation`. Both default-disabled foundations are now implemented in the current integration
   worktree: provider prerequisite evidence is `68/68` focused and `639/639` full Platform; MDM consumer evidence is
   `41/41` focused and `678/678` full MDM, with zero skipped. No worker enablement, credential/tenant-grant
   provisioning or operational run occurred. The control-tower rule remains: all prior product-scope policy intents are
   centrally acknowledged/compacted; the CAS atomically persists operation 14 as a local Pending intent; operational
   completion is claimed only after that intent receives durable central acknowledgement. The runner must never
   forge an acknowledgement.
2. **Zero-delta/write quiescence:** two cross-collection reads cannot close the final-read-to-rollout-CAS race. Local
   Development may use an explicitly evidenced write-quiescence window plus repeated exact inventory proof. A
   Production design requires a cross-aggregate write fence or maintenance lease and is outside H1a.
3. **Blocking evidence scope:** the owner-confirmed activation boundary is exactly aggregate types 7/8 and the seven
   descendant categories already emitted by H1a. No other audit aggregate or relationship is silently inferred.
4. **Transition contract:** the exact expected rollout-state ID/version, deterministic fence, bounded reason,
   immutable inventory snapshot, replay and read-back semantics are frozen below. Operational invocation remains a
   separate gate even after the default-disabled runtime foundation is implemented.

H1b Phase 1.5 control-tower contract (implemented as a default-disabled runtime foundation; operational execution remains open):

- The existing singleton tenant rollout document is the sole admission-control authority. It gains one bounded
  technical active-writer lease and one activation fence; no new collection, index, transaction subsystem or audit
  operation is introduced. Lease/fence takeover or automatic expiry is forbidden. A crash remains durable and
  fail-closed; only exact same-command recovery may resume it.
- Every inventory-affecting mutation must acquire the writer lease while the rollout is `Preparation`, no activation
  fence exists and no other writer lease is active. This includes Global Product reservation/create,
  Revision/GSKU combined create, GSKU update, LSKU create, Finished Good create, all eight ABB mutations and all
  three product-scope policy mutations. Release requires the exact fencing token and persisted business read-back.
  A contract test must fail when a new mutation command is introduced without this admission guard.
- Activation transitions the admission state `Open -> Closing -> Quiesced`. The activator first acquires the fence
  by exact rollout ID/mode/version/command CAS. New writers are then denied; any existing writer lease blocks
  activation. Two equal canonical inventory snapshots are diagnostic evidence, while safety comes from the fence.
- The server-owned immutable snapshot contains rollout facts, eligible/configured/missing Product counts and hash,
  policy ID/version/period hash, all seven descendant total/orphan counts and relationship hashes, type 7/8 audit
  status/count/hash facts, UTC timestamp, schema version and final SHA-256. User-supplied counts or hashes are never
  trusted. Bounded samples are diagnostics only and are not hash inputs.
- The activation CAS must match the exact tenant, rollout ID, expected `Preparation` mode/version, deterministic
  fence, command, actor, bounded reason and evidence hash. One CAS sets `Enforced`, increments the business version,
  clears the fence and appends exactly one operation 14 `Pending` intent. Exact replay returns the same persisted
  facts; same-command drift is `409`; ambiguous writes require exact read-back or reconciliation.
- `Enforced` starts fail-closed consumer enforcement, but operational completion remains `AuditPending` until the
  operation 14 intent has an exact compacted central acknowledgement receipt. The runner must not call audit claim,
  delivery, acknowledgement or compaction methods and must never fabricate central acknowledgement.
- Suspension uses the same fence model and one operation 15 intent. `FailClosedSuspended` denies consumer access;
  it never returns to tenant-wide access or auto-classifies a product. Completion again requires the exact compacted
  central receipt.
- Audit blockers are limited to `ProductLegalEntityScopePolicy` (type 7) and
  `ProductLegalEntityScopeRolloutState` (type 8). Before activation, every prior type-7 intent must be represented by
  an exact, activation-grade compacted central receipt. Pending, Processing, Delivered-but-not-compacted,
  DeadLetter, malformed, unacknowledged, idempotency drift or evidence drift blocks activation.
- Enforced Global Product reserve/create is denied in this MVP. A future additive flow may atomically onboard a new
  Product together with its initial scope policy, but silently creating `LegacyUnclassified` products after
  activation is forbidden.

The H1b Phase 1.5 architecture decisions and exact runtime code-start were accepted on 2026-08-28. The implemented
foundation freezes the tenant-level
single-writer lease, activation/suspension fence, conservative 18-command inventory, type 7/8 audit boundary,
fail-closed recovery and the Enforced Global Product create prohibition. It does not authorize an operational
activation/suspension run, credential/grant provisioning, configuration or data mutation.

##### H1b exact executable facts — code-ready planning contract

The existing `ProductLegalEntityScope:Operational` section remains the single configuration surface. It is
Development-only and default-disabled. The exact case-sensitive actions are `Inspect`, `BootstrapPreparation`,
`ActivateEnforced` and `SuspendFailClosed`; aliases, casing variants, multiple operational arguments and action
values in command-line arguments are rejected. `ActivateEnforced` and `SuspendFailClosed` require `Enabled=true`,
non-empty `TenantId`, `ActorId`, `CommandId`, `ExpectedRolloutStateId`, non-negative `ExpectedRolloutVersion` and one
`ReasonCode`. `ReasonCode` is trimmed, ASCII-only, 1..64 characters and matches
`^[A-Z0-9][A-Z0-9._-]{0,63}$`. These facts are exact replay identity; no value comes from an HTTP body, tenant
override or browser. `Inspect` and `BootstrapPreparation` retain their implemented H1a contracts.

The tenant writer lease is embedded in the singleton rollout document and has these immutable facts: exact tenant,
rollout ID, command ID, actor ID, one of the 18 frozen mutation kinds, payload fingerprint, random opaque token,
strictly increasing positive generation, exact owner `Diten.MDM:<mutation-kind>`, UTC acquired time and UTC expiry.
The lease duration is exactly 120 seconds in code; it is not configurable and no tenant may raise it. There is no
automatic expiry cleanup, takeover or different-command recovery. While unexpired, exact same command/actor/kind/
payload re-entry returns the same token and generation. After expiry, the durable lease remains fail-closed and
requires exact same-command reconciliation; it is never treated as free. A stale, forged or prior-generation token
cannot write or release. Missing rollout retains the pre-FU03 legacy behavior; a persisted rollout makes admission
mandatory.

The activation/suspension fence is also embedded. Its deterministic uppercase SHA-256 token is computed over this
exact UTF-8/LF record, with lowercase canonical `D` GUIDs, invariant decimal integers, uppercase action/reason and a
final LF: `tenantId`, `rolloutStateId`, `expectedVersion`, `action`, `commandId`, `actorId`, `reasonCode`. The fence
stores those facts plus `Open -> Closing -> Quiesced`, acquired UTC time and the two stable fact hashes. It has no
expiry or takeover. Exact same-command recovery may resume; any drift or another command fails closed. Program.cs
must preserve the existing G4 worker registration, audit-temporal CLI exclusion and normal-startup behavior while
adding no hosted H1b path.

The canonical inventory projection is streamed in bounded `_id` order; it never materializes an unbounded tenant
inventory. Canonical GUIDs are lowercase `D`, enums are their frozen invariant decimal values, UTC instants are UTC
ticks, hashes are uppercase SHA-256 hex and counts/versions are invariant decimal. Each logical record is UTF-8
`key=value` followed by LF, keys occur in the fixed order below, values cannot contain CR/LF, and the payload ends
with one LF:

1. schema version `1`; tenant, rollout ID/mode/version; action, command, actor and base64-encoded normalized
   `ReasonCode`; observed UTC ticks;
2. eligible/configured/missing Global Product counts, the sorted eligible-product fact hash and sorted missing-ID
   hash;
3. policy count and one hash over sorted tuples of Global Product ID, policy ID, policy version and full ordered
   period hash. A period hash includes period ID, numeric mode, sorted Legal Entity IDs, from/to UTC ticks, command,
   actor and created UTC ticks; it covers history, not only the current period;
4. exactly these seven categories in this order: `ProductDefinitionRevision`, `Gsku`, `Lsku`, `FinishedGood`,
   `ProductAbbreviationRegister`, `ProductAbbreviationAllocationLedger`, `ProductAbbreviationHistory`. Each carries
   total count, orphan count and a hash over sorted source-to-parent relationship tuples. Diagnostic samples and
   `HasMore` are excluded from every hash;
5. audit types `7` then `8`, each with Pending/Processing/Delivered/DeadLetter/malformed/unacknowledged/compacted-
   receipt counts plus a sorted immutable intent/central-receipt fact hash. Receipt facts include aggregate and
   intent IDs, source/local and central idempotency keys, central acknowledgement, contract version, acknowledged/
   delivered/compacted UTC ticks, compact receipt reference and evidence hash. Final `audit_events` presence is not
   read or inferred.

`StableFactsHash` is SHA-256 over the same canonical payload with only `observedUtcTicks` omitted. Two consecutive
snapshots under the persisted `Closing` fence must have the same `StableFactsHash`; their timestamps and complete
`SnapshotHash` may differ. `SnapshotHash` is SHA-256 over the complete second payload and that immutable second
snapshot is persisted. Activation rejects negative/incoherent counts, duplicate or malformed facts, unbounded scans,
missing hashes, any orphan/missing product, or any type-7/8 Pending, Processing, Delivered-but-not-compacted,
DeadLetter, malformed, unacknowledged or receipt drift. Emergency suspension still acquires the deterministic fence,
quiesces writers and persists the same readable canonical evidence, but readable adverse classification/orphan/audit
facts do not block the restrictive transition to `FailClosedSuspended`. Structurally malformed or unreadable
evidence fails closed by leaving the already restrictive `Enforced` state unchanged. Operation 15 remains
`Pending`/`AuditPending` until its central receipt arrives. Final central `audit_events` polling remains forbidden.

The write-fence behavior is registered after validation and before the existing audit-forwarding behavior so it
wraps the complete handler, nested MediatR command, reconciliation and persisted read-back. The outer command owns
the lease; the GSKU facade and its inner command reuse the exact ambient tenant/token/generation context, while a
direct inner invocation acquires its own lease. Validation or admission rejection occurs before `next` and performs
no release mutation.

After acquiring the technical lease but before calling `next`, the coordinator captures a bounded canonical
`PreWriteStateHash` under that lease and binds it by token/generation CAS. The hash covers all current-tenant,
non-deleted owned business identities/versions/relationships, code-reservation state and only the command-owned
immutable local intent facts that any of the 18 command families can append: intent/tenant/aggregate identities,
operation/version/actor/correlation/causation/command/sequence/timestamp, evidence, snapshot reference and local
idempotency key. Delivery-owned attempt/state/lease/error fields, central acknowledgement/idempotency, compacted
receipts and delivery timestamps are explicitly excluded because the independently running G4 worker may advance
them while a business writer lease is held. Rollout technical lease/fence timestamps and token are also excluded. It
uses the same ordinal UTF-8/LF/incremental SHA-256 conventions as the activation snapshot, fixed collection order and
stable `_id` cursors. The lease remains `Initializing` and no handler starts until that binding succeeds, closing the
read-to-admission race.

An exact successful response is the handler's persisted read-back proof and releases with the current token/
generation. A deterministic unsuccessful response after `next` triggers one bounded authoritative recapture while
the same lease still excludes other writers: exact equality with `PreWriteStateHash` proves zero delta and releases;
any difference, unreadable/incomplete proof or drift retains the lease. `202`, ambiguous/reconciliation-required,
thrown exception, caller cancellation and missing/incoherent success proof always retain it. Exact same-command
replay may release after either successful persisted read-back or the same zero-delta proof. Real-Mongo tests must
prove a deterministic no-write failure releases, while injected reservation/business/audit partial mutation and
ambiguous outcomes retain. A deterministic concurrent test must advance G4 acknowledgement/compaction/receipt facts
during a no-write business failure and still prove safe release, while an injected new local immutable intent must
retain. No elapsed timeout, finally block or best-effort cleanup releases a lease.

Before applying a source-mode precondition, both operational actions first perform exact command/fence/evidence
replay recognition and read the persisted operation-14/15 receipt. Therefore a same-command replay after the mode
already changed can move from `AuditPending` to exact completed read-back; payload drift remains `409`. A new
activation CAS requires `Preparation`, exact ID/version/fence/command/actor/reason and the persisted second snapshot;
it appends exactly one operation 14 intent. A new suspension CAS requires `Enforced` and the corresponding exact
facts, appends one operation 15 intent and remains fail-closed while receipt delivery is pending. Named Mongo
ambiguous outcomes without exact persisted business, snapshot and audit proof return reconciliation-required. This
code-ready owner acceptance is not worker enablement, credential/grant provisioning, data mutation, Local
Development activation or Production/Staging approval.

Exact Enforced Global Product reserve/create failure is `409
PRODUCT_SCOPE_ENFORCED_GLOBAL_PRODUCT_CREATE_NOT_ALLOWED`. The response must be returned before counter,
reservation, product or audit access and must not reveal whether a matching reservation exists. Descendant writes
that remain valid under existing E/F scope rules still acquire the same durable admission lease while Enforced so a
suspension fence cannot race them.

Exact H1b runtime allow-list — existing files:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeRolloutState.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeRolloutStateRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Repositories/IProductLegalEntityScopeOperationalReadinessRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeAuditIntentFactory.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/ReserveCanonicalCodeCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateGlobalProductDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFirstGskuDraftFacadeCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFirstGskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/UpdateGskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateLskuDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Commands/CreateFinishedGoodDraftCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/RequestProductAbbreviationAllocationCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/ApproveProductAbbreviationAllocationCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/RejectProductAbbreviationAllocationCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/CancelProductAbbreviationAllocationCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/InitiateProductAbbreviationCorrectionCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/RequestProductAbbreviationRetirementCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/ApproveProductAbbreviationRetirementCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductAbbreviationRegister/Commands/RejectProductAbbreviationRetirementCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/CreateProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/ReplaceProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/Commands/EndProductLegalEntityScopePolicyCommand.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeOperationalReadinessRepository.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Configuration/ProductLegalEntityScopeOperationalOptions.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs`

Exact H1b runtime allow-list — planned new files:

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Enums/ProductLegalEntityScopeAdmissionState.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeWriterLease.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeActivationFence.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductLegalEntityScopeInventorySnapshot.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/IProductLegalEntityScopeInventoryMutation.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeMutationIdentity.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeWriteFenceCoordinator.cs`
- `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/ProductLegalEntityScopeWriteFenceBehavior.cs`

The command marker is the single authoritative mutation inventory. The behavior spans the complete handler,
reconciliation and persisted replay/read-back. `CreateFirstGskuDraftFacadeCommand` and its nested
`CreateFirstGskuDraftCommand` reuse the exact same scoped tenant/token admission context; direct invocation of the
inner command must still acquire a lease. A `202`, ambiguous write or missing exact persisted read-back must retain
the lease. A tenant without a rollout document retains the pre-FU03 legacy behavior; once its explicit Preparation
singleton exists, all 18 command families are governed by admission.

Exact H1b test allow-list:

- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeRolloutTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeRolloutMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalRunnerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalReadinessMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeOperationalDependencyInjectionTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/AuditIntentDeliveryMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeCommandTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeReconciliationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeConsumerTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeWriteAdmissionContractTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductLegalEntityScopeActivationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GlobalProductApiMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductItemSkuMasterMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/GskuRegisterFacadeTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/LskuDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterUnitTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterAuthorizationTests.cs`
- `services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/ProductAbbreviationRegisterMongoTests.cs`

H1b acceptance requires real-Mongo writer-first/fence-first barriers, nested-command re-entry, stale/forged token and
no-ABA proof, same-command recovery, payload drift, cancellation, ambiguous-write reconciliation, cross-tenant
isolation, bounded canonical snapshot/hash, BSON budget, exact operation 14/15 single-cardinality, all type 7/8 audit
failure states, Preparation and missing-rollout regressions, fail-closed Enforced/Suspended consumers and an
architecture test that fails if a nineteenth relevant command lacks explicit guarded/internal classification.

The audit delivery repository and its intent/receipt contracts remain protected. H1b only appends operation 14/15
as `Pending` and reads exact compacted receipts. It does not discover, claim, deliver, acknowledge or compact audit
work. `MOD-0021-FU01 Trusted Durable Source Audit Intent Ingestion` owns durable central-`audit_outbox` acceptance;
the base MOD-0290 `Trusted Durable Audit Intent Delivery Consumer Foundation` owns the separate MDM client/worker.
Neither final `audit_events` persistence nor a fabricated acknowledgement satisfies this gate. Until both exact
prerequisites close, Enforced may be `AuditPending` but H1b cannot be claimed operationally complete.

All Product/SKU and ABB handlers, workflows, business repositories/entities, `ProductLegalEntityScopePolicy`, policy
repository, controllers, Gateway, frontend, manifest, Auth, Platform, WorkCenter, committed configuration, secrets,
data, migrations, seeds and `.antigravity` remain protected. No new Mongo collection/index, public endpoint, hosted
service, automatic fence expiry/takeover, auto-GroupWide classification or Production/Staging enablement is allowed.
Runtime implementation used only this approved list. Operational execution still requires its own explicit gate.

When those decisions close, Enforced activation must still require every non-deleted, non-`Retired` Global Product
classified; zero evidenced inventory delta; zero descendant parent-chain orphan; and the approved audit-readiness
proof. Normal rollback moves only from Enforced to `FailClosedSuspended`, where consumer evaluation denies instead of
falling back to tenant-wide access. A safety suspension must not auto-classify products or introduce a permissive
fallback. Existing pilot/system products are never auto-classified GroupWide. The later operational approval must
name the tenant, inventory snapshot, classification decisions, completeness proof, activation command, rollback
decision and live allow/deny matrix; pack evidence may be updated only after successful read-back.

## 6. Protected Paths

- `.antigravity` and all descendants.
- `gateway/Diten.ApiGateway/ocelot.json` under this pack.
- `services/Diten.Platform` and `services/Diten.AuthService` under this pack.
- `frontend/Diten.Web/Controllers/Archive` and `frontend/Diten.Web/Views/Archive`.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/GlobalProduct.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/ProductDefinitionRevision.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Gsku.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/Lsku.cs`.
- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/FinishedGood.cs`.
- All committed `appsettings*.json`, launch profiles, environment files, certificates and secret-store material.
- Existing verified GSKU/Market resolver options, clients, credentials and tests; FU21 uses a dedicated credential.
- Existing tenant/JWT context, tenant middleware and propagation contracts; FU03 does not widen the singular tenant
  authorization context or accept browser scope headers.
- FU02 Market Supply Assignment and all Registered Presentation/MA paths.

Protected consumer repository/handler paths become writable only within the separately approved E or F named
step exact lists above. No other existing product path is implicitly authorized.

## 7. Dependencies

| Dependency | Exact contract / blocker |
|---|---|
| MOD-0290 | Global Product is the sole MVP subject. |
| MOD-0220 | Existing `ValidateLegalEntityReferenceQuery` remains the same-tenant active/referenceable authority. |
| MOD-0018-FU21 | Provider implementation is accepted; FU03 still needs the exact D-step MDM client, Development secret provisioning and live contract smoke. |
| MOD-0018-FU22 | Exact six-key Auth profile is accepted; FU03 still owns nav-hidden manifest declarations, controller enforcement and later operational reconciliation. |
| Audit owner | Existing durable intent/delivery reuse, no automated MVP purge and exact append-only values 7..8/11..15 are frozen. Delivery/fencing/replay/ack regression remains a B-step acceptance gate. Legal retention duration is a later Production/runbook decision. |
| Gateway | Existing `/api/global-products/{everything}` GET/POST/OPTIONS route covers the proposed nested API; no route edit is planned. |
| MOD-0290-FU02 | Market supply, Registered Presentation and regulatory roles stay independent. |

FU21 closes the former first-value transport drift without changing the singular tenant authorization context. MDM
still has no FU03 consumer adapter until the separately authorized D step is implemented. Browser-provided
`X-Legal-Entity-Id`, `X-Legal-Entity-Ids`, query strings, form values or client-side filtering remain forbidden.

## 8. Runtime Constraints

- MongoDB, `EntityBase`, server-derived tenant, soft delete, Response envelope and MediatR CQRS remain mandatory.
- Policy absence means `LegacyUnclassified`; it never means GroupWide.
- Phase 1.5 approves one policy aggregate with embedded full-snapshot periods and hard limits of 200 Legal Entity IDs
  per snapshot, 100 periods and 1,048,576 serialized BSON bytes. No tenant-specific higher override is permitted.
- GroupWide current period has no Legal Entity IDs; Scoped current period has a sorted unique non-empty set.
- Validity uses `[EffectiveFromUtc, EffectiveToUtc)` and trusted server time in the foundation. Backdating and
  future-effective planning are deferred.
- At most one current period exists. Closed historical Scoped periods may coexist with current GroupWide.
- Expected-version create/replace/end is one atomic document update; assignment-row CRUD does not exist.
- No arbitrary DELETE endpoint exists. Temporal history is preserved through server-time end or atomic replace.
- No enforcement occurs before trusted multi-scope transport and preparation completeness are verified.
- Enforced + GroupWide does not bypass user data scope. An empty trusted/local Legal Entity candidate intersection
  returns deny; management/audit visibility remains governed by its separate management permissions.
- Audit uses the existing MDM durable intent/delivery path append-only. Automated purge is forbidden in the MVP;
  exact additive enum/discriminator values are frozen and B must prove existing delivery/fencing/replay/ack behavior.
- Missing/deleted/cross-tenant subject, parent link, Legal Entity or policy evidence denies without disclosure.
- Cancellation propagates; audit-delivery failure cannot be reported as business success.

## 9. Layout & Shell Contract

The proposed page is navigation-hidden and uses `_LayoutTenantShell`. Golden Slim is appropriate for three visible
inputs: Global Product, scope mode and a conditional multi-Legal-Entity selector. Effective start/end are server
actions, not user-entered foundation fields. No checkbox, bulk, arbitrary delete, lifecycle toolbar or WorkCenter
action is permitted.

## 10. Backend File Convention

New CQRS records follow the existing MDM conventions in the exact C-step paths. Handlers and validators use the
existing current-tenant Global Product and Legal Entity contracts; they do not query Platform/Auth persistence.
Create-options is a facade with bounded Global Product and Legal Entity lookups and server-side permission
conjunction. It cannot compose separately authorized lookup payloads in the browser.

## 11. Frontend File Contract

The browser calls only same-origin MVC endpoints. MVC proxies only to Gateway `5000`; it never sends tenant IDs,
user Legal Entity IDs, bearer tokens, or service credentials from JavaScript. Save View uses the shared
personalization client. The UI displays aggregate period history separately from the effective consumer
decision and never offers persisted visibility/selectability/mutability/downstream-usage switches.

## 12. Validation Rules

| Condition | Required result |
|---|---|
| Missing policy | `LegacyUnclassified`; no implicit GroupWide behavior. |
| Current GroupWide | current period Legal Entity set must be empty; closed historical Scoped periods may remain. |
| Current Scoped | sorted unique snapshot contains at least one locally revalidated Legal Entity. |
| Scoped snapshot contains more than 200 Legal Entity IDs | 400; no truncation or tenant-specific higher override. |
| Policy would exceed 100 embedded periods or 1,048,576 serialized BSON bytes | deterministic 409; pre-write measurement uses the production Mongo serializer over the complete persisted document, including technical and audit fields; no partial write or history deletion. |
| No current period at activation | completeness failure; Enforced activation blocked. |
| Product or Legal Entity missing/deleted/cross-tenant | non-disclosing 404. |
| Legal Entity inactive/non-referenceable | non-disclosing 404. |
| Client effective date/backdating | 400; foundation uses server time. |
| Multiple current/overlapping period | deterministic 409, including concurrent writers. |
| Stale expected version | 409. |
| Same command and payload | exact replay without a new period/audit intent. |
| Same command with payload drift | deterministic 409. |
| Ambiguous write without exact reread proof | non-success 202 `PRODUCT_SCOPE_RECONCILIATION_REQUIRED`. |
| Client tenant or Legal Entity scope header | 400/403; never trusted. |
| Multi-LE user | exact trusted set is evaluated; first-only truncation is forbidden. |
| FU21 provider payload | collection may be empty; otherwise max 200 sorted/distinct non-empty GUIDs plus echoed context; malformed provider proof maps fail-closed to MDM 503. |
| FU21 provider raw response | maximum 32,768 bytes before JSON parsing; overflow, redirect, HTML/non-JSON, extra/duplicate fields or echo mismatch maps fail-closed to MDM 503. |
| Missing rollout state | `Uninitialized`; reads do not create state and create/replace/end fail closed. |
| Policy mutation while rollout is `Enforced` or `FailClosedSuspended` | deterministic 409; only persisted `Preparation` permits C mutations. |
| Completeness | internal/read-only facts for non-deleted, non-`Retired` Global Products; no public route and not H activation proof. |
| Missing/malformed `Idempotency-Key` | 400; exact non-empty GUID header only. |
| Enforced GroupWide + empty trusted/local candidate intersection | deny; consumer list omits and detail/mutation fails without disclosure. |
| Selector/create-options | assignment-management permission AND underlying Product/Legal Entity lookup rights. |

The approved exact create-options conjunction is
`mdm.product-legal-entity-scopes.configure` + `mdm.global-products.read` + `mdm.legal-entities.read`. All three are
required server-side because the source lookup surfaces have independent permission contracts. UI hiding alone is
not authorization.

## 13. Failure Path to Verify

- Forged client Legal Entity headers do not change server scope.
- A user scoped to Legal Entity A cannot see/select/use a Scoped product assigned only to B.
- A user with two trusted Legal Entity scopes receives the union, not the first value.
- A System Tenant actor does not bypass product row scope merely because module access is bypassed.
- Concurrent create/end/replace cannot create two current periods or partial aggregate transitions.
- Same-command replay is exact, payload drift conflicts, and ambiguous-write recovery requires an exact reread;
  only the three named Mongo infrastructure exceptions can become reconciliation-required.
- Audit intent is durable and replayable; delivery bookkeeping does not mutate business version.
- A missing audit acknowledgement remains discoverable; stale fencing cannot acknowledge another claimant's item.
- Enforced activation requires classified active-product inventory, zero delta, descendant integrity and healthy
  audit outbox under one expected-version/fence/reason decision.
- LegacyUnclassified denies only after explicit Enforced activation; Preparation deployment alone preserves the
  pre-existing behavior while reporting incompleteness.
- Default rollback enters FailClosedSuspended; it never converts unclassified products to GroupWide. Any temporary
  legacy-permissive break-glass requires separate expiring authority and immutable audit.

## 14. Authorization Convention

Exact `ModuleCode`: `product-item-sku-master`.

Exact keys implemented by MOD-0018-FU22 and still awaiting FU03 manifest declaration:

- `mdm.product-legal-entity-scopes.read`
- `mdm.product-legal-entity-scopes.configure`
- `mdm.product-legal-entity-scopes.replace`
- `mdm.product-legal-entity-scopes.end`
- `mdm.product-legal-entity-scope-rollout.activate`
- `mdm.product-legal-entity-scope-rollout.rollback`

Implemented Auth matrix to be enforced by FU03 controller/manifest code:

| Role/state | management read | configure/replace/end | activate/rollback |
|---|---:|---:|---:|
| Tenant Admin + active entitlement | allow | deny | deny |
| Tenant Viewer + active entitlement | deny | deny | deny |
| ProductLegalEntityScopeSteward + active entitlement | allow | allow | deny |
| ProductLegalEntityScopeAuditor + active entitlement | allow/completeness only | deny | deny |
| ProductLegalEntityScopeRolloutOperator + active entitlement | allow | deny | allow |
| Missing/disabled/expired entitlement | deny | deny | deny |

Dedicated roles are idempotently reconciled but no user is automatically assigned. FU22 separates this exact profile
from the generic Admin/Viewer and ABB paths. Management or rollout permission
does not grant consumer row access. Consumer access also requires the existing resource permission, active
entitlement, trusted Legal Entity scope and an effective product-scope decision. Admin, SuperAdmin and Platform
System Tenant module-entitlement bypasses are never product row-scope bypasses.

## 15. Gateway / API Routing Decision

No Gateway edit is required or authorized. Code truth confirms the existing `/api/global-products/{everything}`
GET/POST/OPTIONS route forwards nested policy routes to MDM `5059`. To avoid relying on unsupported PATCH, temporal
end is a POST action.

Proposed state-gated API:

| Method | Route | Permission |
|---|---|---|
| GET | `/api/global-products/{globalProductId}/legal-entity-scope-policy` | read |
| GET | `/api/global-products/{globalProductId}/legal-entity-scope-policy/history` | read |
| GET | `/api/global-products/{globalProductId}/legal-entity-scope-policy/effective` | read |
| GET | `/api/global-products/{globalProductId}/legal-entity-scope-policy/create-options` | configure plus lookup conjunction |
| POST | `/api/global-products/{globalProductId}/legal-entity-scope-policy` | configure |
| POST | `/api/global-products/{globalProductId}/legal-entity-scope-policy/replace` | replace |
| POST | `/api/global-products/{globalProductId}/legal-entity-scope-policy/end` | end |

There is no assignment-row endpoint, DELETE, PATCH, bulk, client-effective-date, public migration, browser-scope
or WorkCenter endpoint. Rollout activation/rollback is an internal operator surface owned by the separate H
operational gate, not a management update route.

## 16. Acceptance Criteria

- [x] Phase 1.5 approves one policy aggregate per product and distinct LegacyUnclassified/current GroupWide/current
  Scoped representation, with exact hard limits of 200 IDs, 100 periods and 1,048,576 BSON bytes.
- [ ] Embedded full-set periods have at most one current period under real concurrency; closed Scoped history may
  coexist with current GroupWide.
- [ ] Initial/replace/end use trusted server time, `int` expected version, exact replay and bounded BSON state.
- [ ] No existing Product entity gains a Legal Entity FK or copied assignment fields.
- [x] FU21 trusted multi-Legal-Entity provider transport is implemented, tested and user-accepted; FU03 MDM adapter
      and live secret-backed contract smoke remain separately gated before enforcement.
- [ ] Global Product list/detail/selector and all named descendants use one effective decision.
- [ ] Descendant and ABB scope is derived through exact parent links; no persistence copy exists.
- [ ] GSKU/LSKU/Finished Good and ABB mutations evaluate scope before reservation or business writes; replay and
  reconciliation read-back cannot bypass the current decision.
- [ ] Missing/deleted/cross-tenant links deny without disclosure.
- [x] Owner freezes the exact create-options conjunction as `mdm.product-legal-entity-scopes.configure` +
      `mdm.global-products.read` + `mdm.legal-entities.read`; implementation must enforce it server-side.
- [x] Enforced + GroupWide with an empty trusted/local candidate intersection denies consumer access; GroupWide is
      product availability, not a user-scope bypass.
- [x] Existing MDM audit intent/delivery is reused append-only and automated purge remains outside the MVP.
- [x] Immutable audit intent/delivery/fencing/replay/acknowledgement/compaction has real-Mongo proof.
- [x] H1a exposes only an exact Development/default-disabled CLI with `Inspect` and idempotent
  `BootstrapPreparation`; it has no controller, hosted-service behavior, product classification or activation path.
- [x] H1a inspection reuses the current completeness repository contract and reports exact descendant/audit counts
  with deterministic maximum-200 diagnostic samples, tenant non-disclosure and no read-time mutation.
- [x] H1a bootstrap creates at most one Preparation state, exact-replays the same command/actor, rejects drift and
  requires persisted read-back without creating an audit intent or policy.
- [x] Preparation deployment does not auto-enforce; Enforced activation requires complete classification, zero
  inventory delta, descendant integrity and healthy audit read-back.
- [x] H1b Phase 1.5 owner decisions and exact runtime/test allow-list are frozen: durable lease/fence, type 7/8 audit
  scope, fail-closed recovery, 18-command inventory and Enforced Global Product create denial.
- [x] H1b default-disabled runtime foundation is implemented under its exact code-start; operational completion still
  requires approved inventory/parameters, configured credentials/tenant grant and live central delivery/
  acknowledgement/compaction proof. No operational action was invoked.
- [x] Default rollback is FailClosedSuspended; existing pilot/system products are never auto-GroupWide.
- [ ] Navigation remains hidden until a separate enablement decision.

## 17. Test Expectations

| Area | Minimum evidence |
|---|---|
| Domain/policy | LegacyUnclassified; GroupWide empty snapshot; Scoped full snapshot; closed history; `int` expected-version transitions and configured bounds. |
| Mongo | unique tenant/product and tenant/creation-command; concurrent single-current-period update; no partial replace; exact replay/payload drift; bounded document. |
| Ambiguous write | only named Mongo exceptions; exact reread recovery; otherwise 202 reconciliation-required; cancellation/programming errors propagate. |
| Audit | Append-only discriminator/operations, discovery, claim, fencing, replay, ack and compaction. |
| FU21/MDM transport | Exact server pair, delegated token and dedicated credential; bounded echoed response; one-batch local intersection; forged headers rejected; timeout/cancellation/failure fail closed. |
| Global Product | list/detail/selector allow/deny/no-leak with preparation and enforced modes. |
| Descendants/ABB | exact parent derivation; query/create/update/lifecycle denial before mutation; replay/read-back no-bypass; missing/deleted/cross-tenant denial. |
| API/auth | 400/401/403/404/409/202, special role profile, permission conjunction and no assignment-row/DELETE/PATCH endpoints. |
| Frontend | Slim verifier, 7 locale parity, same-origin proxy, hidden navigation and browser allow/deny smoke. |
| H1a CLI guard | Exact argument/action matching; Development plus enabled fail-before-resolution; runner is not hosted; no controller/startup execution; cancellation propagation. |
| H1a inspect | Read-only `Uninitialized` behavior; exact completeness reuse; all named descendant chains; tenant isolation; exact counts with maximum-200 deterministic samples and `HasMore`; raw scope-audit state/receipt facts without an activation-ready claim. |
| H1a bootstrap | Missing-state Preparation create; same command/actor exact replay; command/actor/mode drift conflict; ambiguous exact reread recovery; no policy, audit-intent, role, navigation or business-data mutation. |
| H1a real Mongo/DI | Real `localhost:27017`; each descendant orphan class; cross-tenant/deleted-parent non-disclosure; audit state/receipt classification; no new collection/index; tenant context set before repository resolution; exact scoped DI registration. |
| H1b rollout | Default-disabled runtime foundation implemented: exact state/version/fence/reason/snapshot, quiesced zero delta, all 18 writers, chain/audit gates, operation 14/15 durable read-back, Enforced deny, FailClosedSuspended rollback and no permissive fallback. Operational inventory approval, parameters, audit provisioning/live receipt proof and explicit invocation remain open. |

Builds and tests run only within an explicitly authorized named step. This planning update runs none.

## 18. Ready-for-dev Checklist

- [x] DCP-002 parent/FU/collision preflight passed for MOD-0290-FU03.
- [x] Subject is Global Product and target MVP is LegalEntity only.
- [x] User approved the Phase 1.5 control-tower architecture and later standing exact runtime code-start; operational
  invocation remains separately gated.
- [x] Explicit LegacyUnclassified/GroupWide/Scoped semantics and the single-document aggregate boundary are
  approved for planning and named-step implementation.
- [x] Exact hard bounds are approved: 200 Legal Entity IDs per snapshot, 100 periods and 1,048,576 serialized BSON
  bytes; no tenant-specific higher override.
- [x] Enforced + GroupWide with an empty trusted/local candidate intersection is approved as deny.
- [x] Existing MDM audit intent/delivery reuse, append-only behavior and no automated MVP purge are approved.
- [x] Exact append-only audit vocabulary is frozen as aggregate values 7..8 and operation values 11..15; existing
  values are never renumbered. No new audit subsystem is permitted.
- [x] B-step implementation proves existing durable delivery/fencing/replay/ack/compaction and no-automated-purge
  behavior for both new aggregate types. Legal retention duration, archive, legal hold, redaction and future purge
  remain a later Production/runbook gate and do not block foundation A/B code-start.
- [x] MOD-0018-FU21 implements the delegated-Bearer plus dedicated-S2S multi-LE provider contract and the user
  accepted its implementation on 2026-08-26. FU03 D-step code-start and operational secrets remain open.
- [x] MOD-0018-FU22 implements and tests the exact six-key special profile; the user accepted its implementation on
  2026-08-26. FU03 manifest declarations and Local Development reconciliation remain open.
- [x] Derived E/F Global Product, GSKU, LSKU, Finished Good and ABB behavior is approved architecturally; no
  persistence copy or row-scope bypass is permitted.
- [x] Existing `/api/global-products/{everything}` GET/POST/OPTIONS Gateway route is verified to cover the nested POST
  end action; no Ocelot edit is required.
- [x] Preparation/Enforced/FailClosedSuspended semantics and fail-closed default rollback are architecturally
  approved.
- [ ] Migration/operations owner approves the target-tenant inventory, activation fence, completeness evidence,
  exact operational parameters and any break-glass run before execution.
- [x] D, reconciled C, E, F, G, H1a and the H1b default-disabled foundation received exact code-start authority and
  were implemented; H1b activation/suspension execution remains separately blocked and unauthorized.
- [x] Pack status is `in-progress`; unresolved items above remain fail-closed per named step.

## 19. Implementation Notes

Code truth reconciled on 2026-08-26:

- **Section 5 A implemented:** exact 11-file allow-list only. `ProductLegalEntityScopePolicy` and
  `ProductLegalEntityScopeRolloutState` domain aggregates, repository contracts, rollout/evaluation contracts and
  focused tests now exist. At that A-step boundary, B-H production paths were not changed.
- The A foundation enforces explicit GroupWide/Scoped modes, Preparation/Enforced/FailClosedSuspended rollout,
  trusted tenant/product/soft-delete binding, Enforced GroupWide empty-candidate deny and canonical
  trusted-local-policy intersection. It records immutable create/replace/end command, actor and server-time evidence.
- Hard bounds are executable code truth: 200 Legal Entity IDs per snapshot, 100 chronologically canonical periods,
  a 1,048,576-byte complete-document serialization budget contract and `int` expected-version protection. The
  authoritative Mongo serializer measurement remains B-owned and cannot be supplied by an API caller.
- Section 5 A verification: focused domain/rollout tests `24/24` passed with zero skipped; MDM API Release build
  passed with `0` warnings and `0` errors. Independent security/model and allow-list audits passed; `git diff
  --check`, conflict-marker, trailing-whitespace and final-newline checks are clean.
- **Section 5 B implemented:** tenant/soft-delete-safe single-document Mongo persistence, expected-version CAS,
  exact idempotent replay/payload drift, three-exception ambiguous-write recovery, append-only audit operations 7..8
  and 11..15, durable delivery/fencing/retry/dead-letter/acknowledgement/compaction and the 983,040/1,048,576-byte
  business/lifecycle document guards are implemented. Policy writes require exact immutable audit proof; rollout
  `Preparation` bootstrap is the sole zero-intent exception and later rollout transitions require operations 14/15.
- Section 5 B verification after independent P1 security hardening: focused real-Mongo tests `62/62`, full MDM suite
  `470/470`, zero skipped, MDM API Release build `0` warnings/`0` errors and `git diff --check` clean. No `ReplaceOne`,
  new audit subsystem, per-run test database, Platform schema profile, WorkCenter or C-H runtime was added.

- Product records are tenant-scoped but contain no Legal Entity or Organization Unit reference.
- Legal Entity and its validation repository already live in MDM; FU03 must reuse them.
- **Sections 5 D and reconciled C implemented:** MDM consumes FU21 through the dedicated delegated-Bearer plus S2S
  client with a 32,768-byte pre-parse cap, strict echoed context and one bounded local intersection. Preparation-only
  create/replace/end CQRS, create-options, completeness and nav-hidden API contracts are present; no browser scope
  header, local fallback or second provider was introduced.
- The 2026-08-27 code-truth audit corrected the execution order to `A -> B -> D -> reconciled C -> E -> F -> G -> H`.
  D reuses FU21 exactly, adds a 32,768-byte pre-parse response cap, disables redirect/credential leakage and performs
  one bounded local Legal Entity intersection. C then reuses D; it cannot invent a second provider or unbounded scan.
- The reconciled C implementation enforces Preparation-only mutation, missing-rollout `Uninitialized`, management-
  only effective facts, internal non-`Retired` completeness, exact GUID `Idempotency-Key`, server-derived audit
  identity and the three-permission create-options conjunction.
- FU22 implements the exact six-key Auth profile, Admin-read/Viewer-none defaults, dedicated responsibility-role
  matrices, source-scoped cleanup and no automatic user assignment. Section 5 G now declares the nav-hidden MDM page
  and exact six descriptors; live catalog/grant reconciliation remains an operational gate.
- Owner decisions on 2026-08-26 freeze 200 IDs per snapshot, 100 periods, a 1,048,576-byte BSON budget, no
  tenant-specific higher override, deny for Enforced GroupWide with empty trusted/local candidates, append-only reuse
  of existing MDM audit delivery with no automated MVP purge, and the exact three-permission create-options
  conjunction.
- Current enum code truth now includes the implemented additive policy/rollout aggregate values 7/8 and
  create/replace/end/activate/suspend operation values 11..15; values 14/15 do not authorize H runtime implementation.
- **Sections 5 E and F implemented and independently security-reviewed:** Global Product and derived GSKU/LSKU/
  Finished Good/ABB reads and mutations enforce the current persisted rollout and trusted Legal Entity scope. Mongo
  filtering occurs before paging/count; full policy-history BSON validity is shared across E/F; deleted, malformed,
  broken-parent and cross-tenant facts are non-disclosing; replay uses the actual persisted parent chain before any
  reservation or mutation; cancellation propagates. E evidence is `67/67`; F exact matrix is `252/252`.
- **Section 5 G implemented:** the tenant-shell Golden Slim page uses exactly Global Product, mode and conditional
  Legal Entities, same-origin MVC-to-Gateway transport, stable protected idempotency and seven-locale resources.
  Focused frontend tests are `11/11`; manifest/hosted-registration integration tests are `8/8`; navigation remains
  hidden and the existing Gateway catch-all was reused. The generic DataTable verifier reports `74` pass / `17`
  controlled pack variances: one obsolete browser tenant-header expectation, eight unused edit/status/import/bulk
  localization keys, one forbidden direct-Gateway expectation and seven intentionally absent checkbox/bulk/delete
  contracts. No unexpected verifier failure exists and no inert/forbidden surface was added to satisfy the generic
  tool.
- **Section 5 H1a implemented and independently hardened:** one Development-only/default-disabled CLI exposes only
  `Inspect` and idempotent `BootstrapPreparation`. Its bounded read model reports completeness, seven descendant
  chains and valid-versus-malformed audit facts; malformed rollout/audit state fails closed, a concurrent exact
  bootstrap reports one create plus one replay, and real CLI cancellation propagates. H1a is `29/29` on real local
  Mongo. It adds no controller, hosted service, collection, index, policy classification or activation path.
- Integrated evidence after E/F/H1a hardening: full MDM suite `596/596`, zero skipped; MDM API Release build succeeds
  with zero errors; frontend focused tests are `11/11` and the frontend Release build succeeds with zero errors and
  thirteen unrelated existing WorkCenter/Enterprise Strategy warnings; `git diff --check` is clean.
- **Local Development H1a run completed:** tenant `74355e70-4c7d-410c-8cf6-db5fe3b9547f` initially inspected as
  `Uninitialized` with two eligible and zero configured Global Products. Command
  `4cab254b-190d-47a4-b571-8673156d936c` created one `Preparation` rollout; exact replay returned the same state and
  final Inspect reported `Preparation`, eligible `2`, configured `0`. No product policy was auto-created and no
  GroupWide assumption, Enforced activation, suspension or H1b mutation occurred.
- The post-bootstrap read-only inventory identifies `GP-000000000001 — UI Smoke Global Product` and
  `GP-000000000002 — UI Hotfix Smoke Product`, but the pilot tenant currently has zero non-deleted Legal Entity
  records and therefore zero referenceable Legal Entities. Scoped policy authoring is impossible until a real Legal
  Entity exists. GroupWide would also deny under Enforced because the approved empty-trusted-candidate rule is
  fail-closed. No synthetic Legal Entity or automatic GroupWide policy was created.
- **Section 5 H1b default-disabled runtime foundation implemented:** the singleton rollout now owns the non-takeover
  tenant writer lease and activation/suspension fence; the exact 18-command marker inventory is guarded before audit
  forwarding; only the GSKU facade-to-inner logical pair may reuse an ambient fenced lease. The fence binds two
  stable canonical snapshots and exact command/actor/reason/state/version facts before the transition CAS. Operation
  14/15 is appended once as a local Pending intent, and exact replay remains `AuditPending` until mutually exclusive,
  fully bound compacted-receipt proof exists. Enforced Global Product reserve/create is rejected before mutation.
- H1b recovery is fail-closed: stale/forged generations, payload drift, unrelated nested commands, `202`, exception,
  cancellation, partial business/reservation/immutable-intent writes and ambiguous outcomes retain the lease. Exact
  zero-delta deterministic failure may release; independently advancing G4 delivery/receipt state is excluded from
  the command-owned hash and therefore cannot create a permanent false delta. Canonical snapshot parsing rejects
  missing/extra/reordered facts, CR, internal/extra blank lines, drifted headers and incoherent type 7/8 facts.
- H1b verification after the nested execution-context and independent-acceptance findings were closed: focused
  ProductLegalEntityScope domain/runner/DI/real-Mongo tests are `166/166`; the current integrated full MDM suite is
  `753/753`; all have
  zero skipped. Persisted malformed rollout state, full/stable hash recomputation, sample-independent 201+ inventory
  hashing, marker-independent writer discovery and real-Mongo commit-before-response reconciliation are covered. Crash/replay is
  proven after fence acquisition, first and second snapshot, quiesced snapshot binding and commit-before-response.
  Exact facade-to-inner reuse is installed synchronously around the MediatR `next` delegate and disposed on every
  outcome; direct inner and unrelated nested commands cannot inherit it. MDM API isolated Release build succeeds
  with zero errors and five existing persistence warnings.
- **Current-base prerequisite integration evidence (2026-08-30):** MOD-0033-FU02 service identity issuance,
  MOD-0021-FU01 trusted durable source audit ingestion and MOD-0021-FU02 temporal storage hardening are integrated
  in the same local branch. Auth service-token focused tests pass `47/47`; Platform trusted-source/temporal/token/DI
  focused tests pass `159/159`; MDM audit/manifest focused tests pass `109/109`; the DB-010 architecture guard passes
  `6/6`; and Product Legal Entity Scope security-focused tests pass `48/48`, all with zero skipped. Auth and Platform
  API Release builds succeed with zero errors (one existing Auth persistence warning; zero Platform warnings in the
  final isolated build). The BL-030 inventory guard separately fingerprints 410 current-main members as unreviewed
  mechanical debt and the two FU02-reviewed AuditOutbox members as an exact bounded subset; the combined 412-member
  surface is independently fingerprinted. BSON representation/query behavior remains proven by the separate temporal
  real-Mongo suite, and the inventory/serializer guard passes `2/2`.
- Full-suite truth is intentionally not overstated: the current integrated Auth suite is `613/615` because two
  pre-existing user-lookup tests still require a two-field DTO although current main already exposes the two masked
  identity hints. The Platform suite is `3730/3754`; the remaining 24 failures are existing Document Management,
  BRD seed-count and other current-main contract drift, while every named prerequisite and FU03 focused group above
  is green. No unrelated contract was weakened to manufacture a green total.
- H1b remains `BLOCKED_OPERATIONAL_PREREQUISITES`, not code-blocked. The default-disabled G4 provider/consumer
  prerequisites remain implemented with `68/68` + `639/639` Platform and `41/41` + `678/678` MDM evidence. No H1b
  action, operation 14/15 data mutation, credential/grant provisioning or live receipt run occurred; Production-
  readiness must not be inferred. Operational completion still requires approved real inventory and parameters,
  genuine credentials/tenant grant and live central acknowledgement proof.
- Existing Gateway code truth already covers nested GET/POST policy routes through the Global Product catch-all;
  no Ocelot change is needed.
- Current Product reads filter by tenant, not Legal Entity.
- The module-access bypass for the Platform System Tenant does not define Product row-scope policy.
- `EntityBase` timestamps and versioning are not immutable audit evidence. The B implementation now carries the
  approved discriminator/operations and durable intent delivery; future retention/purge policy remains an explicit
  audit-owner/Production gate.
- The single-document aggregate, server-time foundation, numeric bounds, GroupWide-empty-candidate denial, exact
  create-options conjunction, append-only audit vocabulary, trusted transport, derived consumer enforcement and
  persisted rollout semantics are implemented. H1a operational execution and H1b activation remain separate gates.
- **C/E/F compatibility hardening:** a tenant with no rollout document now retains pre-FU03 consumer access without
  provider or policy reads; management create/replace/end remain fail-closed until an explicit `Preparation` state.
  Explicit `Preparation`, `Enforced` and `FailClosedSuspended` behavior is unchanged. Scoped replacement now resolves
  FU21 with the exact `mdm.product-legal-entity-scopes.replace` pair while create continues to use `configure`.
  Focused command/consumer tests pass `21/21`; the full `ProductLegalEntityScope` group passes `142/142`, zero skipped;
  the isolated MDM API Release build succeeds with zero errors and five existing persistence warnings.
- **Independent A-H1b code/security/UI review (2026-09-05):** no P0/P1/P2 implementation defect was found. Review
  reconfirmed strict unknown-field and exact D-format idempotency rejection; same-origin antiforgery-protected UI
  transport; tenant/canonical-subject binding; bounded FU21 candidates and FU22 least-privilege role separation;
  atomic policy CAS/replay/audit contracts; descendant Global Product/GSKU/LSKU/Finished Good enforcement before
  paging, reservation or mutation; and default-disabled lease/fence based Enforced/suspension recovery. Current
  non-Mongo focused runs pass Auth `6/6`, Platform `41/41`, MDM `113/114` and frontend `31/31`. The sole MDM failure is
  the DI test's startup migration attempting `127.0.0.1:27017`; all-FQN attempts in Auth, Platform and MDM independently
  report only connection-refused failures because the OS-level Mongo service is stopped. No code assertion failed.
  Previously recorded real-Mongo evidence above remains authoritative until the coordinated H2 runtime restart.
  Test compilation rebuilt the affected Auth, Platform and MDM API graphs successfully; the isolated frontend Release
  build passes with zero errors and fourteen unrelated existing warnings. Bundled-Python Golden Slim verification
  reproduces `74` pass / `17` controlled pack variances, exactly matching the recorded same-origin/no-bulk design.
  Repository diff/conflict/whitespace checks remain clean. This review performed no process, configuration, business
  data, navigation, commit or push mutation.

Therefore this document remains `in-progress`: Sections 5 A-G, H1a and the default-disabled H1b runtime foundation
are implemented; H1b Enforced activation/FailClosed suspension execution, live reconciliation and user acceptance
are not complete.

### User-visible completion — H2 Local Development operational acceptance amendment

The 2026-09-05 code-truth audit found no additional runtime feature gap for the approved Legal-Entity-only MVP.
The MDM management API, nav-hidden six-permission manifest, same-origin MVC proxy, Golden Slim management surface,
FU21 trusted bounded Legal Entity candidate resolver, FU22 least-privilege role profile, descendant consumer
enforcement and default-disabled H1b runner are present. WorkCenter is not required for this management foundation;
Organization Unit targeting remains deliberately deferred. The remaining work is one bounded Local Development
operational acceptance, not another implementation slice.

H2 is standing-approved under the user's instruction to finish this capability without pausing. It grants no source,
module-pack-other-than-evidence, registry, navigation, commit or push write authority. Its exact source/test allow-list
is `none`: it must use only existing supported UI/API/reconciliation surfaces and the existing
`--run-product-legal-entity-scope-operational` CLI. Secrets may exist only in process memory/environment for the run
and must not be persisted or reported. Production/Staging remains prohibited.

H2 must execute and evidence this exact sequence:

1. Re-read the target tenant's active, non-deleted Legal Entity inventory through the supported Legal Entity API.
   The historical zero-entity snapshot above is evidence of that earlier run, not a current assumption. FU03 must
   neither create a synthetic Legal Entity nor infer Organization Unit targets.
2. Provision/rotate the dedicated FU21 `DITENMDMSERVICE` credential, exact audience and only the six approved
   `(product-item-sku-master, mdm.product-legal-entity-scopes.*)` pairs through the supported Local Development
   operational mechanism. Prove wrong/revoked/expired credential, wrong audience and undeclared pair fail before
   candidate reads; remove temporary secret material after acceptance.
3. Reconcile the nav-hidden manifest/catalog and active entitlement through supported Platform/Auth flows. Prove
   the exact FU22 matrix: Tenant Admin `read`; Tenant Viewer none; `ProductLegalEntityScopeSteward`
   `read/configure/replace/end`; `ProductLegalEntityScopeAuditor` `read`; and
   `ProductLegalEntityScopeRolloutOperator` `read/activate/rollback`. No role receives an automatic user assignment.
   Assign only named temporary acceptance subjects through the supported Auth API and remove those memberships at
   cleanup.
4. While the persisted rollout is `Preparation`, classify every non-deleted, non-`Retired` Global Product explicitly
   as `GroupWide` or `Scoped`. A Scoped decision must select only real, active, same-tenant Legal Entities returned by
   FU21. No product may be auto-classified and an empty candidate set must not be converted to GroupWide. Exercise
   configure, replace, end and history/read-back with exact expected-version and idempotent replay behavior.
5. Deliver and compact all prior type-7 policy intents through the existing G4 consumer into MOD-0021-FU01. Prove
   the mutually bound central acknowledgement/compact receipt for each local intent; final `audit_events` presence
   alone is not sufficient.
6. Run exact `Inspect`, record the bounded inventory snapshot, verify zero missing products, zero descendant-chain
   orphan, zero inventory delta and clean type-7/type-8 audit facts, then invoke exact `ActivateEnforced` with the
   persisted rollout ID/version, a new command ID, trusted actor and bounded reason. A same-command replay must return
   the same operation-14 identity and may become complete only after the genuine compact receipt is read back.
7. With fresh logins/tokens, prove the live permission and row-scope matrix through Frontend `5001` -> Gateway `5000`:
   Steward management allow; Auditor read/history allow and all mutation deny; Admin read-only; Viewer deny. Prove the
   create-options conjunction `configure + global-products.read + legal-entities.read`, tenant A/B non-disclosure,
   Scoped membership visibility across Global Product/GSKU/LSKU/Finished Good/ABB, and Enforced GroupWide plus an
   empty trusted/local candidate intersection denies. Browser console errors and direct `5057/5059` browser calls
   must both be zero.
8. Prove fail-closed suspension on a disposable Local Development tenant or an owner-approved restoration path; do
   not leave the pilot tenant in `FailClosedSuspended`. Exact `SuspendFailClosed` replay must preserve one operation-15
   identity and remain deny-by-default until its genuine compact receipt is read back. If no supported safe restoration
   path exists, suspension evidence remains a separate open acceptance item and must not be simulated by direct Mongo.
9. Cleanup temporary user-role memberships and process-only credentials, then re-read entitlement, roles, grants,
   rollout, policies, audit receipts and business cardinalities. Direct Mongo writes, fabricated acknowledgement,
   implicit GroupWide assignment, new Product creation after Enforced and navigation enablement are forbidden.

FU03 may be reported user-visible complete for the Legal-Entity-only MVP only when steps 1-7 and 9 are evidenced and
step 8 is either safely completed or explicitly retained as the sole operational rollback drill. Navigation remains
hidden for item 7 of the user's delivery sequence and is not a blocker to this H2 acceptance. Until then, the truthful
state is runtime-complete but operational-acceptance-open.

### Navigation item 7 activation gate (user-approved; source implemented, acceptance pending)

The user's 2026-09-06 navigation approval supersedes H2's earlier source-level navigation exclusion but does not waive
H2 operational acceptance. The source-owned visibility flip and its seven normalized navigation labels are now
implemented; they are not evidence that a real scope policy has been operationally accepted. The exact MDM allow-list is
`ProductItemSkuMasterManifestProvider.cs`, limited to the existing `PRODUCT_LEGAL_ENTITY_SCOPES` page's
`IsNavigationVisible` value, plus its existing manifest test. The exact frontend allow-list is the seven existing
`SharedResource.*.resx` files, limited to `Nav.Page.PRODUCT_LEGAL_ENTITY_SCOPES`.

The route remains exact `/MasterDataManagement/ProductLegalEntityScopes` and the menu remains gated by
`mdm.product-legal-entity-scopes.read`. No Legal Entity, policy, rollout, permission, grant, controller, Gateway route,
layout, handwritten menu or static search entry is created by navigation activation. H2 steps 1-7 and 9 must first be
green, including real active Legal Entity inventory, explicit product classification, genuine audit receipts,
Enforced rollout and fresh-token row-scope acceptance. Then self-registration/menu acceptance must prove one catalog
page, seven localized labels, entitled-reader visibility, absence without read/entitlement, route `200/403`, automatic
tenant Ctrl+K discovery, no duplicate menu node and console/network cleanliness. Until those gates close, the source
change is not delivery-accepted or eligible for final merge approval.

## 20. Follow-up Items

- Organization Unit target/hierarchy and precedence versus Legal Entity.
- Legal/regulatory retention duration, archival/compaction, legal hold and any future purge/redaction policy are a
  later Production/runbook decision; automated purge is excluded from the MVP.
- Product owner/steward/manufacturer/supplier/seller/sponsor/MAH relation aggregates.
- FU02 Market Supply Assignment, Registered Presentation and regulatory-owner integration.
- WorkCenter submit/approve/reject/retire only after the foundation is stable.
- Production migration, observability, retention/runbook and navigation enablement.
- PV retains a separate group-capable read/intake rule; verbatim case intake must not be blocked by product scope.
