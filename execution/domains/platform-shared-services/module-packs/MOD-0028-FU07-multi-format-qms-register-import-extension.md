---
id: MOD-0028-FU07
name: Multi-format QMS Register Import Extension
parent: MOD-0028
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: none
entity_base: TenantScopedEntity
status: draft
owner: platform-shared-services
branch: feature/pss/mod-0028-fu07-multi-format-qms-register-import-extension
started: 2026-08-26
target: 2026-08-26
form_field_count: 0
runtime_code_allowed: false
business_dependency: MOD-0028-FU02
delivery_capability_pack: DCP-007
identity_type: retrospective-governance-recovery
source_workbook: docs/GMG-QMS-LOG-0007_v0.36_PROVISIONING_REGISTER_2026-08-12.xlsx
source_workbook_sha256: b7fb649c82f06020dbcec6e187f36f236dda9954c1d73550a25a32a12569564c
source_urs: docs/GMG-CSV-URS-0001_v0.3_DRAFT_2026-08-12.docx
source_urs_sha256: 4e903ae00dedb3138258cb482f0067bc8b8e7df6666866eecb9e1a2d90c0346c
---

# MOD-0028-FU07 — Multi-format QMS Register Import Extension

> **Governance recovery only.** The user/Enterprise Architect explicitly approved this retrospective identity on
> 2026-08-26. This draft inventories existing code and defines a future validation target; it does not approve,
> validate, activate, or authorize changes to that code. Runtime work remains forbidden until this pack is separately
> reviewed and both `status` and `runtime_code_allowed` are explicitly changed.

> **DCP-007 relationship.** `DCP-007` is the governance/orchestration dependency for cross-cutting completion
> visibility and consumer guardrails. It is not a runtime dependency and does not approve FU07, change this pack's
> status, or authorize implementation. The user's 2026-08-27 approval covers governance reconciliation only.

## 1. Module Summary

MOD-0028-FU07 governs the multi-format extension of FU02's QMS structure-baseline import. Its target is a fail-closed
LOG-0007 v0.36 XLSX import/reconciliation profile while preserving FU02's legacy XLSX behavior. It consumes FU02's
semantic endpoints, permissions, baseline model, and tenant boundary and FU03's TenantShell import UI.

LOG-0007 v0.36 is accepted only as a **tenant-owned DRAFT structure-baseline source**. It is not a complete governed-
classification source and not an operational-provisioning source. The committed output is a tenant-owned Group
Baseline definition. FU07 creates no Company `CollectionInstance`, shares no folders with companies, and creates no
company-local folder or file. Company sharing, overlays, local additions, group-node propagation/removal, and template
sharing belong to a later cross-cutting Delivery Capability Pack after FU07 import implementation and verification.

Two states must not be conflated: CSV/flat-JSON register-import code exists retrospectively, but it has not been
verified against this pack or LOG-0007 v0.36 and is not validated, live, approved, or production-ready. The workbook
and URS are themselves DRAFT/not-approved sources; they permit controlled dry-run analysis and a DRAFT-only commit,
never publication or Effective activation.

## 2. Retrospective Governance Context

- Exact-name/parent identity preflight for `MOD-0028-FU07` exited 0 on 2026-08-26.
- Identity recovery is not retroactive implementation approval.
- FU02 is the business dependency and semantic import-contract owner; FU03 owns the consumed TenantShell UI.
- FU06 remains Corporate Collection Instance Foundation and is unrelated to this register import.
- FU08–FU10 are untouched and outside this pack.
- DCP-007 is `under-review`; its existence and review status close no FU07 runtime or approval gate.
- While `status: draft` and `runtime_code_allowed: false`, orchestrator may not change runtime code.

## 3. Existing Runtime Inventory

| Existing surface | Evidence | Retrospective assessment |
|---|---|---|
| CSV parser | `CsvQmsFolderImportParser.cs` | Parses an earlier LOG-0007 CSV package and projects governance fields; not v0.36 validation. |
| Flat JSON parser | `FlatJsonQmsFolderImportParser.cs` | Parses envelope/bare-array JSON and optional register metadata; not the authoritative v0.36 profile. |
| Parser selection | `IQmsFolderImportParser` implementations and `QmsBaselineImportService.cs` | Selects by format/file name and shares planning between dry-run/commit; no proven v0.36 checksum/revision gate. |
| Stable register identity | `QmsCanonicalIdFactory.CreateFromRegisterFolderId` | Tenant/baseline-scoped ID from register folder ID; legacy path-hash fallback remains. |
| Parent validation | `QmsFolderTreeValidator.cs` | Validates existing normalized hierarchy/parent links; not proof of v0.36 joins/import-row dispositions. |
| Metadata projection/persistence | Tree validator, commit handler, repositories | Carries earlier register fields and creates DRAFT data; v0.36 immutable manifest is not established. |
| Frontend CSV/JSON acceptance | Web controller and `QmsBaselines/import.js` | Accepts `.xlsx/.csv/.json` via Gateway proxy; extension checks do not prove source authority. |
| Tests | `QmsRegisterImportFoundationTests.cs` | Covers an earlier v0.8 CSV/JSON package, identity, metadata, legacy fallback, and DRAFT commit—not v0.36 ACs. |
| Endpoint/permission | FU02 controller and `QmsBaselinePermissions.Import` | Consumes FU02 dry-run/commit semantics; no FU07 runtime literal is required. |

The inventory proves code presence only. Existing code was developed without this pack and must be revalidated after
a future ready-for-dev gate.

## 4. Ownership and Boundaries

FU07 owns the target LOG-0007 v0.36 import-profile contract: source identity/revision/checksum/status, versioned
schema mapping, controlled joins/derivations, dry-run reconciliation, DRAFT-only commit, explicit import-row
dispositions, immutable import manifest, idempotency, revision/checksum conflicts, fail-closed eligibility evidence,
and FU02 compatibility.

Production source authority is the FU07-owned, tenant-scoped Mongo aggregate `QmsRegisterSourceProfile`. Its name and
scope remain QMS-register-specific. Document Master Register is an optional provenance reference, never the authority
for source checksum, revision, schema, approval, or usage. FU07 does not modify a MOD-0029 aggregate.

FU07 validates source approval/status, produces authoritative import-completion evidence, records immutable
`ActivationEligibilityAtImport`, and emits finding/blocker evidence. A `DRAFT`/`NOT APPROVED`/`NOT LIVE` source is
always recorded as activation- and provisioning-ineligible, and FU07's own commit creates only a DRAFT baseline. FU07
does not own legacy/manual disposition, combined `EvidenceVerified`, `ReviewVisible`, or downstream action decisions.
Those consumer-boundary decisions are coordinated by DCP-007 and enforced through FU02 and the separately approved
consumer owners. FU07 does not implement or modify downstream lifecycle or provisioning behavior.

FU07 owns its import-operation status and safe findings-summary endpoints, importer self-operation authorization, and
tenant/requester non-leakage. FU02 remains the source of the existing import permission literal and owns the combined
evidence/review/action guard plus baseline consumer enforcement; FU03 is the polling UI consumer. This endpoint
ownership does not make FU07 a broader lifecycle or reconciliation owner.

The FU07-owned Group Baseline remains tenant-owned and versioned. It is neither a Company Collection Instance nor a
shared/propagated company tree. Stable canonical IDs and a versioned baseline are prerequisites for a future sharing
DCP, but FU07 does not define removal/retirement propagation or protection of company-local content.

It also does not own Controlled Document lifecycle, Quality Record implementation, retention/legal-hold/records
disposition engines, provisioning of 32 `PS-*` profiles in the IdP, physical folders, binary upload/storage, Gateway
routes, or FU08–FU10.

## 5. Owned/Consumed Objects

Owned governance objects are `QmsRegisterSourceProfile`, resumable `QmsRegisterImportOperation`, its controlled status
and safe findings-summary endpoint contracts, the tenant-owned DRAFT Group Baseline definition, `LOG-0007-v0.36`
schema profile, source/schema contract, reconciliation result, import-row outcome rules, immutable
`QmsRegisterImportManifest`, tenant-scoped `QmsRegisterImportFinding`, immutable `QmsRegisterFindingResolution`,
idempotency contract, and fail-closed eligibility evidence. Consumed runtime
objects are FU02 `BaselineRelease`, `CollectionDefinition`, import plan/summary,
repositories, semantic endpoints and permissions; FU03's upload/review/commit UI; and platform
audit/correlation/tenant seams. Consumed objects must not be duplicated, renamed, or claimed by FU07.

`QmsRegisterImportOperation` and `QmsRegisterImportManifest` are tenant-owned and never hard-deleted. The operation is
the Commit-only resumable execution aggregate; the manifest is immutable after creation and is written last. Both
inherit tenant, soft-delete, audit timestamp, and technical concurrency behavior from `TenantScopedEntity`; inherited
fields must not be redeclared. Dry-run is persistence-zero: it returns an ephemeral reconciliation/eligibility result
and creates neither operation nor manifest. A future request to persist dry-run evidence requires an explicit pack
amendment.

## 6. Entity Fields

### `QmsRegisterSourceProfile`

`QmsRegisterSourceProfile` is a tenant-owned `TenantScopedEntity`. It stores source authority metadata only; raw
workbook bytes and raw row content are forbidden. `Id` is exposed as the `SourceProfileId` API alias and is not
duplicated as a second persisted business identifier.

| Field | Type | Required | Source/validation | Mutable |
|---|---|---:|---|---:|
| Id / SourceProfileId API alias | Guid | yes | Server-generated inherited `Id`; non-empty | no |
| TenantId | Guid | yes | Server-resolved tenant context; forbidden in client payload | no |
| SourceDocumentCode | string | yes | Controlled, trimmed exact value | no after create |
| BusinessRevision | string | yes | Workbook business metadata; never Office core revision | no after create |
| SchemaProfile | string | yes | Exact supported QMS register schema-profile catalog value | no after create |
| RawFileSha256 | string | yes | SHA-256 of exact uploaded bytes; 64 lowercase hex | no after create |
| WorkbookBusinessStatus | flags enum | yes | Exact source markers `Draft`, `Approved`, `NotApproved`, `NotLive`, `Live`, `Unknown`, and `Unsupported`; combinations preserved | no |
| ProfileApprovalStatus | enum | yes | Platform governance `Draft`, `UnderReview`, `Approved`, or `Rejected` | state machine only |
| UsageStatus | enum | yes | `Disabled`, `ReviewOnly`, `DraftImportAllowed`, `ActivationAllowed`, `Suspended`, or `Retired` | state machine only |
| EffectiveFrom / EffectiveTo | DateTimeOffset? | conditional | Server-controlled UTC validity window; ordered when both exist | gated |
| ApprovedBy / ApprovedAt | actor / DateTimeOffset? | conditional | Server actor/clock when approval completes | set-once |
| ApprovalEvidenceReference | string? | conditional | Bounded controlled evidence reference | set-once |
| SupersedesSourceProfileId | Guid? | no | Same-tenant/source lineage; self-reference prohibited | no |
| DocumentMasterRegisterEntryId | Guid? | no | Optional tenant-scoped MOD-0029 provenance reference | no after promotion |
| SuspendedFromUsageStatus | UsageStatus? | conditional | Previous active usage captured server-side on suspend | server only |
| StatusReason | string? | transition | Bounded, sanitized reason/evidence summary | per transition |
| Notes | string? | no | Bounded descriptive text; never authority | Draft only |
| CreatedAt / CreatedBy | inherited | yes | Server clock/actor | no |
| UpdatedAt / UpdatedBy | inherited | conditional | Server clock/actor | server only |
| Version | inherited int | yes | Technical optimistic-concurrency token, minimum 1 | server only |
| IsDeleted / DeletedAt | inherited bool / explicit nullable timestamp | technical | `IsDeleted` remains false; `DeletedAt` remains null | no |

