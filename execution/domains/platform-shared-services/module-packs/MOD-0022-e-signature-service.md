---
id: MOD-0022
name: e-Signature Service
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
golden_reference: compact
entity_base: TenantScopedEntity
status: in-progress
owner: platform-team
branch: feature/pss/mod-0022-e-signature-service
started: 2026-06-15
target: 2026-07-31
form_field_count: 10
---

# MOD-0022 — e-Signature Service

> **Execution gate:** DCP-003 and this module pack are `approved`. The
> authorized slice is the internal-evidence MVP.
> Tenant-shell and production-provider work remain deferred.

> **Shell note:** Frontmatter records `platform-admin` as the approved first
> shell. Tenant-facing request/signing UX is a follow-up and is not authorized
> by this implementation slice.

## 1. Module Summary

MOD-0022 provides a shared, tenant-isolated e-Signature capability in
`Diten.Platform`. It owns signature envelopes, signer participants,
attestations, internal evidence state and verification artifacts. Tenant business
modules retain ownership of the signed subject and consume MOD-0022 through
the `ESIGN-BUNDLE` contract.

The approved first slice exposes Platform Admin monitoring, internal evidence
verification and audit export. Tenant-facing request/signing UX is deferred.

MVP substitute and production provider behavior are separate delivery modes.
The substitute is internal signed approval evidence and is not represented as
legally binding provider-backed electronic signature.

## 2. Ownership and Boundaries

### In Scope

- tenant-scoped signature envelopes;
- signer participant ordering and state;
- signer identity binding and attestation;
- signature verification artifacts;
- Platform Admin monitoring and internal verification;
- audit export;
- Gateway-only API exposure;
- internal MVP substitute contract.

### Out of Scope

- ownership of the signed business object;
- controlled document content/version ownership;
- approval workflow definition ownership;
- Audit Trail system-of-record ownership;
- records retention/legal-hold implementation;
- provider console implementation;
- tenant-facing request, signing and decline UI;
- provider-neutral port/interface creation;
- provider adapter, callback and credential implementation;
- direct changes to AuthService permission generation;
- direct `ocelot.json` changes;
- provider procurement and commercial decisions.

### System-of-Record Boundary

| Concern | Owner |
|---|---|
| Signature envelope | MOD-0022 |
| Participant and attestation | MOD-0022 |
| Verification artifact | MOD-0022 |
| Signed subject | Consuming tenant/domain module |
| MVP immutable artifact reference | Consuming module contract, snapshotted by MOD-0022 |
| MVP signature policy | MOD-0022 `INTERNAL_APPROVAL_EVIDENCE_V1` |
| Audit event | MOD-0021 |
| Production document/version | MOD-0029 |
| Production signature policy | MOD-0005 |
| Production provider operation | MOD-0264 |

## 3. Owned Objects

### Domain Aggregates

- `SignatureEnvelope`
- `SignatureParticipant`
- `SignerAttestation`
- `SignatureVerificationArtifact`
- `SignatureAuditExport`

Production-deferred aggregate:

- `ProviderCallbackReceipt`

### Domain Enums

- `SignatureEnvelopeStatus`
- `SignatureParticipantStatus`
- `SignatureProviderType`
- `IdentityVerificationMethod`
- `SignatureVerificationStatus`
- `SignatureAttestationType`
- `SignatureSigningMode`

Production-deferred enum:

- `ProviderCallbackProcessingStatus`

### Application Commands

- `CreateSignatureRequestCommand`
- `UpdateDraftSignatureRequestCommand`
- `SendSignatureRequestCommand`
- `CancelSignatureRequestCommand`
- `RecordInternalAttestationCommand`
- `GenerateSignatureAuditExportCommand`

Deferred commands:

- `SignSignatureRequestCommand`
- `DeclineSignatureRequestCommand`
- `ProcessProviderCallbackCommand`
- `ReconcileSignatureRequestCommand`

### Application Queries

