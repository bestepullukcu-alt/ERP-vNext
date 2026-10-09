---
id: MOD-0022
name: e-Signature Service
domain: platform-shared-services
service: Diten.Platform (5057, signature SoR + policy + workflow hook) + Diten.AuthService (5056, password re-authentication ticket) + frontend/Diten.Web
shell: tenant
golden_reference: slim
data_mode: server
screens:
  - module: ESignatureRequests
    data_mode: server
  - module: ESignatureVerification
    data_mode: server
  - module: ESignatureAuditExport
    data_mode: server
  - module: ESignatureMeanings
    data_mode: client
  - module: ESignatureSampleApprovals
    data_mode: client
entity_base: TenantScopedEntity
status: ready-for-dev
status_note: "draft — WP-0022-P0 (CT-MVP1-0022-P0 v3), 2026-10-07. Owner decisions KS-0…KS-13 (2026-10-07) recorded in §19 as DECISIONS. CT decisions CT-D1…CT-D15 TAKEN 2026-10-07 (§19; CT-D8 = own e-İmza nav group, CT override). WP-0022-P0 CT-ACCEPTED 2026-10-07. ready-for-dev (owner) 2026-10-07. ESIGN-BUNDLE v1 proposal: docs/analysis/contracts/drafts/esign-bundle-v1-proposal.md. No registry row yet (CT adds it, §19 'Registry'). DCP-002 verifier not run (no Python — waiver proposal W-0022-01)."
owner: platform-shared-services / mvp-1 control-tower
branch: feature/sce/mod-0022-e-signature
started: 2026-10-07
target: 2026-10-31
form_field_count: 10
dependencies: [MOD-0018, MOD-0021, MOD-0023, MOD-0029-FU23, MOD-0013, DEV-0000]
consumers: [MOD-0175, MOD-0029, MOD-0112, MOD-0195, MOD-0233, MOD-0238, MOD-0358]
---

# MOD-0022 — e-Signature Service

> **Identity (DCP-002):** `MOD-0022 · e-Signature Service` is a Blueprint module — Blueprint 8.1 `Blueprint_Data` row 23
> (`A=MOD-0022 | F=e-Signature Service | B=1) Platform & Shared Services | C=Identity, Access & Trust | D=Controls &
> Non-repudiation`). **It has no row in `execution/registries/module-id-registry.md`** (measured: 0 hits; rows jump from
> MOD-0021-* at :88–96 to MOD-0023 at :97). The only earlier repo mention is the master-7 reconciliation
> `docs/records/audits/2026-06/blueprint-module-id-reconciliation-2026-06-03.md:35`, where registry MOD-0046 "Tenant Core UI"
> was name-matched (0.5) to "e-Signature Service" at Blueprint MOD-0022 — that is a MOD-0046 finding, **not** a collision on
> MOD-0022. The fail-closed preflight `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0022 --name
> "e-Signature Service"` **could not run** (no Python on the MVP-1 machine) → waiver proposal **W-0022-01** (§18), same
> pattern as W-0173-01 (`MOD-0173-inventory-ledger-valuation.md:307-309`). This pack writes no registry row; CT adds it on
> acceptance (proposal in §19 "Registry").
>
> **Authority:** this pack > `execution/domains/platform-shared-services/domain-config.md` > `AGENTS.md` > `.antigravity/`.
> Owner decisions **KS-0…KS-13 (2026-10-07)** are binding (§19); where the approved mockup "e-İmza Servisi v2 (offline).html"
> disagrees with them, KS-* wins and the difference is listed (§21.3). Wire authority for the new API is the **proposal**
> `docs/analysis/contracts/drafts/esign-bundle-v1-proposal.md` (ESIGN-BUNDLE v1) — it becomes
> `docs/analysis/contracts/esign.openapi.yaml` (FROZEN) only after CT accepts it, as a single-writer step in E2.
>
> **This pack builds no code** (status `draft`). It is the plan the MVP-1 slices E1…E6 execute after owner approval.

## 1. Module Summary

MOD-0022 is the **one electronic-signature service of the tenant**. Every module and every Workflow approval step that
needs a regulated sign-off calls it. A signature is: the signer's identity and the name they had at that moment, a
password re-authentication done just before signing, a tenant-defined **signature meaning**, the server time, and a
SHA-256 digest of the signed record. Optionally there is also a **visual signature** (a drawing, or a stored sample the
user drew or uploaded). It is stored **insert-only**: it is never updated or deleted, only *invalidated* by a separate
insert-only record. It can be verified at any time: the record and the image are hashed again and compared.

The legal bond is the password re-authentication plus the meaning. The visual signature is appearance only (KS-10). All
screens live in the **tenant shell** (KS-0). The code lives in Diten.Platform (SoR) and Diten.AuthService (password
ticket). "Platform Shared Services" is only the domain name.

**Why now.** MOD-0175 (quarantine release, MVP-1) needs a GxP signature (DEC-INV-08,
`docs/analysis/inventory-capability-scope-and-dependency-report.md:307` — "Workflow HARD, e-Signature CONDITIONAL").
The existing document e-signature (MOD-0029-FU23) has two gaps: re-authentication is only an opaque string the server
never verifies (`…/DocumentSignatureService.cs:163-171`), and it is wired to document subjects only
(`…/ElectronicSignatureEnums.cs:17-36`). Blueprint 8.1 has five HARD consumers of MOD-0022: `Dependencies_Normalized`
rows 224 (MOD-0112), 398 (MOD-0195), 478 (MOD-0233), 489 (MOD-0238) and 1564 (MOD-0358). DEC-INV-08 adds MOD-0175.

### Slice plan (each slice = its own Work Package, CT-verified before the next)

| Slice | Scope | Contract | Closes |
|---|---|---|---|
| **E1 — AuthService re-authentication** | `POST api/tenant-auth/esign/reauth` (password → ticket) + `POST api/tenant-auth/esign/ticket` (KS-3 window) + internal `POST internal/esign-tickets/consume`. Entities `ESignReauthTicket`, `ESignReauthWindow`. Login lockout shared (KS-13). Audit in `authAuditLogs`. | ESIGN-BUNDLE §4.1–4.3 | KS-3, KS-11 (extension point), KS-13; Part 11 §11.200(a), §11.300(d) |
| **E2 — Signature service + policy + meanings + contract** | `ESignatureRecord` / `ESignatureInvalidation` (insert-only), subject-provider registry + canonical hash, verify, by-subject list, signature requests (envelope), policy (tenant + LegalEntity override, history), meanings (7 languages), signable-action catalog, export log + PDF/CSV, manifest `esign`. Freeze `esign.openapi.yaml` v1.0.0. | ESIGN-BUNDLE §4.4–4.16, 4.20–4.27 | KS-1, KS-2, KS-4, KS-5, KS-6, KS-8 (server rules), KS-10, KS-12 (data) |
| **E3 — Signature samples ("İmzam")** | `ESignatureSample` + `ESignatureSampleEvent` (insert-only), PNG/vector storage via `IContentStorageGateway` (new scope `ESignatureImages`), manager approval, no self-approval, used sample not removable. | ESIGN-BUNDLE §4.17–4.19 | KS-7, KS-9, KS-10 |
| **E4 — Workflow (MOD-0023) hook** | Step attribute `requirements.signatureRequired` + `signatureMeaning` in `DefinitionJson`, carried through `WorkflowRuntimeStep` → `ApprovalTask`. Additive `SignatureId` on `ApproveWorkflowTaskRequest` and on `WorkItemActionPayloadDto`. Gate in `WorkflowTaskTransitionSupport.TransitionAsync`. Designer switch + meaning select. WorkCenter opens the signature dialog. | ESIGN-BUNDLE §5 (Workflow addendum) | approve without a valid signature refused — API **and** WorkCenter |
| **E5 — Web (tenant) screens + shared dialog** | `_ESignatureDialog` partial + `esign-dialog.js` + signature pad, İmzam, İmza Talepleri, Doğrulama, Denetim Dışa Aktarımı, Politika, İmza Anlamları, İmza Örneği Onayları (§11). None on `/Platform/`. | — | mockup v2 1:1 (§21) |
| **E6 — FU23 migration** | Document Management signs through MOD-0022. FU23 records stay readable as they are; enum mapping (§8). | additive only | FU23 gaps 4 and 6 (audit `…fu23…2026-07-21.md:151-160`) |
| **INT-ESIGN** | Gateway route `/api/v1/esign/{everything}` → 5057 + route coverage test (integration-agent). Re-auth uses the existing `/api/tenant-auth/{everything}` route (`ocelot.json:1870-1888`), so it needs no change. | — | external consumers (MOD-0175 in InventoryService) |
| **L10n** | 7 languages (en, tr, fr, es, zh, ar, ru) for every key of §11.6 + workflow designer additions (`Resources/Views/Platform/Workflow/WorkflowIndex.*.resx` already has 7 files). | — | KS-12 |

Order: **E1 → E2 → INT-ESIGN → E3 → E4 → E5 → L10n**. L10n may run per file alongside E5. E6 runs after MOD-0175 (CT-D3).
MOD-0175 can start its own pack after E2's contract is frozen. Its signing UI needs E5's dialog.

## 2. Ownership and Boundaries

**Owns (single writer):**
- Electronic signature records of the tenant: signer, name at signing, meaning text at signing, server time, subject
  digest, image digest, signature digest. Invalidations of those records.
- Signature requests (envelopes): who is asked, by whom, which meaning, due date, rejection.
- e-Signature policy: tenant default + per-LegalEntity override + change history (KS-2, KS-5, KS-8).
- Signature meanings: tenant-scoped, 7-language display names (KS-12).
- Signature samples (İmzam) and their approval decisions (KS-7).
- The signable-action catalog, i.e. the policy rows that consumer modules register.
- Export log (KS-6).
- In Diten.AuthService: re-authentication tickets and windows (`esign` purpose only).
- Permission namespace **`esign.*`** (proposed, PKS-001 §4 `.antigravity/rules/permission-key-standard.md:90-105`;
  measured: no `esign.` key exists — the only signature keys are `platform.document-management.signatures.*`, owned by
  MOD-0029-FU23/FU29, `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs:797-803`).

**Consumes:**
- MOD-0018 AuthService: users, `IPasswordHasher` (BCrypt), lockout fields, tenant login settings.
- MOD-0021 Audit Trail (`AuditBehavior`, `IAuditableCommand`, `audit_events`).
- MOD-0023 Workflow (step definition, approve transition).
- MOD-0262/0029 content storage (`IContentStorageGateway`).
- MDM LegalEntity reference (`ILegalEntityReferenceValidator`).
- SEC-002 data scope (`OrgDataScopeResolver`).
- MOD-0013 premium modal standard.

**Does not own / must not do:**
- Does not own the signed business record. The consumer module owns it and exposes it through a **signable-subject
  provider** (§8). MOD-0022 never writes into a consumer's record.
- Does not decide *who may* sign a business decision. The consumer does (Workflow assignment, request nomination,
  MOD-0175 four-eyes). MOD-0022 decides *how* a signature is made and proven.
- No second factor in v1 (KS-11). There is an extension point only.
- No external provider (DocuSign/Adobe, Blueprint MOD-0264 HARD at `Dependencies_Normalized` row 45) and no qualified
  signature in v1.
- No new file-storage infrastructure. It reuses `IContentStorageGateway`.
- No signing on a user's behalf with a service key. A signature is made only with the signer's own tenant token plus
  their own ticket.
- Does not change the Workflow designer's shell, route or scope (measured in §8; any change is MOD-0023's business).
- Does not edit the FU23 or MOD-0023 packs. E4 and E6 code changes run under this pack, and the CT records them on those
  modules.

## 3. Owned Objects

| Object | Kind | Notes |
|---|---|---|
| `ESignatureRecord` | Platform entity, **insert-only** | collection `esign_signature_records` |
| `ESignatureInvalidation` | Platform entity, insert-only | collection `esign_signature_invalidations`; at most one per signature (unique index) |
| `ESignatureRequest` | Platform entity | collection `esign_signature_requests`; state Waiting → Signed / Rejected / Expired / Cancelled |
| `ESignaturePolicy` | Platform entity | collection `esign_policies`; one tenant default + 0..n LegalEntity overrides; optimistic `Version` |
| `ESignaturePolicyChange` | Platform entity, insert-only | collection `esign_policy_changes` (history, reason ≥ 10 chars) |
| `ESignatureMeaning` | Platform entity | collection `esign_meanings`; `Code` immutable once used |
| `ESignableAction` | Platform entity | collection `esign_signable_actions` (catalog of policy rows registered by consumers) |
| `ESignatureSample` | Platform entity, insert-only | collection `esign_signature_samples` |
| `ESignatureSampleEvent` | Platform entity, insert-only | collection `esign_signature_sample_events`; status = last event |
| `ESignatureExport` | Platform entity, insert-only | collection `esign_exports` |
| `ESignReauthTicket` | AuthService entity | collection `esignReauthTickets`; TTL; same pattern as `MfaChallenge` |
| `ESignReauthWindow` | AuthService entity | collection `esignReauthWindows`; TTL; KS-3 "süre içinde bir kez" |
| `ISignableSubjectProvider` / `SignableSubjectRegistry` | Platform domain service | in-process providers + remote providers (operator config, WC-D1 Y2 style) |
| `ESignatureDigest` | Platform domain service | canonical JSON + SHA-256 (§8) |
| `IESignatureGate` | Platform application service | used in-process by Workflow E4 (`ValidateForApprovalAsync`) |
| `ESignatureController` (+ requests / samples / policy / meanings / exports controllers) | Platform API | `api/v1/esign/**` |
| `ESignReauthController` actions | AuthService API | on `TenantAuthController` route `api/tenant-auth/esign/*` |
| `InternalESignTicketsController` | AuthService API | `internal/esign-tickets/consume` (X-Internal-Api-Key) |
| `ESignatureExpirySweeper` | Platform Hangfire job | requests past `DueAtUtc` → Expired |
| `ESignManifestProvider` | Platform self-registration | `ModuleCode: "esign"` |
| `ESignatureController` (Web) | MVC | `[Route("ESignature")]` + gateway proxy |
| `_ESignatureDialog.cshtml` + `esign-dialog.js` + `esign-pad.js` | shared partial / JS | used by every tenant page that signs |
| Permissions | `esign.*` | §14 |

## 4. Entity Fields

**Entity base decision.** Platform entities derive from `TenantScopedEntity`
(`services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/EntityVariations.cs:8-11`: `required Guid TenantId
{get; init;}`), which itself derives from `BaseEntity` (`…/BaseEntity.cs:3-14`: `Id`, `CreatedAt`, `CreatedBy`,
`UpdatedAt?`, `UpdatedBy?`, `IsDeleted`, `Version`). **Insert-only enforcement:** `TenantRepository<T>` inherits a public
soft-delete `RepositoryBase.DeleteAsync` (`…/Persistence/Repositories.cs:22-46`), and the FU23 record repository still
exposes `UpdateAsync` via `ReplaceOneAsync` (`…/DocumentManagementElectronicSignatureRepositories.cs:107-114`). So
"append-only" there is a convention only. For MOD-0022 the insert-only repositories **do not derive from
`TenantRepository<T>`**. Their interface has `InsertAsync` + reads only, like the precedent `ITaskTransitionRepository`
(`…/Domain/Repositories/ITaskRepositories.cs:82-92`: "no update and no delete, and that is the whole interface"). Domain
properties are `init`-only and validated on append, like `AuditEvent.ValidateAppend()` (`…/Entities/Audit/AuditEvent.cs:47-123`).
An architecture test pins this (§17).

### ESignatureRecord (insert-only)

| Field | Type | Rule |
|---|---|---|
| `SignatureNumber` | string | `ESIG-{yyyy}-{nnnnnn}` per tenant, unique (mockup `ESIG-2026-004790`) |
| `Module` | string | consumer module code, e.g. `inventory`, `document-management`, `workflow` |
| `SubjectType` | string | e.g. `inventory.quarantine-decision` |
| `SubjectId` | string | consumer's id |
| `SubjectDisplay` | string ≤ 300 | "Ne imzalıyorsunuz" title at signing (snapshot) |
| `SubjectSummary` | list<{label,value}> ≤ 20 | snapshot of the summary shown in the dialog |
| `LegalEntityId` | Guid? | company of the signed object (KS-2); from the provider, never from the client |
| `ActionCode` | string? | signable action (policy row), e.g. `inventory.quarantine.release` |
| `Context` | map<string,string> | e.g. `workflowInstanceId`, `workflowTaskId`, `stepCode`, `decision`; part of the digest |
| `MeaningCode` | string | active meaning of the tenant |
| `MeaningTextAtSign` / `MeaningLanguage` | string / string | display name in the signer's UI language at signing (mockup: "mevcut imzalar imza anındaki metni korur") |
| `SignerUserId` | string | JWT `sub` of the caller (never from the body) |
| `SignerLogin` / `SignerNameAtSign` | string / string | snapshot (§11.50(a)(1)) |
| `SignedAtUtc` | DateTime | server clock only |
| `Note` | string? ≤ 500 | "İmza notu" |
| `SubjectHashAlgorithm` / `SubjectHash` | string / hex | `CanonicalJsonSha256` / SHA-256 of the canonical subject (§8) |
| `ReauthKind` | enum | `Password` (password entered for this ticket) · `Window` (KS-3) |
| `PasswordVerifiedAtUtc` | DateTime | from the consumed ticket |
| `TicketRef` | string | HMAC hash of the ticket id (never the ticket itself) |
| `SecondFactor` | enum | `None` (v1 always; KS-11 extension point) |
| `AppearanceType` | enum | `None` (name only) · `Drawn` · `Sample` |
| `AppearanceSampleId` | Guid? | when `Sample` |
| `AppearanceContentId` | string? | `ContentStoreResult.ContentId` of the PNG |
| `AppearanceVectorJson` | string? | strokes when drawn and storage = both/vector (KS-9) |
| `AppearanceImageHash` | hex? | SHA-256 of the PNG bytes |
| `RequestId` | Guid? | `ESignatureRequest` this signature fulfils |
| `PolicySnapshot` | {scope, legalEntityId?, policyVersion, visualMode, reauthEvery, reauthMinutes} | the policy that applied |
| `SignatureDigest` | hex | SHA-256 over the canonical record (§8) — the "doğrulama özeti" |
| `IdempotencyKey` | string | unique per (TenantId, SignerUserId) |
| `CorrelationId` | string | |