There is no delete endpoint. A source profile is never hard-deleted or soft-deleted; retirement is represented only
by `UsageStatus=Retired`.

#### Independent status authorities

- `WorkbookBusinessStatus` is read from the workbook and is never reinterpreted by platform governance. The v0.36
  source markers `Draft + NotApproved + NotLive` remain distinct facts; activation requires `Approved + Live` and no
  contradictory/unknown/unsupported marker.
- `ProfileApprovalStatus` governs the platform source-profile record.
- `UsageStatus` governs what the runtime may do with that record.
- `ProfileApprovalStatus=Approved` does not change a workbook's own business status.

Profile approval lifecycle: `Draft -> UnderReview -> Approved` or `UnderReview -> Rejected`. Create always sets
`ProfileApprovalStatus=Draft` and `UsageStatus=Disabled`; approval without `UnderReview` is prohibited. A rejected
profile remains `UsageStatus=Disabled`.

Usage lifecycle: `Disabled -> ReviewOnly -> DraftImportAllowed -> ActivationAllowed`. Only an approved profile may
enter `ReviewOnly` or a higher usage state. Any active usage state may transition to `Suspended`; suspend stores the
previous state in `SuspendedFromUsageStatus`. Resume revalidates every current gate and may return only to that stored
state. Suspension may instead transition to terminal `Retired`. Direct backward transitions are prohibited.

`DraftImportAllowed` requires an approved profile, an open FU07 runtime gate, and schema/checksum/revision fixture
evidence. `ActivationAllowed` additionally requires workbook business status approved/live and closure of source-
remediation and activation gates. Activation requires a Document Master Register reference only if the separate EA/QA
Controlled Gate selects that policy. The reference is optional for `ReviewOnly` and `DraftImportAllowed`.

#### LOG-0007 v0.36 initial state

The first controlled LOG-0007 v0.36 record is `WorkbookBusinessStatus=Draft/NotApproved/NotLive`,
`ProfileApprovalStatus=Draft`, and `UsageStatus=Disabled`. After governance review it may become
`ProfileApprovalStatus=Approved` and `UsageStatus=ReviewOnly`. It cannot become `DraftImportAllowed` until FU07
development and verification are complete. It cannot become `ActivationAllowed` while the workbook remains
DRAFT/NOT APPROVED/NOT LIVE.

### `QmsRegisterImportOperation`

`QmsRegisterImportOperation` is FU07's standalone-compatible, tenant-scoped, Commit-only resumable execution
aggregate. Correctness does not depend on a multi-document Mongo transaction. There is no delete endpoint;
`IsDeleted` remains false and `DeletedAt` remains null.

| Field | Rule |
|---|---|
| Id | Server-generated operation identifier. |
| TenantId | Required server-resolved tenant; forbidden in client payloads. |
| OperationKey | Required SHA-256 identity defined below; unique within the tenant. |
| SourceProfileId | Required resolved source-profile identity. |
| SourceProfileVersionSnapshot | Required technical-version evidence captured at request acceptance; not idempotency identity. |
| SourceDocumentCode / BusinessRevision / SchemaProfile / RawFileSha256 | Required immutable source identity and schema evidence. |
| RequestedMode | Required controlled mode; persisted operations are Commit-only. |
| Status / CurrentPhase | Required controlled state and last reached execution phase. |
| BaselineReleaseId / ManifestId | Nullable until their respective persistence/finalization steps. |
| SupersedesOperationId | Nullable same-tenant lineage to a previous operation; does not mutate the previous operation. |
| ExpectedDefinitionCount / PersistedDefinitionCount | Required non-negative verification counters. |
| ExpectedFindingCount / PersistedFindingCount | Required non-negative verification counters. |
| ValidationFindingCount / ValidationFindingHash / FindingsTruncated | Required bounded validation-evidence summary. |
| ExpectedDefinitionHash / PersistedDefinitionHash | Required deterministic definition verification evidence when persistence begins. |
| ExpectedFindingHash / PersistedFindingHash | Required deterministic hash of the immutable detection-evidence projection only; mutable workflow/resolution state is excluded. |
| NextDefinitionChunkOrdinal / NextFindingChunkOrdinal | Required independent resumable cursors. |
| ChunkSize | Required typed-option snapshot accepted for this operation. |
| LeaseOwner / LeaseEpoch / LeaseExpiresAt / HeartbeatAt | Server-controlled lease and fencing state. |
| AttemptCount | Required server-controlled execution-attempt counter. |
| LastReasonCode / LastFailurePhase / LastFailureAt | Nullable controlled failure and retry evidence. |
| StartedAt / FinalizedAt | Required start and nullable successful-finalization UTC timestamps. |
| RequestedBy / CorrelationId | Required server-resolved actor and correlation evidence. |
| TransitionLedger | Required bounded append-only regulated transition evidence. |
| Version | Inherited technical optimistic-concurrency token used by operation CAS. |
| CreatedAt / UpdatedAt | Inherited server timestamps. |
| IsDeleted / DeletedAt | Inherited; permanently false/null because operation history is never deleted. |

Baseline, definition, finding, and manifest records carry required `ImportOperationId`. Tenant, actor, clock,
correlation, lease, and worker fields are never accepted from the client.

#### Operation identity and lineage

```text
OperationKey =
SHA256(
  SourceProfileId |
  SourceDocumentCode |
  BusinessRevision |
  SchemaProfile |
  RawFileSha256 |
  RequestedMode
)
```

`SourceProfileVersion`, `RequestedBy`, `CorrelationId`, timestamp, worker/lease identity, `ProfileApprovalStatus`, and
`UsageStatus` do not participate in the key. Technical source-profile `Version` may change during suspend/resume or a
governance transition without creating a duplicate operation for the same immutable source. The captured
`SourceProfileVersionSnapshot` remains evidence on the operation and manifest, not identity.

A corrected or otherwise different immutable source obtains a new `OperationKey` and may link to the previous attempt
through `SupersedesOperationId`. The earlier status is never rewritten and `SupersededByOperationId` is not required.
A `Completed` operation remains historically `Completed`; current source authority is resolved through source-profile
and revision lineage.

#### Operation state machine

Allowed statuses are `Received`, `Validating`, `ValidationFailed`, `ReadyToPersist`, `PersistingDefinitions`,
`PersistingFindings`, `Verifying`, `Finalizing`, `Completed`, `FailedRetryable`, and `FailedTerminal`.

- `Received -> Validating`.
- `Validating -> ReadyToPersist | ValidationFailed | FailedRetryable | FailedTerminal`.
- `ReadyToPersist -> PersistingDefinitions | FailedTerminal`.
- `PersistingDefinitions -> PersistingDefinitions | PersistingFindings | FailedRetryable | FailedTerminal`.
- `PersistingFindings -> PersistingFindings | Verifying | FailedRetryable | FailedTerminal`.
- `Verifying -> Finalizing | FailedRetryable | FailedTerminal`.
- `Finalizing -> Completed | FailedRetryable | FailedTerminal`.
- `FailedRetryable` may resume only at the continuation state allowed by `LastFailurePhase`.
- `ValidationFailed`, `Completed`, and `FailedTerminal` are terminal and cannot be re-executed by normal retry.

State/phase skipping is prohibited. `Completed` is legal only when the exact manifest and verification evidence exist.
There is no `Superseded` operation status.

Only `Completed` may produce `ImportCompletionVerified=true`. The other ten canonical statuses—`Received`,
`Validating`, `ValidationFailed`, `ReadyToPersist`, `PersistingDefinitions`, `PersistingFindings`, `Verifying`,
`Finalizing`, `FailedRetryable`, and `FailedTerminal`—produce false. If a compatibility name
`CompletionVerified` is retained, it is an alias for `ImportCompletionVerified` only and never a combined
import-or-legacy evidence result. FU07 creates no additional operation state.

#### Immutable finding count/hash projection

`ExpectedFindingCount`/`PersistedFindingCount` and `ExpectedFindingHash`/`PersistedFindingHash` cover only immutable
detection evidence. The hash projection includes:

- `FindingScopeKey`;
- controlled `FindingCode`;
- controlled `FindingCategory`;
- immutable field/scope identity;
- immutable observed-evidence hash;
- immutable blocker flags as detected at import time; and
- other detection fields only when this FU07 contract explicitly marks them immutable.

The projection excludes mutable workflow status, assignment/assignee projection, resolution state,
`QmsRegisterFindingResolution` ledger entries, later governance decisions, re-evaluated eligibility projection,
`UpdatedAt`, `Version`, and central-audit projection state. Later assignment, resolution, or parent-resolution
re-evaluation does not invalidate a completed import's manifest or finding hash. Resolution integrity is verified
separately through the append-only resolution ledger, tenant-scoped CAS/version, and audit contract.

Finding hash ordering is deterministic by `FindingScopeKey` ordinal. Exact canonical serialization/normalization and
fixture count/hash values remain an open DCP-007 G12 gate; this pack does not guess them. Production code must never
hard-code fixture finding counts or hashes.

#### Chunk, cursor, lease, and fencing contract

The initial typed-options proposal is default 250, minimum 50, and maximum 500 records per chunk. These values require
load-test acceptance before ready-for-dev and must not become scattered production magic numbers. Definitions use the
deterministic order `Level ASC, CandidateFolderId ordinal`; findings use `FindingScopeKey ordinal`. Definition and
finding cursors remain separate.

Chunks use unordered bulk writes and deterministic idempotent business keys. Each chunk is followed by count/hash
read-back. A cursor advances only through a compare-and-swap filtered by
`TenantId + OperationId + LeaseOwner + LeaseEpoch + ExpectedVersion`. A network timeout never implies success or
failure: the worker read-backs and safely replays. The same immutable content is idempotent success; the same key with
different immutable content is a controlled terminal conflict.

Only one worker owns a lease. Takeover is allowed only after expiry and increments `LeaseEpoch`; a stale epoch cannot
advance a cursor, state, or finalization CAS. Only the current owner/epoch may extend heartbeat. Heartbeats are excluded
from `TransitionLedger`; lease acquisition, takeover, failure, and finalization are included. Lease duration, heartbeat
interval, and the authoritative worker-identity source remain Controlled Gates.

#### Operation and staged-data Mongo index contract

All unique indexes below are non-partial because operation history and staged import evidence are not deleted:

- operation unique: `TenantId + OperationKey`;
- operation supporting: `TenantId + Status + LeaseExpiresAt`, `TenantId + SourceProfileId + StartedAt`,
  `TenantId + BaselineReleaseId`, `TenantId + UpdatedAt`, and `TenantId + SupersedesOperationId`;
- baseline unique: `TenantId + ImportOperationId`;
- definition unique: `TenantId + ImportOperationId + CanonicalId`;
- definition supporting: `TenantId + ImportOperationId + DisplayOrder`;
- existing definition contract retained: `TenantId + BaselineReleaseId + CanonicalId`;
- finding unique: `TenantId + ImportOperationId + FindingScopeKey`;
- finding supporting: `TenantId + ImportOperationId + CurrentStatus` and
  `TenantId + ImportOperationId + CandidateFolderId`;