- `GetSignatureRequestListQuery`
- `GetSignatureRequestByIdQuery`
- `GetSignatureParticipantsQuery`
- `GetSignatureVerificationQuery`
- `GetPlatformSignatureRequestListQuery`
- `GetSignatureAuditExportQuery`

Deferred queries:

- `GetMySignatureListQuery`
- `GetProviderHealthQuery`
- `GetFailedProviderCallbacksQuery`

### Approved MVP Services

- existing MOD-0021 audit append/outbox contract
- `IInternalSignatureArtifactStore`
- `IInternalSignaturePolicy`

### API Groups

- Platform Admin API
- Public health endpoint only

### Frontend Surfaces

- Platform Admin signature operations

## 4. Entity Fields

Base type for tenant-owned records: `TenantScopedEntity`.

Inherited fields are not repeated in entities:

- `Id`
- `TenantId`
- `IsDeleted`
- `DeletedAt`
- `CreatedAt`
- `UpdatedAt`
- `Version`

### SignatureEnvelope

| Field | Type | Required | Rule |
|---|---|---:|---|
| EnvelopeNumber | `string` | Yes | Tenant-unique, server-generated |
| SubjectType | `string` | Yes | Canonical consuming-module object type |
| SubjectId | `Guid` | Yes | Existing subject reference |
| SubjectVersion | `string` | Yes | Immutable source version |
| DocumentArtifactId | `Guid` | Yes | Immutable artifact/version reference |
| ProviderType | `SignatureProviderType` | Yes | `InternalSubstitute`, `DocuSign`, `AdobeSign` |
| ProviderEnvelopeId | `string?` | No | Required after production provider creation |
| Status | `SignatureEnvelopeStatus` | Yes | Domain enum, server-controlled |
| SigningMode | `SignatureSigningMode` | Yes | `Sequential` or `Parallel` |
| RequestMessage | `string?` | No | Max 2000 |
| RequestedBy | `Guid` | Yes | Current user context |
| RequestedAt | `DateTimeOffset?` | No | Set on send |
| ExpiresAt | `DateTimeOffset?` | No | Must be after RequestedAt |
| CompletedAt | `DateTimeOffset?` | No | Set on completion |
| CancelledAt | `DateTimeOffset?` | No | Set on cancellation |
| CancelledBy | `Guid?` | No | Current user context |
| CorrelationId | `Guid` | Yes | Request-to-provider-to-audit trace |
| CreatedBy | `Guid?` | Yes | Current user context |
| UpdatedBy | `Guid?` | No | Current user context |

Envelope status lifecycle:

```text
Draft -> Pending -> PartiallySigned -> Completed
                  -> Declined
                  -> Expired
                  -> Cancelled
                  -> Failed
```

`Draft -> Cancelled` is allowed. Terminal states cannot transition to another
state except `Failed -> Pending` through explicit admin reconciliation after
provider status confirmation.

### SignatureParticipant

| Field | Type | Required | Rule |
|---|---|---:|---|
| EnvelopeId | `Guid` | Yes | Same tenant envelope |
| SignerUserId | `Guid` | Yes | Auth identity reference |
| SignerEmail | `string` | Yes | Normalized, max 256 |
| SignerDisplayName | `string` | Yes | Max 200 |
| SigningOrder | `int` | Yes | `>= 1`; unique per sequential envelope |
| Role | `string` | Yes | Max 120 |
| Status | `SignatureParticipantStatus` | Yes | Server-controlled |
| RequestedAt | `DateTimeOffset?` | No | Set when envelope sent |
| ViewedAt | `DateTimeOffset?` | No | First verified view |
| SignedAt | `DateTimeOffset?` | No | Successful signature time |
| DeclinedAt | `DateTimeOffset?` | No | Decline time |
| DeclineReason | `string?` | No | Required on decline, max 1000 |
| ProviderRecipientId | `string?` | No | Production provider reference |
| IdentityVerificationMethod | `IdentityVerificationMethod` | Yes | Policy-derived |
| CreatedBy | `Guid?` | Yes | Current user context |
| UpdatedBy | `Guid?` | No | Current user context |

### SignerAttestation

