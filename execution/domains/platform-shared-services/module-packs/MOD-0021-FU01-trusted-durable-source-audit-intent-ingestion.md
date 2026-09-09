---
id: MOD-0021-FU01
name: Trusted Durable Source Audit Intent Ingestion
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: audit-owner / security-owner
branch: feature/pss/mod-0021-fu01-trusted-durable-source-audit-intent-ingestion
started: 2026-08-28
target: 2026-09-04
form_field_count: 0
parent: MOD-0021
delivery_capability_pack: DCP-004
---

# MOD-0021-FU01 — Trusted Durable Source Audit Intent Ingestion

## 1. Module Summary

This follow-up closes the MOD-0021 provider-side part of DCP-004 G4: a trusted source service can submit one immutable, locally durable audit intent and receive an acknowledgement only after Platform has durably accepted that intent into the existing `audit_outbox` collection.

The first source is `Diten.MDM`. The first consumer is MOD-0290 Product / Item / SKU Master, including the MOD-0290-FU03 H1b enforcement activation and suspension intents. This pack does not create a second audit store, a second outbox, a second worker, or a new audit event model. It reuses MOD-0021 `AuditOutboxRepository`, `AuditOutboxWorker`, `AuditOutboxPayloadMapper`, `AuditEvent` and existing query/retention surfaces.

The acknowledgement boundary is **durable central outbox acceptance**. It is not final `audit_events` persistence. Final outbox processing and `AuditEvent` read-back remain MOD-0021 health/operations evidence and are not a synchronous MDM receipt prerequisite.

The original implementation used an FU01-local HMAC JWT scheme plus a second Platform credential and tenant allow-list. MOD-0033-FU02 now owns service-client credential lifecycle, tenant/audience grant and Auth-issued RS256 token issuance. The named reconciliation step **R1 — Adopt MOD-0033 TrustedServiceToken Authority** removes the duplicate FU01 trust source; the already-delivered audit envelope, mapping, outbox acceptance and replay behavior remain unchanged.

## 2. Ownership and Boundaries

### In scope

- One Platform-internal source-intent acceptance endpoint for `Diten.MDM`.
- Independently validated Auth-issued RS256 service JWT through the MOD-0033-FU02 named `TrustedServiceToken` scheme.
- Auth-owned persisted service-client tenant/audience grant checked before token issuance; body/header tenant values never grant access.
- Exact immutable versioned intent envelope and strict unknown-field rejection.
- Exact source-operation-to-MOD-0021 mapping; numeric enum passthrough is forbidden.
- Deterministic central idempotency, exact replay and payload-drift conflict.
- Durable acceptance receipt derived from the existing `audit_outbox` record.
- Reuse of the existing MOD-0021 outbox worker for later `audit_events` persistence.
- Unit, contract, security, real-Mongo and two-service contract evidence.

### Out of scope

- MDM-local intent creation, claim, retry, dead-letter, compaction or worker activation; those remain MOD-0290 Class C.
- Any change to MDM entities, repositories, handlers, API, configuration or data.
- Final `audit_events` persistence as a synchronous acceptance condition.
- A new audit collection, parallel audit service, event-bus topic, webhook, Gateway route or browser UI.
- Dead-letter requeue API, retention purge, source-side compaction, production secrets or tenant-grant provisioning.
- WorkCenter, workflow, navigation, frontend, AuthService or entitlement changes.

### Authority

- DCP-004 already owns this cross-owner G4 sequence; no new DCP is required.
- MOD-0021 owns central audit acceptance/outbox/query/retention.
- MOD-0290 owns the local durable business mutation + embedded intent boundary and its delivery client.

## 3. Owned Objects

### New Platform contracts/services

- `TrustedSourceAuditIntentEnvelope` — exact immutable inbound semantic contract.
- `TrustedSourceAuditIntentAcceptanceReceipt` — authoritative outbox-acceptance receipt.
- `TrustedSourceAuditIntentAcceptanceResult` — queued, exact-duplicate, conflict or controlled failure result.
- `ITrustedSourceAuditIntentAcceptanceService` / `TrustedSourceAuditIntentAcceptanceService`.
- `ITrustedSourceAuditIntentOutbox` — narrow reuse seam implemented by existing `AuditOutboxRepository`.
- `TrustedSourceAuditIntentOperationMap` — exact string mapping table.
- `TrustedSourceAuditIntentCanonicalizer` — deterministic semantic fingerprint builder.

### New API/security objects

- `ITrustedSourceAuditIntentServiceIdentity` / `TrustedSourceAuditIntentServiceIdentity`.
- `ITrustedSourceAuditIntentRequestExecutor` / `TrustedSourceAuditIntentRequestExecutor`.
- Strict request model/parser and `InternalTrustedSourceAuditIntentController`.

### Endpoint

- `POST /api/internal/v1/audit/source-intents/accept`.

### Headers

- `Authorization: Bearer <Auth-issued RS256 service token>` is the only accepted authority header and must have exactly one value.
- Legacy `X-Audit-Source-Credential-Id`, `X-Audit-Source-Credential` and `X-Audit-Source-Audience` headers are forbidden and return non-disclosing `403`; they are never ignored as harmless extras.
- `X-Tenant-Id` is forbidden.

### Existing objects reused, not forked

- `audit_outbox` and its unique `IdempotencyKey` index.
- `AuditOutboxRepository`, `AuditOutboxMessage`, `AuditOutboxWorker`, `AuditOutboxPayloadMapper`.
- `AuditEvent`, `AuditCategory.MasterData`, `AuditOperation`, `AuditOutcome.Succeeded`.