Indexes: unique `(TenantId, SignatureNumber)`; unique `(TenantId, SignerUserId, IdempotencyKey)`;
`(TenantId, Module, SubjectType, SubjectId, SignedAtUtc desc)`; `(TenantId, SignedAtUtc desc)` (export);
`(TenantId, SignerUserId, SignedAtUtc desc)`; `(TenantId, RequestId)`; unique partial `(TenantId, Context.workflowTaskId)`
where present (one signature binds one task — E4).

### Other entities

- **ESignatureInvalidation** (insert-only): `SignatureId`, `Reason` (≥ 10), `InvalidatedBy`, `InvalidatedByName`,
  `InvalidatedAtUtc`, `TicketRef`. Unique `(TenantId, SignatureId)`.
- **ESignatureRequest:** `RequestNumber` `SR-{yyyy}-{nnnn}`, `Module`, `SubjectType`, `SubjectId`, `SubjectDisplay`,
  `LegalEntityId?`, `ActionCode?`, `MeaningCode`, `RequestedBy`, `RequestedByName`, `RequestedAtUtc`, `SignerUserId`
  (one signer per request, mockup), `DueAtUtc`, `Status` (`Waiting|Signed|Rejected|Expired|Cancelled`), `SignatureId?`,
  `RejectReason?`, `DecidedAtUtc?`, `IdempotencyKey`. State changes go through guarded methods only, and every change is
  an audit event.
- **ESignaturePolicy:** `Scope` (`Tenant|LegalEntity`), `LegalEntityId?`, `Rows[]` {`ActionCode`, `Required` bool,
  `MeaningCode`, `VisualMode` (`None|Optional|SavedAccepted|DrawRequired`), `SecondFactor` (`NotAvailable`)},
  `ReauthEvery` (bool, default **true** — KS-3), `ReauthWindowMinutes` (1–60, default 15),
  `SampleApprovalRequired` (bool, default **true** — KS-7; tenant scope only, CT-D15), `Version`.
- **ESignaturePolicyChange** (insert-only): `Scope`, `LegalEntityId?`, `Changes[]` (human-readable lines, mockup
  `diffs()`), `Before`/`After` JSON, `Reason`, `ChangedBy`, `ChangedAtUtc`.
- **ESignatureMeaning:** `Code` (`^[A-Z][A-Z0-9_]{1,31}$`), `Names` {en,tr,fr,es,zh,ar,ru} all required (KS-12),
  `Description`, `IsActive`, `FirstUsedAtUtc?` (non-null → code and deletion locked). Seeded per tenant: `APPROVE`,
  `REVIEW`, `RELEASE`, `AUTHOR` (mockup `MEANINGS`).
- **ESignableAction:** `ActionCode` (PKS-like lowercase dotted, e.g. `inventory.quarantine.release`), `Module`, `Names`
  (7 languages), `DefaultRequired`, `DefaultMeaningCode`, `DefaultVisualMode`, `RegisteredAtUtc`. These are registered by
  consumer modules (CT-D12).
- **ESignatureSample** (insert-only): `SampleNumber` `IMZ-{yyyy}-{nnnn}`, `OwnerUserId`, `OwnerName`, `Kind`
  (`Drawn|Uploaded`), `Format` (`Png|PngAndVector|Vector`), `ContentId`, `ImageHash`, `VectorJson?`, `CreatedAtUtc`,
  `TicketRef`.
- **ESignatureSampleEvent** (insert-only): `SampleId`, `Event` (`Submitted|Approved|Rejected|Activated|Deactivated|Withdrawn`),
  `By`, `AtUtc`, `Reason?`. Current status = projection of the last event (`Pending|Active|Inactive|Rejected|Withdrawn`).
  At most one `Active` per owner (activation deactivates the previous one in the same transaction).
- **ESignatureExport** (insert-only): `ExportNumber` `EXP-{yyyy}-{nnnn}`, `Format` (`Pdf|Csv`), `Filter` (JSON + display
  text), `RecordCount`, `By`, `AtUtc`, `FileHash`.
- **ESignReauthTicket** (AuthService, `EntityBase`): `TicketIdHash` (HMAC-SHA256 with a new `ESign:HashSecret`, unique),
  `UserId`, `TenantId`, `Purpose` = `"esign"`, `Intent` (`sign|sample|invalidate`), `Kind` (`Password|Window`),
  `PasswordVerifiedAtUtc`, `ExpiresAtUtc`, `ConsumedAtUtc?`, `CreatedIp`, `UserAgentHash`. TTL index on `ExpiresAtUtc`.
- **ESignReauthWindow** (AuthService): `UserId`, `TenantId`, `PasswordVerifiedAtUtc`, `ExpiresAtUtc` (≤ 60 min),
  `UserAgentHash`, `RevokedAtUtc?`. Unique `(TenantId, UserId)` (newest wins). TTL index.

**Form field count (§6 golden reference rule):** İmza Penceresi 5 user-filled fields (password, meaning, note,
appearance choice, drawing; the user name is read-only and not counted). Meaning editor: code + 7 names + description +
active = **10** (frontmatter `form_field_count`). Policy is a table-form. İmzam is a single-record page.
`golden_reference: slim` follows the prompt ("listeler slim") and the mockup's modal editor. The meaning editor's 10
fields are above the slim limit of 8 (AGENTS.md §6). The pack proposes an explicit exception: the 7 names are **one
localized field group**, edited in a modal (§19 CT-D, ASSUMPTION-0022-03).

## 5. Repo Scope

- E1: `services/Diten.AuthService/src/**`: new feature folder `Features/ESignReauth/**`, `TenantAuthController.cs`
  (new actions only), new `InternalESignTicketsController.cs`, new entities + repositories + index config, options
  section `ESign`. Tests are under `services/Diten.AuthService/tests/**`.
- E2/E3: `services/Diten.Platform/src/**`: new feature folder `Features/ESignature/**`, new controllers under
  `Diten.Platform.API/Controllers/ESignature/**`, new collections in `PlatformCollections.cs` + schema manifest
  partial `PlatformSchemaManifest.ESignature.cs`, additive enum value `ContentStorageScope.ESignatureImages = 4`
  (`…/IContentStorageGateway.cs:65-80`, "Additive only" by its own comment). Self-registration
  `Features/ESignature/SelfRegistration/ESignManifestProvider.cs`. Tests are under `services/Diten.Platform/tests/**`.
  Contract file `docs/analysis/contracts/esign.openapi.yaml` (E2, single writer, after CT acceptance) + row in
  `docs/analysis/contracts/README.md`.
- E4: `…/Features/Workflow/WorkflowModels.cs` (`ApproveWorkflowTaskRequest` trailing parameter),
  `…/Handlers/CommandHandlers/WorkflowDefinitionRuntimePlan.cs`, `WorkflowTaskTransitionSupport.cs`,
  `ApprovalTask` entity (trailing fields), `WorkflowTransitionLog` (trailing `SignatureId`),
  `…/WorkAggregation/Dispatch/WorkItemActionDispatchModels.cs` (`WorkItemActionPayloadDto` trailing field),
  `WorkflowApprovalWorkItemActionDispatcher.cs`, the workflow work-item provider (exposes `signatureRequired`),
  `frontend/Diten.Web/Views/Platform/Workflow/Designer.cshtml`, `VisualDesigner.cshtml`,
  `wwwroot/assets/js/Platform/Workflow/designer-page.js`, `workflow.bpmn-designer.js`,
  `Resources/Views/Platform/Workflow/WorkflowIndex.*.resx` (7), `wwwroot/assets/js/WorkCenterNext/app.js` (dialog before
  approve). The designer stays on `/Platform/Workflow` — its route is **not** changed (§8 measurement).
- E5: `frontend/Diten.Web/Controllers/ESignatureController.cs`, `Views/ESignature/**`, `Views/Shared/ESignature/**`,
  `wwwroot/assets/js/ESignature/**`, `Resources/Views/ESignature/**` (7 languages), `Resources/SharedResource.*.resx`
  (nav keys only), `Views/Shared/_LayoutTenantShell.cshtml` (user menu "İmzam" link only).
- E6: `…/Features/DocumentManagementElectronicSignature/**` (adapter to MOD-0022), the Document Management signable
  subject provider.
- INT-ESIGN: `gateway/Diten.ApiGateway/ocelot.json` + gateway tests — **integration-agent only** (AGENTS.md §4).
- This WP (P0): only the two new files of this pack and the contract proposal.

## 6. Protected Paths

- `.antigravity/**`; `AGENTS.md`; `CLAUDE.md`; `execution/registries/**` (CT adds the row); DCP files.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN); `Controllers/Archive/**`; `Views/Archive/**`.
- `_LayoutPlatformAdmin.cshtml` — no e-signature entry there (KS-0).
- `docs/analysis/contracts/*.openapi.yaml` other than the new `esign.openapi.yaml`; every frozen contract.
- MOD-0023 and MOD-0029-FU* pack files (E4/E6 change code under this pack; the CT records them on those modules).
- `ModulePageDescriptorNormalizer.ScopeFromRoute` and its tests — used, never changed.
- `ShellAccessFilter.IsTenantScopedPlatformPath` — no new `/Platform` exception (`frontend/Diten.Web/Filters/ShellAccessFilter.cs:142-145`).
- Other domains' services (InventoryService is MOD-0175's; MOD-0175 calls MOD-0022 over HTTP).
- The FU23 controller and its 16 endpoints stay as they are until E6. E6 changes are additive only.

## 7. Dependencies

| Dependency | State (measured at 6326354e2) | Effect |
|---|---|---|
| AuthService login + BCrypt | present: `IPasswordHasher.Verify` (`SRC/…/Common/Interfaces/IPasswordHasher.cs:3-7`, impl `PasswordHasher.cs:7-17`, work factor 12) | E1 reuses it; adds no new hashing |
| Tenant login settings (lockout) | present in Platform: `TenantLoginSettings.cs:25-26` (`MaxFailedLoginAttempts = 5`, `LockoutDurationMinutes = 15`), validated 1–20 / 1–1440 (`TenantLoginSettingsValidators.cs:26-27`); AuthService reads them via `PlatformTenantLoginSettingsClient.cs:45-46` | KS-13: the same numbers drive re-auth lockout |
| MFA challenge pattern | present: `MfaChallenge.cs:31-69`, `MfaChallengeService.cs:39-135` (HMAC-hashed ids, `FixedTimeEquals`), TTL index `MongoDbIndexConfigurations.cs:110-119` | template for `ESignReauthTicket`; its non-atomic consume (`:113-114`) is **not** copied |
| MOD-0021 audit | present: `AuditBehavior` (`…/Contracts/Behaviors/AuditBehavior.cs:14`), `IAuditableCommand` (`…/Contracts/Audit/IAuditableCommand.cs:3`), `audit_events` (`AuditCollectionNames.cs:5`); no hash chaining | every command audited; MOD-0022 adds its own digest per signature |
| MOD-0023 workflow | present: approve `WorkflowDefinitionsController.cs:193-202`; DTO `WorkflowModels.cs:325-330`; SoD `WorkflowTaskTransitionSupport.cs:118-127` | E4 additive |
| WorkCenter bridge | present: in-process `WorkflowApprovalWorkItemActionDispatcher.cs:89-94` sends the same `ApproveWorkflowTaskCommand` | the gate in `TransitionAsync` covers both paths |
| Content storage | present: `IContentStorageGateway` (`…/Contracts/DocumentRepository/IContentStorageGateway.cs:33-59`), only implementation local disk (`LocalFileSystemContentStorageGateway.cs:30`), default root `%TEMP%/DitenStorage/Documents` (`:277-286`) | E3 reuses it (CT-D4) |
| Image library | **absent** in Diten.Platform (no ImageSharp / SkiaSharp / System.Drawing in any csproj) | PNG conversion client-side; server validates bytes (§8) |
| PDF library | absent in Platform; `PDFsharp-MigraDoc 6.1.1` in `services/Diten.CrmService/src/Diten.CrmService.Infrastructure/Diten.CrmService.Infrastructure.csproj:13` | KS-6 PDF: CT-D11 |
| LegalEntity | MDM-owned (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:8`); Platform validates via `ILegalEntityReferenceValidator.cs:5`; data scope `OrgDataScopeResolver.cs:10-25`; only the first LegalEntity is hydrated into `JwtTenantAuthorizationContext.cs:71,119-129` | KS-2 visibility (§8) |
| ESIGN-BUNDLE | proposal only | frozen in E2 |
| MOD-0175 | no pack (`execution/domains/supply-chain-execution/module-packs/` holds 0173, 0173-FU01/FU02, 0174 only); registry row reserved (`module-id-registry.md:273`) | first consumer; follows E2 |

**Blueprint dependencies, interpreted.** `Dependencies_Normalized` rows 42–45 list MOD-0022 → MOD-0021, MOD-0029,
MOD-0005 and MOD-0264, all HARD.
- **MOD-0021:** kept; audit is used.
- **MOD-0029** ("Controlled Documents (SOPs/Work Instructions)" in Blueprint row 30): in this repo the direction is
  **reversed**. Document management *uses* e-signature (FU23 → E6). We read the Blueprint edge as "the signature
  procedures and meaning definitions are controlled documents" — an organisational dependency, not a build dependency.
  MOD-0022 builds without it.
- **MOD-0005** (Policy & Control Library, Blueprint row 6): soft for v1. The e-signature policy is self-contained (KS-5).
  A link to the control library is a follow-up.
- **MOD-0264** (e-Sign Vendor, external): optional and out of v1. Note that Blueprint gives MOD-0264 the same SoR,
  "Signature envelopes" (Blueprint_Data row 265, column M), while `SoR_Map` row 107 reports collision count 0. The
  external envelope is a reference only; the SoR stays MOD-0022 (§19 measured facts).
- **Wave mismatch:** `Blueprint_Data` row 23 G=W-2; `Module Pages` rows 66–68 F=W-4.

## 8. Runtime Constraints

**Service placement.** The SoR and API are in Diten.Platform (5057). The password ticket is in Diten.AuthService (5056).
UI is in Diten.Web (5001) in the tenant shell. Every call from the browser goes through the Gateway (5000) with the
**user's tenant token** (AGENTS.md §3 rule).

**Scope.** All `esign.*` keys are Tenant scope. This follows from two measured rules:
- Every page route is outside `/Platform` (`ModulePageDescriptorNormalizer.cs:37-43`: `/Platform/…` → PlatformAdmin,
  anything else → Tenant).
- The first key segment is not in `PlatformAdminModules = {"platform","reference-data"}`
  (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:25-26,44-45`).

So `esign.*` keys classify as Tenant. A tenant admin can grant them to custom roles. They are not refused like
PlatformAdmin keys at `AssignPermissionCommandHandler.cs:57-61`.

**E1 — Re-authentication in AuthService, tenant-auth family (KS-13, NE-1).**
- *Why tenant-auth and not platform-auth.* The signer is a `tenant_user`. Tenant and platform tokens share issuer,
  audience and key, and differ only by `actor_type` (`TokenService.cs:32-53` vs `:108-135`; `appsettings.json:47-48`).
  - `api/platform-auth/*` hard-codes `PlatformTenantId …0001` (`PlatformAuthController.cs:18`).
  - Platform login has **no lockout and no audit** (`PlatformLoginCommandHandler.cs:50-139`).
  - The gateway lets only platform/partner actors through `/api/platform` and `/api/admin`
    (`gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs:151-167`).
  - `api/tenant-auth/*` already has an authenticated action that reads `sub` + `tenant_id` from the tenant JWT:
    `change-password/forced` (`TenantAuthController.cs:67,71-72`). The gateway routes `/api/tenant-auth/{everything}` to
    5056 (`ocelot.json:1870-1888`) and requires `actor_type=tenant_user` on tenant paths
    (`TenantResolutionMiddleware.cs:202-209`).
  - So **re-authentication in the tenant shell is possible** without a new gateway route.
- `POST api/tenant-auth/esign/reauth` (`[Authorize]`, `actor_type` must be `tenant_user`, else 403):
  1. User = JWT `sub`, tenant = JWT `tenant_id`. **The body has no user name**: the dialog shows it read-only from the
     session.
  2. Load the user and the tenant login settings (same call as login, `LoginCommandHandler.cs:67`).
  3. **Lockout first**, as login does (`LoginCommandHandler.cs:84,124-133`). If locked: 423 `ESIGN_ACCOUNT_LOCKED`
     with `lockoutEndUtc`; audit `tenant_esign_reauth_blocked_by_lockout`.
  4. `IPasswordHasher.Verify`. On failure: `user.RecordLoginFailure(settings.MaxFailedLoginAttempts,
     settings.LockoutDurationMinutes)` — **the same counter as login** (`User.cs:131-138`); audit
     `tenant_esign_reauth_password_failed` (+ `tenant_esign_reauth_lockout_started`); 401 `ESIGN_REAUTH_FAILED` with
     `remainingAttempts` (CT-D13).
  5. On success: `RecordLoginSuccess` (`User.cs:119-124`); create `ESignReauthTicket` (Kind `Password`, `Intent` from the
     body, lifetime CT-D1); if the body asks `openWindowMinutes` (1–60, given by the dialog from the effective policy),
     upsert `ESignReauthWindow`; audit `tenant_esign_reauth_succeeded` (metadata: intent, window minutes — never the
     password).
  6. Return `{ ticket, expiresAtUtc, passwordVerifiedAtUtc, windowExpiresAtUtc? }`. **No access token, no refresh token,
     no `SessionId`**: `refreshTokens` is untouched, so it opens no session and revokes none.
- `POST api/tenant-auth/esign/ticket` (KS-3 window; `[Authorize]`, tenant_user): if an unrevoked `ESignReauthWindow` for
  (tenant, user, user-agent hash) has not expired, it issues a ticket with Kind `Window` that carries the window's
  `PasswordVerifiedAtUtc`. Otherwise 409 `ESIGN_REAUTH_WINDOW_EXPIRED`, and the dialog asks for the password. Intent
  `sample` and `invalidate` are **refused** here (password every time, mockup "bu seçenekte şifre her zaman istenir").
- Window revocation: password change, forced change and logout set `RevokedAtUtc`. They hook into
  `ChangePasswordCommandHandler.cs:54` (where refresh tokens are revoked) and `LogoutCommandHandler.cs:55-58`.
  A window never outlives 60 minutes.