- manifest unique: `TenantId + ImportOperationId`;
- manifest replay unique: `TenantId + SourceProfileId + RawFileSha256 + SchemaProfile + ImportMode`.

The manifest replay index deliberately excludes technical source-profile `Version`; version is immutable snapshot
evidence only.

### `QmsRegisterImportManifest`

| Field | Rule |
|---|---|
| TenantId | Required, inherited from `TenantScopedEntity`, resolved server-side; forbidden in client payloads. |
| ImportOperationId | Required same-tenant operation reference; unique within the tenant. |
| SourceProfileId | Required resolved `QmsRegisterSourceProfile.Id`. |
| SourceProfileVersionSnapshot | Required technical profile version captured as evidence; excluded from idempotency identity. |
| SourceDocumentCode | Required controlled value; initial profile is `GMG-QMS-LOG-0007`. |
| BusinessRevision | Required business revision extracted from approved Cover cells by the versioned profile. |
| RawFileSha256 | Required SHA-256 of exact uploaded bytes, stored as 64-character lowercase hex. |
| SchemaProfile | Required versioned mapping profile; initial value `LOG-0007-v0.36`. |
| WorkbookBusinessStatus | Required immutable source-profile snapshot. |
| ProfileApprovalStatus | Required immutable source-profile snapshot. |
| UsageStatus | Required immutable source-profile snapshot. |
| EffectiveFrom / EffectiveTo | Immutable source-profile validity snapshot. |
| ApprovalEvidenceReference | Immutable source-profile approval-evidence snapshot. |
| DocumentMasterRegisterEntryId | Immutable nullable provenance-reference snapshot. |
| SnapshotAt | Required server-side UTC snapshot timestamp. |
| ImportMode | Controlled enum `DryRun`/`Commit`; persisted manifests are `Commit` because dry-run is persistence-zero. |
| BaselineReleaseId | Required because only successful finalized Commit operations have manifests; dry-run has no manifest. |
| TotalCount | Required non-negative calculated count. |
| FolderCount | Required non-negative calculated count. |
| ControlFileCount | Required non-negative calculated count. |
| PilotEligibleCount | Required non-negative calculated count. |
| WithheldCount | Required non-negative calculated count. |
| BlockedCount | Required non-negative calculated count. |
| QuarantinedCount | Required non-negative calculated count. |
| CompoundProfileTotal | Required non-negative calculated count. |
| CompoundProfileEligible | Required non-negative calculated count. |
| CompoundProfileWithheld | Required non-negative calculated count. |
| Outcome | Required controlled enum `Passed`/`Blocked`/`Conflict`/`Failed`. |
| ReasonCode | Required controlled code for non-passed outcomes; no raw row text. |
| ActivationEligibilityAtImport | Required immutable fail-closed import-time summary; false for `DRAFT`/`NOT APPROVED`/`NOT LIVE`; not a lifecycle transition or mutable current-state projection. |
| FieldCoverage | Immutable field-level counts for Resolved/Missing/Ambiguous/Conflict/NotApplicable. |
| RowOutcomeCounts | Immutable counts for Resolved/Candidate/Ambiguous/Conflict/Quarantined/Withheld/NotApplicable. |
| FindingSummary | Immutable aggregate counts and references only; the field-level finding set is stored separately and is never embedded in the manifest. |
| FinalizationStatus | Required immutable value `Finalized`; a partial write has no finalized manifest and is never reported as a successful commit. |
| CorrelationId | Required, resolved server-side. |
| ImportedBy | Required actor-context identity; forbidden in client payloads. |
| ImportedAt | Required server-side UTC timestamp. |
| ManifestSchemaVersion | Required business schema version; must not use the technical `Version` field. |
| Version | Inherited technical optimistic-concurrency field only. |

Raw workbook bytes, raw row payloads, and `Implementation Note` are forbidden in the manifest, finding, resolution,
and audit summaries. `Implementation Note` is never parsed for classification or executed as a command/mapping
instruction. The manifest is append-once/immutable; correction creates a new controlled import attempt rather than
mutating or hard-deleting historical evidence. Dynamic current eligibility is derived from the finding and resolution
ledger and never updates the immutable manifest.

### `QmsRegisterImportFinding`

`QmsRegisterImportFinding` is a FU07-owned tenant-scoped `TenantScopedEntity`. It stores one controlled field- or
manifest-scope detection result without copying a raw workbook row. Detection/evidence fields are immutable after
creation; only the workflow projection is mutable through optimistic concurrency.

| Field | Required | Rule | Mutable |
|---|---:|---|---:|
| Id | yes | Inherited server-generated identifier. | no |
| TenantId | yes | Inherited and server-resolved; forbidden in client payloads. | no |
| ImportOperationId / SourceProfileId | yes | Same-tenant immutable references; operation linkage exists before manifest. | no |
| ImportManifestId | no | Nullable until successful finalization; failed validation evidence has no manifest. | no |
| CandidateFolderId | no | Stable source candidate identity when a row/node scope exists. | no |
| FindingScopeKey | yes | Server-generated deterministic dedupe key; never client asserted. | no |
| SourceRowNumber | no | Positive source row reference; no raw row payload. | no |
| FieldName / FindingCode | yes | Controlled catalog values. | no |
| FindingCategory | yes | Controlled category enum. | no |
| Severity | yes | `Fatal`, `Blocking`, `ReviewRequired`, `Warning`, or `Informational`. | no |
| ObservedValueHash / bounded safe representation | conditional | Prefer a hash; a safe value requires an explicit allowlist and bound. Raw/sensitive text is forbidden. | no |
| ExpectedRuleReference | yes | Bounded controlled rule/evidence reference. | no |
| ParentFindingId / SupersedesFindingId | no | Same-tenant finding lineage; self-reference and cycles prohibited. | no |
| BlocksCommit / BlocksActivation / BlocksProvisioning | yes | Server-computed from the approved finding policy. | no |
| AssignedOwnerRole | no | Controlled semantic owner role; never a client-supplied authorization grant. | CAS only |
| CurrentStatus | yes | Rebuildable workflow projection whose system of record is the resolution ledger. | CAS only |
| DetectedAt / CorrelationId | yes | Server clock and resolved correlation. | no |
| Version / UpdatedAt / UpdatedBy | technical | Inherited technical concurrency/audit projection. | server CAS only |
| IsDeleted / DeletedAt | technical | `IsDeleted` remains false and `DeletedAt` remains null; no delete endpoint exists. | no |

Finding categories are at least `Structural`, `SourceIdentity`, `Schema`, `ChecksumRevision`, `Entity`, `Zone`,
`Domain`, `Function`, `DocumentType`, `Lifecycle`, `PermissionProfile`, `OwnerRole`, `RecordClass`, `Retention`,
`LegacyId`, `ReleaseStatus`, `BlockingGate`, `Path`, and `SourceApproval`.

Active statuses are `Open`, `Assigned`, `UnderReview`, `AwaitingSourceCorrection`, and `Rejected`. Result statuses are
`ResolvedByGovernanceDecision`, `ResolvedBySourceRevision`, `AcceptedAsKnownLimitation`, and `Superseded`. Generic
`Closed` is not used. Active statuses and `Rejected` never remove a blocker. `Rejected` may return to `UnderReview`.
`ResolvedByGovernanceDecision` removes only the applicable governance blocker when the category authority records an
explicit evidence-backed mapping. Structural findings cannot be resolved by governance overlay.
`AcceptedAsKnownLimitation` does not remove activation/provisioning blockers by default. `ResolvedBySourceRevision`
does not make the old manifest eligible, and `Superseded` preserves historical evidence. A normal second resolution
against a terminal result returns 409; correcting a decision requires an explicit superseding-decision action.

### `QmsRegisterFindingResolution`

`QmsRegisterFindingResolution` is a FU07-owned immutable tenant-scoped ledger entity. Its fields are `Id`, `TenantId`,
`FindingId`, `ResolutionRequestId`, `ResolutionType`, `Decision`, `Reason`, `EvidenceReference`, `DecidedBy`, `DecidedAt`,
`PreviousStatus`, `NewStatus`, nullable `SupersedesResolutionId`, `CorrelationId`, and inherited `CreatedAt`. Every
decision creates a new row; no update or delete surface exists. `SupersededByResolutionId` is prohibited because it
would require rewriting historical evidence. `ResolutionRequestId` is the retry-idempotency key. Actor, tenant,
decision time, and correlation are server-resolved. Raw source values, workbook notes, and sensitive content are
forbidden in the ledger and audit payload.

The resolution insert is the system-of-record action. Only after that insert succeeds may the finding's
`CurrentStatus`, `AssignedOwnerRole`, and inherited audit fields be advanced with a tenant-scoped `ExpectedVersion`
compare-and-swap. The ledger remains sufficient to rebuild the projection. Resolution-ledger integrity is independent
from import finalization count/hash and does not mutate a completed manifest's immutable detection-evidence hash.

### Finding and resolution Mongo index contract

The finding collection requires the non-partial unique index
`TenantId + ImportOperationId + FindingScopeKey`. For candidate field findings, `FindingScopeKey` is deterministic over
`CandidateFolderId + FieldName + FindingCode + controlled null marker`; manifest-scope findings use the same controlled
normalization without inventing a candidate identity. Supporting indexes are
`TenantId + ImportOperationId + CurrentStatus`, `TenantId + ImportOperationId + CandidateFolderId`,
`TenantId + AssignedOwnerRole + CurrentStatus`, `TenantId + SourceProfileId`, and
`TenantId + SupersedesFindingId`.

The resolution collection requires the non-partial unique index
`TenantId + FindingId + ResolutionRequestId`. Supporting indexes are
`TenantId + FindingId + DecidedAt DESC` and `TenantId + SupersedesResolutionId`. Finding and resolution records are
never deleted, so their unique indexes are not partial on `IsDeleted`.

Finding workflow updates filter by `TenantId + Id + Version + IsDeleted=false`; no match returns controlled 409.
After a duplicate finding insert, the existing tenant-scoped row is re-read: identical immutable evidence is an
idempotent success, while different immutable evidence under the same key is a controlled 409 conflict.

### `QmsRegisterSourceProfile` Mongo index contract

The following indexes are mandatory and atomic:

1. unique `TenantId + SourceDocumentCode + BusinessRevision`;
2. unique `TenantId + SourceDocumentCode + RawFileSha256`.

They are not partial indexes: source profiles cannot be deleted, and uniqueness must include historical, suspended,
and retired records. Application pre-checks exist only to produce explanatory reason codes. Mongo uniqueness is the
atomic boundary. After duplicate-key failure, the existing tenant-scoped row is re-read and returned as controlled
409 `REVISION_CHECKSUM_CONFLICT` or 409 `CHECKSUM_REVISION_CONFLICT`.

Supporting non-unique indexes are `TenantId + SourceDocumentCode + UsageStatus`,
`TenantId + SourceDocumentCode + ProfileApprovalStatus`, `TenantId + SupersedesSourceProfileId`, and
`TenantId + UpdatedAt`. If `SourceProfileId` is the Mongo `_id`, no additional unique business index is created.

Activation uniqueness remains a Controlled Gate. FU07 does not yet decide whether there is one current activation,
future-effective scheduling, atomic effective-window overlap prevention, or a server-controlled
`IsCurrentActivation` projection. Because LOG-0007 v0.36 cannot be `ActivationAllowed`, this gate does not block the
structure-only DRAFT-import design but blocks operational activation.