## 4. Entity Fields

No new SoR entity or Mongo collection is introduced. Frontmatter uses `entity_base: BaseEntity` because the resulting central `AuditEvent` remains tenant-owned under MOD-0021. The accepted transport is persisted as an existing internal `AuditOutboxMessage` and later becomes the existing `AuditEvent`.

### Exact immutable source envelope (`mod-0290.audit-intent.v1`)

| Field | Type | Required | Exact rule |
|---|---|---|---|
| `SourceService` | string | Yes | Exact ordinal `Diten.MDM`; max 64. |
| `ContractVersion` | string | Yes | Exact ordinal `mod-0290.audit-intent.v1`; max 64. |
| `IntentId` | Guid | Yes | Non-empty. |
| `TenantId` | Guid | Yes | Non-empty; must equal the independently validated JWT tenant. The tenant/audience grant was already authoritatively checked by MOD-0033 before token issuance. |
| `AggregateType` | string | Yes | Exact canonical name from the mapping table; numeric values rejected. |
| `AggregateId` | Guid | Yes | Non-empty. |
| `PreVersion` | int | Yes | `>= -1`. |
| `PostVersion` | int | Yes | `>= 0` and exactly `PreVersion + 1`, except owner-approved create semantics `-1 -> 0`. |
| `Operation` | string | Yes | Exact canonical name from the mapping table; numeric values rejected. |
| `ActorId` | string | Yes | Non-empty, max 160, exact ordinal source human/system subject evidence; never the transport credential identifier. No GUID assumption is made because the authoritative MDM intent contract stores this field as a string. |
| `CorrelationId` | Guid | Yes | Non-empty. |
| `CausationId` | string | Yes | Non-empty, max 160; exact source value retained in metadata. |
| `CommandId` | string | Yes | Non-empty, max 160; exact source value retained in metadata. |
| `Sequence` | long | Yes | `>= 0`; retained exactly. |
| `TimestampUtc` | DateTimeOffset | Yes | UTC (`+00:00`) and not more than 5 minutes in the future; retained exactly. |
| `EvidenceHash` | string | Yes | Exact 64-character uppercase SHA-256 hex; no evidence payload is accepted. |
| `SnapshotReference` | string? | No | Null or non-empty, max 256; identifier/reference only, no embedded document. |
| `IdempotencyKey` | string | Yes | Source-local idempotency evidence, 1..240; not used as the central unique key. |

Forbidden request fields include local `DeliveryState`, attempt/retry/lease fields, claim token/generation, central acknowledgement, compact receipt, before/after payload, credential, tenant header, password, token or secret data.

### Central idempotency and fingerprint

- Central key is exact ordinal: `Diten.MDM:{TenantId:N}:{IntentId:N}:mod-0290.audit-intent.v1`.
- The server computes `SHA-256` over a versioned, fixed-order, length-prefixed UTF-8 encoding of every immutable envelope field above. JSON property order and whitespace do not affect the fingerprint.
- The outbox payload stores the canonical source envelope, `SourceIntentFingerprint`, mapped MOD-0021 category/operation/entity type, and safe correlation/causation metadata.
- The existing `AuditOutboxMessage.Id` and `CreatedAtUtc` form the durable acceptance receipt. No new receipt collection is created.

### Exact operation mapping

| Source AggregateType | Source Operation | MOD-0021 EntityType | MOD-0021 Operation |
|---|---|---|---|
| `CodeReservation` | `CodeReserved` | `CodeReservation` | `Create` |
| `CodeReservation` | `CodeConsumed` | `CodeReservation` | `Update` |
| `CodeReservation` | `CodeBindingConfirmed` | `CodeReservation` | `Update` |
| `CodeReservation` | `CodeBurned` | `CodeReservation` | `Deactivate` |
| `GlobalProduct` | `GlobalProductDraftCreated` | `GlobalProduct` | `Create` |
| `ProductDefinitionRevision` | `ProductDefinitionRevisionDraftCreated` | `ProductDefinitionRevision` | `Create` |
| `Gsku` | `GskuDraftCreated` | `Gsku` | `Create` |
| `Gsku` | `GskuDraftUpdated` | `Gsku` | `Update` |
| `FinishedGood` | `FinishedGoodDraftCreated` | `FinishedGood` | `Create` |
| `Lsku` | `LskuDraftCreated` | `Lsku` | `Create` |
| `ProductLegalEntityScopePolicy` | `ProductLegalEntityScopePolicyCreated` | `ProductLegalEntityScopePolicy` | `Create` |
| `ProductLegalEntityScopePolicy` | `ProductLegalEntityScopePolicyReplaced` | `ProductLegalEntityScopePolicy` | `Update` |
| `ProductLegalEntityScopePolicy` | `ProductLegalEntityScopePolicyEnded` | `ProductLegalEntityScopePolicy` | `Deactivate` |
| `ProductLegalEntityScopeRolloutState` | `ProductLegalEntityScopeEnforcementActivated` | `ProductLegalEntityScopeRolloutState` | `Activate` |
| `ProductLegalEntityScopeRolloutState` | `ProductLegalEntityScopeEnforcementSuspended` | `ProductLegalEntityScopeRolloutState` | `Suspend` |

All rows map to `AuditCategory.MasterData`, `AuditOutcome.Succeeded`, `SourceService=Diten.MDM`, `SourceModule=product-item-sku-master`. Any other aggregate/operation pair is rejected; the provider never casts a source numeric enum to a Platform numeric enum.