| Field | Type | Required | Rule |
|---|---|---:|---|
| EnvelopeId | `Guid` | Yes | Same tenant |
| ParticipantId | `Guid` | Yes | Same envelope and tenant |
| AttestationType | `SignatureAttestationType` | Yes | Policy-approved enum |
| AttestationText | `string` | Yes | Immutable signed text, max 4000 |
| Meaning | `string` | Yes | Max 500 |
| SignedAt | `DateTimeOffset` | Yes | UTC |
| SignerIdentitySnapshot | `string` | Yes | Immutable serialized identity claim subset |
| IpAddress | `string` | Yes | Valid IPv4/IPv6 |
| UserAgent | `string` | Yes | Max 1000 |
| ProviderEvidenceReference | `string?` | No | Required in provider-backed mode |
| CorrelationId | `Guid` | Yes | Matches envelope/provider/audit trace |

Attestations are append-only. Update and delete endpoints are not exposed.

### SignatureVerificationArtifact

| Field | Type | Required | Rule |
|---|---|---:|---|
| EnvelopeId | `Guid` | Yes | Same tenant |
| SignedArtifactId | `Guid` | Yes | Artifact store reference |
| ArtifactHash | `string` | Yes | Lowercase hexadecimal |
| HashAlgorithm | `string` | Yes | `SHA-256` for v1 |
| ProviderCertificateReference | `string?` | No | Required when provider supplies certificate evidence |
| VerificationStatus | `SignatureVerificationStatus` | Yes | Enum |
| VerifiedAt | `DateTimeOffset` | Yes | UTC |
| VerificationDetails | `string` | Yes | Max 8000 |
| EvidenceLinkId | `Guid?` | No | Required when evidence service gate is active |
| CorrelationId | `Guid` | Yes | Trace continuity |

Verification artifacts are append-only. A new verification produces a new
record rather than mutating historical evidence.

### ProviderCallbackReceipt

| Field | Type | Required | Rule |
|---|---|---:|---|
| ProviderType | `SignatureProviderType` | Yes | Provider route match |
| ProviderEventId | `string` | Yes | Globally unique per provider |
| ProviderEnvelopeId | `string` | Yes | Resolves envelope server-side |
| PayloadHash | `string` | Yes | SHA-256 |
| SignatureVerified | `bool` | Yes | Must be true before processing |
| ProcessingStatus | `ProviderCallbackProcessingStatus` | Yes | Enum |
| ReceivedAt | `DateTimeOffset` | Yes | UTC |
| ProcessedAt | `DateTimeOffset?` | No | UTC |
| FailureCode | `string?` | No | Canonical error code |
| FailureDetail | `string?` | No | Sanitized, max 4000 |
| CorrelationId | `Guid` | Yes | Existing or server-generated |

Callback receipts are tenant-scoped after `ProviderEnvelopeId` resolution.
Payload tenant claims are ignored.

### SignatureAuditExport

| Field | Type | Required | Rule |
|---|---|---:|---|
| EnvelopeId | `Guid?` | No | Null only for authorized platform batch export |
| ExportScope | `string` | Yes | Enum-backed scope |
| ArtifactId | `Guid` | Yes | Generated export artifact |
| RequestedBy | `Guid` | Yes | Current user context |
| RequestedAt | `DateTimeOffset` | Yes | UTC |
| CompletedAt | `DateTimeOffset?` | No | UTC |
| CorrelationId | `Guid` | Yes | Export trace |

## 5. Repo Scope

Governance:

- `execution/portfolio/delivery-capability-packs/DCP-003-e-signature-delivery.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0022-e-signature-service.md`

Future approved implementation scope:

- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/ESignature/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/ESignature/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IESignatureRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/ESignature/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/ESignature/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/ESignature/**`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/ESignaturesController.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/ESignature/**`
- `frontend/Diten.Web/Controllers/Platform/ESignaturesController.cs`
- `frontend/Diten.Web/Views/Platform/ESignatures/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/ESignatures/**`
- `frontend/Diten.Web/Resources/Views/Platform/ESignatures/**`
- `gateway/Diten.ApiGateway/**` for integration-agent route work only

## 6. Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `gateway/Diten.ApiGateway/**/ocelot.json` outside integration-agent ownership
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `services/Diten.MdmService/**`
- consuming domain internals

AuthService and consuming domains are consumed through contracts only. This
pack does not authorize modifications to them.

## 7. Dependencies

### Approved MVP Dependency Decisions

| Dependency | MVP decision | Production decision |
|---|---|---|
| `MOD-0021 General Audit Trail` | Required and satisfied by the existing ready-for-dev audit append/outbox contract. | Continue using the canonical audit SoR. |
| `MOD-0029 Controlled Documents` | Caller supplies immutable `ArtifactId`, `SubjectVersion` and `Sha256`; MOD-0022 verifies and snapshots them without owning source content. | Mandatory before controlled-document integration is advertised. |
| `MOD-0005 Policy & Control Library` | Immutable `INTERNAL_APPROVAL_EVIDENCE_V1` policy requires authenticated identity snapshot, explicit attestation and SHA-256 evidence. | Mandatory before configurable policies or higher assurance. |
| `MOD-0264 e-Sign Vendor` | Not used; no provider port, adapter, callback, credentials or provider DTO is implemented. | Mandatory before DocuSign, Adobe Sign or provider-verified mode. |

### MVP Ownership Decisions

- `Diten.Platform` owns the MVP generated signed-PDF and audit-export store.
- MOD-0022 owns MVP evidence references and verification records.
- Artifact bytes and metadata are tenant-scoped and cannot cross tenant boundaries.
- MVP evidence uses a seven-year retention default.
- No hard-delete API is exposed; legal-hold integration is deferred.

### Consumed Services

| Service | Consumption |
|---|---|
| `Diten.AuthService` | Current user claims and read-only signer identity reference |
| Audit Trail Service | Audit event append |
| Caller immutable artifact contract | MVP source artifact/version/hash snapshot |
| `INTERNAL_APPROVAL_EVIDENCE_V1` | MVP signer and attestation rules |
| MOD-0022 evidence reference | Link internal verification artifacts to subject |
| MOD-0022 internal artifact store | Store MVP signed PDF and audit export |
| Records Management | Retention/legal-hold policy, production follow-up |
| Ocelot Gateway | External API exposure |
| DocuSign or Adobe Sign | Deferred production signature execution |

No handler calls these systems with direct `HttpClient`; Application
interfaces are implemented in Infrastructure.

## 8. Runtime Constraints

- Runtime service is `Diten.Platform` on internal port `5057`.
- All browser and external access goes through Gateway `5000`.
- All entities are tenant-owned and use `TenantScopedEntity`.
- `TenantId` is server-resolved and absent from DTO/request/form payloads.
- `SourceArtifactContent` is a transient request field used only to verify the
  supplied SHA-256 value; it is not persisted by MOD-0022.
- Every repository query includes tenant and soft-delete filters.
- Cross-tenant read/write returns `404`.
- Controllers contain no business logic and use MediatR.
- Handlers return `Response<T>` and controllers inherit `CustomBaseController`.
- Provider-specific models and callbacks do not exist in the approved MVP.
- Correlation ID is preserved from request through audit and evidence.
- Signed subject content is not duplicated into MOD-0022.
- No Platform lookup key is required for v1. Provider type and statuses are domain enums, not editable lookups.

## 9. Layout & Shell Contract

### Primary Platform Admin Shell

- Frontmatter: `shell: platform-admin`.
- View folder: `Views/Platform/ESignatures/`.
- Every Razor page explicitly sets:

```cshtml
@{ Layout = "_LayoutPlatformAdmin"; }
```

- Browser uses same-origin MVC proxy.
- Platform Admin localization: `en` and `tr`, following PSS domain config.

Platform Admin pages:

- Signature Request Monitor
- Request Details
- Provider Health
- Reconciliation Queue
- Failed Callback Monitor
- Signature Audit Export

### Tenant-Facing Shell

Status: deferred follow-up; not part of this `ready-for-dev` slice.

- View folder: `Views/ESignatures/`.
- Every Razor page explicitly sets:

```cshtml
@{ Layout = "_LayoutTenantShell"; }
```

- Tenant UI uses tenant context and tenant-user authorization.
- Tenant localization: `en`, `fr`, `es`, `zh`, `ar`, `ru`, `tr`.

Tenant pages:

- Signature Requests
- Create Signature Request
- Request Details
- My Pending Signatures
- Sign/Decline
- Signature Verification Viewer
- Signed Artifact Viewer
- Signature Trace

The two shells must not share controllers, layout files, authorization policies
or navigation registrations. Shared view components may be used only when they
do not carry shell-specific authorization.

## 10. Backend File Convention

Application feature root:

```text
Features/ESignature/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── ESignatureModels.cs
```

Rules:

- each command/query/handler/validator is in its own file;
- commands are sealed records ending `Command`;
- queries are sealed records ending `Query`;
- handlers end only in `Handler`;
- validators end only in `Validator`;
- `CommandHandler`, `QueryHandler`, `RequestHandler` and `CommandValidator`
  suffixes are forbidden;
- provider calls use Application interfaces and Infrastructure adapters;
- callback parsing, artifact storage and provider HTTP calls do not live in handlers.

## 11. Frontend File Contract

`golden_reference: compact` is retained because the canonical full-module
contract contains 10 user-editable request inputs. The approved MVP implements
only the Platform Admin monitor/details/verification surface; it does not
implement the deferred tenant create form.

1. Subject Type
2. Subject ID
3. Subject Version
4. Document Artifact
5. Provider Type
6. Expiration
7. Signing Mode
8. Participants
9. Attestation Type
10. Request Message

Deferred tenant Compact set:

- `Index.cshtml`
- `Create.cshtml`
- `Details.cshtml`
- `_Form.cshtml`
- `_Filter.cshtml`
- `_DataTable.cshtml`
- `_IndexL10n.cshtml`
- `ESignaturesIndex.cs`
- `index.js`
- `index.l10n.js`

There is no general Edit page after sending. Draft updates may reuse `_Form`
through a dedicated draft-edit route only while status is `Draft`.

Platform Admin operational set:

- `Index.cshtml`
- `Details.cshtml`
- `_Filter.cshtml`
- `_DataTable.cshtml`
- `_IndexL10n.cshtml`
- `AuditExports.cshtml`
- `ESignaturesIndex.cs`
- `index.js`
- `index.l10n.js`

All DataTables include `data-dt-standard="v2"` and `#skeleton-loader`.
Compact surfaces do not use create/edit offcanvas files.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---:|---|---|---|
| EnvelopeNumber | Server | Max 80 | Unique `{TenantId, EnvelopeNumber}` partial index | Atomic generator |
| SubjectType | Yes | Trim, max 160, canonical contract value | Composite query index | Subject contract |
| SubjectId | Yes | Non-empty GUID | — | Consuming subject lookup |
| SubjectVersion | Yes | Trim, max 120 | — | Immutable version lookup |
| DocumentArtifactId | Yes | Non-empty GUID | — | Immutable artifact reference |
| ProviderType | Server | Must be `InternalSubstitute` in MVP | — | Fixed by approved policy |
| SigningMode | Yes | Domain enum | — | — |
| Participants | Yes | 1–50 participants | Participant indexes | Auth identity validation |
| SigningOrder | Yes | `>=1`; unique in sequential mode | Unique envelope/order partial index | Validator |
| SignerEmail | Yes | Valid email, max 256, lowercase | — | Auth identity match |
| ExpiresAt | No | Future UTC date | — | Internal policy max duration |
| RequestMessage | No | Max 2000 | — | Validator |
| DeclineReason | On decline | 1–1000 | — | Validator |
| AttestationText | On sign | 1–4000, immutable | — | Policy requirement |
| ProviderEventId | Production only | Max 256 | Unique provider/event index | Deferred |
| ArtifactHash | Verification | 64 lowercase hex chars | Query index | SHA-256 calculation |
| CorrelationId | Yes | Non-empty GUID | Query index | Header/context |

## 13. Failure Path to Verify

- **Unresolved tenant**
  - Expected: `400`; no repository call and no provider call.
- **Cross-tenant envelope access**
  - Expected: `404`; no existence disclosure.
- **Missing or invalid immutable artifact reference**
  - Expected: `400`; envelope is not created.
- **Unsupported policy**
  - Expected: `409`; only `INTERNAL_APPROVAL_EVIDENCE_V1` is accepted.
- **Unauthorized request creator**
  - Expected: `403`; provider is not called.
- **User is not an active participant**
  - Expected: `403`; sign/decline is rejected.
- **Sequential signing order violation**
  - Expected: `409`; participant state remains unchanged.
- **Provider behavior requested in MVP**
  - Expected: `409`; no provider call, callback or provider state is created.
- **Invalid state transition**
  - Expected: `409`; no provider or persistence mutation.
- **Concurrency conflict**
  - Expected: `409`; silent overwrite is forbidden.
- **MVP substitute represented as legal e-signature**
  - Expected: product/API labels reject provider-backed/legal status; mode remains `InternalSubstitute`.

## 14. Authorization Convention

### Platform Admin API

Policy:

```text
[Authorize(Policy = "PlatformAdminOnly")]
```

Permissions:

- `platform.e-signatures.read`
- `platform.e-signatures.audit-export`
- `platform.e-signatures.verify`

Actors:

- `platform_admin`

`partner_admin` is deferred until an explicit target-tenant scope contract is
available.

### Tenant API

Status: deferred follow-up. The permission contract below is reserved but is
not registered or implemented by the approved MVP.

Policy:

```text
[Authorize]
```

Permissions:

- `e-signatures.requests.read`
- `e-signatures.requests.create`
- `e-signatures.requests.send`
- `e-signatures.requests.cancel`
- `e-signatures.signatures.read`
- `e-signatures.signatures.sign`
- `e-signatures.signatures.decline`
- `e-signatures.verification.read`
- `e-signatures.audit-export.read`

Actor:

- `tenant_user`

Sign/decline requires permission, tenant match, active participant assignment
and valid signing order. RBAC alone is insufficient.

Provider callback endpoints are not present in the approved MVP.

## 15. Gateway / API Routing Decision

Gateway route is required.

Canonical upstream base:

```text
/api/platform/e-signatures
```

API groups:

```text
/api/platform/e-signatures/admin
```

Rules:

- This pack does not modify `ocelot.json`.
- Route implementation is an integration-agent task after approval.
- Explicit base and catch-all routes include `OPTIONS`.
- Frontend never calls `5057`.
- Platform Admin browser JS uses same-origin MVC proxy.
- `X-Tenant-Id`, `Authorization` and `X-Correlation-Id` propagation is tested.

## 16. Acceptance Criteria

### Governance and Gate

- [x] DCP-003 is `approved`.
- [x] MOD-0022 passed the approved/ready-for-dev gate and is now `in-progress`.
- [x] Runtime scope is limited to the approved internal-evidence MVP.
- [x] MOD-0022 remains the single signature-envelope SoR.

### Ownership and Tenancy

- [ ] Every envelope, participant, attestation and verification artifact is tenant-scoped.
- [ ] Request payloads contain no trusted `TenantId`.
- [ ] Cross-tenant list/detail/write attempts return `404`.
- [ ] Business subjects remain owned by consuming domain modules.

### Shell Separation

- [ ] Platform pages explicitly use `_LayoutPlatformAdmin`.
- [x] Tenant pages and controllers are deferred.
- [x] Platform authorization is isolated from the reserved tenant contract.
- [ ] Platform Admin resources exist in `en` and `tr`.

### MVP Substitute

- [ ] MVP substitute creates a controlled signed approval PDF, SHA-256 hash, identity snapshot, attestation, audit event and evidence reference.
- [ ] MVP substitute is labeled `Internal Approval Evidence` or `Signed Approval PDF`.
- [ ] MVP substitute is never marked provider-verified or legally binding.
- [ ] MVP substitute does not require DocuSign/Adobe Sign callbacks.

### Production Provider

- [x] Production-provider implementation is excluded from this slice.
- [x] No provider port, adapter, callback, credential or provider DTO is authorized.
- [x] MOD-0264 remains mandatory before provider-backed work.

### UI and Routing

- [ ] Platform Admin monitor DataTable uses v2 contract.
- [x] Tenant request DataTable and Compact create flow are deferred.
- [ ] All traffic uses Gateway `5000`.
- [ ] No frontend source contains direct Platform `5057` calls.
- [ ] Route changes are owned by integration-agent.

## 17. Test Expectations

Build:

```bash
dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
dotnet test services/Diten.Platform
dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug
```

Static and localization gates:

```bash
python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module ESignatures --reference compact
python3 .antigravity/skills/i18n-localization/scripts/resx_sharedresource_checker.py .
```

Required automated scenarios:

- tenant isolation and cross-tenant `404`;
- envelope state transitions;
- internal policy and immutable artifact-reference failures;
- optimistic concurrency;
- signed artifact hash verification;
- audit/evidence correlation;
- rejection of provider-backed behavior in MVP;
- PlatformActor target-tenant authorization.

Required browser smoke:

- internal evidence verification and signed artifact view;
- Platform Admin monitor;
- audit export;
- Platform permission-denied states.

## 18. Ready-for-dev Checklist

The approved MVP readiness decisions are:

- [x] DCP-003 is `ready-for-execution`.
- [x] Canonical ID gate passed against Blueprint/registry.
- [x] Ownership and SoR boundaries approved.
- [x] MOD-0021 audit append/outbox contract is available.
- [x] MVP immutable artifact-reference substitute approved.
- [x] `INTERNAL_APPROVAL_EVIDENCE_V1` policy approved.
- [x] Production provider selection deferred to MOD-0264.
- [x] Provider port, adapter and callback work excluded from MVP.
- [x] Diten.Platform internal artifact-store ownership approved.
- [x] MOD-0022 evidence-reference ownership approved.
- [x] Seven-year retention approved; legal hold explicitly deferred.
- [x] Internal Approval Evidence included in the first implementation.
- [x] Platform Admin is the first shell; Tenant Shell is deferred.
- [x] Platform permission keys in Section 14 approved.
- [x] Gateway integration work assigned to integration-agent.
- [x] Validation and failure paths reviewed.
- [x] Acceptance criteria and test expectations approved.
- [x] Module status is `approved`.

## 19. Implementation Notes

### MVP Substitute Boundary

MVP substitute may implement:

- controlled PDF generation;
- SHA-256 artifact hash;
- current-user identity snapshot;
- signer attestation;
- timestamp and correlation;
- Audit Trail event;
- evidence/artifact reference;
- verification viewer for internal evidence.

MVP substitute must not:

- use the term legally binding e-signature;
- claim provider certificate validation;
- expose provider-backed verification status;
- emulate provider callbacks;
- bypass the MOD-0021 audit contract or immutable artifact-reference validation.

### Production Provider Boundary

Production mode adds:

- selected DocuSign or Adobe Sign adapter;
- provider envelope and recipient mapping;
- provider identity binding;
- callback authentication and signature verification;
- signed artifact retrieval;
- certificate/verification evidence;
- retry and reconciliation;
- provider health monitoring;
- support-grade audit export.

Provider choice is a production-follow-up gate and does not block or authorize
provider behavior in the internal-evidence MVP.

## 20. Follow-up Items

- Add second provider adapter after first-provider production stabilization.
- Add provider failover only through a separately approved pack.
- Bind Records Management retention/legal hold.
- Add regulated identity assurance profiles where required.
- Prepare downstream consumer packs for Governance Attestations, eBR, Regulatory Submissions and Labeling.
- Reconcile DCP-003 and module pack lifecycle after production release.