- `POST internal/esign-tickets/consume` (S2S, `X-Internal-Api-Key`, `InternalEventAuthService.cs:55-63`; not routed
  by the gateway): `{ ticket, tenantId, userId, intent }`.
  - It does an **atomic** `FindOneAndUpdate` with filter `TicketIdHash == h && TenantId && UserId && Intent && ConsumedAtUtc
    == null && ExpiresAtUtc > now`, update `ConsumedAtUtc = now`.
  - Any miss returns 409 `ESIGN_TICKET_INVALID`. One code, no oracle on which part failed.
  - Success returns `{ kind, passwordVerifiedAtUtc, issuedAtUtc }`.
  - The atomic filter is what MFA lacks (`MfaChallengeService.cs:113-114`, replace without a `ConsumedAtUtc` filter).
- Ticket mechanism chosen: **opaque, AuthService-held, consumed over the internal API** (CT-D9). The alternative, a
  signed JWT verified locally, would reuse the access-token key and issuer and needs its own audience plus a jti store
  in Platform. It is rejected unless the CT prefers it.
- **Never logged / stored:** the password. The ticket is stored only as an HMAC hash, and the image as content plus its
  hash.
  - Request-body logging must exclude the `esign/reauth` route. A log-capture test pins this (§17).
  - The dialog field uses `autocomplete="current-password"`, is cleared after every attempt (mockup `pw: ''`), and is
    never put in `sessionStorage`.

**KS-3 window — how it is carried.**
- The policy owns the rule (`ReauthEvery`, `ReauthWindowMinutes`). AuthService owns the proof (`PasswordVerifiedAtUtc`
  in the ticket).
- At sign time Platform consumes the ticket and checks:
  - `ReauthEvery = true` ⇒ `Kind == Password`.
  - Otherwise `now − PasswordVerifiedAtUtc ≤ ReauthWindowMinutes`.
  - Appearance `Sample` ⇒ `Kind == Password` always (mockup `need = needPw() || app === 'saved'`).
- So a stale window, or a policy changed to "every signature", is enforced by Platform even if the browser keeps a
  window open. A Part 11 note on window mode is in CT-D7.

**E2 — Signing (Platform), server order** (`POST api/v1/esign/signatures`):
1. Tenant + authenticated `tenant_user`, else 401/403. Idempotency replay (`TenantId, SignerUserId, IdempotencyKey`)
   returns the stored record, 200, **before** the ticket is consumed.
2. Resolve the subject through `SignableSubjectRegistry` with the caller's token. If unknown or invisible: 404
   `ESIGN_SUBJECT_NOT_FOUND`. This is also the cross-tenant guard and the visibility guard of KS-2. LegalEntity comes
   from the provider.
3. Effective policy for `(ActionCode, LegalEntityId)`: the LegalEntity override if present, else the tenant default
   (KS-5).
   - The meaning must be active. If the policy row fixes a meaning, the body meaning must equal it: 409
     `ESIGN_MEANING_NOT_ALLOWED`.
   - If a second factor were required: 501 `ESIGN_SECOND_FACTOR_NOT_AVAILABLE` (unreachable in v1, the policy cannot
     enable it).
4. Appearance against `VisualMode` (KS-8):
   - `None` ⇒ no image.
   - `Optional` ⇒ any of the three.
   - `SavedAccepted` ⇒ `Sample` or `Drawn`.
   - `DrawRequired` ⇒ `Drawn` only.
   - Violation: 409 `ESIGN_APPEARANCE_NOT_ALLOWED`.
   - `Sample` must be the caller's own **Active** sample: 409 `ESIGN_SAMPLE_NOT_ACTIVE`.
   - `Drawn` PNG is validated (below).
5. Request (optional): must be `Waiting`, and the signer must be its `SignerUserId`. Otherwise 409
   `ESIGN_REQUEST_NOT_OPEN` or 403 `ESIGN_SIGNER_NOT_NOMINATED`.
6. **Consume the ticket** (internal call, intent `sign`). Check Kind against the policy (KS-3 above), else 403
   `ESIGN_REAUTH_REQUIRED`. A ticket is consumed even if a later step fails. That is safe: the user signs again with a new
   password entry.
7. Store the drawn PNG through `IContentStorageGateway` (scope `ESignatureImages`). Compute `SubjectHash`,
   `AppearanceImageHash` and `SignatureDigest`. Insert the record. Mark the request `Signed`. All in one Mongo
   transaction where the store supports it; the image is written first and orphan cleanup is a follow-up.
8. Audit `esign.signature.signed` (MOD-0021, `IAuditableCommand`). Return 201.

**Canonical subject and digests (CT-D2).**
- `SubjectHash` = SHA-256 (lowercase hex) of UTF-8 canonical JSON
  `{"module":…,"subjectType":…,"subjectId":…,"fields":{…keys sorted ordinal…}}`.
- The rules are the FU23 canonical form, extended with `module`: hand-built JSON, ordinal key sort, `null` literal,
  escapes `\ " \n \r \t`, dates UTC ISO-8601 "O", bools `true`/`false`
  (`…/DocumentSignableSubjectResolver.cs:399-442`).
- The `fields` come from the **owning module's provider**: governance fields of the record, never binary content.
- `SignatureDigest` = SHA-256 of canonical JSON over `{signatureNumber, tenantId, module, subjectType, subjectId,
  subjectHash, legalEntityId, actionCode, context, meaningCode, meaningTextAtSign, signerUserId, signerNameAtSign,
  signedAtUtc, reauthKind, passwordVerifiedAtUtc, appearanceType, appearanceSampleId, appearanceImageHash, requestId}`.
  - It includes the image hash (KS-10: "Görüntü (veya hash'i) imza kaydında, değiştirilemez, doğrulama özetine dahil").
- Workflow (E4):
  - The subject is the workflow instance's business object (`objectType` / `objectId`, as CRM starts it,
    `execution/domains/commercial-suite/work-packs/WP-CL-BE-3-workflow-cross-service.md:91-112`).
  - `Context` = `{workflowInstanceId, workflowTaskId, stepCode, decision:"approve"}`.
  - If the object type has no provider, the subject falls back to `module = workflow`, `subjectType = workflow-task`,
    with fields = instance `displayContext` + step code (ASSUMPTION-0022-05).

**Signable-subject providers.**
- In-process: for Platform modules (workflow, document-management).
- Remote: for modules in other services (MOD-0175 → InventoryService) as a config row with `BaseUrl` from operator
  config, never from a manifest. This is the WC-D1 Y2 rule (`.antigravity/rules/workcenter-bridge-standard.md:50-80`),
  with the same "one generic HTTP provider, no per-module class" rule (Y1 :33-48).
- The remote call forwards the user's JWT. A 404 from the owner means "not visible" (KS-2).
- Contract: `GET {base}/esign-subjects/{subjectType}/{subjectId}` → `{display, summary[], legalEntityId, fields{}}`
  (ESIGN-BUNDLE §6).

**Verification** (`POST api/v1/esign/signatures/{id}/verify`). It re-resolves the subject and recomputes `SubjectHash`.
It reloads the PNG bytes and recomputes `AppearanceImageHash`, and recomputes `SignatureDigest`. The result is:
- `Valid`: all three match and no invalidation.
- `Changed`: subject hash differs (mockup "Geçersiz — kayıt imzadan sonra değişti").
- `Invalidated`: an invalidation exists.
- `Tampered`: image or digest differs.
- `SubjectUnresolvable`: the subject cannot be resolved.

The record is **not** updated, because the result is computed. Each run is an audit event `esign.signature.verified`.
Unlike FU23, there is no `RequiresResign` write-back (`DocumentSignatureVerificationService.cs:65-110`).

**Invalidation.** Needs `esign.signatures.invalidate`, a reason of at least 10 characters, and a ticket (intent
`invalidate`, Kind `Password`). It inserts one `ESignatureInvalidation`. A second call returns 409
`ESIGN_ALREADY_INVALIDATED`. The signature is never deleted (mockup "İmza silinmedi, durumu değişti").

**Visibility and company (KS-2).**
- Reads by subject go through the provider (visible subject or 404).
- List/export screens (requests, export) filter by the caller's SEC-002 LegalEntity data scope (`OrgDataScopeResolver`).
  Without a scope, the caller sees only their own signatures and requests (fail-closed, CT-D).
- Measured limit: only the first LegalEntity is hydrated into the authorization context
  (`JwtTenantAuthorizationContext.cs:71,119-129`), and Document Management applies no LegalEntity filter today
  (backlog BL-432, `docs/roadmap/backlog/product-backlog.md:5946-5966`).

**Images (KS-9).**
- Platform has no image library (§7). The browser produces the PNG: a canvas for drawings; for uploads, PNG/JPG is
  converted to PNG via canvas, with crop and background cleanup as in mockup `processUp()`.
- The server checks:
  - the PNG magic bytes `89 50 4E 47 0D 0A 1A 0A`, else 415 `ESIGN_IMAGE_NOT_PNG`;
  - the IHDR width/height (≤ 2000 × 800);
  - size ≤ 1 048 576 bytes, else 413 `ESIGN_IMAGE_TOO_LARGE`.
  These are byte-level checks, no decoding library.
- Vector = stroke JSON (normalized 0..1 points, mockup `pad.strokes`), ≤ 256 KB.
- `ContentStoreRequest` requires `CompanyId` (`IContentStorageGateway.cs:86-96`): signature images use the signed
  object's LegalEntity, and samples use `Guid.Empty` with partition `tenant/{T}/esign-samples/{userId}` (CT-D4).
- The content allow-list already contains `.png` (`ContentStorageOptions.cs:27-28`).

**Signature requests (KS-4).** Created by modules (`esign.requests.create`, API-only) or by a user on a screen that
offers it.
- "İmza Talepleri" lists requests where `SignerUserId = me` (mockup: "yalnız size atanmış imza işlerini listeler").
- Workflow tasks that need a signature are **not** copied into requests; they stay in WorkCenter (CT-D14).
- Expiry: a Hangfire job sets `Expired` after `DueAtUtc`. FU23 never automated this (audit `…fu23…:151-160` gap 8).

**Policy (KS-2/KS-5/KS-8).**
- Rows come from the signable-action catalog. Tenant default always exists (seeded on first read from catalog
  defaults).
- A LegalEntity override is a full copy ("tenant varsayılanından kopya"). Rows that differ are flagged "Farklı".
- "Tenant varsayılanına dön" removes the override.
- Every save needs a reason of at least 10 characters and writes an `ESignaturePolicyChange` with the human-readable
  diff (mockup `diffs()`), plus an audit event.
- Optimistic concurrency on `Version` (409 `ESIGN_POLICY_VERSION_CONFLICT`).
- Second factor column: visible, disabled, "Yakında" (KS-11).

**Meanings (KS-12).**
- All 7 names are required (400 `ESIGN_MEANING_LANGUAGE_MISSING` lists the missing languages).
- Code is immutable after first use.
- A meaning used by a policy row cannot be deactivated (409 `ESIGN_MEANING_IN_USE`, mockup toast).
- Name changes affect new signatures only (records keep `MeaningTextAtSign`).

**Samples (KS-7/KS-9/KS-10).**
- Saving a sample needs a ticket (intent `sample`, Kind `Password`; mockup "Şifreyle kaydet").
- `SampleApprovalRequired` (tenant policy, default true) ⇒ status `Pending`. Otherwise it is immediately `Active` and the
  previous one becomes `Inactive`.
- Approval/rejection needs `esign.samples.approve` (rejection also `esign.samples.reject`) and is **never by the owner**
  (403 `ESIGN_SELF_APPROVAL_FORBIDDEN`).
- A used sample (≥ 1 record references it) cannot be withdrawn (409 `ESIGN_SAMPLE_IN_USE`), only deactivated.
- Old records keep their image because the record references the immutable content and hash.
- "Kayıtlı imza resmi şifre doğrulanmadan hiçbir kayda eklenemez" ⇒ appearance `Sample` requires Kind `Password`.

**Export (KS-6).** `POST api/v1/esign/exports` returns the file stream plus `X-Export-Number`.
- Insert-only `ESignatureExport` plus audit `esign.export.created`, **for every export** (PDF and CSV).
- CSV: `;` separator, UTF-8 BOM, columns as mockup `downloadCsv()`.
- PDF: one block per signature (image or "Görsel imza yok · yalnız ad soyad", name, meaning, date-time in the tenant
  time zone, signature number, verification status). Engine: CT-D11.
- Row limit 10 000 per export (413 `ESIGN_EXPORT_TOO_LARGE`, ASSUMPTION-0022-06).

**E4 — Workflow hook (NE-4), additive only.**
- `DefinitionJson` step `requirements` gains `signatureRequired` (bool) and `signatureMeaning` (meaning code).
  - Unknown keys are already tolerated and kept in the stored JSON (`WorkflowDefinitionRuntimePlan.cs:24-84`, helpers
    :147-167), so old templates parse unchanged.
  - `WorkflowRuntimeStep` (:6-17) gets trailing fields `SignatureRequired = false`, `SignatureMeaning = null`. They are
    copied to `ApprovalTask` like `CommentRequired` (`WorkflowTaskTransitionSupport.cs:187-188`).
- `ApproveWorkflowTaskRequest(…, string? EvidenceRef)` (`WorkflowModels.cs:325-330`) gets a trailing
  `string? SignatureId = null`.
  - Same pattern as `StartWorkflowInstanceRequest.DisplayContext = null` (:252-253).
  - Positional callers stay valid: `WorkflowApprovalWorkItemActionDispatcher.cs:93`, `WorkflowTaskTransitionTests`,
    `WorkflowCrossServiceTests`.
- `WorkItemActionPayloadDto` (`WorkItemActionDispatchModels.cs:75-127`) gets trailing `SignatureId` with
  `[JsonIgnore(WhenWritingNull)]`, like `ClosureFieldValues` :109-111. The dispatcher passes it into the request
  (:89-94).
- **Gate** in `WorkflowTaskTransitionSupport.TransitionAsync`, on `Approve`, after the actor check (:109-116) and the SoD
  check (:118-127), before the state change. If `task.SignatureRequired`:
  - `SignatureId` missing ⇒ 409 `ESIGN_SIGNATURE_REQUIRED`.
  - Otherwise `IESignatureGate.ValidateForApprovalAsync` (in-process) requires: same tenant;
    `SignerUserId == actorId`; `MeaningCode == task.SignatureMeaning`;
    `Context.workflowTaskId == taskId`; `Context.decision == "approve"`; not invalidated; `SubjectHash` equal to the
    current one; signed after the task was created; not bound to another task (unique index, §4).
    Any failure ⇒ 409 `ESIGN_SIGNATURE_MISMATCH`.
  - The signature id is written to `WorkflowTransitionLog` (new trailing field next to `EvidenceRef`, :253-254).
- Both paths hit this one handler: the API `POST api/v1/workflow/tasks/{id}/approve` and WorkCenter
  (`WorkItemsController.cs:175-263` → dispatcher → the same MediatR command). So WorkCenter approval without a signature
  is refused by the server, whatever the UI does.
- Idempotent replay (`TransitionAsync` :55) returns the first result. A replay with a different `SignatureId` is still
  the first result (documented).
- Reject / delegate / request-info are unchanged (no signature in v1). A "signature on reject" flag is a follow-up.
- WorkCenter UI: the workflow work-item provider exposes `signatureRequired` + `signatureMeaning` (additive). The
  approve button opens `_ESignatureDialog` with subject + context. On success it dispatches `approve` with
  `payload.signatureId`.