## 5. Repo Scope

The original Section 5 implementation is preserved as historical delivery evidence. R1 is a new, separately gated remediation step. No R1 runtime or test change is authorized by the earlier FU01 code-start approval.

### Runtime allow-list

- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentEnvelope.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentAcceptanceReceipt.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/TrustedSourceAuditIntentAcceptanceResult.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/ITrustedSourceAuditIntentAcceptanceService.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/ITrustedSourceAuditIntentOutbox.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentAcceptanceService.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentCanonicalizer.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentOperationMap.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditOutboxRepository.cs` — existing, narrow interface implementation only.
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` — existing, narrow DI registration only.
- `services/Diten.Platform/src/Diten.Platform.API/Models/Audit/TrustedSourceAuditIntentRequest.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Models/Audit/TrustedSourceAuditIntentRequestParser.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedSourceAuditIntentServiceIdentity.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentServiceIdentity.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedSourceAuditIntentRequestExecutor.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentRequestExecutor.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalTrustedSourceAuditIntentController.cs` — new.
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — existing, options/security/service registration only.

`AuditOutboxMessage`, `AuditEvent`, indexes and worker files are reuse-only unless an implementation review proves an unavoidable additive field is required. Such a finding stops code-start and requires this pack's allow-list to be revised and re-approved; it cannot be added opportunistically.

### Test allow-list

- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentContractTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentSecurityTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentAcceptanceTests.cs` — new.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentMongoTests.cs` — new, real `localhost:27017`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentTwoServiceContractTests.cs` — new, exact MDM wire fixture/receipt contract.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs` — existing, registration/non-hosted assertions only.
- Existing MOD-0021 audit/outbox test files may be executed as regression evidence but not edited unless separately added to this allow-list by owner-approved pack revision.

### R1 — Adopt MOD-0033 TrustedServiceToken Authority (separate code-start)

R1 may change only the following exact paths. All other FU01 runtime, acceptance, persistence, mapping and parser files are reuse-only.

Modify:

- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedSourceAuditIntentServiceIdentity.cs` — remove the FU01-local allowed-tenant-set argument; return only the independently validated token identity and tenant, plus an explicit `Unavailable` result for invalid validator configuration.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentServiceIdentity.cs` — authenticate only `TrustedServiceTokenValidationExtensions.AuthenticationScheme`, retain defense-in-depth exact service/audience/tenant cardinality checks, and map validator-configuration `InvalidOperationException` to `IdentityResult.Unavailable` without falling back to another scheme.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentRequestExecutor.cs` — remove the second credential dependency and required legacy headers; enforce one bearer header, reject legacy credential headers and `X-Tenant-Id`, require token tenant = envelope tenant, and map `IdentityResult.Unavailable` to non-leaking `503 AUDIT_SOURCE_INTENT_UNAVAILABLE` before body/repository dispatch.
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs` — remove the FU01-local credential options, HMAC/JwtSettings named bearer registration and credential-authenticator DI; reuse the MOD-0033 named RS256 scheme without changing the human JWT default.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentSecurityTests.cs` — replace the obsolete double-credential/static-grant tests with bearer-only scheme, downgrade-rejection, tenant-equality and validator-configuration `InvalidOperationException` → `503` tests.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs` — prove the old FU01 scheme/config/authenticator is absent and the MOD-0033 named scheme is reused without becoming a hosted service or human default.