### Import-operation and manifest idempotency

The operation identity is the §6 `OperationKey`; Mongo enforces non-partial uniqueness on
`TenantId + OperationKey`. The finalized manifest separately enforces non-partial uniqueness on
`TenantId + ImportOperationId` and on the replay key
`TenantId + SourceProfileId + RawFileSha256 + SchemaProfile + ImportMode`. Neither identity includes technical
`SourceProfileVersion`; `SourceProfileVersionSnapshot` is evidence only.

Required behavior is fixed:

- an exact same-checksum/same-revision Commit replay returns the existing manifest/baseline result and creates no
  duplicate;
- the same business revision with a different checksum returns 409 `REVISION_CHECKSUM_CONFLICT`;
- the same checksum with a different business revision returns 409 `CHECKSUM_REVISION_CONFLICT`;
- dry-run is recalculated without persisted idempotency state because it is persistence-zero;
- an invalid same-key Commit retry returns the existing `ValidationFailed` result;
- corrected bytes/checksum produce a new operation and may record `SupersedesOperationId`.

The two source-profile conflict indexes above remain authoritative and are not replaced by operation or manifest
indexes.

## 7. Source Workbook Contract

| Property | Required value/rule |
|---|---|
| Workbook | `docs/GMG-QMS-LOG-0007_v0.36_PROVISIONING_REGISTER_2026-08-12.xlsx` |
| Raw-file SHA-256 | `b7fb649c82f06020dbcec6e187f36f236dda9954c1d73550a25a32a12569564c` |
| Business version/status | Cover metadata: `0.36`, `DRAFT — NOT APPROVED — NOT LIVE`; Office core revision is not a substitute. |
| Canonical worksheet | Exact name `19_Candidate_Provisioning`; no sheet-index fallback. |
| URS | `docs/GMG-CSV-URS-0001_v0.3_DRAFT_2026-08-12.docx`; SHA-256 `4e903ae00dedb3138258cb482f0067bc8b8e7df6666866eecb9e1a2d90c0346c` |

The canonical worksheet has exactly these 15 named columns: `Candidate Folder ID`, `Parent Candidate ID`, `Level`,
`Explicit Path`, `Entity`, `Node Type`, `Permission Profile`, `Owner Role`, `Release Status`, `Blocking Gate`,
`Path Length`, `Platform Path Status`, `Implementation Note`, `Object Type`, `Provision in Pilot`.

For the authoritative checksum, tests calculate and verify 4,385 objects, 4,384 folders, one control file, 4,186
pilot-eligible, 199 withheld, and 55 compound-profile rows split into 54 pilot-eligible and one withheld. These are
fixture expectations, never runtime constants or parser shortcuts.

The fixed SHA-256 in this pack identifies only the authoritative review fixture used to define and test this draft.
Production code must not hardcode it. Accepted checksum/revision pairs are resolved only from tenant-scoped
`QmsRegisterSourceProfile`. A semantically identical workbook whose package bytes—and therefore raw SHA-256—differ is
not accepted automatically; it requires a new controlled source-profile record/revision decision.

## 8. Column Mapping Contract

| Source | Contract |
|---|---|
| Candidate Folder ID | Required unique source identity; feeds register-backed canonical identity only after validation. |
| Parent Candidate ID | Exact self-join, zero/one parent; blank means root. Orphan, cycle, or inconsistency fails closed. |
| Level | Integer consistent with approved parent/path rules; no correction by assumption. |
| Explicit Path | Normalize only by approved algorithm; never sole persisted identity when Candidate Folder ID exists. |
| Entity | Trim then exact case-sensitive join to `02_Entity_Model`.`Entity Code`; no alias/default. |
| Node Type | Preserve; map only through an approved vocabulary. |
| Permission Profile | Atomic exact join to `08_Permission_Profiles`.`Profile ID`; compounds remain conflicts and grant nothing. |
| Owner Role | Preserve as provenance; `TBC` remains unresolved and never grants ownership/authorization. |
| Release Status | Preserve and map through a versioned import-row disposition table; blocked cannot be eligible. |
| Blocking Gate | Split only on approved `/`; exact many-to-many join to `11_Go_Live_Gates`.`Gate`; unknown token fails. |
| Path Length | Recalculate with approved normalization and report disagreement. |
| Platform Path Status | Explicit import-classification input; blank/unknown is never ready. |
| Implementation Note | Bounded row provenance only; never parse/execute or copy raw into audit/manifest summaries. |
| Object Type | Controlled folder/control-file discriminator. |
| Provision in Pilot | Controlled eligible/withheld value; withheld rows are reported, never discarded. |

Missing/duplicate normalized headers, unknown schema version, or incompatible semantics are controlled failures.

## 9. Authoritative Classification Mapping Decisions

These are final FU07 structure-import decisions. They do not promote candidate evidence into controlled master data.

### Entity

- Trim `Entity`, then exact case-sensitive join to `02_Entity_Model`.`Entity Code`.
- 4,190 exact rows are Resolved; `GROUP` (25) is Ambiguous; `GMT_UNRESOLVED` (170) is Conflict/Quarantined.
- No alias, inferred legal entity, or default entity may be generated.

### Zone

- Never derive Zone from a fixed path index or arbitrary `Node Type = Governed zone` match.
- Follow `Parent Candidate ID` to one of the approved thirteen level-1 zone ancestors.
- 4,383 rows resolve deterministically; repository root and `_README.txt` are NotApplicable.

### Domain

- Only the nearest explicit `Domain container` ancestor is authoritative: 716 Resolved.
- 2,123 path-derived domain candidates remain Ambiguous; 1,546 rows are NotApplicable.
- Candidate segments may be retained as evidence but never persisted/promoted as approved Domain master data until an
  approved vocabulary/source revision exists.

### Function

- Only `Function container`, `Operational function`, and `Function working area` ancestors are authoritative.
- 1,767 rows are Resolved, 451 are Ambiguous, and 2,167 are NotApplicable.
- `Structural bridge` and untyped BPG COM/HR/SCM branches are never auto-promoted to Function.

### Document Type and Lifecycle Status

- Under an exact `01_Controlled_Documents` marker, its immediate child is structural Document Type evidence; this
  applies to 2,702 rows. Global path-position and deeper-segment type inference are prohibited.
- A type node's immediate child is Lifecycle evidence; 2,316 rows resolve to exactly one of `Draft`, `In_Review`,
  `Approved_Pending_Effective`, `Effective`, `Superseded`, or `Retired`. Unknown states are Quarantined.
- Structural type evidence is not promoted to master data without an approved domain-specific type catalog. FU07
  performs no Controlled Document lifecycle transition.

### URS filing gap

`URS` is not globally absent: it exists under BPG/COM. The actual missing branch is
`08_CSV/01_Controlled_Documents/URS/{six lifecycle states}`. This is `source correction required` or `formal
filing-decision revision required`; FU07 must not synthesize the branch.

### Permission Profile and Owner Role

- Atomic Permission Profile exact-joins to `08_Permission_Profiles`.`Profile ID`: 4,330 Resolved.
- 55 compound rows are Conflict/Quarantined (54 pilot-eligible, one withheld). Never split a compound, select its
  first profile, or issue an automatic grant.
- All 4,385 Owner Role values may be carried as provenance; 687 containing `TBC` remain unresolved. Owner Role is not
  an authorization or runtime ownership-grant source without an approved role registry.

### Record Class, Retention Class, and Legacy ID

- 566 `Record-class candidate` rows plus seven Quality Records candidates (573 total) remain Candidate evidence/
  Quarantined. None is an approved Record Class; FU07 creates no class and assigns no default.
- Retention Class has no authoritative canonical row-level source. Do not emit, default, or derive it; report
  `source revision required`.
- General Legacy ID/crosswalk is absent. Do not generate Legacy ID. Earlier CSV/JSON `legacy_code` is not v0.36
  authority; `36_Retired_Codes` is usable only for an explicit historical entity-code decision. General Legacy ID is
  `source revision required`.

### Direct-safe structure/provenance fields

Carry `Candidate Folder ID`, `Parent Candidate ID`, `Level`, `Explicit Path`, `Node Type`, `Release Status`,
`Blocking Gate`, `Platform Path Status`, `Object Type`, and `Provision in Pilot` as controlled structure/provenance.
Tokenize `Blocking Gate` on `/` and exact-join every token to `11_Go_Live_Gates`.`Gate`; successful lookup does not
grant activation. Duplicate IDs, orphan parents, cycles, level mismatches, and conflicting paths fail closed.

## 10. Validation Rules

### Import quarantine terminology

Import quarantine is a controlled source-row governance-readiness state. It is not a physical folder, does not move a
row under the workbook's `99_Inbox_and_Quarantine` zone, and is not Records Management disposition, retention, or legal
hold. It neither deletes nor changes the source row and creates no physical folder.

1. Compute SHA-256 over exact uploaded bytes and validate it with business revision/status before row parsing.
2. Revision mismatch, checksum mismatch, same revision/different checksum, or same checksum/conflicting revision is a
   controlled conflict; no implicit replacement.
3. Select the canonical sheet by exact name and validate the versioned 15-column schema.
4. Validate unique IDs, parent existence, acyclic hierarchy, level/path, types, joins, and import-row dispositions.
5. Calculate all reconciliation counts from rows; do not hardcode the expected totals in runtime logic.
6. Report blocked, withheld, quarantined, and unknown rows. Unknown classification receives no default.
7. Preserve compound permission expressions as conflicts and report total/pilot/withheld counts.
8. Dry-run performs no business persistence.
9. Commit may persist only a tenant-scoped DRAFT baseline and immutable `QmsRegisterImportManifest`; dry-run persists
   neither baseline nor manifest.
10. Resolve a tenant-scoped `QmsRegisterSourceProfile` before parsing; profile identity, immutable source fields, three
    independent status controls, usage permission, and effective window must pass fail-closed validation.
11. Every source-profile lifecycle transition requires permission, `ExpectedVersion`, reason/evidence, correlation,
    and an audit event. Stale version is a non-destructive 409.
12. The two source-profile unique indexes and replay/conflict behavior in §6 are mandatory. Activation uniqueness
    remains a Controlled Gate and is not inferred from application pre-checks.
13. Produce fail-closed eligibility evidence. `DRAFT`/`NOT APPROVED`/`NOT LIVE` is never eligible, but FU07 does not
    modify or perform FU08 lifecycle or FU05/FU06 provisioning enforcement.
14. FU02 legacy XLSX behavior remains regression-green.
15. Every source row receives a controlled primary outcome from `Resolved`, `Candidate`, `Ambiguous`, `Conflict`,
    `Quarantined`, `Withheld`, or `NotApplicable`; all additional field-level findings remain attached. Multiple field
    problems on one row must not be collapsed or silently discarded.
16. The manifest records aggregate outcome/coverage counts, finalization, import-time eligibility, baseline relation,
    and the immutable source-profile snapshot. Field-level findings live in the separate finding aggregate. Outcome
    precedence must be deterministic;
    Conflict/Quarantined/Withheld findings may never be downgraded to Resolved.

### Structural versus governance blocker matrix