**E4 — measurement: where the workflow designer lives, who publishes (NE-4, required by the prompt).**
1. **Routes:** the manifest declares `/Platform/Workflow` (DEFINITIONS, nav-visible) and the designers
   `/Platform/Workflow/Definitions/{id}/Designer` and `…/VisualDesigner`
   (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/SelfRegistration/WorkflowManifestProvider.cs:32,51,62`),
   plus Versions :73, Instances :85, Tasks :97, SlaRules :115, Escalations :129, TransitionGate :143. The Web controller
   is `[Route("Platform/Workflow")]` (`frontend/Diten.Web/Controllers/WorkflowController.cs:17-18`).
2. **Shell:** the folder default is `_LayoutPlatformAdmin` (`frontend/Diten.Web/Views/Platform/_ViewStart.cshtml:2`), but
   **every Workflow view overrides it with `Layout = "_LayoutTenantShell"`**: `Views/Platform/Workflow/Index.cshtml:8`,
   `Designer.cshtml:7`, `VisualDesigner.cshtml:7`, `Versions.cshtml:8`, `Instances.cshtml:8`, `Tasks.cshtml:8`,
   `SlaRules.cshtml:8`, `Escalations.cshtml:6`, `TransitionGate.cshtml:6`. `ShellAccessFilter` lets `tenant_user` in on
   that path by an explicit exception (`frontend/Diten.Web/Filters/ShellAccessFilter.cs:19-21,111-114,142-145`). So the
   designer is a **tenant-shell screen on a `/Platform` route**.
3. **Menu:** the tenant shell menu is manifest-driven (`_LayoutTenantShell.cshtml:392` →
   `DynamicModuleMenuViewComponent.cs:95` → `GET /api/platform/navigation/menu`, `NavigationController.cs:36-50`). The
   handler keeps entitled, nav-visible, active descriptors **without a scope filter**
   (`GetTenantNavigationMenuQueryHandler.cs:53-61,96-98`). The item is shown when `Perms.Has(RequiredPermission)`
   (`Views/Shared/Components/DynamicModuleMenu/Default.cshtml:10-11`). That is why the owner sees "İş Akışı →
   /Platform/Workflow" in the tenant menu.
4. **Scope:** because of the route, every `platform.workflow.*` key is **PlatformAdmin**:
   - sync `RegisterModuleManifestCommandHandler.cs:521`;
   - seed `DataSeeder.cs:805-820`;
   - baseline CSV `…/permission-scope-baseline.csv:272-284`;
   - never downgraded, `InternalPermissionsController.cs:146-152`;
   - pinned by `ScopeFromRouteTests.cs:12-13` and `TenantRouteScopeCorrectionTests.cs:326,354-360`.
5. **Who can publish** (`POST api/v1/workflow/definitions/{id}/publish`, key `platform.workflow.definitions.publish`,
   `WorkflowDefinitionsController.cs:55-64`):
   - (a) Platform/partner admins bypass the key (`HasPermissionAttribute.cs:36-38,79`), but a template is tenant-scoped
     (`PublishWorkflowDefinitionHandler.cs:95`), so they need the target tenant's context.
   - (b) The **built-in Admin role of a workflow-entitled tenant**. Entitlement sync grants all `workflow` keys
     regardless of PlatformAdmin scope: `ModulePermissionResolver.cs:46-47,82-88` (`PlatformHostedTenantModules =
     {"workflow"}`), `EntitlementPermissionSyncService.cs:110-121`, Admin = all / Viewer = read
     (:367-375); dev seed `DataSeeder.cs:2032-2056`.
   - (c) **Nobody else.** A tenant admin cannot grant `definitions.publish` to a custom role: 403 "This permission
     cannot be assigned to a tenant role" (`AssignPermissionCommandHandler.cs:57-61`, `IsPlatform` =
     `Scope == PlatformAdmin`, `DefaultRolePermissionTemplate.cs:140-141`).
6. **Where the "İmza gerekli + anlam" setting goes:**
   - Form designer step card: next to the CommentRequired/EvidenceRequired switches
     (`Views/Platform/Workflow/Designer.cshtml:149-164`, JS `designer-page.js:199-202`).
   - Visual designer "Options" section (`VisualDesigner.cshtml:133-152`, JS `workflow.bpmn-designer.js:120,176-177,243-256,281-282,379`).
   - The meaning select loads `GET /api/v1/esign/meanings?activeOnly=true` through the Web proxy (Workflow already
     proxies `api/{**everything}`, `WorkflowController.cs:133-138`; E5 adds an esign proxy).
   - The designer view's resx already has 7 languages (`Resources/Views/Platform/Workflow/WorkflowIndex.{ar,en,es,fr,ru,tr,zh}.resx`).
7. **Result for MOD-0175:**
   - The quarantine release approval template can be published today **only by the tenant's built-in Admin role**
     (workflow entitlement required) **or by a platform admin acting in that tenant**.
   - A QA manager with a custom role cannot be given publish rights.
   - MOD-0175 should therefore either (i) ship the template as a seeded tenant default that the tenant Admin publishes
     once, or (ii) state in its pack that the tenant Admin publishes it.
   - Either way the template step sets `signatureRequired: true`, `signatureMeaning: RELEASE` (or APPROVE).
   - This pack proposes **no** shell, route or scope change for the designer (prompt "YAPMA"). CT-D6 records the choice
     for MOD-0175.
   - Side finding: the comment at `ModulePageDescriptorNormalizer.cs:34-36` says tenants get 403 on `/Platform/…`. For
     Workflow, ReferenceData and PersonReferences that is not true, because of the `ShellAccessFilter` exception. This
     goes to §20 as a follow-up and is not changed here.

**E6 — FU23 backward compatibility.** Until E6 the 16 FU23 endpoints, `document_management_signature_*` collections and
`platform.document-management.signatures.*` keys (PlatformAdmin, SuperAdmin-only — `Mod0029Fu29PermissionSeedHardeningTests.cs:9-18,101-105`)
work unchanged.
- E6 adds a Document Management signable-subject provider, reusing `DocumentSignableSubjectResolver`'s 15 subject types
  (:117-391).
- It switches Document Management's sign path to MOD-0022. Old FU23 records stay readable in the Signatures tab
  (`Views/DocumentManagement/MasterRegister/Details.cshtml:938-1010`, `details.js:2672-2770`).
- Meaning map: AuthorApproval → AUTHOR; ReviewerApproval → REVIEW; ReleaseAuthorization → RELEASE;
  QAGQDApproval / LocalQAApproval / *Approval → APPROVE; the others are tenant-created meanings (E6 seed).
- FU23 records are **not** copied into MOD-0022, so there is no rewrite. The verification viewer reads both stores
  (FU23 records shown with source "Belge Yönetimi (FU23)").

**Idempotency.** Every write op takes an `idempotencyKey`. Signature: unique per signer (see above). Request create:
unique per requester. Sample: unique per owner. Policy save: `expectedVersion`.

**Browser.** The signature pad uses pointer events (mouse / pen / touch). It has a full-screen landscape mode for phones
and tablets (mockup `openFull()`), undo, clear, and pen width and color (thin/medium · black/navy). It must work at 375
px width.

## 9. Layout & Shell Contract

- Every e-signature page uses `Layout = "_LayoutTenantShell"`. Users log in at `/account/login` (KS-0).
- **No route starts with `/Platform`.** Routes are under `/ESignature/**` (measured: no existing `ESignature` route in
  `frontend/Diten.Web/Controllers`).
- Pages appear in two places:
  - User pages in the tenant menu module "e-İmza".
  - Admin pages under **"Kiracı Ayarları"** (`Nav.Module.TENANTSETTINGS`, `SharedResource.tr.resx:590`; manifest
    `TenantSettingsManifestProvider.cs:22-35`, `ModuleCode "tenant-settings"`, `IsBaseline: true`). The mechanism is
    CT-D8.
- "İmzam" is also linked from the user menu in `_LayoutTenantShell` (mockup user menu: Profilim · İmzam · Çıkış yap).
- UAS-001: a user without the page key sees one explanation and what to do. No skeleton, no redirect.
- Inside pages, mockup role "user" is read-only: buttons hidden, and invalidate shows "Geçersiz kılma için e-İmza
  yöneticisi yetkisi gerekir".
- 7 languages, RTL for `ar` (meaning editor `dir="rtl"` for the ar field, mockup `langs[].dir`).
- Time display: tenant time zone with offset (mockup "UTC+03:00"); storage is UTC.

## 10. Backend File Convention

```text
services/Diten.AuthService/src/
  Diten.AuthService.Domain/Entities/ESignReauthTicket.cs, ESignReauthWindow.cs
  Diten.AuthService.Application/Features/ESignReauth/{Commands,Handlers,Validators,Models}/…
  Diten.AuthService.Infrastructure/Services/ESignTicketService.cs   (HMAC, atomic consume)
  Diten.AuthService.Infrastructure/Settings/ESignOptions.cs          (section "ESign": TicketLifetimeSeconds, HashSecret, MaxWindowMinutes=60)
  Diten.AuthService.Persistence/Repositories/ESignReauth*Repository.cs (+ index config)
  Diten.AuthService.Api/Controllers/TenantAuthController.cs          (esign/reauth, esign/ticket)
  Diten.AuthService.Api/Controllers/InternalESignTicketsController.cs
services/Diten.Platform/src/
  Diten.Platform.Domain/Entities/ESignature/*.cs, Enums/ESignature/*.cs, Repositories/IESignature*Repository.cs (insert-only interfaces)
  Diten.Platform.Application/Features/ESignature/{Commands,Queries,Handlers,Validators,Models,Services,Subjects,Policy,Samples,Export,SelfRegistration}/…
  Diten.Platform.Application/Features/ESignature/ESignaturePermissions.cs
  Diten.Platform.Infrastructure/Persistence/Repositories/ESignature*Repository.cs, Schema/PlatformSchemaManifest.ESignature.cs
  Diten.Platform.Infrastructure/Services/ESignature/{AuthServiceESignTicketClient,HttpSignableSubjectProvider,ESignatureExpirySweeper}.cs
  Diten.Platform.API/Controllers/ESignature/*.cs
```

Rules: 5 layers + CQRS; `Response<T>` + `CustomBaseController`; 4 pipeline behaviors; every command is
`IAuditableCommand` + `IAuditMetadataProvider` with `SourceModule: "MOD-0022"` (pattern
`…/ElectronicSignatureCommands.cs:12-18`). A new `AuditCategory.ESignature` value is additive. TenantId always comes from
context. Cross-tenant ⇒ 404.

## 11. Frontend File Contract

### 11.1 Mockup screen → page / route (all Scope = Tenant)

| Mockup screen (v2 tab) | PageCode | Route | Shell | RequiredPermission | Scope | Nav place |
|---|---|---|---|---|---|---|
| İmza Penceresi (shared dialog) | — (not a page) | partial `Views/Shared/ESignature/_ESignatureDialog.cshtml`, opened on the host page | host page (tenant) | `esign.signatures.sign` (manifest `ApiPermissions`) | Tenant | — |
| İmzam | `ESIGN_MYSIGNATURE` | `/ESignature/MySignature` | `_LayoutTenantShell` | `esign.my-samples.manage` | Tenant | user menu + e-İmza module (nav-visible) |
| İmza Talepleri | `ESIGN_REQUESTS` | `/ESignature/Requests` | `_LayoutTenantShell` | `esign.requests.read` | Tenant | e-İmza |
| Doğrulama (Signature Verification Viewer) | `ESIGN_VERIFICATION` | `/ESignature/Verification` (`?module=&type=&id=` or `?signature=`) | `_LayoutTenantShell` | `esign.signatures.read` | Tenant | e-İmza |
| Denetim Dışa Aktarımı (Signature Audit Export) | `ESIGN_AUDITEXPORT` | `/ESignature/AuditExport` | `_LayoutTenantShell` | `esign.signatures.export` | Tenant | e-İmza |
| e-İmza Politikası | `ESIGN_POLICY` | `/ESignature/Settings/Policy` | `_LayoutTenantShell` | `esign.policy.read` | Tenant | Kiracı Ayarları |
| İmza Anlamları | `ESIGN_MEANINGS` | `/ESignature/Settings/Meanings` | `_LayoutTenantShell` | `esign.meanings.read` | Tenant | Kiracı Ayarları |
| İmza Örneği Onayları | `ESIGN_SAMPLEAPPROVALS` | `/ESignature/Settings/SampleApprovals` | `_LayoutTenantShell` | `esign.samples.approve` | Tenant | Kiracı Ayarları |

Blueprint `Module Pages` rows 66–68 (Signature Requests · Signature Verification Viewer · Signature Audit Export) map to
REQUESTS, VERIFICATION and AUDITEXPORT. The other five come from the owner-approved mockup v2.

**Route guard:** no RoutePath in this table starts with `/Platform`. `ESignManifestProviderTests` asserts every page
satisfies `ScopeFromRoute(route) == "Tenant"` and that every `ApiPermissions` entry has `Scope = "Tenant"` (sabotage §17).

### 11.2 Files

```text
frontend/Diten.Web/Controllers/ESignatureController.cs        [Authorize][Route("ESignature")] + api/{**everything} proxy → gateway /api/v1/esign/**
frontend/Diten.Web/Views/ESignature/{MySignature,Requests,Verification,AuditExport}.cshtml
frontend/Diten.Web/Views/ESignature/Settings/{Policy,Meanings,SampleApprovals}.cshtml
frontend/Diten.Web/Views/ESignature/_ESignatureL10n.cshtml
frontend/Diten.Web/Views/Shared/ESignature/_ESignatureDialog.cshtml, _SignaturePad.cshtml, _SignatureZoom.cshtml
frontend/Diten.Web/wwwroot/assets/js/ESignature/{esign-dialog,esign-pad,my-signature,requests,verification,audit-export,policy,meanings,sample-approvals,esign.l10n}.js
frontend/Diten.Web/Resources/Views/ESignature/ESignature.{en,tr,fr,es,zh,ar,ru}.resx
```

Rules:
- Lists use the DataTable v2 contract (`data-dt-standard="v2"`) in the golden slim pattern.
- Modals follow the premium SweetAlert2 standard (MOD-0013); the dangerous invalidate uses the red warning style.
- The dialog JS API is `ESign.open({ module, subjectType, subjectId, actionCode?, meaningCode?, requestId?, context? }) → Promise<{ signatureId, signatureNumber } | null>`.
  The host page passes **only** ids. Summary, meaning list, policy and time come from the server (mockup "Ortak bileşen"
  text).

### 11.3 Screens (mockup v2, 1:1)

- **İmza Penceresi** (`v2.app.js:268-299`, markup lines 268–309):
  - Header "Elektronik imza · 21 CFR Part 11 · EU GMP Annex 11". The badge is a reference label only; no compliance
    claim is made.
  - "Ne imzalıyorsunuz": module badge, title, summary rows.
  - "Kullanıcı adı" read-only. "Şifre", or the window line "Şifreniz HH:MM'de doğrulandı … (n dk kaldı)".
  - "İmza anlamı": select + description. "İmza görünümü": "Kayıtlı imzamı kullan" / "Şimdi çiz" / "Yalnız ad soyad",
    with disabled reasons (§12). Pad shown when drawing.
  - "İmza notu".
  - Statement "Bu elektronik imza, el yazısı imzamla eşdeğerdir." + "İmza; kimliğiniz, tarih-saat, imza anlamı ve kaydın
    o anki özetiyle değiştirilemez şekilde saklanır."
  - Phases: form / locked / timeout / success.
    - Success shows signature number, time stamp with offset, signer, meaning, appearance, record id, record digest,
      image digest, and "Doğrulamayı görüntüle".
    - Locked shows the corrected text (§21.3 #2).
    - Timeout: "Yeniden oturum aç" → `/account/login?returnUrl=…`; the host keeps its draft.
- **İmza Talepleri:**
  - Filters: module, status, request date from/to, "Temizle".
  - Columns: Modül · Kayıt (title + `SR-… · record`) · İstenen anlam · Talep eden (+ time) · Son tarih ("Bugün" badge) ·
    Durum (+ info: signature number or rejection reason).
  - Row actions: İmzala (opens the dialog), Reddet (reason modal, ≥ 10 chars).
  - Header text "{n} talep · {m} bekliyor". Tab badge = waiting count.
- **Doğrulama:**
  - Record select / deep link; module + company badge; title; "Güncel kayıt özeti (SHA-256)".
  - Count chips (geçerli / geçersiz / geçersiz kılındı); "Son doğrulama" + "Yeniden doğrula".
  - Timeline per signature: signer (or "yalnız ad"), login, meaning, time + offset, status line, note, appearance type,
    signature number, "İmza anındaki özet", "Görüntü özeti", change explanation, invalidation reason/by/at.
  - "İmzayı geçersiz kıl" (reason ≥ 10 + password). Zoom modal for the image.
- **Denetim Dışa Aktarımı:**
  - Filters: start, end, module, signer, meaning, company.
  - Views: Tablo / PDF önizleme. Buttons: "PDF olarak dışa aktar" / "CSV olarak dışa aktar".
  - Table: Tarih-saat · İmza kimliği · İmzalayan (+login) · Anlam · Modül · Kayıt · Şirket · Doğrulama · Görüntü.
  - "Dışa aktarım denetim kaydı" list (number, time, by, format, count, filter text).
- **e-İmza Politikası:**
  - Scope toggle: Tenant varsayılanı / Şirket bazında + company select ("· farklı politika" / "· tenant varsayılanı").
  - Hint lines; "tenant varsayılanını kullanıyor" card with "copy and edit"; "Tenant varsayılanına dön".
  - Table "İşlem bazında imza kuralları": İşlem (module · action) · Zorunlu/Kapalı segmented · İmza anlamı select ·
    Görsel imza select (Yok / İsteğe bağlı / Kayıtlı imza kabul / Zorunlu: o an çiz) · İkinci faktör "Yakında"
    (disabled) · "Farklı" tag.
  - "Genel ayarlar":
    - "Ard arda imzalarda şifre": Her imzada / Süre içinde bir kez.
    - "Şifre yeniden doğrulamanın geçerlilik süresi" 1–60 dk (disabled when "Her imzada").
    - "İmza örneği kullanılmadan önce yönetici onayı gereksin" switch.
  - Dirty bar "{n} değişiklik kaydedilmedi" + Vazgeç + "Gerekçe girip kaydet". "Değişiklik geçmişi" list.
- **İmza Anlamları:**
  - Table: Kod · Görünen ad (tr + en) · dil sayısı "{n}/7 dil" · Açıklama · Kullanım "{n} politika satırı" · Aktif
    toggle · Düzenle/Görüntüle.
  - "Yeni anlam" modal: code (locked after use), 7 name fields, description, usage warning.
- **İmza Örneği Onayları:**
  - Note "Kendi imza örneğinizi onaylayamazsınız. Her onay ve ret kararı denetim kaydına yazılır."
  - Table: Kullanıcı (name, login · IMZ) · Eklenme · Nasıl eklendi · Saklama · Görüntü (zoom).
  - Actions: Onayla / Reddet (reason). Own row shows "Başka bir yönetici onaylar".
  - "Karar geçmişi".
- **İmzam:**
  - Statement "Görsel imza, imzanın görünümüdür. …" (KS-10).
  - "İmza örneğim" card: image, Aktif, Kimlik, Eklenme · kind, Saklama, Onaylayan, "{n} imzada kullanıldı"; or the empty
    state; pending info.
  - "Yeni imza örneği": tabs Çiz / Resim yükle.
    - Draw: pad, Geri al, Temizle, Kalınlık İnce/Orta, Renk Siyah/Lacivert, "Tam ekran (yatay)".
    - Upload: PNG/JPG ≤ 1 MB, crop sliders ×4, "Arka planı temizle" + Hassasiyet, original / result preview.
    - "Saklama biçimi: …", approval note, "Şifreyle kaydet" → password modal "Şifrenizi doğrulayın / Doğrula ve kaydet".
  - "İmza örneği geçmişi": Örnek · Eklenme · Nasıl eklendi · Saklama · Kullanım · Durum (+ rejection reason) · actions
    Pasif yap / Sil (unused, not active → withdraw) / "Silinemez".
  - The mockup's "Örnek taranmış imza ile dene" is a demo-only button and is dropped (§21.3).

### 11.4 Host-page integration

- MOD-0175 quarantine panel (mockup "Karantina Bloke Stok"): step 1 "Karar", then step 2 "Elektronik imza" (two-step
  panel, `kar.app.js:99-100,257-260`).
  - Step 2 becomes `ESign.open({ module:'inventory', subjectType:'inventory.quarantine-decision', … })`, followed by the
    quarantine commit with `signatureId`.
  - The quarantine mockup's simplified meanings ("Onaylıyorum", "İnceledim", `kar.app.js:213`) are replaced by the
    tenant's meanings from MOD-0022.
- WorkCenter: approve on a `signatureRequired` item → dialog → dispatch with `payload.signatureId`.

### 11.5 Mockup demo controls (not built)

Not built: "Oturum ve hesap durumu" demo card (Oturum zaman aşımı / Hesabı kilitle / Sıfırla), "Görsel imza politikasına
göre pencere" demo cases, the "Mockup kararları" panel, the role switcher, and the demo password text.

### 11.6 Resx keys (per page; all 7 languages)

`ESign.Dialog.*` (title, standard ref, what-you-sign, username, password, meaning, appearance + 3 options + disabled
reasons, note, statement 1/2, locked, timeout, success fields, buttons, errors) · `ESign.Requests.*` · `ESign.Verify.*`
(status names Geçerli / Geçersiz / Geçersiz kılındı / Değiştirilmiş görüntü, sublines) · `ESign.Export.*` (+ PDF header
"İmza Denetim Raporu", footer text) · `ESign.Policy.*` (visual modes, general settings, history, dialogs) ·
`ESign.Meanings.*` · `ESign.Samples.*` · `ESign.Approvals.*` · `ESign.Errors.{ESIGN_*}` (every error code of the
contract) · `Nav.Module.esign`, `Nav.Page.ESIGN_*` (self-registration standard :190) · Workflow designer:
`Workflow.Designer.SignatureRequired`, `Workflow.Designer.SignatureMeaning`. Meaning display names are **data** (per
tenant, 7 languages), not resx.

## 12. Validation Rules

| # | Field / rule | Rule | Server error |
|---|---|---|---|
| V1 | ticket | required on sign / invalidate / sample save | 400 `ESIGN_TICKET_REQUIRED` |
| V2 | ticket | must consume (exists, same user/tenant/intent, not consumed, not expired) | 403 `ESIGN_TICKET_INVALID` |
| V3 | reauth kind | `ReauthEvery` ⇒ Password; window ⇒ age ≤ minutes; Sample appearance / invalidate / sample save ⇒ Password | 403 `ESIGN_REAUTH_REQUIRED` |
| V4 | subject | resolvable and visible for the caller | 404 `ESIGN_SUBJECT_NOT_FOUND` |
| V5 | meaningCode | active; equals the policy row meaning when the row fixes one | 409 `ESIGN_MEANING_NOT_ALLOWED` |
| V6 | appearance | allowed by VisualMode (KS-8) | 409 `ESIGN_APPEARANCE_NOT_ALLOWED` |
| V7 | sampleId | caller's own, status Active | 409 `ESIGN_SAMPLE_NOT_ACTIVE` |
| V8 | png | magic bytes PNG; IHDR ≤ 2000×800 | 415 `ESIGN_IMAGE_NOT_PNG` |
| V9 | png | ≤ 1 048 576 bytes | 413 `ESIGN_IMAGE_TOO_LARGE` |
| V10 | drawing | Drawn ⇒ ≥ 1 stroke / non-empty PNG | 400 `ESIGN_DRAWING_EMPTY` |
| V11 | note | ≤ 500 | 400 |
| V12 | requestId | Waiting; signer nominated | 409 `ESIGN_REQUEST_NOT_OPEN` / 403 `ESIGN_SIGNER_NOT_NOMINATED` |
| V13 | reason (reject, invalidate, policy save, revert, sample reject) | trimmed length ≥ 10, ≤ 1000 | 400 `ESIGN_REASON_TOO_SHORT` |
| V14 | invalidate | not already invalidated | 409 `ESIGN_ALREADY_INVALIDATED` |
| V15 | policy.reauthWindowMinutes | 1–60 | 400 |
| V16 | policy | `expectedVersion` equals stored | 409 `ESIGN_POLICY_VERSION_CONFLICT` |
| V17 | policy row secondFactor | must be `NotAvailable` (KS-11) | 400 `ESIGN_SECOND_FACTOR_NOT_AVAILABLE` |
| V18 | meaning code | `^[A-Z][A-Z0-9_]{1,31}$`, unique; immutable after first use | 400 / 409 `ESIGN_MEANING_CODE_EXISTS` / 409 `ESIGN_MEANING_CODE_LOCKED` |
| V19 | meaning names | all 7 languages non-empty | 400 `ESIGN_MEANING_LANGUAGE_MISSING` |
| V20 | meaning deactivate | not used in any policy row | 409 `ESIGN_MEANING_IN_USE` |
| V21 | sample approve/reject | not by owner; sample Pending | 403 `ESIGN_SELF_APPROVAL_FORBIDDEN` / 409 `ESIGN_SAMPLE_NOT_PENDING` |
| V22 | sample withdraw | unused and not Active | 409 `ESIGN_SAMPLE_IN_USE` |
| V23 | request dueAtUtc | > now | 400 |
| V24 | export | ≤ 10 000 rows; from ≤ to | 413 `ESIGN_EXPORT_TOO_LARGE` / 400 |
| V25 | workflow approve | step requires signature ⇒ signatureId present | 409 `ESIGN_SIGNATURE_REQUIRED` |
| V26 | workflow approve | signature matches signer, meaning, task, decision, subject hash; not invalidated; not bound elsewhere | 409 `ESIGN_SIGNATURE_MISMATCH` |
| V27 | reauth | account locked | 423 `ESIGN_ACCOUNT_LOCKED` |
| V28 | reauth | wrong password | 401 `ESIGN_REAUTH_FAILED` (+ `remainingAttempts`, CT-D13) |
| V29 | reauth / ticket | actor_type ≠ tenant_user | 403 `ESIGN_ACTOR_NOT_TENANT_USER` |

UI-only rules (mockup):
- Disabled appearance options with reasons: "Politika bu işlem için o an çizim istiyor"; "Bu işlemde görsel imza
  kullanılmaz"; "İmza örneğiniz yönetici onayı bekliyor"; "Kayıtlı imza örneğiniz yok · İmzam sayfasından ekleyin";
  "Bu işlem görsel imza gerektiriyor".
- The Enter key submits the dialog.
- The reason counter shows "{n} / en az 10".

## 13. Failure Path to Verify

1. Sign without a ticket → 400 `ESIGN_TICKET_REQUIRED`.
2. Sign with an already consumed ticket → 403 `ESIGN_TICKET_INVALID`.
3. Two parallel consumes of one ticket → exactly one 200 (atomic filter).
4. Ticket from user A used by user B (same tenant) → 403 `ESIGN_TICKET_INVALID`.
5. Ticket from tenant T1 used in T2 → 403 `ESIGN_TICKET_INVALID`.
6. Window ticket while the policy says "her imzada" → 403 `ESIGN_REAUTH_REQUIRED`.
7. Saved-sample appearance with a window ticket → 403 `ESIGN_REAUTH_REQUIRED`.
8. Pending (unapproved) sample used → 409 `ESIGN_SAMPLE_NOT_ACTIVE`.
9. "Zorunlu: o an çiz" row signed with the saved sample → 409 `ESIGN_APPEARANCE_NOT_ALLOWED`.
10. Wrong password `MaxFailedLoginAttempts` times → 423 `ESIGN_ACCOUNT_LOCKED`. **Login** is also refused. The correct
    password inside the lockout is still 423.
11. Re-auth with a platform_admin token → 403 `ESIGN_ACTOR_NOT_TENANT_USER`.
12. Subject of another tenant → 404 `ESIGN_SUBJECT_NOT_FOUND`.
13. Record changed after signing → verify `Changed`. Stored PNG bytes changed → verify `Tampered`.
14. Invalidate twice → 409 `ESIGN_ALREADY_INVALIDATED`. The record is still present and unchanged.
15. Owner approves own sample → 403 `ESIGN_SELF_APPROVAL_FORBIDDEN`.
16. Withdraw a used sample → 409 `ESIGN_SAMPLE_IN_USE`.
17. Workflow step with `signatureRequired`, approve via API without `signatureId` → 409 `ESIGN_SIGNATURE_REQUIRED`.
18. The same through WorkCenter dispatch (`/api/v1/work-items/{id}/actions/approve`) → 409 `ESIGN_SIGNATURE_REQUIRED`.
19. Approve with another user's / other meaning / other task's signature → 409 `ESIGN_SIGNATURE_MISMATCH`.
20. One signature used for two tasks → second 409 `ESIGN_SIGNATURE_MISMATCH`.
21. Step without the flag, no `signatureId` → approved as today (regression). Old `DefinitionJson` without the keys →
    unchanged.
22. Meaning with only 6 languages → 400 `ESIGN_MEANING_LANGUAGE_MISSING`.
23. Upload 1.2 MB → 413. JPG bytes posted as PNG → 415.
24. Export → `esign_exports` row + audit event exists for both PDF and CSV.
25. A user without `esign.requests.read` opens `/ESignature/Requests` → UAS-001 surface (no skeleton, no redirect).

## 14. Authorization Convention

**Module code `esign` (lowercase slug, self-registration standard :16), namespace `esign.*` (proposed, owner MOD-0022 —
PKS-001 §4).** Tier-3 verbs registered by this pack (PKS-001 §5 :126-130): `sign`, `verify`, `invalidate`, `manage`.

| Key | Tier | Gate |
|---|---|---|
| `esign.signatures.read` | 1 | VERIFICATION page; GET signature / by-subject |
| `esign.signatures.sign` | 3 | sign op; dialog (**API-only → `ApiPermissions`**, standard §2d :85-104) |
| `esign.signatures.verify` | 3 | "Yeniden doğrula"; POST verify |
| `esign.signatures.invalidate` | 3 | "İmzayı geçersiz kıl" (dangerous) |
| `esign.signatures.export` | 2 | AUDITEXPORT page; PDF/CSV |
| `esign.requests.read` | 1 | REQUESTS page (own requests) |
| `esign.requests.create` | 1 | create request (**API-only → `ApiPermissions`**; called by consumer modules with the user's token) |
| `esign.requests.reject` | 2 | "Reddet" on a request |
| `esign.my-samples.manage` | 3 | MYSIGNATURE page; save / deactivate / withdraw own sample |
| `esign.samples.approve` | 2 | SAMPLEAPPROVALS page; "Onayla" |
| `esign.samples.reject` | 2 | "Reddet" on a sample |
| `esign.policy.read` | 1 | POLICY page (read-only for non-managers) |
| `esign.policy.manage` | 3 | policy save / company override / revert |
| `esign.meanings.read` | 1 | MEANINGS page; dialog meaning list is served under `esign.signatures.sign` too |
| `esign.meanings.manage` | 3 | new / edit / activate-deactivate meaning |

All keys are **Scope = Tenant**. Pages are on non-`/Platform` routes; `ApiPermissions` entries pass `Scope: "Tenant"`
explicitly (`ModuleManifestDocument.cs:65-68` default is "Tenant").

**Manifest (`ESignManifestProvider`, in-process in Platform):**
- `ModuleCode "esign"`, `Domain "PlatformSharedServices"`, `IsTenantAssignable: true`, **`IsBaseline: true`** (CT-D10;
  `ModuleManifestDocument.cs:15-34`).
- `ApiPermissions: [esign.signatures.sign, esign.requests.create]`.

| PageCode | RoutePath | RequiredPermission | Nav-visible | Parent |
|---|---|---|---|---|
| `ESIGN_REQUESTS` | `/ESignature/Requests` | `esign.requests.read` | true | — |
| `ESIGN_VERIFICATION` | `/ESignature/Verification` | `esign.signatures.read` | true | — |
| `ESIGN_AUDITEXPORT` | `/ESignature/AuditExport` | `esign.signatures.export` | true | — |
| `ESIGN_MYSIGNATURE` | `/ESignature/MySignature` | `esign.my-samples.manage` | true | — |
| `ESIGN_POLICY` | `/ESignature/Settings/Policy` | `esign.policy.read` | true (Kiracı Ayarları, CT-D8) | — |
| `ESIGN_MEANINGS` | `/ESignature/Settings/Meanings` | `esign.meanings.read` | true (Kiracı Ayarları, CT-D8) | — |
| `ESIGN_SAMPLEAPPROVALS` | `/ESignature/Settings/SampleApprovals` | `esign.samples.approve` | true (Kiracı Ayarları, CT-D8) | — |

**Screen → button → action → permission (every mockup button):**

| Screen | Button (mockup) | ActionCode | Placement | PermissionKey | Dangerous |
|---|---|---|---|---|---|
| İmza Penceresi | İmzala ve kaydet | — (API) | dialog | `esign.signatures.sign` | no |
| İmza Penceresi | Doğrulamayı görüntüle | — (link) | dialog | `esign.signatures.read` | no |
| İmza Talepleri | İmzala | `SIGNREQUEST` | RowAction | `esign.signatures.sign` | no |
| İmza Talepleri | Reddet | `REJECTREQUEST` | RowAction | `esign.requests.reject` | no |
| İmza Talepleri | (row) kayıt aç | `OPENRECORD` | RowAction | `esign.requests.read` (target page enforces its own key) | no |
| Doğrulama | Yeniden doğrula | `REVERIFY` | Toolbar | `esign.signatures.verify` | no |
| Doğrulama | İmzayı geçersiz kıl | `INVALIDATE` | RowAction | `esign.signatures.invalidate` | **yes** |
| Doğrulama | (image) zoom | — | — | `esign.signatures.read` | no |
| Denetim Dışa Aktarımı | PDF olarak dışa aktar | `EXPORTPDF` | Toolbar | `esign.signatures.export` | no |
| Denetim Dışa Aktarımı | CSV olarak dışa aktar | `EXPORTCSV` | Toolbar | `esign.signatures.export` | no |
| e-İmza Politikası | Gerekçe girip kaydet | `SAVEPOLICY` | Toolbar | `esign.policy.manage` | no |
| e-İmza Politikası | (company) kopyala ve düzenle | `CREATEOVERRIDE` | Toolbar | `esign.policy.manage` | no |
| e-İmza Politikası | Tenant varsayılanına dön | `REVERTOVERRIDE` | Toolbar | `esign.policy.manage` | **yes** |
| e-İmza Politikası | Vazgeç | — (client) | — | — | no |
| İmza Anlamları | Yeni anlam | `NEWMEANING` | Toolbar | `esign.meanings.manage` | no |
| İmza Anlamları | Düzenle | `EDITMEANING` | RowAction | `esign.meanings.manage` (read-only "Görüntüle" with `esign.meanings.read`) | no |
| İmza Anlamları | Aktif toggle | `TOGGLEMEANING` | RowAction | `esign.meanings.manage` | no |
| İmza Örneği Onayları | Onayla | `APPROVESAMPLE` | RowAction | `esign.samples.approve` | no |
| İmza Örneği Onayları | Reddet | `REJECTSAMPLE` | RowAction | `esign.samples.reject` | no |
| İmzam | Şifreyle kaydet / Doğrula ve kaydet | `SAVESAMPLE` | Toolbar | `esign.my-samples.manage` | no |
| İmzam | Pasif yap | `DEACTIVATESAMPLE` | RowAction | `esign.my-samples.manage` | no |
| İmzam | Sil (withdraw) | `WITHDRAWSAMPLE` | RowAction | `esign.my-samples.manage` | **yes** |
| İmzam | Tam ekran / Geri al / Temizle / kalınlık / renk / kırp / arka plan | — (client) | — | — | no |

Not actions: Filters, Temizle, Tablo/PDF önizleme toggle, zoom and pad tools are client-side.
Nav keys: `Nav.Module.esign`, `Nav.Page.ESIGN_*`.

**Who gets keys by default (CT-D10):**
- Entitlement sync: Admin gets everything, Viewer gets `*.read`.
- `esign.signatures.sign` and `esign.my-samples.manage` are proposed for every default tenant role, because the
  authority to sign a *business decision* is checked by the consumer (workflow assignment, request nomination).
  `TenantSelfServicePermissions` (`DefaultRolePermissionTemplate.cs:54-62`) is for `platform.*` keys only, so this needs
  a template entry decided by the CT.

## 15. Gateway / API Routing Decision

**Change REQUIRED for Platform, none for AuthService. Owned by a separate integration-agent WP (INT-ESIGN).**

| Operation | Route | Source |
|---|---|---|
| Re-auth / window ticket | `/api/tenant-auth/esign/reauth`, `/api/tenant-auth/esign/ticket` | existing `/api/tenant-auth/{everything}` → 5056 (`ocelot.json:1870-1888`); no change |
| Ticket consume | `/internal/esign-tickets/consume` | S2S Platform → AuthService (`AuthService:BaseUrl` + `X-Internal-Api-Key`); **not** in the gateway |
| e-signature API | `/api/v1/esign/{everything}` → 5057 `/api/v1/esign/{everything}` | **new route** (no catch-all for Platform, measured: one route per area, e.g. `/api/platform/audit/events` `ocelot.json:1051`) |
| Remote subject providers | `{BaseUrl}/esign-subjects/{type}/{id}` | operator config; called by Platform with the user's JWT |

The gateway treats `/api/v1/*` as a tenant path: it requires `actor_type=tenant_user` and injects `X-Tenant-Id`
(`TenantResolutionMiddleware.cs:202-211`), as for `/api/v1/workflow` today. A route coverage test like
`gateway/Diten.ApiGateway.Tests/Mod0029Fu30DocumentManagementRouteCoverageTests.cs` is added.

## 16. Acceptance Criteria

**E1 — re-authentication**
- [ ] Correct password → 200 `{ticket, expiresAtUtc, passwordVerifiedAtUtc}`; `refreshTokens` count unchanged; no
      `Set-Cookie`, no access token in the response.
- [ ] Wrong password increments `User.FailedLoginAttempts` (the login counter). The `MaxFailedLoginAttempts`-th failure
      sets `LockoutEnd = now + LockoutDurationMinutes` from tenant login settings (default 5 / 15). Login is then
      refused with "Account is locked".
- [ ] Locked account + correct password → 423, no ticket.
- [ ] Each attempt writes one `authAuditLogs` event (`tenant_esign_reauth_*`). No event, log line or exception contains
      the password or the plain ticket (log-capture test).
- [ ] Consume is atomic and single-use; user, tenant, intent and expiry are bound.
- [ ] `platform_admin` / `partner_admin` token → 403.
- [ ] Window: a ticket without a password inside an open window; 409 after expiry or after a password change / logout.
- [ ] **Sabotage:** remove `ConsumedAtUtc == null` from the consume filter → parallel-consume test red; skip
      `CheckLockout` → lockout test red; log the request body → log-capture test red.

**E2 — signature service**
- [ ] Sign creates exactly one record with signer = JWT `sub`, server time, meaning text at signing, subject hash, image
      hash and signature digest. 201.
- [ ] Insert-only: repository interfaces have no update / replace / delete member. An architecture test (reflection
      over `IESignature*Repository`) is green. **Sabotage:** add `UpdateAsync` → red.
- [ ] Verify returns `Valid` / `Changed` / `Invalidated` / `Tampered` / `SubjectUnresolvable` as in §8.
      **Sabotage:** change one subject field after signing → `Changed`; flip one byte of the stored PNG → `Tampered`.
- [ ] Invalidation inserts one row; the record is untouched.
- [ ] Policy: tenant default + LegalEntity override; effective policy picks the override; revert removes it; every save
      writes history with reason. Concurrency 409.
- [ ] Meanings: 7 names required; code locked after use; in-use meaning cannot be deactivated.
- [ ] Requests: create / list mine / reject (reason) / expire (job).
- [ ] Export: PDF + CSV. Every export writes `esign_exports` + an audit event.
- [ ] Cross-tenant → 404 on every read.
- [ ] `esign.openapi.yaml` v1.0.0 frozen, equal to the accepted proposal.

**E3 — samples**
- [ ] Save requires a password ticket. Approval required → Pending.
- [ ] Self-approval 403. Approve → Active and the previous one Inactive in one transaction.
- [ ] Pending / Rejected / Inactive sample cannot be used (409). A used sample cannot be withdrawn.
- [ ] Images stored through `IContentStorageGateway` with scope `ESignatureImages`. No new storage type.

**E4 — workflow hook**
- [ ] Step flag round-trips through both designers and `DefinitionJson`. Old templates parse unchanged.
- [ ] Approve of a flagged step without / with a wrong signature → 409, via API **and** via WorkCenter dispatch.
      **Sabotage:** remove the gate call → both tests red.
- [ ] Unflagged steps unchanged: existing workflow tests green without edits (except adding the new optional
      argument nowhere).
- [ ] `SignatureId` stored on the transition log.

**E5 — Web**
- [ ] 7 pages + dialog as §11, mockup v2 1:1 except §21.3. All `_LayoutTenantShell`. UAS-001.
- [ ] **Sabotage:** change one manifest route to `/Platform/ESignature/Requests` → `ESignManifestProviderTests` red
      (route rule + `ScopeFromRoute` = Tenant).
- [ ] Pad works with mouse, pen and touch; full screen landscape on 375 px.
- [ ] Password field never persisted; cleared after every attempt.

**E6 — FU23**
- [ ] Document Management signs through MOD-0022. All FU23 endpoints and records still readable. Existing FU23 tests
      green.

**INT-ESIGN / L10n**
- [ ] Gateway route + coverage test.
- [ ] Every key in 7 languages; resx parity test green. **Sabotage:** delete one `ar` key → red.

## 17. Test Expectations

- DCP-002 verifier exit 0, or the explicit waiver W-0022-01 (environment-blocked record, never a fabricated PASS).
- Unit:
  - canonical JSON / digest (golden vectors);
  - PNG byte validator;
  - policy resolution;
  - visual-mode matrix (4 modes × 3 appearances);
  - reauth-kind matrix;
  - sample status projection.
- Handler / API: every failure path of §13 (1–25) as an automated test.
- Mongo: indexes exist (schema manifest test); unique `(TenantId, Context.workflowTaskId)`.
- Architecture:
  - insert-only repository interfaces;
  - no `esign` route under `/Platform`;
  - `esign.*` keys Scope Tenant after sync (AuthService test similar to `TenantRouteScopeCorrectionTests`);
  - DocsPathGuard (`tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs:14-16,42-52`) — no
    code file may reference `docs/analysis/…`.
- Manifest completeness (standard §3): pages ↔ keys ↔ actions both directions, including `ApiPermissions`.
- Concurrency: parallel ticket consume; parallel sample approval.
- Log capture: no password, ticket or image bytes in logs.
- Web: DataTable v2 verifier for the lists; resx parity (7 languages).
- Runtime E4 (G1): owner signs a quarantine release in the live tenant shell (after MOD-0175).
- Sabotage proofs: listed per slice in §16. A guard without a sabotage proof is not a guard (CLAUDE.md "Test").

## 18. Ready-for-dev Checklist

- [ ] Canonical ID/name present in the registry — **missing**: CT adds the MOD-0022 row (§19 "Registry").
- [ ] DCP-002 verifier exit 0 — **waiver proposal W-0022-01**:
  - owner: Ali;
  - reason: no Python on the MVP-1 machine;
  - compensating control: Blueprint 8.1 `Blueprint_Data` row 23 read directly from the xlsx (§19 measured facts);
  - expiry: before the MVP-1 push/PR (re-run, exit 0).
- [x] (added by the CT 2026-10-07) Domain-config in-scope list: MOD-0022 was **not** listed (`execution/domains/platform-shared-services/domain-config.md:8-14`).
      CT adds it.
- [ ] Owner decisions KS-0…KS-13 recorded (§19). ✔ in this draft.
- [x] **CT-D1** (taken 2026-10-07, §19) ticket lifetime.
- [x] **CT-D2** (taken 2026-10-07, §19) canonical subject representation + provider contract.
- [x] **CT-D3** (taken 2026-10-07, §19) FU23 migration timing.
- [x] **CT-D4** (taken 2026-10-07, §19) file storage location / partition / root path.
- [x] **CT-D5** (taken 2026-10-07, §19) sample approval authority.
- [x] **CT-D6** (taken 2026-10-07, §19) workflow designer measurement → MOD-0175 template publisher.
- [x] **CT-D7** (taken 2026-10-07, §19) window mode vs Part 11 §11.200(a)(1)(i).
- [x] **CT-D8** (taken 2026-10-07, §19) "Kiracı Ayarları" placement mechanism.
- [x] **CT-D9** (taken 2026-10-07, §19) ticket mechanism (opaque + internal consume).
- [x] **CT-D10** (taken 2026-10-07, §19) baseline module + default role grants.
- [x] **CT-D11** (taken 2026-10-07, §19) PDF engine.
- [x] **CT-D12** (taken 2026-10-07, §19) signable-action catalog registration.
- [x] **CT-D13** (taken 2026-10-07, §19) remaining-attempts disclosure.
- [x] **CT-D14** (taken 2026-10-07, §19) requests vs workflow tasks.
- [x] **CT-D15** (taken 2026-10-07, §19) sample approval setting tenant-only.
- [x] Meaning editor slim exception (ASSUMPTION-0022-03) — accepted by the CT.
- [x] ESIGN-BUNDLE v1 proposal accepted by CT 2026-10-07 (frozen in E2).
- [ ] ≥ 4 failure paths (25 in §13). ✔
- [ ] Part 11 / Annex 11 mapping reviewed by QA (§19) — OPEN, owner/QA action before GxP go-live (not a dev blocker). Every "not met" item has an owner (slice or decision).
- [x] **Owner approval → `ready-for-dev`** (Ali, 2026-10-07; tenant shell `/account/login` confirmed).

## 19. Implementation Notes

### Owner decisions (2026-10-07) — DECISIONS

| ID | Decision | Where in this pack |
|---|---|---|
| **KS-0** | Every e-signature screen is in the **tenant shell** (`/account/login`, `_LayoutTenantShell`). No route starts with `/Platform/`. All keys Scope = Tenant. Admin screens (policy, meanings, sample approvals) sit under "Kiracı Ayarları". "Platform Shared Services" is only the domain name: code in Diten.Platform + AuthService, screens in the tenant shell (FU23 `/DocumentManagementMasterRegister` pattern). | §8 Scope, §9, §11.1, §14, §16 E5 sabotage |
| **KS-1** | We build central e-signature ourselves; one service every module uses. | §1, §2 |
| **KS-2** | Tenant-based service. A record carries the signed object's company (LegalEntity). Visibility is limited to who can see the object. Meanings are tenant-based. Policy = tenant default + per-company override. | §4 `LegalEntityId`, §8 visibility, policy |
| **KS-3** | (A) Consecutive signatures: password **every signature** by default. Policy may choose "once within a window" (1–60 min). | §8 KS-3 window, E1 `esign/ticket`, CT-D7 |
| **KS-4** | (B) "İmza Talepleri" is a separate screen, in addition to WorkCenter. | §11.1 ESIGN_REQUESTS, CT-D14 |
| **KS-5** | (C) Per-company policy is in the first release. | §4 policy, §11.3 |
| **KS-6** | (D) Export PDF + CSV; every export written to audit. | §8 export, V24, §16 E2, CT-D11 |
| **KS-7** | (E) A sample is used only after manager approval. Users cannot approve their own. Policy can switch approval off. | §8 samples, V21, CT-D5, CT-D15 |
| **KS-8** | (F) Visual signature policy per meaning/action: Yok · İsteğe bağlı · Kayıtlı imza kabul · Zorunlu: o an çiz. | §4 `VisualMode`, §8 step 4, V6 |
| **KS-9** | (G) Drawings PNG + vector. Uploads PNG/JPG → PNG, max 1 MB. | §8 images, V8–V9 |
| **KS-10** | Visual signature is **appearance**. Legal bond = password re-authentication + meaning. A stored image is never attached without password verification. The image (or its hash) is in the record, immutable, inside the verification digest. | §1, §8 digest, V3, V7 |
| **KS-11** | No second factor in this release; policy shows "yakında" (disabled); extension point = existing e-mail MFA. | §4 `SecondFactor`, V17, §8 policy |
| **KS-12** | 7 languages (en, tr, fr, es, zh, ar, ru); the mockup's de/it are examples only. | §4 meanings, §11.6, V19, §21.3 |
| **KS-13** | Lockout uses the AuthService login lockout setting; the mockup's "3" is not a separate number. **Measured:** `TenantLoginSettings.MaxFailedLoginAttempts = 5`, `LockoutDurationMinutes = 15` (`services/Diten.Platform/src/Diten.Platform.Domain/Entities/TenantLoginSettings.cs:25-26`), per tenant, validated 1–20 / 1–1440; applied by `User.RecordLoginFailure` (`services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/User.cs:131-138`). | §8 E1, V27–V28, §16 E1, §21.3 |

### KS trace table (prompt "DOĞRULA": each KS in the pack)

| KS | Sections |
|---|---|
| KS-0 | §8 Scope · §9 · §11.1 · §14 · §15 · §16 E5 · §17 |
| KS-1 | §1 · §2 |
| KS-2 | §4 · §8 (visibility, policy) · §11.3 (company filter/badge) |
| KS-3 | §4 policy · §8 E1 + KS-3 window · V3 · CT-D7 |
| KS-4 | §8 requests · §11.1 · CT-D14 |
| KS-5 | §4 policy · §8 policy · §11.3 |
| KS-6 | §8 export · §11.3 · §13 #24 · CT-D11 |
| KS-7 | §8 samples · V21 · §13 #15 · CT-D5 · CT-D15 |
| KS-8 | §4 · §8 step 4 · V6 · §13 #9 |
| KS-9 | §8 images · V8–V10 · §13 #23 · CT-D4 |
| KS-10 | §1 · §8 digest + samples · V3/V7 · §11.3 İmzam statement |
| KS-11 | §4 · §8 policy · V17 · §11.3 "Yakında" |
| KS-12 | §4 meanings · §11.6 · V19 · §13 #22 · §16 L10n |
| KS-13 | §8 E1 · V27–V28 · §13 #10 · §16 E1 · §21.3 #1–#2 |

### Mockup decisions (data-props) → pack

| Mockup prop (v2 `data-props`) | Default | Pack |
|---|---|---|
| `reauth` A · every / window | every | KS-3; `ReauthEvery = true` default |
| `requestsScreen` B | true | KS-4; ESIGN_REQUESTS |
| `companyOverride` C | true | KS-5 |
| `exportFormat` D · both/pdf/csv | both | KS-6 |
| `approvalReq` E | true | KS-7; `SampleApprovalRequired = true` |
| `drawNow` F | true | KS-8; `DrawRequired` exists |
| `storage` G · both/png/vector | both | KS-9; `PngAndVector` for drawings |
| `role` admin/user | admin | demo only; real behaviour from keys (§14) |

### Measured facts

| # | Fact | Evidence |
|---|---|---|
| F1 | Blueprint MOD-0022 = e-Signature Service, W-2, pages Signature Requests · Signature Verification Viewer · Signature Audit Export, ESIGN-BUNDLE "(signature envelope schema, signer identity binding, audit hooks, evidence link)", SoR "Signature envelopes, signer attestations, verification artifacts" | Blueprint 8.1 `Blueprint_Data` row 23 (cols A,B,C,D,F,G,J,L,M,AC) |
| F2 | HARD deps of MOD-0022: 0021, 0029, 0005, 0264 | `Dependencies_Normalized` rows 42–45 (E=HARD) |
| F3 | HARD consumers: 0112, 0195, 0233, 0238, 0358 | `Dependencies_Normalized` rows 224, 398, 478, 489, 1564 |
| F4 | MOD-0284, 0236, 0237, 0239 do **not** name e-Signature in their dependency gate; they are indirect (approval/evidence/signature binding text). MOD-0175 names "Lot/Batch/Serial; Workflow Designer" only; e-signature comes from DEC-INV-08 | `Blueprint_Data` rows 285, 237, 238, 240 (col H/L); row 176 (H) |
| F5 | ESIGN-BUNDLE in the contract dictionary | `Contract Bundle Dictionary` row 87 |
| F6 | SoR "Signature envelopes" also declared by MOD-0264; SoR_Map collision count 0 | `Blueprint_Data` row 265 col M/V; `SoR_Map` row 107 |
| F7 | Wave mismatch W-2 vs W-4 | `Blueprint_Data` row 23 G; `Module Pages` rows 66–68 F |
| F8 | MOD-0022 absent in registry | `execution/registries/module-id-registry.md` (0 hits; :88–97) |
| F9 | Tenant/platform tokens differ only by `actor_type` | `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs:32-53,108-135`; `…/Api/appsettings.json:47-48` |
| F10 | No `jti` / `sid` / `auth_time` / `amr` in access tokens; access tokens not checked against the refresh store | `TokenService.cs`; `RefreshTokenRepository.cs:16`; `RefreshToken.cs:30-46` |
| F11 | No re-auth / step-up concept exists in AuthService | grep `Reauth|StepUp|VerifyPassword|esign` in `services/Diten.AuthService` → none |
| F12 | Password verify call sites: login, platform login, change, forced change ×2 | `LoginCommandHandler.cs:90`; `PlatformLoginCommandHandler.cs:57`; `ChangePasswordCommandHandler.cs:45`; `ForcedChangeTenantPasswordCommandHandler.cs:76`; `PlatformAuthController.cs:213` |
| F13 | Wrong current password in change-password does not count toward lockout | `ChangePasswordCommandHandler.cs:45-46` |
| F14 | Lockout counter not reset when a lockout expires (first wrong password after expiry locks again) | `User.cs:131-138` vs `:119-124` |
| F15 | MFA challenge: HMAC-hashed ids, constant-time compare, TTL, but consume is not atomic | `MfaChallenge.cs:31-69`; `MfaChallengeService.cs:96-135`; `MfaChallengeRepository.cs:27-35`; `MongoDbIndexConfigurations.cs:110-119` |
| F16 | AuthService audits to its own `authAuditLogs`, not to `audit_events` | `…/Persistence/Repositories/AuthAuditService.cs:13`; `AuthAuditLog.cs:17-21` |
| F17 | Platform validates JWTs locally; no introspection | `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs:72-100` |
| F18 | Internal S2S auth = `X-Internal-Api-Key` | `…/AuthService.Infrastructure/Services/InternalEventAuthService.cs:55-63` |
| F19 | Users carry no LegalEntity | grep `LegalEntity|CompanyId` in AuthService src → none |
| F20 | FU23 re-auth is an unverified opaque string; 2FA → 501 | `…/DocumentSignatureService.cs:152-171` |
| F21 | FU23 record is append-only by convention only (Replace + inherited soft delete) | `DocumentManagementElectronicSignatureRepositories.cs:80-84,107-114`; `Repositories.cs:22-46` |
| F22 | FU23 canonical form + SHA-256 | `DocumentSignableSubjectResolver.cs:399-442` |
| F23 | FU23 keys are PlatformAdmin, SuperAdmin-only; the audit's "not seeded" note is stale | `DataSeeder.cs:797-803`; `DefaultRolePermissionTemplate.cs:25-26,44-45`; `Mod0029Fu29PermissionSeedHardeningTests.cs:9-18`; audit `…fu23…2026-07-21.md:113-121` |
| F24 | FU23 web: tenant shell, read + verify only, no sign dialog anywhere | `frontend/Diten.Web/Controllers/DocumentManagementMasterRegisterController.cs:17-18,654-679`; `Views/DocumentManagement/MasterRegister/Details.cshtml:9` |
| F25 | Content storage = `IContentStorageGateway`, local disk only, `%TEMP%` default root, `.png` allowed, media type not enforced | `IContentStorageGateway.cs:33-106`; `LocalFileSystemContentStorageGateway.cs:30,52-61,277-286`; `ContentStorageOptions.cs:15-31` |
| F26 | No image library in Platform; PDF library only in CrmService | csproj package lists; `Diten.CrmService.Infrastructure.csproj:13` |
| F27 | Audit events immutable (`ValidateAppend`), outbox write, no hash chain | `AuditEvent.cs:7,47-123`; `AuditEventRepository.cs:20-26`; `AuditService.cs:40,89` |
| F28 | Approve DTO and safe additive pattern | `WorkflowModels.cs:325-330`, precedent `:252-253` |
| F29 | WorkCenter approve = same MediatR command, actor from JWT | `WorkflowApprovalWorkItemActionDispatcher.cs:59-67,89-94`; `WorkItemsController.cs:175-263` |
| F30 | Workflow designer = tenant shell on `/Platform` route; keys PlatformAdmin; tenant Admin can publish via entitlement; custom roles cannot | §8 E4 measurement (1)–(7) |
| F31 | Kiracı Ayarları = manifest `tenant-settings` (baseline), pages `/TenantSecurity`, `/TenantNavigation` | `TenantSettingsManifestProvider.cs:18,22-35`; `SharedResource.tr.resx:590` |
| F32 | Manifest record supports `IsBaseline` and `ApiPermissions` (Scope default "Tenant") | `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.ModuleRegistration.Abstractions/ModuleManifestDocument.cs:15-34,65-68` |
| F33 | `ContentStorageScope` is additive by design | `IContentStorageGateway.cs:60-80` |
| F34 | No existing `ESignature` route / `esign.` key | grep in `frontend/Diten.Web/Controllers`, `services` |
| F35 | Quarantine mockup has a 2-step decide → e-sign panel, four-eyes prop, meanings "Onaylıyorum / İnceledim" | "Karantina Bloke Stok (offline).html" template script: `openDecide`/`openDisp` (lines 99–100), steps 257–260, meanings 213; `data-props` eSign/fourEyes |

### Open CT decisions (prompt §19 list + found while measuring)

| # | Question | Options | Recommendation |
|---|---|---|---|
| **CT-D1** | Ticket lifetime | 60 s · **120 s** · 5 min (MFA uses 1–15 min, `MfaChallengeService.cs:43`) | 120 s, config `ESign:TicketLifetimeSeconds` (30–300). One ticket = one action. |
| **CT-D2** | Canonical record representation | (a) **provider registry + FU23 canonical JSON + `module`** (§8); (b) caller sends hash (untrusted); (c) hash of the consumer's full document | (a). The provider's field list is part of each consumer's pack. Golden test vectors are in the contract. |
| **CT-D3** | FU23 migration time | before MOD-0175 · **after MOD-0175 (MVP-1 tail)** · post-MVP-1 | after MOD-0175. FU23 works unchanged until then (§8 E6). |
| **CT-D4** | File storage | `IContentStorageGateway` + **new scope `ESignatureImages`** (additive) + partition `tenant/{T}/esign-*`; samples `CompanyId = Guid.Empty` | Accept. **A `ContentStorage:RootPath` must be configured** in GxP tenants: the `%TEMP%` default (`LocalFileSystemContentStorageGateway.cs:277-286`) is not acceptable for signature evidence. |
| **CT-D5** | Who approves samples | holders of `esign.samples.approve` (tenant Admin by default) · a named "e-İmza yöneticisi" role | the key; tenant Admin by default; never the owner (server rule). Every decision is audited. |
| **CT-D6** | Effect of the designer measurement on MOD-0175 | (i) MOD-0175 seeds the quarantine template, tenant Admin publishes once; (ii) the tenant Admin authors it | (i). No designer shell/scope change (out of scope). |
| **CT-D7** | Window mode vs Part 11 §11.200(a)(1)(i): within a continuous session later signings need **at least one** signature component. In window mode the mockup asks for none (user name comes from the session). | (a) keep the mockup (risk accepted by QA); (b) in window mode the user re-types the user name; (c) remove the window mode | (b). Low cost, keeps KS-3. Default "every signature" is unaffected. |
| **CT-D8** | How admin pages appear under "Kiracı Ayarları" (a nav group = a manifest module) | (a) add three nav pages to `TenantSettingsManifestProvider` pointing to `/ESignature/Settings/*` with `esign.*` keys (cross-manifest key use — completeness test to adjust); (b) separate "e-İmza" module only (deviates from KS-0 wording); (c) a "nav group alias" in the manifest standard | (a). Owner = Platform for both manifests. |
| **CT-D9** | Ticket mechanism | **opaque + internal atomic consume** · signed JWT (own audience) + jti store in Platform | opaque (§8). |
| **CT-D10** | Entitlement + default grants | `IsBaseline: true` (like tenant-settings) + `sign` / `my-samples.manage` to every default role · entitlement-gated module | baseline + default grants |
| **CT-D11** | PDF engine (KS-6) | add `PDFsharp-MigraDoc 6.1.1` to Platform (already in CrmService; configuration-safety review) · browser print-to-PDF | server-side MigraDoc, so the file hash is recorded and the output is identical for every viewer |
| **CT-D12** | Signable-action catalog | internal register endpoint (`POST api/internal/esign/actions/register`, like module-catalog) · extend `ModuleManifestDocument` (standard change, `.antigravity` approval) | internal endpoint |
| **CT-D13** | Show "Kalan deneme: n" (mockup) | show · hide (login does not show it) | show (mockup); low risk, since the user is already authenticated |
| **CT-D14** | Workflow tasks needing a signature also as requests? | no (WorkCenter only) · yes (mirror) | no. The requests screen = module/ad-hoc requests. |
| **CT-D15** | `SampleApprovalRequired` per company? (mockup has it in every scope) | tenant only · per company | tenant only. A sample belongs to a user, not a company (§21.3). |

### Part 11 / Annex 11 mapping (design mapping — **not a compliance claim**)

| Requirement | Design element | Status | Owner |
|---|---|---|---|
| §11.50(a)(1)–(3) printed name, date/time, meaning | `SignerNameAtSign`, `SignedAtUtc` (server), `MeaningTextAtSign` | designed | E2 |
| §11.50(b) same items in display / printout | dialog success, verification viewer, PDF/CSV export | designed | E2/E5 |
| §11.70 signature linked to record, cannot be excised / copied / transferred | `SubjectHash` + `SignatureDigest` (incl. image hash, context); insert-only; one signature binds one workflow task; image is appearance only (KS-10) | designed | E2/E4 |
| §11.100(a) unique to one individual, not reused / reassigned | signer = JWT `sub`; users unique per tenant (`{Email,TenantId}` index `MongoDbIndexConfigurations.cs:153-160`) | partly; **user re-use / reassignment rules not measured** | follow-up (MOD-0018) |
| §11.100(b) identity verified before assignment | organisational procedure | **not met by software** | tenant SOP (out of scope) |
| §11.100(c) certification to the agency | organisational | **not met by software** | tenant (out of scope) |
| §11.200(a)(1) two distinct components (ID + password) | user ID from session (read-only) + password re-entry | designed | E1/E5 |
| §11.200(a)(1)(i) continuous session: later signings ≥ 1 component | "every signature" default ✔; window mode ✘ as mocked | **open** | CT-D7 |
| §11.200(a)(1)(ii) not continuous: all components | session timeout → re-login + password | designed | E1/E5 |
| §11.200(a)(2) only the genuine owner | ticket bound to user + tenant + intent; no service-key signing | designed | E1/E2 |
| §11.200(a)(3) use by others needs two people | BCrypt hashes; admins cannot read passwords | designed (existing) | — |
| §11.300(a) unique ID/password combination | AuthService users | existing | — |
| §11.300(b) periodic checking / aging | password policy exists (`ChangePasswordCommandHandler.cs:48`); aging not measured | **open** | follow-up (MOD-0018) |
| §11.300(c) loss management for tokens / devices | no devices / tokens in v1 | not applicable v1 | — |
| §11.300(d) prevent / detect / report unauthorised use | shared lockout + `authAuditLogs` events | detect ✔; **urgent reporting to security not built** | follow-up (notification on `lockout_started`) |
| §11.300(e) device testing | N/A | — | — |
| Annex 11 §14(a) same impact as a handwritten signature within the company | dialog statement + tenant SOP | designed (UI) / SOP | E5 / tenant |
| Annex 11 §14(b) permanently linked to the record | digests + insert-only | designed | E2 |
| Annex 11 §14(c) time and date | server UTC, displayed in tenant time zone with offset | designed | E2/E5 |
| Annex 11 §9 / §11.10(e) audit trail | MOD-0021 events for every command; export log | existing + designed | E2 |

### CT decisions — TAKEN 2026-10-07 (CT acceptance of WP-0022-P0)

| # | Decision |
|---|---|
| CT-D1 | 120 s, config `ESign:TicketLifetimeSeconds` (30–300); one ticket = one action. |
| CT-D2 | (a) provider registry + FU23 canonical JSON + `module`; each consumer pack lists its fields; golden vectors in the contract. |
| CT-D3 | FU23 migration (E6) after MOD-0175, MVP-1 tail; FU23 unchanged until then. |
| CT-D4 | `IContentStorageGateway` + additive scope `ESignatureImages`, partition `tenant/{T}/esign-*`; **a configured `ContentStorage:RootPath` is a go-live precondition for a GxP tenant** (the `%TEMP%` default is not acceptable) → FU-0022-RootPath. |
| CT-D5 | Key `esign.samples.approve` (tenant Admin by default); never the sample owner (server rule); every decision audited. |
| CT-D6 | (i) MOD-0175 seeds the quarantine template; tenant Admin publishes it once. No designer shell/scope change. |
| CT-D7 | **(b)** in window mode the signer re-types the user name (one signature component, §11.200(a)(1)(i)); default "every signature" unchanged. |
| CT-D8 | **(b) — CT overrides the recommendation (a):** own nav group / module "e-İmza" holding the admin pages too (permission-gated), NOT pages in `TenantSettingsManifestProvider`. Reason: MOD-0174 measured (FU-0174-CAT-01) that the catalog sync re-attributes a key's `Module` to the last manifest that declares it — `esign.*` keys on the tenant-settings manifest would move to `tenant-settings`. KS-0's "Kiracı Ayarları altında" wording is superseded until FU-0174-CAT-02 (Platform guard) exists; then a nav alias can be revisited. Every `esign.*` key is declared by the MOD-0022 manifest only (namespace test, MOD-0174 pattern). |
| CT-D9 | Opaque ticket + internal atomic consume (§8). |
| CT-D10 | Baseline module (`IsBaseline: true`) + default grants `esign.signatures.sign` and `esign.my-samples.manage` to the default roles (signing alone authorises nothing; each consumer action keeps its own key). |
| CT-D11 | Server-side PDFsharp-MigraDoc 6.1.1 in Platform (already in CrmService); configuration-safety review in E2/E5; output file hash recorded. |
| CT-D12 | Internal register endpoint `POST api/internal/esign/actions/register` (no `ModuleManifestDocument` change). |
| CT-D13 | Show remaining attempts (signer is already authenticated); value from the tenant login settings (KS-13: 5 / 15 min). |
| CT-D14 | No mirror: workflow tasks needing a signature stay in WorkCenter; the requests screen = module / ad-hoc requests. |
| CT-D15 | `SampleApprovalRequired` tenant-only (a sample belongs to a user, not a company). |

Registry row written by the CT on acceptance (`module-id-registry.md`, status `reserved / planned` → `in-progress` at
ready-for-dev); waiver **W-0022-01** (DCP-002 verifier not run — no Python; run before the MVP-1 PR, W-0173-01 pattern).

### E1 — implementation note (WP-0022-E1, 2026-10-07; CT-ACCEPTED 2026-10-08)

**CT acceptance (K13):** re-measured at `66f97c69c` — `Diten.AuthService.Application.Tests` 1039/1039 (owner fleet PIDs identical before/after; `AccountKindAcceptanceHost.Tests` not run). CT sabotage: window revocation on password change removed (`ChangePasswordCommandHandler.cs:59`) → 1 red, restored. CT fixes: internal consume path corrected to `internal/esign-tickets/consume` in this pack (§1/§3/§8/§15) and ESIGN-BUNDLE §4.3; 423-on-lockout-start written into ESIGN-BUNDLE §4.1. **CT decision FU-0022-12:** outside Development `ESign:HashSecret` is REQUIRED (no fallback to `JwtSettings:Secret`; missing → 503 `ESIGN_REAUTH_UNAVAILABLE`, fail closed) — implemented in E2 together with the configuration-safety review. FU-0022-05 (MFA consume not atomic) stays a follow-up for the AuthService owner.

- Branch `feature/sce/mod-0022-e1` from `cd43a4533`. Commit `1450b5408` (AuthService + 16 tests); local only, no push.
  Evidence: `docs/records/audits/2026-10/mod-0022-e1-evidence-2026-10-07.md`.
- Test counts:
  - `Diten.AuthService.Application.Tests`: 1023 → **1039**
  - architecture: 18/18
  - build: 0 errors, the same 6 pre-existing warnings
- §16 E1: every item **PASS**. Three sabotages red, and the diff is empty after restore. Runtime probe 27/27 against
  the real Platform login settings (5 / 15). Owner fleet PIDs unchanged.
- **Route deviation (E2 / contract must follow):** the consume endpoint is `POST internal/esign-tickets/consume`, not
  `api/internal/...`.
  - Every AuthService internal controller uses `internal/...`.
  - `TenantResolutionMiddleware.cs:114` exempts only `/internal`.
  - Fix §1, §3, §8, §15 and ESIGN-BUNDLE §4.3 before the bundle is frozen.
- The attempt that **starts** the lockout answers 423 (`remainingAttempts=0`), not 401. ESIGN-BUNDLE §4.1 should say so.
- Ticket hash key: `ESign:HashSecret`, falling back to `JwtSettings:Secret` with a purpose prefix (the
  `RefreshTokenHasher` precedent). A separate secret is recommended for production (proposed FU-0022-12).
- The MFA consume is still non-atomic (FU-0022-05). E1 did not copy it and did not touch MFA.
- `SensitiveDataLogEventEnricher` does not mask a secret embedded in a string property (sabotage c). The log-capture
  test is the guard.
- KS-11 second factor: extension point only (`ESignReauthKind`), no flow; FU-0022-01.

### E2 — implementation note (WP-0022-E2, 2026-10-08; CT-ACCEPTED 2026-10-08)

**CT acceptance (K13):** re-measured at `39a847d8a` — Platform `~ESign` filter 80/80 (needs `DITEN_TEST_MONGOD=<mongod.exe>`; without it the replica-set fixture fails 20 tests at start-up — environment, not code), BackgroundJobs 22/22, architecture 18/18; AuthService change limited to the FU-0022-12 HashSecret rule. CT sabotage: `DrawRequired` appearance rule loosened (`ESignatureRules.cs:28`) → 1 red, restored. CT decisions: **CT-D10 → E3 scope** (default-role grants of `esign.signatures.sign` + `esign.my-samples.manage` in AuthService `DefaultRolePermissionTemplate`); envelope field `reason_code` (contract states the real field) accepted; global signable-action catalog accepted (policy rows stay tenant-scoped); nav pages hidden until E5 accepted; ticket SHA-256 in Platform records accepted (high-entropy random ticket; AuthService keeps its HMAC); export dates UTC (FU-0022-TZ: tenant time zone not modelled); FU23 subjects without LegalEntity (BL-432) accepted; Windows `--nounixsocket` fix in the shared test harness accepted.

- Branch `feature/sce/mod-0022-e2` from `2bdedbb26`. Commits `90024eab4` (ESIGN-BUNDLE 1.0.0 FROZEN, single writer) and
  `a7508d583` (Platform e-signature service + AuthService FU-0022-12); local only, no push.
  Evidence: `docs/records/audits/2026-10/mod-0022-e2-evidence-2026-10-08.md`.
- Tests: Platform filtered 489 → **564** (+75 ESign); AuthService 1039 → **1045** (+6 FU-0022-12); BackgroundJobs 22/22;
  architecture 18/18; build 0 errors, the same unique warnings as the base.
- §16 E2: every item **PASS**. Five sabotages red (skip ticket consume, no key ordering, `UpdateAsync` on the record
  store, `/Platform/ESignature` route, HashSecret fallback in Production), diff empty after restore. Runtime probe
  23/23 (real AuthService + Platform, own remote subject provider, PDF export hash = log row). Owner fleet unchanged.
- **OPEN for the CT — CT-D10 default grants:** the grant of `esign.signatures.sign` / `esign.my-samples.manage` to the
  default tenant roles lives in AuthService's role template (`DefaultRolePermissionTemplate`), outside this slice's
  allowed diff. Proposed one-line change: `AdminModules += "esign"`, `TenantSelfServicePermissions +=` the two keys.
- Deviations recorded in the contract (`x-implementation-notes`) and the evidence:
  - the Platform envelope carries the code in `reason_code` (the proposal wrote `errorCode`);
  - the signable-action catalog (§4.23) is global — the request has no tenant; policy rows stay tenant-scoped;
  - manifest pages are declared but hidden from the menu until E5 builds them (`PagesNavigable = false`);
  - signature times are truncated to the stored millisecond before hashing (else every record verified as tampered);
  - a duplicate key inside a Mongo transaction aborts it, so it surfaces as `ESignatureDuplicateException`;
  - `TicketRef` = SHA-256 of the ticket (Platform does not hold AuthService's HMAC key);
  - export dates in UTC (no tenant time zone modelled); FU23 provider `legalEntityId` null (BL-432).
- `DisposableMongoReplicaSet` (Platform test harness) no longer passes `--nounixsocket` on Windows, where mongod
  rejects it — the harness could not start on this machine before.

### E3 — implementation note (WP-0022-E3, 2026-10-08; CT-ACCEPTED 2026-10-08)

**CT acceptance (K13):** re-measured at `0c5ac78de` — Platform `~ESign` 106/106 (`DITEN_TEST_MONGOD` set), AuthService role tests 309/309; AuthService change = `DefaultRolePermissionTemplate` + its test only. CT sabotage: used/active-sample withdraw guard disabled (`SampleHandlers.cs:435`) → 1 red, restored. CT decisions: **CT-D10 existing tenants = (b)** — no automatic grant to roles that already exist (an automatic grant to existing roles is a silent privilege change); the tenant administrator grants `esign.signatures.sign` / `esign.my-samples.manage` on the role screen (the keys are Tenant-scoped and assignable); new tenants get them from the template. `esign_sample_owner_states` (single-Active guard) accepted; the CRLF-robust contract-test reader (E2 latent defect) accepted; the commit-message count (16 vs 12) is corrected by the evidence.

- Branch `feature/sce/mod-0022-e3` from `48479faf9`. Commit `5452b9dfe` (samples + approval, E2 Sample port, CT-D10); local
  only, no push. Evidence: `docs/records/audits/2026-10/mod-0022-e3-evidence-2026-10-08.md`. Contract unchanged.
- Tests: Platform filtered 154/156 → **182/182** (+12 sample HTTP, +14 sample rules; the 2 base reds were the E2 contract
  test reading a CRLF checkout — fixed line-ending agnostic); AuthService 1045 → **1062** (+17 CT-D10); architecture 18/18;
  build 0 errors, the same unique warnings as the base.
- §16 E3: every item **PASS**. Four sabotages red (owner may approve own sample, window ticket saves a sample, no sample
  ownership check at signing, a management key in the default signer set), diff empty after restore. Runtime probe 23/23
  (A saves → self-approval 403 → B approves → A signs with the Sample → verify valid; CT-D10 on a new tenant). Fleet unchanged.
- **CT-D10:** `AdminModules += "esign"` (Admin holds all 15 `esign.*`, incl. `samples.approve` — CT-D5); exact
  `DefaultSignerPermissions = { esign.signatures.sign, esign.my-samples.manage }` added to Viewer; every other grant of
  every default role unchanged (test). **OPEN for the CT:** tenants provisioned BEFORE this change do not receive the keys
  automatically (the startup reconciler backfills only the self-service keys); options: extend the reconciler, or the
  tenant admin grants them (all `esign.*` are Tenant-scoped and assignable).
- One Active sample per owner is guarded by a compare-and-set owner pointer (`esign_sample_owner_states`) inside the same
  transaction as the insert-only sample events; a parallel second decision on one sample hits the unique
  (sample, sequence) index and gets 409.

### Registry row proposal (CT writes it)

`| MOD-0022 | e-Signature Service | e-signature-service | Module | reserved / planned |  |  | platform-shared-services | Blueprint 8.1 Blueprint_Data row 23 (W-2; Module Pages rows 66–68 say W-4). ESIGN-BUNDLE. SoR: signature envelopes, signer attestations, verification artifacts. DCP-002 verifier pending (waiver W-0022-01, no Python). Pack: execution/domains/platform-shared-services/module-packs/MOD-0022-e-signature-service.md (draft 2026-10-07). |`

Also: add MOD-0022 to `domain-config.md` in-scope (:12–14). Add an implementation-status row in
`execution/registries/module-implementation-status.md` (module-pack-standard :456-462).

### Assumptions

- **ASSUMPTION-0022-01:** branch. The prompt fixes the working branch `feature/sce/mod-0022-e-signature` (owner decision
  2026-10-07, MVP-1 branch). The AGENTS.md §9 convention would be `feature/pss/mod-0022-e-signature-service`; the owner
  decision wins.
- **ASSUMPTION-0022-02:** UI folders `Views/ESignature/**`, `js/ESignature/**`, controller `ESignatureController`.
- **ASSUMPTION-0022-03:** meaning editor 10 fields stays a modal (7 names = one localized group); exception to the
  slim ≤ 8 rule.
- **ASSUMPTION-0022-04:** the Platform → AuthService consume call is synchronous; if AuthService is down, signing fails
  closed (503 `ESIGN_REAUTH_UNAVAILABLE`).
- **ASSUMPTION-0022-05:** workflow subjects without a provider fall back to `workflow-task` + `displayContext`.
- **ASSUMPTION-0022-06:** export limit 10 000 rows.
- **ASSUMPTION-0022-07:** signature numbers are per tenant per year, generated by an atomic counter document (same as
  other Platform numbering).

### WP-MVP1-INT-QE — INT-ESIGN (CT-ACCEPTED 2026-10-08)

**CT acceptance (K13, 2026-10-08):** gateway tests 101/102 re-run at `75c981df6` (only the known FU-INT1-01 red); ocelot diff = additions only; CT sabotage: quarantine `{everything}` block port 5066 → 5067 → 3 red, restored. Body limit (Kestrel default 30 MB) sufficient for the 1 MB PNG; `/internal/*` stays unrouted (404).

Branch `feature/sce/mvp1-int-qe` from `720a58ede` (isolated worktree), commit `ae2055739`: four ocelot blocks after
`/api/trace/{everything}`, cloned from the INT-TRACE blocks (only path / port / methods changed, no gateway auth, headers passed
through): `/api/quarantine[/{everything}]` → 5066 GET/POST/PATCH/PUT/OPTIONS and `/api/v1/esign[/{everything}]` → 5057
GET/POST/PUT/OPTIONS (the contracts' methods; no DELETE in either). +74 lines, no existing line changed. Gateway tests 88/89 →
**101/102** (13 new green; the known red FU-INT1-01 grows by exactly the two 5066 lines, 128 → 130); architecture 18/18. Every
contract operation is matched by exactly one route (tests); K3 port / narrowed upstream → red, restored clean. Runtime (own
gateways on 15290 / 15291): requests reach `localhost:5066/api/quarantine/…` and `localhost:5057/api/v1/esign/…` (gateway log);
1 MiB PNG as base64 (1 398 223 B) and 2× forwarded byte-identical with all pass-through headers; 31 MB → 413 (Kestrel default
30 000 000, sufficient). Evidence `docs/records/audits/2026-10/mvp1-int-qe-evidence-2026-10-08.md`.
Re-auth / ticket (`/api/tenant-auth/esign/*`) go through the existing `/api/tenant-auth/{everything}` route (5056) — no new route,
measured; `/internal/esign-tickets/consume` and `/api/internal/esign/actions/register` stay off the gateway (no route, 404).

### E4 — implementation note (WP-0022-E4, 2026-10-08; agent result, pending CT)

- Branch `feature/sce/mod-0022-e4` from `c31967db8` (isolated worktree). Commit `60ad5fe62` (Platform only; contract unchanged —
  implements `x-workflow-addendum`); local only, no push. Evidence: `docs/records/audits/2026-10/mod-0022-e4-evidence-2026-10-08.md`.
- Step `requirements.signatureRequired / signatureMeaning` → `WorkflowRuntimeStep` → `ApprovalTask` (first + next step); publish
  refuses a flagged step without an active meaning (400 `ESIGN_MEANING_NOT_ALLOWED`). Gate in `TransitionAsync` after replay →
  state → assignment → actor → SoD: no id → 409 `ESIGN_SIGNATURE_REQUIRED`; `IESignatureGate` (signer = actor, meaning, context
  task / instance / decision, subject = `workflow/workflow-task/{taskId}` or the instance object, signed after the task, verify
  `valid`, single use on the transition log) → else 409 `ESIGN_SIGNATURE_MISMATCH`; no gate → fail closed. API and WorkCenter
  dispatcher share the handler. `SignatureId` on the transition log + history; WorkCenter approve action carries
  `signatureRequired / signatureMeaning`; `workflow-task` provider (ASSUMPTION-0022-05). Reject unchanged (ASSUMPTION-0022-E4-02).
- Tests: Platform filtered 558 → **580/580** (+22 `WorkflowESignatureGateTests`, real HTTP host + Mongo); CRM 2109/5/2114
  unchanged; architecture 18/18; no new build warning. K3: (a) WorkCenter-only bypass, (b) signer ≠ actor, (c) single use, (d)
  subject match, (e) gate call removed → red, restored clean. Runtime probe (own AuthService + Platform, own mongod, login + real
  re-auth) **21/21**. Owner fleet untouched.
- **OPEN for the CT:** O-E4-1 — E2's unique index "one signature per workflow task" blocks re-signing a task after a wrong /
  invalidated signature (the task can then only be rejected / cancelled / time out); ASSUMPTION-0022-E4-01 (object subject binds
  by `subjectType == objectType`) to be confirmed with MOD-0175 Q3; designers + WorkCenter dialog = E5.
### E4 — CT acceptance (2026-10-08)

- **CT-ACCEPTED.** CT re-ran Platform filtered **580/580** and architecture **18/18** in the agent worktree; read the gate, the
  transition support, both dispatchers (the HTTP dispatcher strips `SignatureId`, so a remote provider cannot carry one) and the
  single-use query (tenant `ExecutionFilter`). CT sabotage (own, not in the agent set): drop the `decision == approve` check →
  `A_signature_for_another_decision_is_refused` red (1/22), restored, porcelain clean. Fast-forwarded into the MVP-1 branch.
- **CT-D16 (O-E4-1):** the unique index `ux_esign_records_tenant_workflow_task` stays for now (no schema change in an accepted
  slice); re-signing after an invalidated / wrong signature is **FU-0022-E4-01**, delivered in **E5** together with the
  signature dialog (index relaxed so only the bound signature is unique; single use already enforced by the gate on the
  transition log + context task binding). Until then a stuck task is closed by reject / cancel.
- **CT-D17 (ASSUMPTION-0022-E4-01):** confirmed direction — MOD-0175 Q3 signs with `subjectType` = the workflow instance
  `objectType` of the quarantine decision (no mapping table); Q3 must prove it with a live sign → approve.
- ASSUMPTION-0022-E4-02 (reject needs no signature) accepted for MVP-1.

### E5W1 — implementation note (WP-0022-E5W1, 2026-10-09; agent result, pending CT)

- Branch: `feature/sce/mod-0022-e5w1` from `a9e669f26` (isolated worktree).
- Commit: `cfcf9e6c0`. Web only, plus the ESign manifest page flag. The contract is unchanged.
- Local only, no push.
- Evidence: `docs/records/audits/2026-10/mod-0022-e5w1-evidence-2026-10-09.md`, plus scripts and 11 screenshots.
- **Shared dialog** (`Views/Shared/ESignature/_ESignatureDialog|_SignaturePad|_SignatureZoom` +
  `wwwroot/assets/js/ESignature/esign-dialog|esign-pad|esign.l10n.js`):
  - API: `ESign.open({module, subjectType, subjectId, actionCode?, meaningCode?, requestId?, context?})` →
    `{signatureId, signatureNumber} | null`.
  - The host passes ids only. Summary, meanings, policy, visual mode and time come from `policy/effective`.
  - Flow: E1 reauth (or the window ticket with the re-typed user name, CT-D7) → E2 sign.
  - The password is emptied in a `finally` after every attempt and is never stored.
  - The pad uses Pointer Events and goes full screen landscape at 375 px.
- **İmzam:** `/ESignature/MySignature` (`ESIGN_MYSIGNATURE`, UAS-001), plus a user-menu link gated by `esign.my-samples.manage`.
- **Controller:** `ESignatureController`, `[Authorize]`. It proxies `api/**` to `/api/v1/esign/**` and `auth/reauth|ticket` to AuthService.
  The proxy never logs a body.
- **WorkCenter:**
  - A `signatureRequired` approve opens the dialog first and dispatches `payload.signatureId`.
  - Cancel sends nothing.
  - Unflagged items are unchanged.
- **Designers (form + visual):**
  - "İmza gerekli" plus an active meaning → `requirements.signatureRequired / signatureMeaning`.
  - The keys are written only when the switch is on.
  - The visual designer reads the values back.
  - The designer route is unchanged.
- **Manifest:** only `ESIGN_MYSIGNATURE` is nav-visible (the explicit `new ModuleManifestPage(` calls are parsed by the Web nav guard).
- **Tests:**

  | suite | before | after |
  |---|---|---|
  | Web | 619/622 | **630/632** |
  | Platform filtered | 580 | **581/581** |
  | architecture | 18 | **18/18** |

  - The 2 remaining Web failures are the pre-existing quarantine nav keys (MOD-0175 Q5 scope).
  - No new build warning.
- **K3 sabotages** — each went red and was restored clean:
  - (a) manifest route under `/Platform/` → red in Platform + Web;
  - (b) WorkCenter `signatureRequired` check removed → test red, and at runtime 409 `ESIGN_SIGNATURE_REQUIRED`;
  - (c) password clear removed → red.
- **Runtime probe** (own stack, browser):
  - The designer published the signature step and a reopen read it back.
  - A started the instance.
  - B approved: wrong password → 401 and the field was emptied; cancel → nothing was sent; drawing + password → sign 201 →
    approve 200, and the history shows `signatureId`.
  - The unsigned item approved without the dialog.
  - DrawRequired: the client refuses an empty pad; the API returns 409 `ESIGN_APPEARANCE_NOT_ALLOWED`.
  - İmzam: a sample was saved with the password (201).
  - The owner fleet was untouched.
- **OPEN for the CT:**
  - O-E5W1-1: the `workflow-task` provider's summary labels are English.
  - O-E5W1-2: the İmzam history table is DataTable v2 client mode without filter / SaveView.
  - O-E5W1-3: WorkCenter sends no `actionCode`, so the approve visual mode is Optional.
  - FU-0022-E4-01 is not in this part: it needs a schema change (E5B).
  - The Python DataTable verifier was not run (W-0022-01).

### E5W1 — CT acceptance (2026-10-09)

- **CT-ACCEPTED.** CT re-ran Web.Tests **630/632**, Platform filtered **581/581**, architecture **18/18** in the agent worktree. The 2
  Web reds are `NavManifestL10nGuardTests` for the quarantine pages (`Nav.Page.QUARANTINEQUEUE / BLOCKEDSTOCK / RETESTQUEUE /
  QUARANTINESETTINGS`, all 7 languages) — present on the MVP-1 branch since MOD-0175 Q1 (a CT miss: Web tests were not run at the
  Inventory acceptances) → MOD-0175 Q5 scope. Read the proxy controller: cookie token, tenant from the token, bodies never logged;
  CSRF covered by the `SameSite=Lax` auth cookies (`AuthTokenCookies.cs:124`). CT sabotage (own): UAS-001 gate of İmzam →
  `@if (false)` → `UAS_001_without_the_page_key_only_the_access_explanation_is_drawn` red, restored, porcelain clean. Screenshots
  01 / 07 reviewed against mockup v2. Touches outside §5 accepted: `WorkCenterNext/{Index,Details}.cshtml` (dialog partial),
  `mock-data.js` (action fields). §17.3 table written after coding (recorded in the evidence §0) — accepted once; next Web WPs
  must write it first. Fast-forwarded into the MVP-1 branch.
- **To E5W2:** O-E5W1-1 `workflow-task` summary labels localized (Platform provider, tr/en); O-E5W1-3 WorkCenter passes the
  step action code so the visual mode follows the policy; O-E5W1-2 İmzam history filters / saved views as the golden slim list;
  **CT-E5W1-N1** the menu shows the e-İmza item under the domain heading "Platform Paylaşılan Hizmetler" — CT-D8 wants the
  item in its own "e-İmza" group (measure the nav grouping; no domain name visible to the tenant user).

## 20. Follow-up Items

- MOD-0175 pack: consumes ESIGN-BUNDLE; quarantine signable actions (`inventory.quarantine.release / reject / block /
  dispose / unblock`, mockup `ACTS`); remote subject provider in InventoryService; template seed (CT-D6).
- FU-0022-E4-01: re-sign a workflow task after an invalidated / wrong-meaning signature — relax `ux_esign_records_tenant_workflow_task` (E5, CT-D16).
- FU-0022-01: second factor (KS-11) — e-mail OTP via the existing MFA challenge (`MfaChallengeService`).
- FU-0022-02: immediate notification to security on `tenant_esign_reauth_lockout_started` / `tenant_login_lockout_started`
  (§11.300(d)).
- FU-0022-03: lockout counter not reset after lockout expiry (`User.cs:131-138`); change-password wrong current
  password not counted (`ChangePasswordCommandHandler.cs:45-46`) — MOD-0018 owner.
- FU-0022-04: `ModulePageDescriptorNormalizer.cs:34-36` comment ("tenant gets 403 on /Platform") is false for
  Workflow / ReferenceData / PersonReferences (`ShellAccessFilter.cs:142-145`) — MOD-0023 / platform owner.
- FU-0022-05: MFA consume is not atomic (`MfaChallengeService.cs:113-114`) — MOD-0018 owner.
- FU-0022-06: FU23 audit doc statements on seeding are stale (`…fu23…2026-07-21.md:113-121`; comments
  `DocumentManagementSignaturesController.cs:34-36`, `…ElectronicSignatureModels.cs:9-13`).
- FU-0022-07: signature on workflow **reject**; signature requests visible in WorkCenter as work items.
- FU-0022-08: link policy rows to MOD-0005 Policy & Control Library (Blueprint HARD edge).
- FU-0022-09: external provider (MOD-0264) envelope reference.
- FU-0022-10: `docs/analysis/` is not one of the five folders of `docs-organization.md` (§1 :13-20). This contract draft
  follows the existing precedent `drafts/trace-v1.1-proposal.md`. The CT decides the long-term place.
- FU-0022-11: orphan image cleanup when a sign transaction fails after storing the PNG.

## 21. UI input — approved mockup

### 21.1 How it was read

- Files: "C:\Users\Ali\Downloads\e-İmza Servisi v2 (offline).html" (binding; covers v1) and "e-İmza Servisi
  (offline).html" (v1).
- Method:
  - Parse the `__bundler/template` JSON (no Python; Node).
  - Take the longest `<script type="text/x-dc">` block (v2: 633 lines of app logic) and the markup without scripts.
  - Read the `data-props` (A–G + role).
- v1 → v2 difference:
  - v1 has 6 tabs (Sign, Requests, Verification, Export, Policy, Meanings) and props A–D.
  - v2 adds "İmza Örneği Onayları", "İmzam", visual signatures, and props E–G.
- Quarantine consumer: "Karantina Bloke Stok (offline).html" (two-step decide → e-signature panel).

### 21.2 Screens → pack

| Mockup tab | Pack |
|---|---|
| İmza Penceresi | §11.1 dialog; §11.3 |
| İmza Talepleri | ESIGN_REQUESTS |
| Doğrulama | ESIGN_VERIFICATION |
| Denetim Dışa Aktarımı | ESIGN_AUDITEXPORT |
| e-İmza Politikası | ESIGN_POLICY |
| İmza Anlamları | ESIGN_MEANINGS |
| İmza Örneği Onayları | ESIGN_SAMPLEAPPROVALS |
| İmzam (+ user menu) | ESIGN_MYSIGNATURE |

### 21.3 Differences

| # | Mockup | Pack | Why |
|---|---|---|---|
| 1 | `MAXTRY = 3` ("Ard arda 3 hatalı giriş") | tenant `MaxFailedLoginAttempts` (default 5) | KS-13 (measured) |
| 2 | "Kilidi sistem yöneticisi açabilir" | "Hesabınız {n} dakika kilitlendi" from `LockoutDurationMinutes` (default 15); lockout expires by time | measured `User.cs:131-138`; an admin unlock path was not measured, so the text must not promise one |
| 3 | 8 languages incl. de, it; "8 dilde zorunlu", "{n}/8 dil" | 7 languages (en, tr, fr, es, zh, ar, ru); "{n}/7 dil" | KS-12 |
| 4 | one page with 8 tabs, breadcrumb "Sistem / e-İmza Servisi" | 7 pages + shared dialog; user pages in "e-İmza", admin pages in "Kiracı Ayarları" | KS-0; tab ≠ page (self-registration §2a) |
| 5 | "Mockup kararları", role switcher, demo session card, demo cases, demo password text, "Örnek taranmış imza ile dene" | not built | demo only |
| 6 | general settings (incl. sample approval) editable per company | sample approval tenant-only | CT-D15 |
| 7 | window mode: dialog asks only meaning + note | proposed: also re-type user name | CT-D7 (Part 11) |
| 8 | "21 CFR Part 11 · EU GMP Annex 11" header | kept as a reference label; no compliance claim anywhere | prompt "YAPMA" |
| 9 | client-side `hx()` hash (FNV demo) | server SHA-256 (§8) | demo only |
| 10 | quarantine mockup meanings "Onaylıyorum / İnceledim" | tenant meanings from MOD-0022 | KS-2, KS-12 |