Delete:

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedSourceAuditIntentCredentialOptions.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Security/ITrustedSourceAuditIntentCredentialAuthenticator.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedSourceAuditIntentCredentialAuthenticator.cs`.

MOD-0033-FU02 files are predecessor artifacts and **reuse-only** in R1; R1 may not edit or fork them:

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/TrustedServiceTokenValidationOptions.cs`.
- `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs`.
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Security/TrustedServiceTokenValidationTests.cs`.

R1 code-start is blocked until the user separately approves this exact remediation allow-list and the implementation base contains an immutable, verified MOD-0033-FU02 predecessor.

### Governance write scope

- This pack.
- `execution/registries/module-id-registry.md` identity row only.

## 6. Protected Paths

- `.antigravity/**`.
- `services/Diten.MdmService/**` — both runtime and tests; MOD-0290 owns its transport client/worker.
- `services/Diten.AuthService/**`, `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`.
- Existing MOD-0021 `AuditEvent`, retention, query/export, redaction and UI behavior.
- Existing `AuditOutboxWorker`, retry/dead-letter state machine and `AuditOutboxPayloadMapper` unless a pack revision is approved.
- New Mongo collection/entity/index, parallel outbox, event bus, webhook, hosted ingestion worker or auto-start provisioning.
- `gateway/Diten.ApiGateway/**`, `frontend/Diten.Web/**`, WorkCenter and navigation.
- `appsettings*.json`, committed credentials/secrets, tenant grants or production data.
- Direct Mongo writes outside the real-Mongo test fixture.
- Git branch/stage/commit/push operations.

## 7. Dependencies

- DCP-004 G4, especially durable-central-outbox acknowledgement and central idempotency material.
- MOD-0021 existing `audit_outbox`, unique idempotency index, worker, mapper and `AuditEvent` store.
- MOD-0290 existing `LocalAuditIntent`, `AuditIntentContract.BuildCentralIdempotencyKey`, fenced delivery repository and exact 15 operation vocabulary.
- MOD-0290-FU03 H1b requires central acceptance only for its type 7/8 activation/suspension intents; final audit-event persistence is observed asynchronously.
- MOD-0033-FU02 owns the dedicated `Diten.MDM` client identity, credential rotation, persisted exact tenant/audience grant, Auth-issued RS256 token and Platform `TrustedServiceToken` current/previous public-key validator.
- FU02 temporal compatibility/migration code is a predecessor of the current FU01 implementation. Its operational migration and cutover remain separate prerequisites for FU01 operational completion.
- Integration base must be a verified successor containing current main integration (`c2cc8e10` or its fetched successor), MOD-0021-FU02, the existing MOD-0021-FU01 provider implementation and MOD-0033-FU02. The branches may not be implicitly rebased, cherry-picked or copied while the MOD-0033 predecessor is uncommitted.

Lookup/reference-data decision: no lookup key or business reference-data family is required. Operation/aggregate vocabulary is an invariant mapping table, not editable lookup data.

## 8. Runtime Constraints

- Processing order is fixed: request-size and duplicate/forbidden-header guard → named `TrustedServiceToken` RS256 authentication → exact service identity/audience/claim cardinality → JWT tenant extraction → strict body parse → token/envelope tenant equality → envelope validation/mapping → canonical fingerprint → durable outbox acceptance.
- JWT must satisfy the exact MOD-0033 contract: RS256, known non-expired `kid`, exact issuer, exact 300-second lifetime, exactly one `actor_type=service`, one `service_name=Diten.MDM`, one non-empty `tenant_id`, and exact `TRUSTED_AUDIT_SOURCE_INGEST` audience. A tenant-user JWT, platform-admin JWT or self-declared body actor cannot act as the transport service.
- `X-Tenant-Id` is always rejected. `TenantId` from the envelope is equality evidence only, never authorization.
- The exact tenant/audience grant is checked by Auth before issuance and cryptographically attested by the short-lived token. Platform does not maintain a second tenant allow-list or accept a wildcard/default/body/header fallback.
- Grant disable/revoke has a bounded maximum propagation delay equal to the already-issued token's exact 300-second lifetime. Platform does not introspect Auth per request; accepting this bounded lag is the price of one authoritative grant source and must be approved by the Security and Operations owners before R1 code-start.
- Legacy `X-Audit-Source-Credential-Id`, `X-Audit-Source-Credential` and `X-Audit-Source-Audience` headers return `403` before body parse or repository access. There is no HMAC/JwtSettings or human-token fallback.
- Missing/malformed TrustedServiceToken public-key/issuer configuration fails closed as `503 AUDIT_SOURCE_INTENT_UNAVAILABLE`; it is never translated to `401`, allowed through, or retried against the human JWT scheme.
- Request body maximum is 32 KiB. Snapshot/evidence bodies are prohibited; only a SHA-256 hash and optional bounded reference are accepted.
- Acceptance has a two-second linked server budget. Caller cancellation propagates unchanged.
- The endpoint is never routed through browser/Gateway and never permits anonymous semantic access despite using a custom internal executor.
- New acceptance: insert exactly one outbox row and return `201`.
- Exact replay: same central key and same canonical fingerprint returns the original receipt with `200`; no second row/event is created.
- Drift replay: same central key but different fingerprint returns `409 AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT`; existing row remains unchanged.
- A duplicate-key race must read back the winning row and classify exact replay versus drift; it cannot report an unverified duplicate.
- `201/200` means durable `audit_outbox` acceptance only. Worker completion or `audit_events` visibility is not awaited.
- The existing outbox worker later writes one `AuditEvent`; final-event failures remain governed by existing MOD-0021 retry/dead-letter operations.
- No log line contains credential, JWT, raw envelope, actor email, evidence content or secret material.

## 9. Layout & Shell Contract

- `shell: none`.
- No Razor layout, MVC page, navigation entry, DataTable or localization resource is added.
- `_LayoutPlatformAdmin`, `_LayoutTenantShell` and `_Layout.cshtml` are unaffected.

## 10. Backend File Convention

This is a backend-only trusted ingestion adapter inside the existing MOD-0021 audit feature, not a CRUD/DataTable module. `golden_reference: none` is therefore intentional.

- One public type per file.
- API controller remains thin and delegates to the request executor/acceptance service.
- Security parsing/authentication remains in API security classes.
- Canonicalization and source-operation mapping remain deterministic Application services.
- Mongo driver code remains in Infrastructure through `ITrustedSourceAuditIntentOutbox`.
- No generic `Create/Update/Delete` command/query surface is created.
- Existing `Response<T>` / `CustomBaseController` envelope conventions apply to controlled endpoint responses.

## 11. Frontend File Contract

Not applicable. This pack creates no frontend files, route, menu, form, Save View, browser token/header logic or Gateway change.

## 12. Validation Rules

| Input | Required | Validation | Failure |
|---|---|---|---|
| Request body | Yes | JSON object, max 32 KiB, exact known properties, no duplicate properties | `400 AUDIT_SOURCE_INTENT_INVALID` or `413 AUDIT_SOURCE_INTENT_TOO_LARGE` |
| Authorization | Yes | Exactly one bearer value; MOD-0033 named RS256 scheme, exact current/previous `kid`, issuer, 300-second lifetime and ten-claim service contract | `401/403` |
| Legacy/source tenant headers | Forbidden | Any old FU01 credential/audience header or `X-Tenant-Id` is a downgrade attempt | non-leaking `403 AUDIT_SOURCE_INTENT_FORBIDDEN` |
| Tenant binding | Yes | JWT tenant = envelope tenant; grant authority is the Auth issuance decision | non-leaking `403 AUDIT_SOURCE_INTENT_FORBIDDEN` |
| Contract/source | Yes | Exact ordinal version and source service | `409 AUDIT_SOURCE_INTENT_CONTRACT_UNSUPPORTED` |
| IDs/version/sequence | Yes | Rules in §4 | `400 AUDIT_SOURCE_INTENT_INVALID` |
| Aggregate/operation | Yes | Exact pair in §4 mapping; numeric values/aliases/case folding rejected | `409 AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` |
| Evidence | Yes | Uppercase SHA-256 hex; no raw evidence | `400 AUDIT_SOURCE_INTENT_INVALID` |
| Central key/fingerprint | Server | Recomputed; never accepted from request | `409` on drift |

No trim/case-fold/alias/fuzzy normalization is used for source service, contract version, aggregate type or operation. Text fields that permit surrounding whitespace are invalid rather than silently normalized.

## 13. Failure Path to Verify

- **Missing/malformed/cryptographically invalid bearer** → `401`; no body dispatch and no outbox write.
- **Wrong service identity/audience/claim cardinality, human token or tenant mismatch** → `403`; no tenant existence leak and no outbox write.
- **Legacy FU01 credential headers, `X-Tenant-Id` or duplicate auth headers** → `403`; no downgrade or body-derived tenant switch is possible.
- **Invalid/missing named-validator issuer/public-key configuration** → `503 AUDIT_SOURCE_INTENT_UNAVAILABLE`; no body parse, repository access or scheme fallback.
- **Malformed/unknown/duplicate JSON property, numeric enum, invalid GUID/version/hash** → `400`; no write.
- **Unsupported contract or aggregate-operation pair** → `409`; no write.
- **Oversized request** → `413`; body is not dispatched.
- **Persistence unavailable** → `503 AUDIT_SOURCE_INTENT_UNAVAILABLE`; no false acknowledgement.
- **Two-second budget exceeded** → `504 AUDIT_SOURCE_INTENT_TIMEOUT`.
- **Exact replay** → `200`, original acknowledgement/key/version/time, `Duplicate=true`, one outbox row.
- **Concurrent first submit** → one `201`, remaining exact requests `200`, one outbox row.
- **Payload drift under the same central key** → `409 AUDIT_SOURCE_INTENT_IDEMPOTENCY_CONFLICT`, winning row unchanged.
- **Crash after durable insert but before HTTP response** → retry returns exact duplicate receipt; no second row.
- **Outbox worker not yet completed** → acceptance still valid; local source may mark delivered from the durable receipt.
- **Final audit-event processing later fails** → existing outbox retry/dead-letter handles it; synchronous receipt is not revoked.

## 14. Authorization Convention

- This endpoint does not use human RBAC permission keys and is not `[AllowAnonymous]` in the trust sense; it uses a dedicated, fail-closed internal executor.
- Transport identity and business actor are separate:
  - transport: independently validated Auth-issued RS256 `TrustedServiceToken` bearer;
  - business actor: immutable `ActorId` in the signed/authenticated source envelope and resulting audit metadata.
- Exact consumer: `Diten.MDM`.
- Exact audience: `TRUSTED_AUDIT_SOURCE_INGEST`.
- Exact tenant grant: persisted and enforced by MOD-0033/Auth before token issuance; wildcard forbidden.
- Raw tenant header/body values never create authority.
- Platform does not keep an FU01-specific shared secret, HMAC validation key or duplicate tenant grant.
- Existing shared `AuthService:InternalApiKey` endpoint is not used for this contract.

## 15. Gateway / API Routing Decision

Decision: Gateway change is unnecessary and forbidden.

- The endpoint is service-to-service on Platform's internal API surface.
- Browser/frontend callers are not supported.
- No Ocelot base/catch-all pair is added.
- MOD-0290's future client obtains a short-lived token from the MOD-0033 Auth issuance endpoint and calls the configured Platform internal base address with only that bearer under its own separately approved Class C step.

## 16. Acceptance Criteria

- [ ] DCP-002 verifier passes for `MOD-0021-FU01` under parent `MOD-0021`.
- [ ] Exactly one new internal endpoint exists: `POST /api/internal/v1/audit/source-intents/accept`.
- [ ] Existing shared-key `/api/internal/audit/append` is neither widened nor used by this contract.
- [ ] Only the independently validated MOD-0033 RS256 service token is accepted; the grant is enforced by Auth before issuance and no second Platform credential/grant source remains.
- [ ] `X-Tenant-Id`, body-derived authority, wildcard tenant grant and platform/tenant-user JWTs are rejected.
- [ ] All three legacy `X-Audit-Source-*` headers are rejected with `403` before body parse/repository access.
- [ ] Exact immutable envelope, 32 KiB limit and 15-row mapping table are enforced without numeric enum passthrough.
- [ ] New acceptance reuses `audit_outbox`; no new entity, collection, index, outbox, worker or final audit store is created.
- [ ] `201` is returned only after durable outbox insert.
- [ ] Exact replay returns the original receipt with `200` and one persisted outbox row.
- [ ] Same key with any immutable-field drift returns `409` and does not mutate the winning row.
- [ ] Receipt contains `CentralAcknowledgement`, exact central idempotency key, exact contract version, `AcceptedAt`, and `Duplicate`.
- [ ] MDM can treat `201/200` as durable central-outbox acknowledgement without waiting for `audit_events` persistence.
- [ ] Existing outbox worker later maps the accepted row to exactly one `AuditEvent` with `MasterData`, mapped operation/entity, source correlation/causation and evidence hash.
- [ ] Final `audit_events` read-back is health/operations evidence, not a synchronous acceptance prerequisite.
- [ ] No secret/JWT/raw evidence is logged or committed.
- [ ] No MDM, Auth, Gateway, frontend, WorkCenter, config/data or navigation file changes occur.

## 17. Test Expectations

### Focused unit/contract/security

- Strict JSON parser: exact fields, duplicate/unknown fields, case drift, numeric enums, size limit.
- All 15 mapping rows plus every invalid pair.
- Canonical fingerprint stability across JSON property order/whitespace and sensitivity to every semantic field.
- Direct A→B→FU01 scheme evidence: an Auth-issued current-key token passes the Platform named scheme and FU01 endpoint; an eligible previous-key token passes only strictly before overlap expiry.
- Wrong/unknown/missing `kid`, HS256, `none`, wrong issuer/audience/service, human claims, duplicate claims and non-exact 300-second lifetime fail closed before body/repository dispatch.
- Authorization is the only accepted authority header; missing/duplicate bearer, `X-Tenant-Id` and every legacy `X-Audit-Source-*` header are rejected before body dispatch or repository access.
- Invalid/missing TrustedServiceToken validator configuration raises no unhandled response: `IdentityResult.Unavailable` maps deterministically to `503 AUDIT_SOURCE_INTENT_UNAVAILABLE`, and the body/repository remains untouched.
- Token tenant must equal envelope tenant. Auth grant disable/revoke prevents new issuance; an already-issued token remains bounded by the exact 300-second lifetime and no longer.
- DI/static contract tests prove the old `TrustedSourceAuditIntent` HMAC/JwtSettings scheme, options and authenticator are absent while the human JWT default remains unchanged.
- Exact `Response<T>` codes for 400/401/403/409/413/503/504.

### Real Mongo (`mongodb://127.0.0.1:27017`)

- Disposable, uniquely named test database; no fake/in-memory/skip path.
- DB-010 applies: initialize only `SchemaProfile.AccessGovernance` through the merged
  `PlatformSchemaManifest.ApplyAsync` API. Calling the legacy full-schema `EnsureIndexesAsync` path or copying a
  second audit schema/index list into the test fixture is forbidden.
- The current feature branch must first contain merged schema-profile commit `2935f9d1` and test-infrastructure
  hardening commit `aced8c84` (or their verified successors). Their absence blocks runtime code-start; this dirty
  worktree must not be merged/rebased implicitly by the implementation task.
- New insert persists one existing `audit_outbox` row and returns its durable receipt.
- Sequential and concurrent exact replay preserve one row and the original receipt.
- Sequential and concurrent payload drift return `409` and preserve the winning row.
- Simulated response loss after insert followed by replay returns exact duplicate.
- Tenant A/B with the same `IntentId` produce different central keys and isolated rows.
- Worker processing produces exactly one existing `AuditEvent`; duplicate acceptance produces no second event.
- Outbox processing failure/retry/dead-letter regressions remain green.
- Audit Outbox DateTimeOffset correctness evidence must measure the existing BSON representation for
  `AuditOutboxMessage.NextAttemptAtUtc` and `CreatedAtUtc`, including the existing single-key ascending
  `CreatedAtUtc` claim ordering and status/range query. This FU must not add a new ascending `DateTimeOffset`
  sort/index or silently introduce a global serializer/migration change.

### Two-service contract evidence

- Serialize an exact `Diten.MDM` `mod-0290.audit-intent.v1` fixture using the source wire contract and submit it through the real Platform HTTP endpoint.
- Validate `201`, source-side acknowledgement projection, exact replay `200`, and drift `409` without direct Mongo mutation.
- For FU03 H1b, prove both `ProductLegalEntityScopeEnforcementActivated` and `ProductLegalEntityScopeEnforcementSuspended` obtain durable receipts and can be marked delivered/compacted by the separately authorized MDM consumer step.
- A failure after Platform acceptance and before MDM local acknowledgement must replay to the same receipt.
- This end-to-end test may run only after the MOD-0290 Class C client exists; its absence blocks integration acceptance, not this provider pack's draft authoring.

### Regression/build

- All focused MOD-0021 audit/outbox tests.
- Full Platform test suite, zero skipped in the scoped groups.
- Platform API Release build.
- `git diff --check`, conflict markers, trailing whitespace, final newline and secret-pattern scan.

## 18. Ready-for-dev Checklist

- [x] DCP-004 G4 authority and MOD-0021 ownership read.
- [x] Existing MOD-0021 outbox/controller/security code truth measured.
- [x] DCP-002 preflight passed mechanically against parent `MOD-0021`.
- [x] No existing `MOD-0021-FU01` collision found.
- [x] `shell: none`, `golden_reference: none`, `form_field_count: 0` selected for backend-only scope.
- [x] Exact endpoint, envelope, mapping, idempotency, acknowledgement and failure contracts written.
- [x] Exhaustive runtime/test allow-list and protected paths written.
- [ ] Audit owner accepts the 15-row mapping and `mod-0290.audit-intent.v1` wire contract.
- [ ] Security owner accepts R1's single MOD-0033 RS256 authority, explicit legacy-header rejection and bounded 300-second grant-revocation lag.
- [ ] MDM/Product Data owner confirms its future client emits the exact v1 envelope and does not use shared internal key/body tenant trust.
- [ ] Operations owner accepts the two-second/32-KiB bounds, Auth-owned client/grant provisioning, Platform public-key rotation and bounded token-revocation lag.
- [x] The implementation base contains verified successors of `2935f9d1` and `aced8c84`; real-Mongo tests use only
  `SchemaProfile.AccessGovernance` under DB-010.
- [x] Real-Mongo evidence classifies the existing audit-outbox `DateTimeOffset` claim ordering/range behavior. Any
  serializer, migration or shared index fix remains outside this pack and blocks operational completion if unresolved.
- [x] Pack status explicitly promoted to `ready-for-dev` by user approval on 2026-08-28.
- [x] User separately authorized Section 5 runtime/test code-start on 2026-08-28.
- [x] R1 exact remediation allow-list received explicit user code-start approval on 2026-08-28.
- [x] R1 was integrated on current-main base `2e7c83e6` with FU02 `635ef04f`, FU01 `baee3fef` and the reviewed MOD-0033-FU02 A+B runtime/test delta ported in dependency order; no predecessor file was silently preferred over current-main code truth.

## 19. Implementation Notes

- Preflight command (mechanical compatibility evidence):
  - `python .antigravity/scripts/verify_module_id.py . --check-id MOD-0021-FU01 --name "Trusted Durable Source Audit Intent Ingestion" --parent MOD-0021`
  - Result on 2026-08-28: `OK MOD-0021-FU01: proven against Blueprint/registry.`
- The current `/api/internal/audit/append` is not sufficient: it uses the shared internal key, trusts body tenant after middleware bypass, derives a generic idempotency key, and cannot distinguish exact replay from payload drift.
- The current governed human/JWT append endpoint is also not the service contract: it does not use the exact MOD-0033 service-token scheme and does not accept the DCP-004 source idempotency material.
- Phase 1.5 architecture was user-approved and the pack was promoted to `ready-for-dev`; the user separately authorized Section 5 runtime/test code-start on 2026-08-28. Configuration, credential, tenant-grant and operational-run authority remain outside that approval.
- The existing `audit_outbox` unique key and record identity are sufficient for durable acceptance if duplicate-key races are followed by exact winner read-back and fingerprint comparison.
- The accepted outbox payload must remain compatible with the existing `AuditOutboxPayloadMapper`; this pack does not authorize a mapper fork.
- Source `DeliveryState`, claim token/generation, retries and compaction fields are local mutable bookkeeping and are never part of the central request.
- Contract changes require a new explicit contract version and owner-approved mapping; silent additive/optional drift under v1 is forbidden.
- No production credential, tenant grant or endpoint enablement is implied by runtime implementation completion.
- The 2026-08-28 Enterprise Strategy handoff is not an authority for MOD-0021. Its DateTimeOffset statement applied
  only to Enterprise Strategy; code truth shows MOD-0021 audit outbox uses `DateTimeOffset` and an ascending
  `CreatedAtUtc` claim sort, so DateTimeOffset BSON correctness is a real audit operational gate. The shared Diten.Web/Enterprise Strategy
  database and their pre-existing red tests remain separate baseline debt and are not changed by this pack.
- Audit Outbox DateTimeOffset measurement on 2026-08-28 used MongoDB.Driver 2.27.0 and an isolated real
  `localhost:27017` database. Both fields were persisted as `[ticks, offsetMinutes]` BSON arrays. Mixed-offset
  `CreatedAtUtc` ascending order selected the latest `-12:00` item before the earlier UTC and `+14:00` items.
  Both retry-ready and stale-processing `NextAttemptAtUtc <= cutoff` predicates produced false negatives for
  eligible `+14:00` values and a false positive for an ineligible future `-12:00` value. Explain used
  `ix_audit_outbox_status_next_attempt` as a multikey `IXSCAN`; the index therefore accelerated an incorrect
  predicate rather than correcting it. Product serializers, indexes and data were not changed; the temporary
  database and measurement helper were removed.
- Canonical `docs/product-backlog.md` assigns `BL-030` to the DateTimeOffset BSON storage migration. FU02 implements
  the versioned scalar-shadow compatibility and migration foundation on this branch; an operational migration run
  remains separately gated and was not executed by FU01.
- Runtime/test evidence on 2026-08-28: focused FU01 `73/73`, existing Audit Outbox plus FU01 regression `148/148`,
  and full Platform `2783/2783`, all with zero skipped. The six real-Mongo cases used `localhost:27017` and only
  `SchemaProfile.AccessGovernance`; Release Platform API build completed with zero errors and four existing warnings.
- Independent rereview of the original implementation closed its named-JWT audience conflict, exact claim casing,
  mapper compatibility, bounded SnapshotReference, full two-second budget and one-public-type-per-file findings. That
  evidence predates R1 and does not claim the MOD-0033 alignment blocker is closed.
- R1 reconciliation completed on 2026-08-28. The endpoint now authenticates only through the MOD-0033-FU02 named
  RS256 `TrustedServiceToken` scheme. The FU01-local HMAC/JwtSettings scheme, symmetric credential options and
  authenticator, Platform tenant allow-list and positive legacy `X-Audit-Source-*` transport path are absent.
  Legacy credential headers and `X-Tenant-Id` are explicitly rejected before body parsing; the validated token tenant
  must equal the immutable envelope tenant, and invalid validator configuration maps fail-closed before persistence.
- Integration preserved current-main WorkAggregation/remote-provider/domain-normalization behavior, FU02 temporal-safe
  outbox persistence and explicit non-hosted migration runner, and MOD-0033's unchanged human-JWT default. Focused
  R1/FU01/FU02/MOD-0033/DI/schema tests passed `171/171` (including owned two-second timeout `504` and caller-cancellation propagation), Auth token tests passed `47/47`, full Platform passed
  `2882/2882` and full Auth passed `393/393`, all with zero skipped. Platform API Release compiled with zero warnings/errors; Auth API Release compiled with zero errors and one existing GUID-representation warning.
  Real-Mongo tests used the existing isolated fixtures; no operational migration, configuration, credential, tenant
  grant, business data, commit or push action was performed.

### MOD-0290 GSKU lifecycle audit-map amendment — approved code-start (2026-09-08)

Only the strict `TrustedSourceAuditIntentOperationMap` and its contract test may add the 17 exact
`ProductDefinitionRevision`/`Gsku` lifecycle pairs specified by MOD-0290 19.17: Revision submit/approve/reject/
withdraw/retire; GSKU submit/approve/reject/withdraw; correction request/applied/rejected/manual-reconciliation;
retirement request/rejected/manual-reconciliation; and final GSKU retire. The first fifteen map to
`LifecycleTransition`; the two retire pairs map to `Deactivate`. Numeric, case-insensitive, wildcard, LSKU and ABB
aliases remain rejected. This amendment creates no source client, credential, transport, configuration or runtime
activation.

## 20. Follow-up Items

### LSKU lifecycle strict mapping integration — approved 2026-09-09

MOD-0290 section 19.18.4 and the current user approval authorize only these paths and this pack:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentOperationMap.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentContractTests.cs`

Protected source `ffdd280a` was checked against `ProductAuditOperation` and
`LskuIdentityLifecycleAuditIntentFactory`, the draft producer and retirement processor.
Every row has aggregate `AuditAggregateType.Lsku`, transported as exact `Lsku`.
The original ordinal identities are preserved, not accepted as numeric wire aliases:

| MDM operation | Ordinal | Central action |
|---|---|---|
| LskuDraftCreated | 10 | Create |
| LskuIdentitySubmitted | 28 | LifecycleTransition |
| LskuIdentityApproved | 29 | LifecycleTransition |
| LskuIdentityRejected | 30 | LifecycleTransition |
| LskuIdentityRetired | 31 | Deactivate |
| LskuIdentityApprovalWithdrawn | 66 | LifecycleTransition |
| LskuRetirementRequested | 67 | LifecycleTransition |
| LskuRetirementRejected | 68 | LifecycleTransition |

Draft creation already existed. Seven lifecycle rows are added. The protected final map omitted
submitted/approved/rejected despite their explicit approved contract and intent producers; those omissions
are reconciled against the contract, not reproduced. No other aggregate, operation, alias or wildcard is added.
This does not claim source worker delivery, lifecycle integration or live acceptance.

Verification: exact mapping/strict parser contract tests 67 passed / 0 failed / 0 skipped.
The overlapping FU01 trusted-source regression run passed 116 / 0 / 0. Platform Release build passed
with 0 warnings and 0 errors. No runtime intake, config, credential or operational data was changed.

- MOD-0290 Class C client/worker/operational activation remains in the approved DCP-004 sequence and its owning pack, not here.
- End-to-end FU03 H1b activation/suspension acceptance can close only after this provider pack and the MDM client step both pass.
- FU02 operational migration execution and Production/Staging temporal cutover remain separately authorized work;
  FU01 does not execute them or mutate existing outbox data.
- Auth service-client credential/tenant-grant provisioning, signing/private-key configuration, Platform public-key configuration, rotation/revocation runbook and live smoke require separate operational authorization.
- Final outbox-worker-to-`AuditEvent` processing/read-back, retry/dead-letter alerts and health metrics remain MOD-0021 operations evidence.
- Source-side retention/purge/redaction and compact-receipt timing remain MOD-0290 G4 gates; this provider does not own them.
- Additional source services or contract versions require separate owner-approved allow-list/mapping changes; wildcard multi-source ingestion is forbidden.

> Module pack `review` durumundadır. İlk Section 5 runtime/test implementation ve R1 MOD-0033 RS256 authority
> reconciliation tamamlandı ve doğrulandı. Configuration, credential, tenant-grant, MDM source client, FU02 operational migration/cutover,
> operational-run ve Production/Staging enablement hâlâ ayrı onay kapılarıdır.
> Backend-only olduğu için Golden Reference `none`; UI/DataTable sapması yoktur.

### 2026-09-09 ABB Phase 1.5 strict mapping integration

Owner-approved MOD-0290-FU01 amendment: only the exact ProductAbbreviation aggregate and its eleven named
allocation/correction/retirement operations (MDM ordinals 51-61) are appended to the existing operation map.
Requested allocation/correction map to Create; approved retirement maps to Deactivate; the other eight map to
LifecycleTransition. The protected factory's eleven history event cases were compared individually with the enum.
Exact paths:

- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/TrustedSourceAuditIntentOperationMap.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/TrustedSourceAuditIntentContractTests.cs`

Focused contract tests: 89 passed, 0 failed, 0 skipped, including eleven positive ABB pairs and eleven parameterized
negative cases (wrong aggregate, case drift, numeric aliases, suffixes and unknown operations). Existing mappings
remain intact. Platform API Release build passed. These are contract tests, not real-Mongo or live intake acceptance.