| Class | Included conditions | Commit behavior | Candidate/eligibility behavior |
|---|---|---|---|
| Fatal structural | Missing canonical sheet; missing/duplicate header; unsupported schema; duplicate Candidate Folder ID; duplicate normalized path; orphan parent; cycle; broken parent/path/level relationship; checksum/revision/source-identity conflict | Block the entire baseline commit; create no `CollectionDefinition`, partial tree, or success response | No committed candidate tree; fail-closed evidence behavior remains subject to the failed-attempt Controlled Gate below |
| Governance | Entity ambiguity/conflict; missing Domain/Function or Document Type/Lifecycle vocabulary; compound Permission Profile; Owner Role `TBC`; Record Class candidate; missing Retention or Legacy ID authority; blocked Release Status; unresolved/unknown Blocking Gate; withheld pilot status | Does not by itself block the structurally valid DRAFT structure commit | Preserve the source row as a DRAFT candidate, create one or more findings, and block activation/provisioning as specified by effective finding policy |

Unknown structural schema or grammar is Fatal. Unknown governed vocabulary or mapping is candidate/quarantine and
must not be collapsed into the structural 400/no-persistence rule. Governance quarantine never silently drops the row.

### Parent and descendant eligibility

FU07 produces import-completion, source, and finding/blocker evidence only. The following review/action formulas are
consumer-boundary policy coordinated by DCP-007 and enforced by FU02 plus the separately approved consumer owners;
they are not FU07 lifecycle or provisioning actions. `LegacyDispositionAllowed` and the combined `EvidenceVerified`
decision are outside FU07.

```text
EffectiveBlockX(finding) =
    finding.BlocksX
    AND latest authorized resolution has not explicitly removed that blocker

AncestorBlockedX(node) =
    true when EffectiveBlockX exists on the node or any ancestor
```

```text
CommitEligible(manifest) =
    source profile UsageStatus = DraftImportAllowed
    AND schema/checksum/revision is valid
    AND structural tree is valid
    AND no effective BlocksCommit finding exists
```

```text
ActivationEligible(node) =
    ImportCompletionVerified
    AND consumer-boundary EvidenceVerified
    AND source authority is Approved + Live + ActivationAllowed
    AND the manifest represents the current source revision
    AND NOT AncestorBlockedActivation(node)
```

```text
ProvisioningEligible(node) =
    ActivationEligible(node)
    AND NOT AncestorBlockedProvisioning(node)
    AND the node is not withheld
```

A quarantined parent makes every descendant activation- and provisioning-ineligible even if the child's local findings
are resolved. Resolving a parent triggers deterministic, idempotent descendant re-evaluation. Any effective blocker on
a node makes the applicable result false. Governance findings do not automatically hide a completed DRAFT tree from
an authorized governance review surface, but they continue to block activation/provisioning according to policy.
LOG-0007 v0.36 is DRAFT/NOT APPROVED/NOT LIVE, so its `ActivationEligible=false` and
`ProvisioningEligible=false` under every path. FU07 performs no lifecycle or provisioning transition.

### Re-import and revision lineage

- The same operation identity returns the existing operation and, when finalized, the same manifest/finding result; it
  creates no duplicate.
- A corrected checksum or new source revision creates a new source profile when required, a new operation, and—only
  after successful finalization—a new manifest. The new operation may reference the previous operation through
  `SupersedesOperationId`; it never changes the previous operation's status.
- Revision comparison uses stable `CandidateFolderId + FieldName + FindingCode` semantics.
- A problem corrected by the new source adds an immutable `ResolvedBySourceRevision` or `Superseded` resolution to the
  historical finding. A continuing problem creates a new finding linked through `SupersedesFindingId`.
- No old finding or source value is overwritten, and no old manifest becomes retroactively eligible.
- Re-evaluation and resolution replay are idempotent.

## 11. Failure Paths

| Failure | Outcome |
|---|---|
| Missing canonical sheet/column, duplicate header, unsupported structural schema/grammar | Fatal controlled 400; no baseline, definition tree, partial tree, or success response. |
| Missing permission | 403; no parser/commit side effect. |
| Cross-tenant lookup | 404 non-leakage. |
| Revision/checksum/idempotency conflict | 409; preserve existing data. |
| Stale source-profile `ExpectedVersion` | 409; no transition. |
| Unsupported schema profile | Fail closed before row parsing. |
| `ReviewOnly` profile used for Commit | Controlled `usage-not-allowed`; no persistence. |
| `Disabled`, `Suspended`, or `Retired` profile used for Commit | Prohibited; no persistence. |
| DRAFT/not-approved/not-live workbook used for activation | Prohibited regardless of profile approval. |
| Manifest source-profile snapshot cannot be persisted | Commit is not successful. |
| Compound profile, blocked gate/status, withheld row, unresolved join | Explicit conflict/quarantine/withheld import-row disposition; no silent drop/default. |
| Draft/not-approved/not-live source | Return `ActivationEligibilityAtImport=false` (or equivalent) evidence; FU07 performs no downstream lifecycle/provisioning transition. |
| Duplicate ID/path, missing parent, cycle | Validation failure; import cannot commit. |
| Unknown governed vocabulary/mapping | Preserve the row as DRAFT candidate/quarantined evidence; create findings; do not silently default or drop it. |
| Unauthorized finding assignment/resolution | 403; no resolution or projection change. |
| Cross-tenant finding lookup | 404 non-leakage. |
| Stale finding `ExpectedVersion` | 409; no projection overwrite. |
| Normal second resolution against a terminal result | 409; only the explicit superseding-decision action may append a correcting decision. |
| Source-revision lineage mismatch | 409; no cross-lineage link or mutation. |
| Parent blocker while evaluating a child | Child activation/provisioning evidence is false. |
| Missing resolution evidence / unknown resolution type | Controlled 400; no ledger or projection change. |
| Finding assignment/resolution audit evidence failure | Fail closed; assignment or resolution is not successful. |
| Operation central-audit projection failure | Operation state plus same-CAS TransitionLedger evidence remains authoritative; deterministic projection enters retry/dead-letter reconciliation and is not best effort. |
| Manifest/finding divergence | Commit is not successful or finalized; baseline is not publishable, active, or provisionable. |
| Partial batch failure | No success response or finalized marker; retry is idempotent and creates no duplicate evidence. |
| Stale lease owner or epoch | CAS fails; no cursor, state, ledger, manifest, or finalization mutation. |
| Same operation/business key with different immutable content | Controlled terminal conflict; existing evidence is preserved. |
| Supported input limit exceeded | Persist one bounded `INPUT_LIMIT_EXCEEDED` finding plus deterministic summary/hash; no unbounded finding fan-out. |
| Incomplete baseline list/publish/provisioning attempt | Hidden or rejected by the mandatory consumer-visibility rule; never treated as successful import output. |

## 12. Repo Scope

This draft authorizes no runtime edits. After a separate ready-for-dev gate, prospective scope is limited to the
`QmsRegisterSourceProfile`, `QmsRegisterImportOperation`, `QmsRegisterImportFinding`, and
`QmsRegisterFindingResolution` domain/repository contracts, their Infrastructure Mongo repositories/indexes, the
existing `DocumentManagementQmsBaseline` application feature,
explicitly approved additive FU02 API contract fields, related Platform tests, and existing FU03
import/reconciliation surfaces. No new page/DataTable is owned; consumed UI is
TenantShell, so `golden_reference: none` and `form_field_count: 0` are intentional.

## 13. Protected Paths

- `.antigravity/**` and `gateway/Diten.ApiGateway/**/ocelot.json`
- `services/Diten.AuthService/**` and IdP permission-profile provisioning
- FU08 approve/publish/mark-effective lifecycle runtime
- FU05/FU06 Company/Corporate provisioning runtime
- Controlled Document, Quality Record, retention/legal-hold/records-disposition, physical-folder, binary-storage runtime
- FU08–FU10 identities, packs, registry/tracker rows, annotations, and runtime
- dirty/staged `execution/registries/module-implementation-status.md`
- all unrelated dirty-worktree content

## 14. Dependencies

| Dependency | Contract |
|---|---|
| MOD-0028 | Parent Documentation & Evidence Management boundaries. |
| MOD-0028-FU02 | Business dependency: semantic import API/permission, DRAFT model, validation, persistence. |
| MOD-0028-FU03 | Consumed TenantShell upload/dry-run/review/commit UI and Gateway-only proxy. |
| MOD-0018 | Permission ownership; FU07 creates no permission. |
| MOD-0029 Document Master Register | Optional read-only provenance ID/reference; absence does not block `ReviewOnly` or `DraftImportAllowed`. |
| LOG-0007 v0.36 / URS v0.3 | Draft controlled sources; neither grants production authority. |

FU06/DCP-004 is a boundary reference, not a dependency; Corporate behavior must not enter this profile.

## 15. Runtime Constraints

`QmsRegisterSourceProfile`, `QmsRegisterImportOperation`, `QmsRegisterImportManifest`, `QmsRegisterImportFinding`, and
`QmsRegisterFindingResolution` are tenant-owned `TenantScopedEntity` aggregates.
`TenantId`, actor, timestamps, technical `Version`, `ImportedBy`, `ImportedAt`, and
`CorrelationId` are resolved server-side and are never accepted from the client payload. All manifest/baseline access
is tenant-filtered; cross-tenant lookup returns 404 without source/checksum leakage. A source profile has no delete
path: inherited `IsDeleted` remains false and `DeletedAt` remains null. The manifest is immutable and never deleted or
business-updated. Findings and resolutions also have no delete path; resolution history is append-only.

FU07 validates source status and emits fail-closed completion/source/finding evidence. It does not claim
FU08/FU05/FU06 behavior. DCP-007 is the required cross-cutting governance/orchestration boundary; each affected
consumer still requires its owning Module Pack amendment and approval.

### Standalone-compatible resumable persistence decision

FU07 uses `QmsRegisterImportOperation` with manifest-last finalization. Correctness and recovery do not depend on a
multi-document Mongo transaction. If replica-set capability is explicitly verified, a small chunk or finalization
transaction may be used as an optional optimization, but its absence cannot change correctness or recovery behavior.
A long transaction spanning the complete import is prohibited.

No-partial-success means no import-completion evidence, not an absence of intermediate staging records. A Commit may
persist its operation, baseline, definitions, and findings while incomplete; none is successful output until the
`ImportCompletionVerified` predicate below is true.

#### Transaction-free finalization order

1. Enter `Verifying` under the valid lease owner/epoch.
2. Read back definitions and findings.
3. Match expected/actual counts and deterministic hashes.
4. Verify tenant, source profile, checksum, schema, and baseline relationships.
5. Enter `Finalizing` through CAS.
6. Insert the immutable manifest under unique `TenantId + ImportOperationId`.
7. On duplicate manifest, accept identical immutable content as replay; treat different content as terminal conflict.
8. With exact manifest ID and verification evidence, set `Completed` through one operation-document CAS.
9. Append the bounded final transition-ledger event in that same CAS.

If a crash occurs after manifest insert, the operation remains `Finalizing`. Retry verifies that manifest and reapplies
the `Completed` CAS; it creates neither a second manifest nor a second successful transition.

### Failed structural Commit-attempt decision

Dry-run remains business-persistence-zero. A Commit request may persist `QmsRegisterImportOperation`, but fatal
validation creates no `BaselineRelease`, `CollectionDefinition`, or manifest. The operation becomes
`ValidationFailed`, and controlled fatal findings link to required `ImportOperationId` while `ImportManifestId` remains
nullable. Successful manifests may keep required `BaselineReleaseId`; failed-attempt evidence is never represented as
a successful manifest.

The same invalid operation retry returns the existing `ValidationFailed` result. Corrected bytes/checksum create a new
operation. For a malicious or excessive source beyond the supported input limit, persistence is bounded to an
`INPUT_LIMIT_EXCEEDED` finding plus deterministic summary/hash evidence rather than unbounded row findings.

### Import completion evidence and ownership impact gate

```text
ImportCompletionVerified =
    baseline.TenantId == currentTenant
    AND baseline.IsDeleted == false
    AND baseline.ImportOperationId != null
    AND same-tenant operation exists
    AND operation.Status == Completed
    AND exact same-tenant immutable import manifest exists
    AND operation.ManifestId == manifest.Id
    AND manifest.ImportOperationId == operation.Id
    AND manifest.BaselineReleaseId == baseline.Id
    AND immutable definition count/hash matches finalization
    AND immutable detection-finding count/hash matches finalization
```

Only `Completed` can satisfy this predicate; all other ten canonical operation states return false. FU07 produces this
completion evidence only. It does not own legacy/manual disposition, and a new FU07 import can never use a
legacy/manual path to bypass its operation/manifest evidence. `LegacyDispositionAllowed`, combined
`EvidenceVerified`, review visibility, activation eligibility, and provisioning eligibility are DCP-007/FU02
consumer-boundary decisions.

An incomplete operation's baseline/definitions are absent from normal lists, cannot be published, approved, or made
Effective, cannot be instantiated or provisioned, and cannot be exposed to template/document consumers. A completed
DRAFT import may be considered for an authorized governance review; governance findings do not automatically hide it
from that review, while applicable blockers continue to prevent activation/provisioning.

| Consumer | Existing owner | Change required? | Member-pack/DCP decision |
|---|---|---|---|
| Baseline list/detail | FU02 backend + FU03 UI | Yes | Separate FU02 and FU03 amendments under DCP-007 |
| Publish/approve/effective | FU02 | Yes | FU02 amendment owns completion-guard integration; FU08 annotation is AS-IS drift only |
| Company instantiation | FU05 | Yes | Separate FU05 amendment under DCP-007 |
| Corporate provisioning | FU06 | Yes | Separate FU06 amendment under DCP-007 |
| Company reconciliation/readiness | FU05 | Yes | FU05 amendment; explicit `CollectionScopeType + ScopeOwnerId` required |
| Corporate reconciliation/readiness | FU06 | Yes | FU06 amendment; explicit `CollectionScopeType + ScopeOwnerId` required; FU09 annotation is AS-IS drift only |

Cross-cutting consumer visibility is governed by DCP-007, which remains `under-review`. FU07 remains `draft` and
`runtime_code_allowed: false`; it implements none of these consumer behaviors and produces only
`ImportCompletionVerified` evidence. Every consumer change requires its own approved amendment. Lifecycle and
reconciliation ownership is resolved as FU02 lifecycle guard, FU05 Company reconciliation/readiness, and FU06
Corporate reconciliation/readiness amendments. The shared reconciliation engine is owner-neutral technical code, not
a business SoR; scope-less or owner-mismatched calls fail closed, and provider/readiness queries cannot collect all
same-baseline instances without scope filtering. Existing FU08/FU09 annotations are neither canonical members nor
systems of record and must not become authority or dependencies.

The imported Group Baseline is tenant-owned and `TenantId` is resolved server-side. FU07 creates no Company
`CollectionInstance`, company share, overlay, local addition, local folder/file, group-node propagation/removal, or
template propagation. A later DCP may consume stable canonical IDs and versioned baselines only after FU07 import and
verification complete. Protection of company-local content and group-node retirement/removal behavior are mandatory
downstream governance decisions, not FU07 behavior.

CQRS, `Response<T>`, audit/correlation, and pipeline conventions apply. No physical folder, binary, document, or
Quality Record is created. Workbook bytes are not written to local disk or manifest/audit storage. Deterministic
identity/hash includes tenant and source-baseline context and remains stable under retry. Production checksum,
revision, schema, approval, and usage authority comes only from `QmsRegisterSourceProfile`, never code or appsettings.

## 16. Layout & Shell Contract

No page is added. Existing FU03 TenantShell/Gateway-only flow may consume an approved additive reconciliation contract
later. `_LayoutTenantShell.cshtml` remains the shell; `_Layout.cshtml` and direct port 5057 calls are forbidden.

## 17. Backend File Convention

Future work adds the source-authority aggregate/repository `QmsRegisterSourceProfile`, resumable
`QmsRegisterImportOperation`, plus the FU07-owned quarantine finding and immutable resolution-ledger persistence inside
the existing FU07/FU02 feature boundary. It must
not create a broader source-authority aggregate, another tenant context, reuse FU09 deviations, or create a parallel
import route family. Source-profile resolution and source-contract checks precede parsing and commit.

## 18. Frontend File Contract

No frontend edit is authorized now. Future approved work is limited to displaying calculated reconciliation and
import-row disposition evidence in FU03's existing import flow. Browser extension checks never replace backend
validation.

## 19. Authorization

`QmsBaselinePermissions.Import` continues to govern the FU02 import action but is not source-profile governance
authority. Source-profile governance requires separate semantic actions: view, manage/create, submit-review, approve,
enable-review, enable-draft-import, activate, suspend, resume, and retire. Exact permission literals, role grants, and
approver/activator segregation of duties remain Security Controlled Gates. This draft authorizes no permission seed.
Server authorization precedes lookup, parsing, lifecycle transition, and persistence.

FU07 owns the import-operation status and safe findings-summary endpoints. Their exact MVP authorization policy is:

```text
CanPollOwnImportOperation =
    same tenant
    AND HasPermission("platform.document-management.qms-baselines.import")
    AND operation.RequestedBy == current actor
```

The same policy protects safe findings-summary. Cross-tenant or another-actor access returns 404 and discloses no
operation existence, source identity, checksum, finding metadata, or actor metadata. Cross-user governance reviewer
polling is outside MVP, deny-by-default, and deferred to a future permission/security amendment. Existing `view`,
`publish`, or `import` permission alone never grants cross-user access; the deferral is not a DCP-007
ready-for-execution blocker for the self-polling MVP, and this draft authorizes no new permission seed.

Controlled status/findings responses may contain only `OperationId`, canonical `Status`, controlled display phase,
`Version`, `IsReplay`, retryability, bounded progress counts, controlled `ReasonCode`, `StatusUrl`, safe aggregate
finding counts/categories, and `BaselineReleaseId` only after `Completed`. Raw workbook, raw row,
`Implementation Note`, sensitive Notes, secrets, uncontrolled source payload, and another user's actor metadata are
forbidden.

Completed baseline governance review is a separate FU02 consumer policy:

```text
CanReviewCompletedBaseline =
    same tenant
    AND HasPermission("platform.document-management.qms-baselines.view")
    AND ReviewVisible == true
```

Polling permission produces no `ReviewVisible` value and grants no baseline list/detail access.

Quarantine governance additionally requires semantic actions to view findings, assign an owner, submit for review,
await source correction, record a governance decision, accept a known limitation, reject a proposed resolution, link
or supersede a source-revision finding, trigger re-evaluation, and view resolution/audit history. Exact permission
literals are deliberately not defined here. The importer/detector and the actor deciding a blocking resolution remain
a segregation-of-duties Controlled Gate.

| Finding category | Semantic owner / decision authority |
|---|---|
| Structural / Schema / Path | Platform/Data Engineering |
| SourceIdentity / ChecksumRevision | Platform/Data Engineering + QA/CSV |
| Entity / Zone | Data Governance/EA |
| Domain / Function / DocumentType / Lifecycle | QA/QMS |
| PermissionProfile | Security/IT |
| OwnerRole | QA/QMS + Security/IT |
| RecordClass / Retention | Records Management/Legal |
| LegacyId | EA/Data Governance |
| ReleaseStatus / BlockingGate / SourceApproval | QA/CSV |

## 20. Audit Events

Future approved work audits source-profile create, submit-review, approve/reject, enable-review, enable-draft-import,
activate, suspend, resume, retire, dry-run outcomes, commit/idempotent replay, checksum/revision conflicts, quarantine
summaries, and fail-closed eligibility outcomes through platform audit/correlation. Every lifecycle transition carries
`ExpectedVersion`, permission, reason/evidence, correlation, actor, outcome, and controlled before/after statuses.
Audit metadata includes no raw workbook, raw row, `Implementation Note`, or sensitive `Notes` content.

Finding assignment, every resolution attempt/outcome, superseding decision, revision lineage, descendant re-evaluation,
and persistence finalization are audited. Audit metadata is limited to IDs, controlled category/code, before/after
status, actor, owner role, evidence reference, correlation, and outcome.

The bounded append-only `QmsRegisterImportOperation.TransitionLedger` is the system of record for regulated operation
transitions. State mutation and ledger append occur in the same operation-document CAS; if the append fails, state does
not change. Lease acquisition/takeover, failure, and finalization are recorded; heartbeat is excluded to avoid evidence
noise. Central audit is an idempotent projection of this ledger, not best-effort evidence authority. A deterministic
audit-event key supports retry and reconciliation, and dead-letter/reconciliation visibility is mandatory. Projection
failure does not lose evidence and does not rewrite operation state; best-effort audit is prohibited. Embedded-ledger
maximum event count/document size,
the reconciliation worker owner, and the dead-letter operating procedure remain Controlled Gates.

## 21. Gateway / API Routing

Consume existing FU02 compatibility routes:

- `POST /api/v1/document-management/qms-baselines/import/dry-run`
- `POST /api/v1/document-management/qms-baselines/import/commit`

No new route or `ocelot.json` change is required.

## 22. Acceptance Criteria

- [ ] Pack is explicitly approved/ready-for-dev and `runtime_code_allowed` changed before runtime edits.
- [ ] Existing retrospective surfaces are tested against this pack; presence is not treated as validation.
- [ ] Review-fixture SHA-256 values match; production accepts checksum/revision/schema/usage only through a tenant-
      scoped `QmsRegisterSourceProfile`, never code, appsettings, or Document Master Register authority.
- [ ] `QmsRegisterSourceProfile` uses `TenantScopedEntity`, has no delete path, and keeps inherited `IsDeleted=false`
      and `DeletedAt=null`; retirement uses only `UsageStatus=Retired`.
- [ ] WorkbookBusinessStatus, ProfileApprovalStatus, and UsageStatus remain independent and both state machines reject
      skipped, backward, unauthorized, or stale-version transitions.
- [ ] LOG-0007 v0.36 begins Draft/Disabled, may reach approved/ReviewOnly after governance review, cannot reach
      DraftImportAllowed before FU07 implementation verification, and cannot reach ActivationAllowed while DRAFT/
      NOT APPROVED/NOT LIVE.
- [ ] The two non-partial tenant/document revision and checksum unique indexes atomically enforce both controlled 409
      conflicts across historical, suspended, and retired profiles.
- [ ] Exact canonical sheet and versioned 15-column schema are enforced.
- [ ] Dry-run is persistence-zero and reports every import-row disposition.
- [ ] Workbook-calculated results equal `4385/4384/1/4186/199` and compound `55/54/1`; runtime contains no count shortcuts.
- [ ] Commit creates only a tenant DRAFT baseline, separate tenant-scoped findings, an append-only resolution seam,
      and immutable `QmsRegisterImportManifest`; no raw workbook, raw row, `Implementation Note`, or sensitive notes
      enter manifest, finding, resolution, or audit summaries.
- [ ] Every committed manifest contains an immutable source-profile/version/status/effective-window/evidence/DMR
      snapshot plus resolved tenant, actor, correlation, and snapshot time; later suspend/retire does not mutate it.
- [ ] FU07 emits `ActivationEligibilityAtImport=false` (or equivalent) for DRAFT/not-approved/not-live sources and performs no
      FU08 lifecycle or FU05/FU06 provisioning change; downstream enforcement requires separate governance scope.
- [ ] Retry is idempotent; revision/checksum conflicts are non-destructive 409s.
- [ ] Fatal structural cases block the whole commit and create no partial tree; unknown governed vocabulary/mapping
      preserves the row as DRAFT candidate/quarantined evidence and blocks activation/provisioning without silently
      defaulting or dropping the row.
- [ ] Tenant isolation, 403/404/409, audit/correlation, deterministic ID/hash, and FU02 compatibility are verified.
- [ ] `TenantId` and actor/correlation/time fields are server-resolved; cross-tenant manifest/baseline access is 404.
- [ ] No permission seed, Gateway, FU08 lifecycle, FU05/FU06 provisioning, physical folder, binary, FU08–FU10 identity,
      or records-disposition-engine change.
- [ ] Output is only a tenant-owned DRAFT Group Baseline; no Company `CollectionInstance`, company share/overlay,
      local folder/file, propagation, removal, or template-sharing behavior is introduced.
- [ ] Every source row has one controlled primary outcome and retains all field findings; no row is silently omitted.
- [ ] Manifest holds only immutable snapshots, aggregate counts, import outcome, import-time eligibility, baseline
      relationship, and finalization; the 4,385-row field-level finding set is not embedded in one Mongo document.
- [ ] Finding immutable evidence, status lifecycle, append-only resolution ledger, source-revision lineage, tenant
      isolation, non-partial unique indexes, CAS 409, duplicate normalization, and parent/descendant eligibility match
      the contracts in §§6 and 10.
- [ ] Commit success requires durable consistency of the `BaselineRelease`, every candidate `CollectionDefinition`, the
      complete immutable detection-finding set, the immutable manifest, and finalization marker; partial staging never
      satisfies `ImportCompletionVerified`.
- [ ] Commit uses the exact operation key, state machine, manifest-last finalization, deterministic chunk order,
      independent cursors, count/hash read-back, lease epoch fencing, and non-partial indexes defined in §6.
- [ ] Fatal Commit validation persists at most the operation and bounded fatal findings, produces no baseline,
      definitions, or manifest, and returns the same `ValidationFailed` result on an identical retry.
- [ ] `ImportCompletionVerified` requires the same-tenant non-deleted baseline, `Completed` operation, exact immutable
      operation/manifest/baseline links, and matching immutable definition plus detection-finding counts/hashes; every
      other canonical operation status returns false.
- [ ] A new FU07 import cannot use legacy/manual disposition to bypass operation/manifest verification; FU07 emits no
      `LegacyDispositionAllowed` or combined `EvidenceVerified` decision.
- [ ] Finding finalization hash uses `FindingScopeKey` ordinal ordering and only immutable detection evidence; mutable
      workflow/assignment/resolution/eligibility/version/timestamp/audit projections are excluded.
- [ ] Later assignment, resolution, or parent-resolution re-evaluation leaves completed import hash integrity intact;
      resolution integrity remains independently verifiable through the append-only ledger, CAS/version, and audit.
- [ ] Exact finding-hash canonical serialization/normalization and fixture values remain blocked by DCP-007 G12 and
      are not guessed or hard-coded.
- [ ] Transition ledger and state advance atomically in one operation CAS; central audit projection is deterministic,
      retryable, reconcilable, and never the sole evidence source.

### Authoritative fixture classification coverage

| Field | Resolved | Missing | Ambiguous | Conflict | N/A |
|---|---:|---:|---:|---:|---:|
| Entity | 4,190 | 0 | 25 | 170 | 0 |
| Zone | 4,383 | 0 | 0 | 0 | 2 |
| Domain | 716 | 0 | 2,123 | 0 | 1,546 |
| Function | 1,767 | 0 | 451 | 0 | 2,167 |
| Document Type | 2,702 | 0 | 0 | 0 | 1,683 |
| Lifecycle | 2,316 | 0 | 0 | 0 | 2,069 |
| Permission Profile | 4,330 | 0 | 0 | 55 | 0 |
| Record Class | 0 | 0 | 573 | 0 | 3,812 |
| Retention Class | 0 | 4,385 | 0 | 0 | 0 |
| Legacy ID | 0 | 4,385 | 0 | 0 | 0 |

Every row totals 4,385. These values are calculated acceptance expectations for the authoritative review fixture;
production code must not hardcode them.

## 23. Test Expectations

Future tests: source-profile create and immutable-field validation; independent approval/usage state machines;
permission and `ExpectedVersion` gates; suspend/resume/terminal-retire behavior; no delete surface; both non-partial
unique indexes under concurrent insert; deterministic duplicate-key 409 normalization; tenant/cross-tenant profile
lookup; optional DMR reference; manifest snapshot immutability; a small anonymous v0.36-schema XLSX fixture;
checksum-gated read-only test of the real workbook;
persistence-zero dry-run; calculated `4385/4384/1/4186/199` and `55/54/1`; import-row disposition coverage; missing sheet/column;
duplicate ID/path; orphan/cycle; checksum/revision conflict; idempotency/concurrency; tenant isolation; 403/404/409;
audit/correlation; deterministic canonical ID/hash; and regression for FU02 legacy XLSX plus earlier CSV/JSON fixtures.
Audit tests prove metadata contains no raw workbook, raw row, `Implementation Note`, or sensitive `Notes`. Fixtures
assert calculated results; production code never embeds source-workbook totals.

Quarantine tests additionally cover: whole-commit rejection and zero definitions for each Fatal structural class;
governance-problem rows retained as DRAFT candidates; multiple findings per row; non-embedded manifest summaries;
finding immutability/no-delete; append-only resolution and decision supersession; active/Rejected/terminal status
semantics; forbidden structural overlay; known-limitation non-unblocking behavior; parent-to-descendant blocking and
idempotent re-evaluation; same-revision replay; new-revision lineage; non-partial unique indexes; duplicate-key
normalization; tenant 404; permission 403; missing evidence 400; stale/terminal/lineage 409; audit fail-closed; and
manifest/finding/partial-batch finalization failure. The selected persistence protocol is load-verified with 4,385
rows and multiple findings without hardcoded counts.

Mandatory resumability/failure-injection tests cover baseline-create crash; definition chunk N crash; finding chunk N
crash; verification hash mismatch; manifest-insert crash; crash before `Completed` CAS; concurrent workers; expired
lease takeover; stale lease epoch; duplicate retry; same key with different immutable content; central-audit projection
failure; Mongo duplicate key; network timeout; cross-tenant operation lookup; and incomplete-baseline list, publish, and
provisioning attempts. Acceptance requires no duplicates, no import-completion-verified incomplete baseline, continuation from
the recorded cursor, exactly one manifest, exactly one `Completed` transition, no data loss, and reproducible audit
evidence.

Finding-hash tests use `FindingScopeKey` ordinal ordering and prove that immutable detection-field changes alter the
hash while assignment, workflow status, resolution-ledger append, parent re-evaluation, `UpdatedAt`, `Version`, and
central-audit projection changes do not. Exact canonical serialization/normalization and fixture values cannot be
implemented until DCP-007 G12 receives explicit approval; production tests and code must not hard-code workbook
finding counts or hashes.

The real-workbook reconciliation test also verifies every §22 coverage row totals 4,385; exact Entity and atomic
Permission joins; parent-chain Zone resolution; authoritative Domain/Function ancestor rules; marker-relative
Document Type/Lifecycle evidence; the `08_CSV` URS filing gap; all seven row outcomes; multi-field findings; 687 `TBC`
Owner Roles; source-revision-required Retention/Legacy results; and zero Company-instance/sharing side effects.

## 24. Ready-for-dev Checklist

- [x] EA/user approved the retrospective identity on 2026-08-26.
- [x] Exact name/parent identity preflight passed.
- [x] Draft pack and registry reservation exist.
- [x] Source hashes and reconciliation were independently read and recorded.
- [x] Authoritative structure-import mapping decisions and coverage expectations are recorded in §§9 and 22.
- [x] Structure-only DRAFT Group Baseline boundary is fixed; full classification/provisioning is excluded.
- [x] Production source authority is fixed as FU07-owned tenant-scoped `QmsRegisterSourceProfile`.
- [x] Workbook, profile-approval, and usage status authorities and their separate state machines are fixed.
- [x] Source-profile immutable fields, no-delete rule, two atomic non-partial unique indexes, supporting indexes, and
      manifest snapshot contract are fixed.
- [x] OperationKey and manifest replay/idempotency indexes exclude technical source-profile Version and are fixed.
- [x] Cross-cutting consumer visibility requires DCP-007; the DCP artefact exists and is `under-review`.
- [x] FU07 is only the authoritative import-completion evidence owner; legacy/manual disposition and combined
      downstream evidence decisions are outside FU07.
- [x] Immutable finding-hash field scope, ordinal ordering, and mutable-field exclusions are fixed at governance level.
- [ ] `TenantScopedEntity` decision is verified against the concrete FU02 runtime base contract.
- [x] Quarantine terminology, structural-versus-governance behavior, FU07-owned finding/ledger model, status lifecycle,
      revision lineage, parent/descendant eligibility semantics, and non-partial finding/resolution index contracts are approved.
- [x] FU07 eligibility-evidence versus downstream-enforcement boundary is approved.
- [x] FU08/FU05/FU06 and Company sharing/overlay/propagation are excluded from FU07.
- [x] Cross-cutting sharing DCP is explicitly deferred until FU07 implementation and verification complete.
- [x] Import-row disposition and import quarantine terminology are approved and disambiguated from Records Management.
- [x] Approved source checksum/revision/schema/usage authority method is selected: `QmsRegisterSourceProfile`.
- [ ] Platform, QA/QMS, security, and data-governance owners review the AC/test plan.
- [ ] Exact categories eligible for governance-overlay resolution are approved.
- [ ] QA/QMS, Security/IT, Data Governance/EA, and Records Management/Legal approve the authority matrix.
- [ ] Exact permissions and importer/detector versus blocking-resolution decision-maker SoD are approved.
- [x] Failed structural Commit-attempt evidence uses a Commit-only operation plus bounded fatal findings and no manifest.
- [x] Standalone-compatible resumable operation plus manifest-last finalization is the selected persistence protocol;
      correctness is transaction-independent and a long import transaction is forbidden.
- [x] Bounded TransitionLedger is regulated transition evidence; central audit is its idempotent projection.
- [ ] Chunk size is accepted by load test; lease duration, heartbeat interval, and worker identity are approved.
- [ ] Transition-ledger maximum size, audit reconciliation owner, and dead-letter operating procedure are approved.
- [ ] DCP-007 is explicitly `approved` or `ready-for-execution`; `under-review` is not execution authority.
- [x] FU02/FU03/FU05/FU06 DCP-007 amendments were separately approved by the user on 2026-08-27.
- [x] DCP-007 G2 member-governance gate is resolved/approved at governance level.
- [ ] FU06 parent pack passes its separate runtime-readiness/execution gate; its status remains `review`.
- [x] Baseline lifecycle completion-guard integration owner is FU02 amendment; broader Controlled Document lifecycle
      is unchanged and FU08 annotation remains AS-IS drift.
- [x] Reconciliation/readiness ownership is split between FU05 Company and FU06 Corporate amendments; the generic
      engine is owner-neutral and requires explicit `CollectionScopeType + ScopeOwnerId`.
- [ ] Exact finding-hash canonical serialization/normalization and fixture values are explicitly approved under
      DCP-007 G12.
- [x] FU07 owns operation status and safe findings-summary endpoints under the same-tenant + import permission +
      `RequestedBy == current actor` MVP policy.
- [x] Cross-user operation/findings polling is explicitly deferred and deny-by-default pending a future
      permission/security amendment.
- [ ] Finding and manifest retention is approved.
- [ ] Runtime DTO, endpoint, enforcement, and focused-test evidence is implemented and verified under later runtime
      authority.
- [ ] User explicitly changes status to `approved`/`ready-for-dev` and `runtime_code_allowed` to `true`.

Open source-remediation items in §26 do not block approval of the structure-only dry-run/DRAFT-import design. They do
block operational activation and any claim of complete governed classification.

## 25. Implementation Notes

- This is a post-code recovery record, not retroactive authorization.
- The 2026-08-27 DCP-007 reconciliation is governance-only. DCP-007 is `under-review`; FU07 remains `draft` and
  `runtime_code_allowed: false`.
- On 2026-08-27 the user approved FU02 baseline lifecycle guard ownership, the FU05 Company/FU06 Corporate
  reconciliation split, FU07 polling/findings endpoint ownership, the importer self-polling policy, and cross-user
  polling deferral/deny-by-default. These decisions close the DCP-007 G4 governance decision and G6 MVP policy
  decision only; they authorize no runtime or permission-seed change.
- Target v0.36 differs from the existing v0.8 CSV/JSON fixtures and FU02's legacy `last version` XLSX sheet.
- Existing neutral runtime annotations remain unchanged in this governance-only task.
- Quarantine is a governance-readiness evidence state only. It creates/moves no physical folder and owns no Records
  Management disposition, retention, or legal-hold behavior.
- The persistence decision is a standalone-compatible resumable operation with manifest-last finalization. Correctness
  is transaction-independent; optional small transactions require verified replica-set capability, and one long import
  transaction is prohibited. Load proof remains required before runtime authorization.
- The staged/dirty implementation tracker was intentionally not edited. FU07 reconciliation is deferred until that
  conflict is resolved; the future tracker row must separate existing code from validation status and must not label
  FU07 approved, validated, live, or done merely because code exists.
- `target: 2026-08-26` is the governance draft date, not a runtime delivery promise.

### Governance ownership and access decision record — 2026-08-27

- The user approved FU02 amendment ownership for baseline list/detail/definition and
  publish/approve/mark-effective completion-guard integration without changing broader Controlled Document lifecycle
  ownership; FU08 remains AS-IS drift evidence.
- The user approved FU05 Company and FU06 Corporate reconciliation/readiness ownership. The shared engine is
  owner-neutral, requires explicit `CollectionScopeType + ScopeOwnerId`, and is not a business SoR; FU09 remains AS-IS
  drift evidence.
- The user approved FU07 ownership of the operation aggregate, status endpoint, safe findings-summary endpoint,
  self-operation authorization, and tenant/requester non-leakage. FU02 retains the import permission literal and
  combined consumer guard; FU03 remains the polling UI consumer.
- The G4 governance ownership decision and G6 MVP self-polling policy decision are resolved. Cross-user polling is
  deny-by-default/deferred. The four member amendments and G2 are approved/resolved at governance level; runtime
  DTO/endpoint/enforcement/test evidence remains open.
- No runtime, frontend, Gateway, registry, tracker, test, permission seed, `.antigravity`, or Git-state change was made
  by this governance reconciliation.

## 26. Follow-up Items

### Closed by this review

- `TenantScopedEntity` is the operation/manifest base contract; tenant/actor/correlation/time values are server-resolved and
  cross-tenant reads return 404.
- `QmsRegisterImportManifest` minimum fields, immutability, no-hard-delete rule, and raw-content exclusions are fixed.
- Dry-run is persistence-zero; only Commit persists an operation and successful manifest linked to a DRAFT baseline.
- FU07 produces authoritative `ImportCompletionVerified` evidence and source/finding blocker evidence; it does not own
  legacy/manual disposition, combined `EvidenceVerified`, review/action decisions, or FU08/FU05/FU06 enforcement.
- Records Management disposition is disambiguated from **import-row disposition** throughout this pack.
- The fixed SHA-256 identifies the review fixture only; production uses controlled checksum/revision authority and
  byte-different repackaging requires a controlled source-revision update.
- Entity exact-join outcomes, parent-chain Zone derivation, explicit-ancestor Domain/Function rules, marker-relative
  Document Type/Lifecycle evidence, Permission/Owner handling, Record Class quarantine, and Retention/Legacy omissions
  are fixed for the structure-only profile.
- The imported object is a tenant-owned DRAFT Group Baseline, never a Company Collection Instance or shared company
  tree.
- Production source authority is the FU07-owned tenant-scoped `QmsRegisterSourceProfile`; Document Master Register is
  optional provenance and cannot own checksum/revision/schema/usage decisions.
- Source-profile approval and usage state machines, no-delete policy, atomic revision/checksum indexes, and immutable
  manifest snapshot fields are fixed.
- Fatal structural failures block the complete tree commit; governance findings preserve DRAFT candidate nodes and
  block activation/provisioning without silently dropping source rows.
- FU07-owned tenant-scoped findings, append-only resolutions, status semantics, revision lineage, parent/descendant
  eligibility formulas, and finding/resolution index/concurrency contracts are fixed.
- Manifest scope is limited to immutable source snapshots, aggregate counts, outcome, import-time eligibility,
  baseline relationship, and finalization. Dynamic eligibility is derived from finding/resolution evidence.
- FU09 reconciliation/deviation persistence is not FU07 authority and is not reused.
- Standalone-compatible resumable operation, manifest-last finalization, transaction-independent correctness, and the
  prohibition on one long import transaction are fixed.
- Operation aggregate fields, technical-Version-free `OperationKey`, immutable historical status/lineage, terminal
  states, state machine, deterministic chunk/cursor algorithm, lease epoch fencing, indexes, and crash recovery are
  fixed.
- Fatal Commit validation persists no baseline/definitions/manifest; it may retain the operation and bounded findings
  linked by `ImportOperationId`.
- The operation's bounded append-only TransitionLedger is regulated transition evidence; central audit is its
  idempotent, reconcilable projection.
- DCP-007 is the required cross-cutting consumer-visibility governance artefact and is currently `under-review`.
- Baseline lifecycle guard integration is assigned to an FU02 amendment. Company reconciliation/readiness is assigned
  to an FU05 amendment and Corporate reconciliation/readiness to an FU06 amendment; FU08/FU09 annotations remain
  AS-IS drift evidence only.
- FU07 owns self-operation status and safe findings-summary endpoints. MVP access is same-tenant, requires
  `platform.document-management.qms-baselines.import`, and requires `RequestedBy == current actor`; cross-user access
  is 404 deny-by-default and deferred.
- Immutable finding-hash field scope, mutable-field exclusions, and `FindingScopeKey` ordinal ordering are fixed;
  exact canonical serialization/normalization and fixture values remain open under DCP-007 G12.

### Open source-remediation items

1. Approved Domain vocabulary.
2. Approved Function vocabulary.
3. Approved domain-specific Document Type catalog.
4. Corrected `08_CSV/01_Controlled_Documents/URS/{six lifecycle states}` branch or formal filing-decision revision.
5. Approved Record Class list.
6. Record Class/entity-based Retention schedules.
7. General Legacy ID/crosswalk source.
8. Correction of 55 compound Permission Profile rows (54 pilot-eligible, one withheld).
9. Resolution of 687 Owner Role values containing `TBC` through an approved role registry.
10. IQ-01/IQ-02 update for the v0.36 checksum and `4385/4384/1` reconciliation.
11. Resolution of the QA free-entity-code `1` versus Cover/Entity Model codes `60` and `70` (`2`) inconsistency.

These source gaps do not block FU07's structure-only dry-run/DRAFT-import design. They block operational activation,
master-data promotion of candidate classifications, and any assertion of complete governed classification.

### Remaining implementation-design gates

- Approve Cover business-version/status extraction and the treatment of Office core `revision=0`.
- Decide approver/activator segregation of duties.
- Decide whether `ActivationAllowed` requires `DocumentMasterRegisterEntryId`.
- Approve the approval-evidence format and SchemaProfile catalog owner.
- Decide activation effective-window/current-activation uniqueness and atomic overlap behavior.
- Approve source-profile, manifest, finding, and resolution retention.
- Accept chunk-size default 250/minimum 50/maximum 500 through load testing.
- Fix lease duration, heartbeat interval, and authoritative worker-identity source.
- Fix the embedded TransitionLedger maximum event/document size, audit-reconciliation owner, and dead-letter procedure.
- Approve the controlled-CI method for the real workbook.
- Resolve the MOD-0029-FU06 governance-record gap before making DMR an activation dependency.
- Approve the remaining FU02 snapshot relation details.
- Approve the exact governance-overlay-eligible categories and the QA/QMS/Security/Records authority matrix.
- Select exact permission literals and close importer/detector versus blocking-resolution decision-maker SoD.
- Approve DCP-007 as `approved`/`ready-for-execution`; FU02/FU03/FU05/FU06 amendments are already approved at governance
  level, while FU06 parent runtime readiness remains separate.
- Implement and verify the FU07 status/findings DTOs, endpoints, tenant/requester enforcement, 404 non-leakage, safe
  response exclusions, and focused tests under later runtime authority.
- Approve exact finding-hash canonical serialization/normalization and fixture values under DCP-007 G12.
- Prove resumability, fencing, finalization, and consumer invisibility at the 4,385-row multi-finding fixture scale.
- Reconcile FU07 in `module-implementation-status.md` only after its staged user changes are resolved.

### Delivery Capability Pack decision

Cross-cutting completion visibility and consumer guardrails are definitively governed by DCP-007. DCP-007 is
`under-review`, not approved or ready for execution. FU02, FU03, FU05, and FU06 amendments are approved at governance
level and G2 is resolved; that approval grants no runtime authority, and FU06 parent runtime readiness remains open.
Lifecycle guard ownership is resolved to FU02; reconciliation/readiness ownership is resolved to FU05 Company and FU06
Corporate scopes. FU07 polling/findings endpoint ownership and the importer self-polling MVP policy are resolved, while
cross-user polling is deny-by-default/deferred. Existing FU08/FU09 annotations are neither canonical members nor
systems of record and cannot be used as boundary shortcuts. FU07 cannot become ready-for-dev until the remaining DCP,
runtime-evidence, G12, load/lease/heartbeat, retention/audit, permission/SoD, and pack status/runtime gates close.

DCP-007 excludes tenant-baseline sharing, Company Collection Instance overlays, company-local additions, group-node
propagation/removal, protection of company-local content, and template sharing. Those concerns remain a separate
future Delivery Capability Pack after FU07 import implementation and verification. A MOD-0029/Records Management
workflow likewise requires separate cross-cutting governance; none of that behavior is authorized by this draft.
