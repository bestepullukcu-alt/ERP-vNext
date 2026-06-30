---
id: MOD-0029-FU01
name: Controlled Document Versioning & Template Sharing Foundation
parent: MOD-0029
previous: MOD-0028-FU05
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: TenantScopedEntity
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0029-fu01-controlled-document-versioning-template-sharing-foundation
started: 2026-06-22
target: 2026-07-20
form_field_count: 10
---

# MOD-0029-FU01 - Controlled Document Versioning & Template Sharing Foundation

> Parent canonical module: **MOD-0029 — Controlled Documents (SOPs/Work Instructions)**.
> The parent module name is **never** renamed to "Document Lifecycle / Template Repository / Sharing"; those
> phrases are only capability/scope descriptions *inside* MOD-0029, not the canonical module name.
> The Blueprint-exact spelling is `Controlled Documents (SOPs/Work Instructions)` (no spaces around the slash);
> this exact string is the DCP-002 canonical authority and must match the Blueprint and the module-id registry.

### Scope clarification (parent name vs. FU01 capability)

The **parent canonical name is intentionally narrow because it is taken verbatim from the Blueprint**, where
MOD-0029 is registered as *Controlled Documents (SOPs/Work Instructions)*. The FU01 **scope is not limited to
SOPs and Work Instructions** — those are two of the supported `DocumentType` values, not the whole capability.

What FU01 actually delivers is **the first reusable document-control foundation under that parent**: a
`CollectionInstance`-attached **document/template repository, versioning, and controlled sharing foundation**.
Specifically:

- The implemented `DocumentType` set includes **SOP** and **Work Instruction**, but also **Policy**, **Form**,
  **Template**, and **Other** — FU01 is *not* a QMS-only SOP manager.
- The foundation is **used by the folder nodes created in MOD-0028** (FU04 manual structure builder /
  FU05 company `CollectionInstance` provisioning). **MOD-0028 owns the folder/tree/documentation structure;
  MOD-0029-FU01 owns the documents, templates, versions, shares, and access policies attached to those folder
  nodes.**
- The capability stays **governed and tenant/company-scoped** — it is a controlled document library, **not a
  consumer file drive**. **Generic, uncontrolled, public file sharing remains out of scope** (no
  public/anonymous sharing, no external portal — see §2 *Explicitly out of scope*).
- **Each LegalEntity/company attaches its own documents/templates to its own company-scoped
  `CollectionInstance` folder tree.** A company's documents never leak to another company unless **explicitly
  shared**.
- **Folder-level upload authorization:** a user can upload a document into a folder only where they hold
  folder-level upload permission for that `CollectionInstance` node.
- **Document-level access control:** view / download / edit-metadata / upload-new-version / share / manage-access
  are each permission-gated, per document or inherited from the folder. Backend authorization is **authoritative**;
  frontend hide/disable is only UX.

**Daily use is not limited to folder sharing.** The everyday flow is a legal entity / company user **adding,
versioning, and access-controlling its own documents and templates inside its own folder tree**; cross-company
**folder/branch sharing (with associated templates)** is one capability *on top of* that everyday document
library — not the only way documents enter the system. The access-control and folder-upload model below
applies to **normal in-company use first**, and to sharing second.

Product-facing labels for this foundation are **Controlled Documents**, **Document Library**, **Folder
Documents**, **Templates**, **Version History**, **Access Control**, **Upload to Folder**, and **Share with
Company** — not "SOP management". Renaming the parent MOD-0029 or changing its Blueprint-canonical name is
**prohibited**; this clarification only scopes what FU01 builds *under* that fixed parent name, and it adds
**no runtime scope beyond the approved FU01 scope** (it elaborates the already-approved attach / version /
share / permission behavior).

## 1. Module Summary

MOD-0029-FU01 is a **backend + TenantShell-frontend** foundation for `MOD-0029 Controlled Documents
(SOPs/Work Instructions)`. It is the first follow-up after the MOD-0028 documentation-structure family
(FU01 backend contract → FU02 baseline import → FU03 tenant baseline UI → FU04 manual structure builder →
FU05 company adoption / `CollectionInstance` provisioning).

MOD-0028-FU05 produces a company-scoped `CollectionInstance` folder tree. **FU01 of MOD-0029 attaches
controlled documents, SOPs, work instructions, and template files to those folder nodes**, makes them
**versionable** (immutable activated versions), and makes selected documents/templates and whole
folder/branches **shareable** to other companies/legal entities in the same tenant under a controlled
share policy (`REFERENCE` or `COPY_ON_ADOPT`).

| Concept | Meaning |
|---|---|
| `CollectionInstance` | Company-specific folder node from MOD-0028-FU05 (**consumed read-only**, never mutated) |
| `ControlledDocument` | Logical controlled document attached to a `CollectionInstance` (FU01 owned) |
| `ControlledDocumentVersion` | Immutable versioned file/content record of a controlled document (FU01 owned) |
| `TemplateDocument` | Reusable template attached to a folder/company structure (FU01 owned) |
| `TemplateVersion` | Versioned template file (FU01 owned) |
| `DocumentType` | SOP / Work Instruction / Policy / Form / Template / Other |
| `DocumentSharePolicy` | Who can access / use / copy a document or template |
| `FolderSharePackage` | A folder/branch + its associated templates shared to another company |
| `FileRef` / `ContentRef` | Pointer to the approved binary/content storage abstraction (no physical FS) |
| `VersionStatus` | DRAFT / ACTIVE / SUPERSEDED / ARCHIVED |

### Controlled Documents Explorer & active-structure model

The Controlled Documents day-to-day surface behaves like a **Windows-Explorer-style file browser** over a
company's **active, instantiated/adopted documentation structures** — **not** over the raw published baseline
catalog. This elaborates the already-approved attach / version / share / permission behavior with a UX model;
it adds **no runtime scope beyond the approved FU01 scope** and **no new aggregate** (search is a read-only
query over already-owned data + the read-only `CollectionInstance` seam).

**Terminology split (governance vs. daily use):**

| Surface | Vocabulary |
|---|---|
| Admin / governance (MOD-0028) | Baseline / Baseline Release / Publish / **Instantiate** |
| Controlled Documents (daily, MOD-0029-FU01) | **Documentation Structure** / **Folder Tree** / **Folder Documents** |

**Active-structure model (rules):**

1. **Company + Documentation Structure selection.** The Explorer first resolves (a) the selected
   company/legal entity and (b) the **active instantiated Documentation Structures** for that company. If
   exactly one active structure exists, **auto-select** it; if multiple exist, show a `Documentation Structure`
   selector. **Raw published baselines are never exposed as selectable document roots** — only
   company-instantiated/adopted **active** structures appear.
2. **Folder tree source.** The left tree is built from **active `CollectionInstance` nodes** for the selected
   company + selected Documentation Structure, consumed **read-only** through `ICollectionInstanceReferenceReader`
   (FullPath prefix / `ParentCanonicalId` chain). FU01 never mutates the structure.
3. **Multiple baselines / multiple structures.** A company may have multiple published baselines instantiated
   as active structures. The Explorer shows the company's **active instantiated structures**, not every
   published baseline. If multiple releases exist for the same baseline, use the company's **active instantiated
   release**; do not show all published releases as selectable document roots.
4. **Side-by-side mode is NOT default.** Showing two structures side by side requires an explicit
   governance/product decision in a later wave; FU01 default avoids confusing duplicate structures.

**3-panel Explorer layout (TenantShell):**

| Panel | Content |
|---|---|
| Top/left selector | Company / Legal Entity + Documentation Structure selection |
| Left tree | Active instantiated `CollectionInstance` folder tree for the selection |
| Middle list | Search empty → the **selected folder's** documents/templates; search active → permission-filtered matches across the **selected Documentation Structure** |
| Right detail | Selected document/template **details + version history + access control + share + download/upload** actions |

> **Provenance, not attachment root:** documents/templates attach to **`CollectionInstanceId`** (a folder
> node), **never** directly to a raw `BaselineReleaseId`. `BaselineReleaseId` may be stored only as
> **provenance/trace** metadata (see §3/§4); it is never a selectable document root in the daily surface.

### Sharing model (first version)

| Mode | Behavior | First-version status |
|---|---|---|
| `REFERENCE` | Target company sees/uses the **same** active template version; source updates may be visible per policy | In scope (default for templates) |
| `COPY_ON_ADOPT` / `COPY_ON_SHARE` | Target company receives its **own copied** document/template; version lineage starts from the copied source; source and target diverge after copy | In scope (only when explicitly selected) |

Default recommendation: templates are shared by `REFERENCE`; `COPY_ON_ADOPT` is opt-in per share operation.

### Versioning model (first version)

- A `ControlledDocument` has many `ControlledDocumentVersion` records; a `TemplateDocument` has many
  `TemplateVersion` records.
- A **new upload creates a new version — never an overwrite**. Activated (`ACTIVE`) versions are immutable.
- The current `ACTIVE` version is always queryable; previous versions stay readable per permission.

> **`ACTIVE` is a technical activation, not an approval.** In FU01, `ACTIVE` is a **technical activation /
> current-version resolution** state only: it means this version is the current active version for
> retrieval/use. It is **not a formal approval decision.** FU01 does **not** implement a review/approve
> workflow, an approver decision, e-signature, or a controlled-release approval gate. If formal approval is
> required later, it must be implemented in a **separate parent-approved follow-up** (see §20). The
> `DRAFT / ACTIVE / SUPERSEDED / ARCHIVED` set is a lifecycle of *technical* version states, not an
> approval state machine.

### Approval Scope

- This pack is `status: approved` by explicit user approval on 2026-06-22.
- Approval is limited to the FU01 controlled-document / SOP / work-instruction / template versioning /
  sharing foundation scope only (see In scope §2 and the approved-scope list below).
- **Approved runtime work** is limited to: `ControlledDocument` creation; SOP / Work Instruction / Policy /
  Form / Template / Other document types; attaching controlled documents/templates to a `CollectionInstance`
  folder node; `ControlledDocumentVersion` creation; `TemplateDocument` creation; `TemplateVersion` creation;
  immutable activated versions; new-upload-creates-new-version (never overwrite); current-active-version query;
  previous-version read by permission; template flags (`reusable` / `shareable` / `copyableOnAdopt` /
  `referenceOnly`); individual document/template sharing; folder/branch share with associated templates;
  `REFERENCE` share mode; `COPY_ON_ADOPT` / `COPY_ON_SHARE` mode (feature-flag gated); folder-share dry-run;
  folder-share execute; per-item share outcomes; the TenantShell Controlled Documents UI (library, folder
  attachments, version history, upload new version, share document/template, folder-share wizard); controlled
  `reason_code`/`correlation_id` failures; MOD-0220 target-company fail-closed validation; and binary/content
  storage abstraction consumption only.
- **Not approved:** MOD-0028 folder-structure editing, `CollectionDefinition` editing, FU05 instantiation
  changes, direct filesystem storage, physical folder creation, implementing binary storage provider
  internals, OCR / content indexing, e-signature, approval workflow (unless the MOD-0029 parent explicitly
  approves it later), retention / legal hold, evidence export, public/anonymous sharing, external portal,
  email notification, browser-based document editing, and MOD-0030 / MOD-0031 implementation.
- No runtime code is written by this pack; it is a contract/specification only. Implementation begins only
  after the controlled gates in §18 are satisfied.

## 2. Ownership and Boundaries

### In scope

- **Attach controlled document / template to a folder**: select a `CollectionInstance` folder; upload or link
  a controlled document / SOP / work instruction / template file with metadata; store via the approved
  binary/content storage abstraction (`FileRef`/`ContentRef`). No physical filesystem folder creation.
- **Versioning**: immutable `ControlledDocumentVersion` / `TemplateVersion` records; new upload = new version;
  current active version queryable; previous versions readable per permission.
- **Template support**: folder nodes can have associated template files marked `reusable` / `shareable` /
  `copyableOnAdopt` / `referenceOnly`. First version attaches templates to a `CollectionInstance`.
- **Sharing** of an individual controlled document, a template document, or a folder/branch with its
  associated templates — to users/roles in the same company, or another company/legal entity in the same
  tenant (optional plant/business-unit scope) — in `REFERENCE` or `COPY_ON_ADOPT` mode.
- **Folder/branch share with templates**: identify the selected `CollectionInstance` branch, discover
  associated templates under included nodes, choose include-templates yes/no and share mode, **dry-run**
  preview (folders included, templates included/skipped, conflicts, permission issues), then **execute**
  (share records or target copies per mode). No unselected branch is shared; no unrelated document/template
  is exposed.
- Document-control metadata; flow-level `correlation_id` across dry-run/execute; MOD-0021 audit seams.
- A TenantShell **Controlled Documents** surface (library + folder-detail attachments + version history +
  upload-version + share controls + folder-share wizard).
- Focused backend + frontend tests.

#### Explorer document/template operations (in scope — per-item, permission-gated)

The Explorer (§1 *Explorer & active-structure model*) supports these **document/template** operations as FU01
implementation targets. Every operation is permission-gated by the **already-approved** two-layer model
(§14); the frontend only hides/disables, the backend is authoritative. None of these mutate the MOD-0028 folder
tree (which stays read-only — see *Folder tree operations* below):

- **Add document/template** to the selected `CollectionInstance` folder (folder-level upload permission).
- **Upload new version** (immutable; never overwrite).
- **View version history**; **Download** the current/selected version.
- **Preview** (controlled backend endpoint, new tab; no public URL — see §15).
- **Share** document/template (and folder/branch share) per share policy + MOD-0220 fail-closed.
- **Copy** a document/template to another authorized folder (see *Copy semantics*).
- **Move** a document/template to another authorized folder (see *Move semantics*).
- **Soft delete / archive** a document/template (no hard delete — see *Delete semantics*).
- **Add / remove favorite** (per-user; see *Favorite semantics*).
- **Open details panel** (details + version + access + share).
- **Manage access** panel — enforcement is in scope; a **full per-folder/per-document access-management UI may
  be a phased follow-up** (placeholder allowed; see §20). This is MOD-0029 Layer 2 domain data, **never** the
  central RBAC screen (which manages Layer 1 only).

**Copy semantics.** Copy creates a **new** document/template record (a new folder attachment) in the target;
the **source remains unchanged**. The target folder must be in the **same tenant** and an **authorized
company/structure scope**; the user needs **source view/download** + **target upload/create** permission.
Version-lineage handling is explicit: either **copy the current active version as the new initial version**
(default — independent target) **or reference the source version** when reference/share mode is explicitly
selected. **Default recommendation: copy the current active version into the target as a new independent
document/template** unless share/reference is explicitly chosen.

**Move semantics.** Move changes the item's **`CollectionInstanceId` + `CollectionPath` snapshot** (and
`CanonicalId` snapshot); it does **not** change binary content or version rows. The target folder must be
**active**, **same tenant**, valid **company/legal-entity scope**; the user needs **source manage/edit** +
**target upload/create** permission. **Move across company is blocked** (use explicit share/copy instead). The
operation records a move `reason_code` / `correlation_id` on the MOD-0021 audit seam.

**Delete semantics.** Delete in FU01 is **soft delete / archive by default**; **no physical binary deletion**
in the first step (unless a retention/storage-cleanup policy explicitly allows it — out of FU01). A soft delete
**must not break version-history references**. **Hard delete / purge stays out of scope** (future
retention/storage-cleanup scope, MOD-0030).

**Favorite semantics.** A user may favorite **folders / documents / templates they can access**; favorites are
**tenant + user scoped**. A favorite **does not grant access** — if access is later removed, the favorite item
is **hidden or shown as unavailable** per convention. Candidate object: `DocumentFavorite` /
`UserDocumentFavorite` (or equivalent).

**Preview semantics.** Preview opens a **controlled backend endpoint in a new tab** (no direct public file
URL); the backend checks **Layer 1 + Layer 2** before streaming. First-version preview supports **PDF + image**
types; Office render (DOCX/XLSX/PPTX) stays a follow-up unless an existing viewer is available; **unsupported
preview types fall back to download**.

### Consumed, not owned

- MOD-0028-FU05 `CollectionInstance` (read-only): `CollectionInstance` id, tree path, company/legal-entity
  binding, folder/branch scope — consumed **only** through a dedicated read-only seam
  **`ICollectionInstanceReferenceReader`** (mirroring the MOD-0220 `ILegalEntityReferenceValidator` pattern).
  The existing `ICollectionInstanceRepository` is **read+write mixed** (`CreateAsync` / `CreateManyAsync` /
  `ArchiveManyAsync` / `ReactivateManyAsync`); FU01 handlers/services/controllers **must never inject it
  directly**. The entity is metadata-only (no physical folder / binary / lifecycle side effect); parent-child
  is **`CanonicalId`/`FullPath`-based, not a Guid parent ref**, so branch/descendants are derived read-only
  from a `FullPath` prefix / `ParentCanonicalId` chain. See §10 for the seam contract.
- MOD-0028 FU01 `Response<T>` (`reason_code`/`correlation_id`), the `api/v1/document-management` route family,
  `[HasPermission]`, the directional `PermissionAliasMap` convention, and the FU01/FU02 typed-options pattern.
- MOD-0220 LegalEntity lookup/eligibility for share-target company validation (the FU05-confirmed
  `ILegalEntityReferenceValidator` seam).
- MOD-0018 permission ownership for new keys; MOD-0021 audit store.
- Platform Common `TenantScopedEntity`, tenant context, tenant repository filtering, correlation middleware.
- The **approved binary/content storage abstraction** (an interface/seam; the concrete provider is consumed,
  never re-implemented as direct filesystem access in FU01).

### Explicitly out of scope

- Editing MOD-0028 folder structure; `CollectionDefinition` editing; FU04 manual-builder changes;
  FU05 instantiation logic changes (read-only consumption only).
- **Folder tree operations (explicit GAP / MOD-0028 territory):** create folder, rename folder, move folder,
  delete folder, copy/paste folder, alter `CollectionInstance` hierarchy, or mutate `CollectionDefinition` /
  `BaselineRelease` / `CollectionInstance` are **NOT** implemented directly inside MOD-0029-FU01. These are
  **MOD-0028-owned structure operations** (or a separate company-instance-folder-management follow-up — §20)
  and require a separately approved MOD-0028/structure extension. The Controlled Documents UI may show
  **disabled placeholders / future-action notes only**; it must **never** mutate the folder tree. (Document/
  template copy/move **into** a folder is in scope — only the folder node tree is read-only.)
- OCR / content indexing; e-signature; approval workflow (unless already approved by the MOD-0029 parent);
  retention / legal hold (MOD-0030); evidence export (MOD-0031); external customer portal; public/anonymous
  sharing; email notification.
- Physical folder creation; direct filesystem storage; browser-based document editing; **hard delete / purge**
  of document binaries/version rows (soft delete/archive only — future retention/storage-cleanup scope).

### Access Control & Folder-Upload Authorization Model

This model elaborates the **already-approved** attach / version / share / permission behavior; it adds no new
runtime scope. The first version **may operate at role/company level if a user-level ACL is not yet ready**;
that reduction is acceptable **only if explicitly documented** and never weakens tenant/company isolation.
Backend evaluation is **authoritative everywhere**; the frontend only hides/disables controls as UX.

**1. Folder-level document permission.** A `CollectionInstance` folder node (consumed read-only from MOD-0028
— never mutated) carries a set of FU01-owned, document-related permissions, evaluated **server-side**:

- `canViewFolderDocuments`
- `canUploadDocument`
- `canEditFolderDocuments`
- `canUploadNewVersion`
- `canShareFolderDocuments`
- `canManageFolderDocumentAccess`

Because the `CollectionInstance` is read-only, these folder-level permissions live in an **FU01-owned sidecar
record keyed by `CollectionInstanceId`** (see `FolderDocumentAccessPolicy` in §3/§4); FU01 never writes
permission fields back onto the MOD-0028 structure object.

**2. Document-level access control.** Each `ControlledDocument` / `TemplateDocument` may carry an
`AccessPolicy` defining **who can: view, download, edit metadata, upload a new version, share, manage access**.
Access targets may be a **user, role, company/legal entity, plant, or business unit**. First version may
support **role/company-level** targets if user-level ACL is not ready — documented here as such.

**3. Permission inheritance.** Document access **inherits from the parent `CollectionInstance` folder unless
explicitly overridden** on the document. A document-level override **must not weaken tenant/company isolation**
(it can only narrow, never cross-tenant/cross-company widen). When inheritance is used, the document result
**indicates whether the effective access is `inherited` or `explicit`**.

**4. Upload authorization.** A user may upload a document/template only when **all** hold:

- the target `CollectionInstance` belongs to the **tenant**, and
- it belongs to the **selected company/legal-entity scope**, and
- the user has **`canUploadDocument`** (or equivalent) for that folder, and
- the user has the module permission **`…controlled-documents.create`** / **`…templates.create`**.

On failure: **403 `PERM_DENIED`** for an unauthorized but visible folder; **404 `NOT_FOUND_NON_LEAKAGE`** for a
cross-tenant / non-visible folder.

**5. Version authorization.** A user may upload a **new** version only when: the document is **visible** to the
user; the user has **version-create** module permission; the user has document-level or inherited folder-level
**`canUploadNewVersion`**; and the **active version is never overwritten** — a new upload creates a new
immutable version.

**6. Share authorization.** A user may share a document / template / folder branch only when: the source item
is **visible**; the user has **share** permission; the item is **shareable**; the **target legal entity/company
is valid via MOD-0220** (fail-closed); and the share **does not expose unselected branches or unrelated
documents/templates**.

**7. LegalEntity/company document ownership.** `ControlledDocument` and `TemplateDocument` carry
`OwnerCompanyId`, `CollectionInstanceId`, a `CollectionPath` snapshot, `CreatedBy`, `CurrentVersionId`, and
`AccessPolicy` / share policy. Each company/legal entity owns its documents under its own `CollectionInstance`
folders; **one company's documents must not leak to another unless explicitly shared.**

## 3. Owned Objects

FU01 owns the controlled-document/template aggregates plus the sharing lineage and contracts:

- `ControlledDocument` — logical controlled document attached to a `CollectionInstance` (primary aggregate).
- `ControlledDocumentVersion` — immutable versioned file/content record.
- `TemplateDocument` — reusable template attached to a folder/company structure.
- `TemplateVersion` — versioned template file.
- `DocumentSharePolicy` — embedded value object describing access/use/copy rules.
- `DocumentAccessPolicy` — embedded value object on a `ControlledDocument`/`TemplateDocument` describing per-action
  grants (view / download / edit / version / share / manage-access) and their access targets
  (user / role / company / plant / business-unit), plus an `Inherited`-vs-`Explicit` indicator.
- `FolderDocumentAccessPolicy` — FU01-owned **sidecar** record keyed by `CollectionInstanceId` that holds the
  folder-level document permissions (`canViewFolderDocuments` / `canUploadDocument` / `canEditFolderDocuments` /
  `canUploadNewVersion` / `canShareFolderDocuments` / `canManageFolderDocumentAccess`). It **never mutates the
  read-only MOD-0028 `CollectionInstance`**.
- `FolderShareOperation` — a folder/branch dry-run/execute share operation record (status, counts,
  correlation id, lineage).
- `FolderShareOutcome` — per-item share outcome (folder/template, status, reason_code, retryable).
- `FileRef`/`ContentRef` — embedded pointer value object to the approved binary/content storage abstraction.
- Controlled-document/template/share request/result contracts and the minimum FU01 permission mapping (§14).

FU01 must **not** create or mutate `CollectionInstance`, `CollectionDefinition`, `BaselineRelease`, or any
MOD-0028 structure object; it must not implement an **external** binary storage provider (Phase 2 controlled
gate), create physical business folders, or own retention / evidence export. The **Phase 1
`LocalFileSystemContentStorageGateway`** is in scope **as an `IContentStorageGateway` implementation behind the
seam** (see §8 *Storage Architecture Decision*).

## 4. Entity Fields

FU01 persists tenant-owned, company-scoped aggregates using
`Diten.Platform.Common.Persistence.TenantScopedEntity` (confirmed by FU01/FU02/FU05). `TenantId`,
`IsDeleted`, and technical `Version` are inherited / server-resolved and never accepted from client payloads.
Business versions use semantic names (`VersionNumber`), never the technical `Version`.

| Object | Principal fields | Required constraints / indexes |
|---|---|---|
| ControlledDocument | DocumentKey, CompanyId, OwnerCompanyId, CollectionInstanceId, CollectionPath, CanonicalId? (snapshot), BaselineReleaseId? (provenance/trace only — never an attachment root), Title, DocumentType, Description, Tags[], Controlled (bool), EffectiveDate?, ReviewDate?, ExpiryDate?, CurrentVersionId?, Status, CreatedBy, AccessPolicy (DocumentAccessPolicy) | Tenant + DocumentKey unique (non-deleted); tenant-first index; CollectionInstanceId indexed; no hard delete |
| ControlledDocumentVersion | DocumentId, VersionNumber, FileRef (ContentRef), Checksum, UploadedBy, UploadedAt, ChangeSummary, VersionStatus | Tenant + DocumentId + VersionNumber unique; activated version immutable |
| TemplateDocument | TemplateKey, CompanyId, OwnerCompanyId, CollectionInstanceId?, CollectionPath?, CanonicalId? (snapshot), BaselineReleaseId? (provenance/trace only), Title, Description, Tags[], TemplateFlags (reusable/shareable/copyableOnAdopt/referenceOnly), CurrentVersionId?, Status, CreatedBy, AccessPolicy (DocumentAccessPolicy) | Tenant + TemplateKey unique (non-deleted); tenant-first index; no hard delete |
| TemplateVersion | TemplateId, VersionNumber, FileRef (ContentRef), Checksum, UploadedBy, UploadedAt, ChangeSummary, VersionStatus | Tenant + TemplateId + VersionNumber unique; activated version immutable |
| DocumentSharePolicy (embedded) | ShareMode (REFERENCE/COPY_ON_ADOPT), CanUse, CanCopy, VisibilityScope (COMPANY/PLANT/BU), SourceVisibleOnUpdate | COMPANY target uses MOD-0220 LegalEntity GUID |
| DocumentAccessPolicy (embedded) | Grants[] {Action (VIEW/DOWNLOAD/EDIT/VERSION/SHARE/MANAGE_ACCESS), TargetType (USER/ROLE/COMPANY/PLANT/BU), TargetId}, Source (INHERITED/EXPLICIT) | Override may only narrow; never crosses tenant/company isolation; first version may be ROLE/COMPANY-level |
| FolderDocumentAccessPolicy | CollectionInstanceId, CompanyId, FolderPermissions {canViewFolderDocuments, canUploadDocument, canEditFolderDocuments, canUploadNewVersion, canShareFolderDocuments, canManageFolderDocumentAccess} keyed by TargetType/TargetId | Tenant + CollectionInstanceId (+TargetId) unique; CollectionInstanceId indexed; sidecar only — never mutates the MOD-0028 CollectionInstance |
| FolderShareOperation | OperationId, SourceCompanyId, TargetCompanyId, SourceBranchCollectionInstanceId, IncludeTemplates, ShareMode, OperationType (DRY_RUN/EXECUTE), Status, FoldersIncluded/TemplatesIncluded/TemplatesSkipped/Failed/Total, CorrelationId, RequestedBy, StartedAt, CompletedAt | Tenant + OperationId unique; CorrelationId indexed |
| FolderShareOutcome | OperationId, ItemType (FOLDER/TEMPLATE), ItemKey, Status (SHARED/COPIED/SKIPPED/FAILED), ReasonCode, Message, Retryable | Tenant + OperationId + ItemKey unique |
| FileRef / ContentRef (embedded) | ContentId, StorageProvider, ObjectKey/StoragePath (internal), FileName, MediaType, ByteSize, Checksum (SHA-256), CreatedAt, CreatedBy, VersionId | Pointer only; FU01 never stores raw bytes in Mongo; `ObjectKey`/`StoragePath` is an internal detail never exposed to the client |

**Deterministic keys (closed at implementation):** `DocumentKey = {tenantId}|{companyId}|{collectionInstanceId}|{slug(title)}`
and `TemplateKey = {tenantId}|{companyId}|{collectionInstanceId?}|{slug(title)}` (or a repo-approved
equivalent). `CollectionPath` is copied read-only from the consumed `CollectionInstance`; FU01 never derives
or edits folder hierarchy.

**Attachment target vs. provenance:** documents/templates attach to **`CollectionInstanceId`** (a folder
node), never to a raw `BaselineReleaseId`. `BaselineReleaseId` (and `CanonicalId`) are stored only as a
**read-only provenance/trace snapshot** captured at attach time; they are never selectable document roots and
are never used to drive the daily Explorer (see §1 *Explorer & active-structure model*).

## 5. Repo Scope

### Authorized FU01 implementation scope (after approval)

- `services/Diten.Platform/src/Diten.Platform.API/**` — thin controlled-document/template/folder-share
  controller actions + controlled response wiring under the existing route family.
- `services/Diten.Platform/src/Diten.Platform.Application/**` — CQRS commands/queries/handlers/validators,
  document/template/versioning/sharing services, MOD-0220 + binary-storage seam consumption, permission
  constants, reason codes.
- `services/Diten.Platform/src/Diten.Platform.Domain/**` — document/template/version/share aggregates +
  repository interfaces (only if the live convention places entities here).
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/**` — repository implementations, Mongo indexes,
  DI, approved alias registration, binary-storage seam adapter wiring, and the **Phase 1
  `LocalFileSystemContentStorageGateway`** (`IContentStorageGateway` impl behind the seam, config-driven root;
  see §8). **No external provider re-implementation** (Phase 2 controlled gate).
- `frontend/Diten.Web/**` — TenantShell Controlled Documents library + folder attachments + version history +
  upload + share controls + folder-share wizard (controller proxy, views, JS, RESX) per FU03/FU04/FU05
  conventions.
- `services/Diten.Platform/tests/**` and frontend tests/smoke.

### Separately governed scope

- `gateway/Diten.ApiGateway/**/ocelot.json` — only if a new route is needed beyond the existing widened
  catch-all (see §15); FU01 expects GET/POST under the existing catch-all, so **no gateway change is
  anticipated**.
- Permission seed/alias ownership for new keys through the MOD-0018/security-owned location when outside
  FU01 scope (see §14).
- The concrete **external binary/content storage provider** internals (Phase 2: MinIO / S3 / Azure Blob /
  dedicated server / MOD-0266 — consumed via the `IContentStorageGateway` abstraction only). The **Phase 1
  `LocalFileSystemContentStorageGateway`** is **not** separately governed — it is in FU01 Infrastructure scope
  (§5 authorized scope, §8).

## 6. Protected Paths

- `.antigravity/**`
- `gateway/**` except a separately approved integration-agent task if a new route is unexpectedly required
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` (permission seed/alias) except a separately approved MOD-0018/security task
- `services/Diten.MdmService/**` (MOD-0220 is consumed via its contract, never modified)
- `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`
- MOD-0028 FU01–FU05 owned files except read-only **consumption** of their public contracts
  (`CollectionInstance` id / path / company binding / folder scope)
- MOD-0030 (retention) and MOD-0031 (evidence) implementation files
- **External** content storage provider internals (Phase 2); physical business-folder creation (the Phase 1
  `LocalFileSystemContentStorageGateway` behind `IContentStorageGateway` is permitted — see §8)
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and any frozen/legacy layout
- Parent pack and sibling packs unless a separate governance reconciliation authorizes an update

## 7. Dependencies

| Dependency | FU01 usage |
|---|---|
| MOD-0029 parent | Supplies controlled-document / SOP / template ownership + Wave boundary |
| MOD-0028-FU05 | Supplies `CollectionInstance` folder tree (id / path / company binding / scope) — consumed, not modified |
| MOD-0028-FU01/FU04 | Supply `Response<T>`, route family, `[HasPermission]`, manual-builder navigation handoff (read-only) |
| MOD-0220 | LegalEntity lookup/eligibility for share-target company validation (fail-closed seam) |
| MOD-0018 | Approves new lowercase permission keys, any uppercase aliases, seed ownership |
| MOD-0021 | Audit/correlation seams across upload/version/share |
| Binary/content storage abstraction | Approved seam for `FileRef`/`ContentRef`; provider consumed, never re-implemented |
| Platform Common | `TenantScopedEntity`, tenant context, repository filtering, correlation middleware |

## 8. Runtime Constraints

- Persistence: MongoDB, tenant isolation on every owned aggregate.
- `TenantScopedEntity` base; no client-controlled `TenantId`, technical `Version`, audit actor, or correlation
  identity.
- **Files are stored only through the approved binary/content storage abstraction** (`IContentStorageGateway`,
  `FileRef`/`ContentRef`); **no direct filesystem access from controllers/handlers and no physical folder
  creation**. Raw bytes are never persisted in Mongo. The Phase 1 `LocalFileSystemContentStorageGateway` may
  touch the filesystem **only inside the Infrastructure provider, behind the seam** (see *Storage Architecture
  Decision* below) — never as inline controller/handler filesystem code.
- **Versions are immutable once `ACTIVE`**; a new upload creates a new version, never an overwrite. The current
  `ACTIVE` version is queryable; previous versions are readable per permission.
- **Sharing is controlled**: `REFERENCE` (target uses the same active version; source updates visible per
  policy) or `COPY_ON_ADOPT` (target receives an independent copy; lineage forks). A non-shareable source
  template cannot be shared (controlled `VALIDATION_FAILED`).
- **Folder/branch share**: dry-run mutates nothing; execute creates share records / target copies only for the
  selected branch and its included templates; no unselected branch is shared and no unrelated item is exposed.
- **MOD-0220 fail-closed** for share-target company: a missing/inactive/non-referenceable LegalEntity rejects
  the share without orphaned writes; caller cancellation is preserved.
- **No metadata orphan on storage failure**: if the binary/content store is unavailable, the operation fails
  in a controlled way and no document/version metadata row is left dangling.
- Every FU01 API envelope includes body-level `correlation_id`; the **same flow correlation id** flows through
  folder-share dry-run → execute; controlled failures include a stable `reason_code`; no stack traces.
- Cross-tenant / cross-company detail behavior is 404 non-leakage; unauthorized actions are 403.

Feature flags (reused FU01/FU02 typed-options pattern; FU01 adds only what it needs):

| Key | Default | FU01 relevance |
|---|---|---|
| `mod0029.controlled_documents.enabled` | on | The FU01 surface |
| `mod0029.template_sharing.enabled` | on | Document/template share actions |
| `mod0029.folder_share_copy_on_adopt.enabled` | off until copy lineage is verified | Gates `COPY_ON_ADOPT` execution |

### Storage Architecture Decision (phased)

The binary/content storage seam discovery confirmed there is **no pre-existing approved storage abstraction or
provider** in the repo. The approved decision is a **phased storage architecture behind a single abstraction**
so that the storage backend can change later **without any change to the `ControlledDocument` / versioning /
sharing domain code**.

**Single seam (both phases): `IContentStorageGateway`.** All file persistence — upload, download/stream,
delete — goes through this Application-layer interface. Domain/Application code never touches a filesystem path,
a provider SDK, or raw bytes directly. **Direct controller filesystem code is prohibited**; the
EnterpriseStrategyService `UploadsController` direct-filesystem pattern (`Directory.CreateDirectory` /
`System.IO.File.Create` / `PhysicalFile`) **must not be copied**. **GridFS remains disallowed** because storing
raw bytes in Mongo conflicts with the pack's "no raw bytes in Mongo" rule.

**Phase 1 provider: `LocalFileSystemContentStorageGateway`** (an `IContentStorageGateway` implementation in
Infrastructure). First-phase files live on the server where the app is deployed, but only under these rules:

- Files are kept under a **config-driven root path**; the path is an **internal implementation detail** never
  exposed to the client.
- Root path **must not be under `wwwroot`** and **must not be reachable by any public/static URL**.
- File access is **always** through a backend API **after** permission checks; **no direct URL or file path is
  ever handed to the user**.
- The object key/path is built **deterministically and safely per tenant/company/document/version**; `FileName`
  is **sanitized**; allowed **extensions/media types** and **max file size** are enforced; **SHA-256 checksum**
  is computed.
- **Raw bytes are never written to Mongo.** **No physical business-folder creation** — only a storage object
  path is produced (this is *not* MOD-0028 folder structure).
- Recommended config root (example only, not hard-coded): Windows `D:\DitenStorage\Documents`, Linux
  `/var/lib/diten/documents`.
- Example object key pattern:
  `tenant-{tenantId}/company-{companyId}/documents/{documentId}/versions/{versionId}/{safeFileName}`.

**Phase 2 provider: external content storage** — MinIO, S3-compatible storage, Azure Blob, a dedicated
file/storage server, or the MOD-0266 provider if/when available. Phase 2 rules:

- The **`IContentStorageGateway` interface stays identical**; **only the provider implementation changes**.
- Existing `ContentRef` records keep their `StorageProvider` + `ContentId`/`ObjectKey`, so they remain
  resolvable.
- A **migration/reconciliation plan is required before moving old content**; **no domain/application rewrite**
  should be needed.

**Upload order (controlled, storage-first):**

1. Validate `CollectionInstance` (tenant-resolvable) → else 404 `NOT_FOUND_NON_LEAKAGE`.
2. Validate company/legal-entity scope.
3. Validate **folder-level upload permission** (`canUploadDocument` on the target folder).
4. Validate **module permission** (`…controlled-documents.create` / `…templates.create`).
5. Validate **file type and size** (allowed media types / max size) → else 400 `VALIDATION_FAILED`.
6. **Write content through `IContentStorageGateway`.**
7. If storage **succeeds**, commit the metadata/version row.
8. If storage **fails**, **do not commit** metadata (controlled failure, no orphan).
9. If metadata commit **fails after** storage succeeded, **best-effort delete** the stored content.
10. If the delete also fails, **record an orphan-cleanup follow-up** (reconciliation sweep).

**Download rule:** there is **no direct public file URL**. A controlled backend download/stream endpoint checks
**tenant → company/legal entity → folder/document access → version access → download permission**, and **only
then** streams the bytes from the storage provider via `IContentStorageGateway`.

**Production-readiness note:** Phase 1 local storage is acceptable for the first deployment, but full production
readiness additionally depends on **server backup of the storage root, storage-path security/hardening, a
malware-scanning policy, and the Phase 2 external-provider migration plan**. External provider integration
remains a **follow-up / controlled gate** while no provider exists.

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`; every FU01 page declares `Layout = "_LayoutTenantShell";` explicitly.
- Primary actor type: `tenant_user`.
- No `_LayoutPlatformAdmin.cshtml`, frozen `_Layout.cshtml`, or legacy layout.
- Nav: a TenantShell **Controlled Documents** entry (or a sub-action under the existing documentation nav),
  permission-gated on `…controlled-documents.view`; the folder detail/attachments surface is reached from the
  MOD-0028 Documentation Structures `CollectionInstance` detail.
- Exact view/controller/route folder confirmed against the live TenantShell convention (FU03/FU04/FU05 reuse).

## 10. Backend File Convention

FU01 follows the live Diten.Platform CQRS action-based shape (Golden Reference Compact mirrored), split into
focused feature folders:

```text
Features/DocumentManagementControlledDocuments/
|-- Commands/
|   |-- CreateControlledDocumentCommand.cs      (sealed record)
|   |-- CreateControlledDocumentVersionCommand.cs
|   |-- ShareControlledDocumentCommand.cs
|   |-- CreateTemplateDocumentCommand.cs
|   |-- CreateTemplateVersionCommand.cs
|   |-- ShareTemplateCommand.cs
|   |-- DryRunFolderShareCommand.cs
|   `-- ExecuteFolderShareCommand.cs
|-- Queries/
|   |-- GetControlledDocumentListQuery.cs
|   |-- GetControlledDocumentByIdQuery.cs
|   |-- GetControlledDocumentVersionsQuery.cs
|   |-- GetControlledDocumentVersionByIdQuery.cs
|   |-- GetTemplateListQuery.cs
|   |-- GetTemplateByIdQuery.cs
|   |-- GetTemplateVersionsQuery.cs
|   `-- GetFolderShareOperationQuery.cs
|-- Handlers/{CommandHandlers,QueryHandlers}/    (sealed, no Command/Query suffix)
|-- Validators/                                  (no Command suffix)
|-- Services/
|   |-- IContentStorageGateway.cs                (binary/content storage abstraction seam)
|   |-- ICollectionInstanceReferenceReader.cs    (READ-ONLY MOD-0028-FU05 CollectionInstance consumption seam)
|   |-- DocumentVersioningService.cs             (immutable version creation + active-version resolution)
|   |-- TemplateSharingService.cs                (REFERENCE vs COPY_ON_ADOPT semantics)
|   |-- IFolderSharePlanner.cs                   (builds folder-share dry-run plans from a CollectionInstance branch)
|   `-- DocumentKeyFactory.cs                    (deterministic DocumentKey/TemplateKey)
`-- DocumentManagementControlledDocumentsModels.cs (DTOs/result models in one file)
```

### CollectionInstance read-only consumption seam (`ICollectionInstanceReferenceReader`)

FU01 consumes MOD-0028-FU05 `CollectionInstance` **only** through a dedicated **read-only** Application seam.
The existing `ICollectionInstanceRepository` is read+write mixed (it exposes `CreateAsync` / `CreateManyAsync`
/ `ArchiveManyAsync` / `ReactivateManyAsync`), so **FU01 handlers/services/controllers must never inject it
directly.** The Infrastructure adapter **may wrap** `ICollectionInstanceRepository` but **must expose only
read-only methods**; no create/archive/reactivate/provision operation is reachable from FU01 through this seam.

Proposed read-only contract (names may adjust to the live convention):

```text
ICollectionInstanceReferenceReader
|-- ResolveByIdAsync(collectionInstanceId, ct)        // tenant-scoped resolve; null -> 404 non-leakage
|-- ValidateScopeAsync(collectionInstanceId, companyId, ct)  // company/legal-entity scope check
|-- GetPathSnapshotAsync(collectionInstanceId, ct)   // FullPath + CanonicalId snapshot to copy into doc metadata
|-- GetCompanyBindingAsync(collectionInstanceId, ct) // CompanyId + ScopeBindings (legal entity / plant / BU)
|-- IsUsableAsync(collectionInstanceId, ct)          // InstanceStatus == Active / usable
`-- GetBranchAsync(rootCollectionInstanceId, ct)     // read-only descendants via FullPath prefix / ParentCanonicalId
```

The returned read DTO carries: `CollectionInstanceId`, `CompanyId`, `ScopeBindings`, `CanonicalId`,
`ParentCanonicalId`, `BaselineReleaseId`, `Name`, `FullPath`, `InstanceStatus`, `IsActive`/`IsUsable`, and the
path snapshot. `TenantId` stays internal-only (resolved from tenant context, never returned to the client).

**Attach validation (document/template → folder):** (1) resolve the `CollectionInstance` via
`ICollectionInstanceReferenceReader`; (2) tenant isolation is enforced by the tenant repository/context; (3)
validate company/legal-entity scope; (4) validate `InstanceStatus == Active`/usable; (5) validate folder-level
upload permission; (6) **copy the `FullPath` / `CanonicalId` / `CompanyId` snapshot into the document metadata**;
(7) **never mutate the `CollectionInstance`.**

**Folder/branch share:** FU01 uses `GetBranchAsync` to **read** descendants (derived read-only from the
`FullPath` prefix / `ParentCanonicalId` chain). Branch resolution is strictly read-only; FU01 shares **only the
documents/templates attached to the included `CollectionInstance` nodes** — **no unselected branch is exposed.**

**Protected boundary (seam):** FU01 must not edit `CollectionInstance`, `CollectionDefinition`, or
`BaselineRelease`; must not call FU05 execute/provisioning logic; must not inject the mixed read/write
`CollectionInstance` repository directly; and may consume **only** the read-only reference reader / query DTO.

- Commands/queries are sealed records; handlers `{Verb}{Slice}Handler` (no `CommandHandler`/`QueryHandler`
  suffix); validators `{Verb}{Slice}Validator` (no `CommandValidator` suffix).
- Controllers inherit `CustomBaseController`, remain thin, dispatch via MediatR; MOD-0220 and the content
  storage abstraction are accessed via Application interfaces + Infrastructure implementations (no raw
  `HttpClient` / no direct filesystem in handlers).
- Repository access uses the live tenant repository convention; tenant-first indexes mandatory.
- Versioning, sharing, and key generation are split into focused services, not oversized handlers.

## 11. Frontend File Contract

`golden_reference: compact`; the add-document/template surface is a multi-field route/page-based form
(`form_field_count: 10`), and the folder-share surface is a multi-step route-based wizard. Reuse the
FU03/FU04/FU05 TenantShell building blocks: same-origin MVC proxy → Gateway `5000`, shared toast/confirm,
the L10n bridge (`_IndexL10n` JSON → `index.l10n.js` → `window.L10n`), DataTable v2 for library lists, the
jsTree folder/branch renderer for folder selection, and `backbone-custom.css` for shared styles (no
page-embedded styles).

Compact view set (final names confirmed against the live convention):

```text
Views/DocumentManagement/ControlledDocuments/
|-- Index.cshtml                 (Layout = "_LayoutTenantShell"; library list)
|-- Create.cshtml                (add controlled document / template — Compact)
|-- Edit.cshtml                  (edit document metadata — Compact)
|-- Details.cshtml               (document detail + version history panel — Compact)
|-- _Form.cshtml                 (Compact shared form partial)
|-- _Filter.cshtml
|-- _DataTable.cshtml            (data-dt-standard="v2" + skeleton)
|-- _IndexL10n.cshtml            (only approved page-local JSON L10n bridge; <script id="controlleddocuments-l10n">)
`-- ControlledDocumentsIndex.cs  (marker class for IHtmlLocalizer<ControlledDocumentsIndex>)

wwwroot/assets/js/DocumentManagement/ControlledDocuments/
|-- index.js                     (all UI text from window.L10n; no hardcoded EN/TR)
`-- index.l10n.js                (QmsBaselines toPascalCase + requiredKeys pattern)

Resources/Views/DocumentManagement/ControlledDocuments/
`-- ControlledDocumentsIndex.{ar,en,es,fr,ru,tr,zh}.resx   (7-language parity; identical key sets)
```

Compact rule: `_CreateEditOffcanvas.cshtml` and `_DetailsQuickView.cshtml` are **forbidden** for this pack.

### TenantShell L10n contract (MOD-0028 pattern confirmed)

FU01 reuses the confirmed MOD-0028 (QmsBaselines/Instantiations) localization bridge:

- **`_IndexL10n.cshtml` is the only approved page-local JSON bridge** — `@inject IHtmlLocalizer<ControlledDocumentsIndex>`
  (surface keys) + `@inject IHtmlLocalizer<SharedResource>` (generic DataTable/toast/common labels) →
  `<script id="controlleddocuments-l10n" type="application/json">`. No other page-embedded localization JSON.
- **`index.l10n.js` follows the QmsBaselines pattern**: parse the JSON payload, `toPascalCase` each key, merge
  into `window.L10n`, and keep a `requiredKeys` array that must stay **in sync** with `_IndexL10n.cshtml`
  (missing key → `[L10N WARNING]` + an undefined `window.L10n.*` lookup).
- **All JS text comes from `window.L10n.*` — no hardcoded EN/TR strings** in `index.js`.
- **`SharedResource` is reused** for generic labels (SaveView/Print/Copy/PDF/Search/Export/Filter/Apply/Reset/
  ShowAll/ColumnVisibility/Actions/Status/NotAvailable/Unknown/RecordSaved/ErrorOccurred/BulkDeleteConfirm…).
- **7-language RESX parity is mandatory** — `ar, en, es, fr, ru, tr, zh` — each `ControlledDocumentsIndex.{lang}.resx`
  carries the **identical key set**.
- **Localized message classes:** library/type/version-history/share/folder-share-wizard/access-control labels,
  empty/loading/saving states, validation errors, storage errors (incl. a new `ReasonStorageUnavailable`),
  permission/access-denied messages, and the `reason_code` + `correlation_id` display strings.
- **Explorer & search L10n key group (added; 7-language parity, identical key set, `requiredKeys`-synced):**
  `Search`, `SearchIn`, `ThisFolder`, `ThisFolderAndSubfolders`, `EntireStructure`, `SearchResults`,
  `FolderResult`, `DocumentResult`, `TemplateResult`, `NoSearchResults`, `Path`, `OpenFolder`,
  `OpenDocument`, `OpenTemplate` (plus `DocumentationStructure` / `SelectStructure` selector labels). Generic
  `Search` may reuse `SharedResource`. **No approval/review/e-signature labels.**
- **Explorer operations L10n key group (added; 7-language parity, identical key set, `requiredKeys`-synced):**
  `Copy`, `Paste`, `Move`, `Delete`, `SoftDelete`, `Archived`, `Favorite`, `RemoveFavorite`, `Preview`,
  `OpenInNewTab`, `Download`, `UploadDocument`, `UploadTemplate`, `UploadNewVersion`, `AddFolder`,
  `FolderActionsUnavailable`, `FolderOperationsDeferred`, `CopyToFolder`, `MoveToFolder`, `SelectTargetFolder`,
  `PreviewUnavailable`, `UnsupportedPreviewType`. Generic `Copy`/`Delete`/`Download` may reuse `SharedResource`.
  Folder-mutation labels (`AddFolder` / `FolderActionsUnavailable` / `FolderOperationsDeferred`) back **disabled
  placeholders only** — FU01 never mutates the folder tree.
- **Approval-workflow labels must NOT be added** (review/approve, approver, e-signature, MOD-0023 are
  out-of-scope per §1/§2/§19; `ACTIVE` is a technical-activation label, not an approval label).

Surfaces:

- **Controlled Documents Explorer** (Windows-Explorer-style; see §1 *Explorer & active-structure model*):
  - **Company / Documentation Structure selector** — resolve the selected company/legal entity and its
    **active instantiated** Documentation Structures; auto-select when one exists, else show a
    `Documentation Structure` selector. Raw published baselines are **not** selectable document roots.
  - **Left folder tree** — active `CollectionInstance` nodes for the selection (read-only via the seam).
  - **Middle list** — *search empty* → the selected folder's documents/templates (current-folder browsing);
    *search active* → server-side, permission-filtered matches across the selected structure (see §15 search
    endpoint). A `Search in` dropdown offers **This folder / This folder and subfolders / Entire structure**
    (default: empty → current folder; active → entire structure, authorization-filtered).
  - **Right detail panel** — selected document/template **details + version history + access control + share +
    download/upload** actions.
- **Search navigation:** clicking a **Folder** result selects/navigates to that folder in the tree; clicking a
  **Document/Template** result opens the details/version panel. Every result shows its **path/breadcrumb**
  (`Path`); search never detaches an item from its folder context.
- **Document Library list** (DataTable v2): title, type (SOP / Work Instruction / Policy / Form / Template /
  Other), company, folder path, current version, status, actions — used as the middle-panel list renderer.
- **Add controlled document/template** (Compact form): the §12 metadata + file upload/link through the proxy.
- **Version history panel** + **upload new version** (new version, never overwrite).
- **Share document/template** controls (target + mode).
- **Folder/branch share wizard** (route-based): Select source company/branch → Select target
  company/legal entity → Include templates yes/no → Share mode (`REFERENCE` / `COPY_ON_ADOPT`) → Dry-run
  preview → Execute → Results, all permission-gated.

## 12. Validation Rules

| Input / operation | Required | Rule | Failure |
|---|---|---|---|
| CollectionInstance id | Yes | Must resolve to an existing tenant `CollectionInstance` via `ICollectionInstanceReferenceReader` | 404 `NOT_FOUND_NON_LEAKAGE` |
| CollectionInstance usable | Yes | `InstanceStatus == Active`/usable for attach/upload | 400 `VALIDATION_FAILED` / 409 `CONFLICT` (archived/inactive folder) |
| CollectionInstance company scope | Yes | Target folder must belong to the selected company/legal-entity scope | 404 `NOT_FOUND_NON_LEAKAGE` (or 403 per repo convention) |
| Title | Yes | Non-empty, trimmed, length-bounded | 400 `VALIDATION_FAILED` |
| Document type | Yes | One of SOP / Work Instruction / Policy / Form / Template / Other | 400 `VALIDATION_FAILED` |
| File (upload/link) | Yes | Supported media type; stored via content abstraction | 400 `VALIDATION_FAILED` (unsupported type) |
| Effective/Review/Expiry dates | No | Valid dates; expiry ≥ effective if both present | 400 `VALIDATION_FAILED` |
| Tags | No | Trimmed, de-duplicated | normalized |
| Version number | Yes (server-assigned) | Monotonic per document/template; activated version immutable | 409 `CONFLICT` on duplicate version number |
| New version upload | Yes | Creates a new version; never overwrites an existing one | 409 `CONFLICT` |
| Template shareability | Conditional | A `referenceOnly`/non-shareable template cannot be `COPY_ON_ADOPT`/shared | 400 `VALIDATION_FAILED` |
| Share mode | Yes | `REFERENCE` or `COPY_ON_ADOPT`; copy gated by feature flag | 400 `VALIDATION_FAILED` |
| Share target company | Yes | MOD-0220 ACTIVE + referenceable LegalEntity in the same tenant | 404 `NOT_FOUND_NON_LEAKAGE` / fail-closed |
| Folder-share branch | Yes | Selected `CollectionInstance` branch resolves; only included nodes are shared | 400 `VALIDATION_FAILED`; no unselected exposure |
| Dry-run gate | Yes | Execute disabled until a non-blocked folder-share dry-run for the current selection | Execute stays disabled |
| Binary storage availability | Yes | Content abstraction must accept the file before metadata commit | controlled failure, no metadata orphan |
| Folder upload permission | Yes | User must hold folder-level `canUploadDocument` (or equivalent) for the target `CollectionInstance` | 403 `PERM_DENIED` (visible folder) |
| Document view permission | Yes | User must have document-level or inherited folder-level view access | 403 `PERM_DENIED` or 404 `NOT_FOUND_NON_LEAKAGE` per repo convention |
| Version-create permission | Yes | User must hold version-create + document/inherited `canUploadNewVersion` | 403 `PERM_DENIED` |
| Share permission | Yes | User must hold the share permission for the source item | 403 `PERM_DENIED` |
| Cross-company access | Conditional | Another company's document is reachable only via an explicit share | 404 `NOT_FOUND_NON_LEAKAGE` |
| Access-policy scope | Yes | An `AccessPolicy` grant can never target outside the tenant; an override can only narrow, never cross company/tenant isolation | 400 `VALIDATION_FAILED` / rejected |
| Access inheritance | No | Document access inherits from the folder unless explicitly overridden; result marks `inherited`/`explicit` | normalized |
| TenantId | Never client input | Resolved from tenant context | request contract rejected / test fails |
| Correlation id | All APIs | Non-empty; shared across the flow; body/header identical | generated server-side if absent |

## 13. Failure Path to Verify

- **Missing `CollectionInstance`** (attach target): 404 `NOT_FOUND_NON_LEAKAGE`; no document created.
- **Archived/inactive `CollectionInstance`** (attach/upload target): 400 `VALIDATION_FAILED` or 409 `CONFLICT`;
  no document/version created; `CollectionInstance` not mutated.
- **Wrong company/legal-entity scope** for the folder: 404 `NOT_FOUND_NON_LEAKAGE` (or 403 per repo convention);
  no leaked id.
- **Cross-tenant folder/document/template** access: 404 `NOT_FOUND_NON_LEAKAGE`; no leaked id.
- **No global/module permission (Layer 1)**: 403 `PERM_DENIED`; no side effect, no success audit — even if a
  Layer 2 resource grant exists (Layer 1 participation gate is mandatory).
- **No folder-upload policy (Layer 2)**: 403 `PERM_DENIED`; no document/version written into the folder.
- **No document-view policy (Layer 2)**: 403 `PERM_DENIED` (or 404 `NOT_FOUND_NON_LEAKAGE` per repo convention);
  no leaked metadata.
- **No document-download policy (Layer 2)**: 403 `PERM_DENIED`; no bytes streamed.
- **No document edit/version/share policy (Layer 2)**: 403 `PERM_DENIED`; no new version / no share record.
- **No `access.manage` (Layer 1) or `canManageAccess`/`canManageFolderDocumentAccess` (Layer 2)**: 403
  `PERM_DENIED`; AccessPolicy unchanged.
- **Cross-company document access without an explicit share**: 404 `NOT_FOUND_NON_LEAKAGE`; no leaked id.
- **AccessPolicy / override attempting to widen across tenant or company**: 400 `VALIDATION_FAILED`; rejected;
  isolation preserved (override may only narrow / make explicit).
- **Unsupported file type**: 400 `VALIDATION_FAILED`; no version stored.
- **Duplicate version number**: 409 `CONFLICT`; no second row.
- **Overwrite attempt** of an `ACTIVE` version: rejected; a new version is required.
- **Source template not shareable**: 400 `VALIDATION_FAILED`; no share record/copy.
- **Share target company not found / inactive**: 404 `NOT_FOUND_NON_LEAKAGE` / fail-closed; cancellation
  preserved.
- **Binary storage unavailable**: controlled failure with reason code; **no metadata orphan** (no dangling
  document/version row).
- **Folder-share copy/share partial failure**: per-item `FolderShareOutcome` (`FAILED` + reason_code +
  retryable); honest counts; no fabricated success.
- **Unselected branch / unrelated template**: never shared; dry-run reports skipped/excluded; execute does not
  expose it.
- All controlled failures carry `reason_code` + body/header `correlation_id`; no stack traces / exception text.

## 14. Authorization Convention

- Policy: `[Authorize]` on the tenant-facing controller; `[HasPermission]` per semantic action.
- Actor type: `tenant_user`; runtime canonical format PKS-001 lowercase dotted keys under
  `platform.document-management.{resource}.{action}`.
- Spec keys remain traceable directional aliases only if MOD-0018/security approves; reverse/dynamic aliases
  prohibited.

### Two-layer authorization model (APPROVED)

FU01 authorization is **two layers, both of which must pass (Layer 1 AND Layer 2)**. This model is the
approved decision; the rejected alternative ("manage every per-folder/per-document grant as a central
permission key") is **explicitly out** — modelling per-folder/per-document access as catalog permission keys
causes catalog explosion. **Per-folder/per-document access is domain data, not a permission-catalog key.**

**Layer 1 — central RBAC / global module permission** (catalog key, `[HasPermission]`): decides the user's
*general* document-management capability — e.g. can the user see the module, create a document, upload a new
version, share, or manage access policy. Examples (the §14 key table):
`platform.document-management.controlled-documents.view` / `.create` / `.version.create` / `.share` /
`.access.manage`.

**Layer 2 — MOD-0029 resource-level AccessPolicy** (tenant/company/resource-scoped **domain data**, owned in
MOD-0029 collections, **not** managed from the central permission screen): decides the *actual* access to a
specific folder or a specific document/template.

- `FolderDocumentAccessPolicy`: `canViewFolderDocuments`, `canUploadDocument`, `canEditFolderDocuments`,
  `canUploadNewVersion`, `canShareFolderDocuments`, `canManageFolderDocumentAccess`.
- `DocumentAccessPolicy`: `canView`, `canDownload`, `canEditMetadata`, `canUploadNewVersion`, `canShare`,
  `canManageAccess`.

**Authorization rule — both layers required:**

- Global permission present **but** no resource policy grant → **denied**.
- Resource policy grant present **but** no global permission → **denied**.
- Only **Layer 1 AND Layer 2** together allow the operation.

**Edge-case decision (Layer 1 participation gate is mandatory):** if a user has a document-level share/access
grant **but lacks the global/module permission**, the **standard decision is: access is NOT granted.** A
resource share/access is a **narrowing/specializing** layer *inside* the global permission; it **never
substitutes for** the global permission. A future external/limited-user single-document-share scenario, if
ever wanted, must be designed as a **separate follow-up** (§20) — FU01 does **not** silently support it.

**Ownership:**

- **Layer 1 ownership = central Permission/RBAC.** FU01 only uses `[HasPermission]` (backend) + the frontend
  global-permission gate. If permission seed/alias requires a protected security path
  (`services/Diten.AuthService/**`), it stays a **separate MOD-0018/security task** (FU01 never edits it).
- **Layer 2 ownership = MOD-0029.** FU01 owns `FolderDocumentAccessPolicy` and `DocumentAccessPolicy`, managed
  through the MOD-0029 document/access UI. **The central permission screen never manages per-document /
  per-folder ACL data.**

**Grantee model (Layer 2, kept flexible/extensible):** access targets are addressed by a typed grantee key,
following the workflow-candidate pattern: `user:{id}`, `role:{id}`, `position:{id}`, `group:{id}`,
`company:{id}` (and `plant:{id}` / `business-unit:{id}` for the visibility scopes). **First version may support
`user` / `role` / `company` only**, but the model must stay open to `position` / `group` later without a
schema break.

**Tenant isolation (Layer 2):** every AccessPolicy is tenant-scoped; `TenantId` is **never** taken from the
client payload — it is resolved server-side from tenant context. An AccessPolicy can **never** widen across
tenant/company isolation; a document-level override may **narrow** the folder-inherited grant or make it
`explicit`, but it can **not** leak to another tenant/company. **Cross-company access is possible only through
an explicit share policy.**

**Backend authoritative:** the frontend only hides/disables buttons for UX; the **backend always re-checks
Layer 1 (global permission) AND Layer 2 (resource AccessPolicy)** even when a UI gate appears to allow it.

Proposed FU01 permission keys (Layer 1 — central catalog keys; **minimal**, no per-folder/per-document keys):

| Key | Endpoint(s) |
|---|---|
| `platform.document-management.controlled-documents.view` | document list/detail/version view |
| `platform.document-management.controlled-documents.create` | create controlled document |
| `platform.document-management.controlled-documents.version.create` | upload new document version |
| `platform.document-management.controlled-documents.version.view` | view document versions |
| `platform.document-management.controlled-documents.share` | share a controlled document |
| `platform.document-management.controlled-documents.access.manage` | manage a document's access policy (who can view/download/edit/version/share) |
| `platform.document-management.folder-documents.upload` | folder-level upload of a document/template into a `CollectionInstance` node |
| `platform.document-management.folder-documents.access.manage` | manage folder-level document permissions (`FolderDocumentAccessPolicy`) |
| `platform.document-management.templates.view` | template list/detail |
| `platform.document-management.templates.create` | create template |
| `platform.document-management.templates.version.create` | upload new template version |
| `platform.document-management.templates.share` | share a template |
| `platform.document-management.folder-shares.create` | folder/branch share dry-run + execute |
| `platform.document-management.folder-shares.view` | folder-share operation status/outcomes |

Permission strategy (controlled gate):

**Layer 1 seed status: `DONE / PASS` — the 14 §14 catalog keys are now seeded in AuthService.**
The MOD-0018/security task seeded all 14 canonical lowercase keys into
`services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs` (MOD-0028 pattern) and added a
14-`InlineData` seed contract test in
`services/Diten.AuthService/tests/.../Authorization/DocumentManagementPermissionSeedTests.cs`. `PermissionAliasMap`
was **not** needed (new canonical keys; no legacy alias). **SuperAdmin is granted automatically** (the default
role template gives SuperAdmin the full catalog). Tests passed: seed + role-template `34/34`, full
`Diten.AuthService.Application.Tests` `167/167`, Platform `PermissionAliasResolverTests` `19/19`,
`git diff --check` clean.

**FU01 implementation IS allowed to:**
- add Platform-local permission **constants** (e.g. extend `DocumentManagementPermissions` in
  `Diten.Platform.Application`) and `[HasPermission]` **attributes** on the controller actions;
- use the **same lowercase effective key** in backend and frontend (now backed by the seeded catalog).

**FU01 implementation is still NOT allowed to:**
- edit the AuthService `DataSeeder` or its seed tests (the seed task is already done);
- modify any protected security-owned path.

**Release / runtime validation:** the catalog seed is complete, so the gate is no longer blocked on a missing
seed. **A runtime tenant-user smoke still requires granting these keys to the proper tenant role/user** —
`platform.*` keys are deliberately **not** auto-granted to tenant roles (privilege-escalation boundary, same as
MOD-0028), so this is a **runtime entitlement/grant step, not a missing catalog seed**. If that grant is absent,
the backend **correctly fails closed with `403 PERM_DENIED`** and the UI gate shows hidden/disabled controls.

- Backend and frontend resolve the **same** effective lowercase key; hidden/disabled controls are the UI
  expression of the backend's 403.

### Permission-filtered search (server-side, non-leakage)

The Explorer search is **backend-supported**, not only a frontend DataTable filter. Search results are filtered
**server-side** by, in order: **tenant → company/legal entity → selected active Documentation Structure /
`CollectionInstance` structure → folder-level access policy (Layer 2) → document-level access policy (Layer 2)
→ share policy → Layer 1 global permission → Layer 2 resource policy**. This reuses the **already-approved**
two-layer model (no new authorization decision):

- An item the user has **no access** to **simply does not appear** in results — names/titles/paths of
  unauthorized folders/documents/templates **must not leak**.
- Cross-company items appear only via an explicit share; raw, non-instantiated published baselines never appear.
- The same effective-permission flags returned per result (see §15 result DTO) are advisory UX only; the
  backend remains authoritative on every subsequent action.

- The existing `/api/v1/document-management/{everything}` catch-all already supports `GET, POST, PUT, PATCH,
  DELETE, OPTIONS` (after the FU04 widening). **FU01 uses GET/POST only**, so **no gateway change is
  anticipated** — verify and confirm; if a new explicit route is somehow required, it is a separate
  integration-agent task.
- All FU01 routes stay version-explicit under `api/v1/document-management`; no `v2`, no unversioned route.
- Frontend uses Gateway `5000` or a same-origin proxy; never the Platform API service port directly.

Candidate endpoints (names may adjust to repo convention):

| Method | Path | Used by |
|---|---|---|
| POST | `api/v1/document-management/controlled-documents` | create controlled document |
| GET | `api/v1/document-management/controlled-documents` | document library list |
| GET | `api/v1/document-management/controlled-documents/search` | Explorer permission-filtered mixed search |
| GET | `api/v1/document-management/controlled-documents/{documentId}` | document detail |
| POST | `api/v1/document-management/controlled-documents/{documentId}/versions` | upload new version |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions` | version list |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions/{versionId}` | version detail |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions/{versionId}/download` | controlled download (attachment) |
| GET | `api/v1/document-management/controlled-documents/{documentId}/versions/{versionId}/preview` | controlled inline preview (new tab; PDF/image; fallback download) |
| POST | `api/v1/document-management/controlled-documents/{documentId}/copy` | copy document to another authorized folder |
| POST | `api/v1/document-management/controlled-documents/{documentId}/move` | move document to another authorized folder |
| DELETE | `api/v1/document-management/controlled-documents/{documentId}` | soft delete / archive document |
| POST | `api/v1/document-management/controlled-documents/{documentId}/favorite` | toggle per-user favorite |
| POST | `api/v1/document-management/controlled-documents/{documentId}/share` | share document |
| POST | `api/v1/document-management/templates` | create template |
| GET | `api/v1/document-management/templates` | template list |
| GET | `api/v1/document-management/templates/{templateId}` | template detail |
| POST | `api/v1/document-management/templates/{templateId}/versions` | upload template version |
| GET | `api/v1/document-management/templates/{templateId}/versions` | template version list |
| GET | `api/v1/document-management/templates/{templateId}/versions/{versionId}/preview` | controlled inline template preview |
| POST | `api/v1/document-management/templates/{templateId}/copy` | copy template to another authorized folder |
| POST | `api/v1/document-management/templates/{templateId}/share` | share template |
| POST | `api/v1/document-management/folder-shares/dry-run` | folder/branch share dry-run |
| POST | `api/v1/document-management/folder-shares/execute` | folder/branch share execute |
| GET | `api/v1/document-management/folder-shares/{operationId}` | folder-share operation status + outcomes |
| GET | `api/v1/document-management/documentation-structures?companyId=...` | active instantiated Documentation Structures for the company (Explorer selector source) |

### Explorer permission-filtered search endpoint

`GET /api/v1/document-management/controlled-documents/search` (route name may adjust to repo convention, but a
**backend** search capability is required — frontend-only DataTable filtering is **not** sufficient). It uses
GET only, so the existing catch-all covers it (no gateway change).

**Candidate filters:** `companyId`, `activeStructureId` / `structureRootId`, `collectionInstanceId` (optional),
`scope = currentFolder | subtree | structure`, `query`, `documentType`, `includeTemplates`, `status`.
Default: search empty → current-folder contents; search active → entire selected structure, authorization-filtered.

**Mixed result DTO** (one shape for folders + documents + templates):

| Field | Notes |
|---|---|
| `ResultType` | `FOLDER` / `DOCUMENT` / `TEMPLATE` |
| `Id` | result id |
| `Name` / `Title` | folder name or document/template title |
| `FullPath` / `FolderPath` | path for breadcrumb display |
| `CollectionInstanceId` | owning folder node |
| `DocumentId` / `TemplateId` | when `ResultType` is DOCUMENT/TEMPLATE |
| `DocumentType` | document type when applicable |
| `CurrentVersion` | current version number |
| `Status` | item status |
| `ModifiedAt` / `UploadedAt` | last change |
| Permission flags | `canView`, `canDownload`, `canEditMetadata`, `canUploadNewVersion`, `canShare`, `canManageAccess` (advisory UX; backend stays authoritative) |

All results are server-side filtered per §14 *Permission-filtered search*; unauthorized items never appear and
their names/paths never leak.

## 16. Acceptance Criteria

- [x] FU01 pack promoted to `status: approved` after explicit user approval for this FU01 scope only
  (governance item; the implementation criteria below remain open until built and verified).
- [ ] Controlled documents/templates can be attached to a `CollectionInstance` folder (FU05 node consumed
  read-only: id, path, company binding, folder scope).
- [ ] SOP / Work Instruction document types are supported (alongside Policy / Form / Template / Other).
- [ ] Documents/templates have **immutable** version records; a new upload creates a new version, never an
  overwrite.
- [ ] The current/active version can be retrieved; previous versions remain accessible per permission.
- [ ] Templates can be marked `reusable` / `shareable` / `copyableOnAdopt` / `referenceOnly`.
- [ ] `REFERENCE` and `COPY_ON_ADOPT` semantics are documented and enforced (copy forks lineage; reference
  shares the active version).
- [ ] Folder/branch share dry-run shows folders included, templates included, templates skipped, conflicts,
  and permission issues.
- [ ] Execute shares/copies **only** the selected branch and its included templates; no unselected branch or
  unrelated item is exposed.
- [ ] Files are stored only via the approved binary/content storage abstraction; **no physical folder
  creation**, no direct filesystem storage, no raw bytes in Mongo.
- [ ] **No metadata orphan** if binary storage fails (controlled failure, no dangling row).
- [ ] LegalEntity/company users can attach documents/templates to **their own** `CollectionInstance` folders.
- [ ] Upload is allowed **only** when the user holds folder-level upload permission for that folder.
- [ ] Document view/edit/version/share actions are **permission-gated** (document-level or inherited folder-level).
- [ ] Document access can be **inherited from the folder or explicitly controlled per document**; the result
  marks `inherited`/`explicit` and an override never weakens tenant/company isolation.
- [ ] Cross-company document access is **blocked unless explicitly shared**.
- [ ] Folder/branch sharing can **include associated templates** according to the share policy.
- [ ] Backend authorization is **authoritative**; frontend gating is only UX.
- [ ] **No uncontrolled public file-drive behavior** is introduced.
- [ ] Tenant/company isolation enforced on every owned aggregate; `TenantId` never client-controlled;
  tenant-first indexes; no hard delete.
- [ ] One flow `correlation_id` shared across folder-share dry-run/execute; body/header parity.
- [ ] Permissions are the minimal FU01 subset; backend and frontend resolve the same effective lowercase key;
  missing permission → 403 `PERM_DENIED`.
- [ ] Controlled failures (400/403/404/409) return `reason_code` + `correlation_id`, no stack traces.
- [ ] TenantShell Controlled Documents surface (library + folder attachments + version history + upload +
  share + folder-share wizard) delivered with `Layout = "_LayoutTenantShell"`.
- [ ] No MOD-0028 structure edit / `CollectionDefinition` edit / FU05 instantiation change; no MOD-0030 /
  MOD-0031 side effect.
- [ ] Controlled Documents Explorer resolves the company's **active instantiated Documentation Structures**
  only (auto-select when one; selector when many); raw published baselines are never selectable document roots.
- [ ] Left tree is built from active `CollectionInstance` nodes (read-only seam); search-empty middle list
  shows the selected folder's contents.
- [ ] Search is **backend-supported** and permission-filtered server-side; a `Search in`
  (folder / folder+subfolders / structure) scope is honored; unauthorized items/names/paths never leak.
- [ ] Mixed search results (folder / document / template) carry path/breadcrumb + per-result permission flags;
  folder result navigates the tree, document/template result opens the detail/version panel.
- [ ] User can **add a document/template** to an authorized folder and **upload a new version** when authorized.
- [ ] User can **preview** PDF/image through the controlled backend endpoint (new tab; unsupported type →
  download fallback); no direct public file URL.
- [ ] User can **favorite/unfavorite** accessible documents/templates/folders (tenant+user scoped; favorite
  does not grant access).
- [ ] User can **copy/move** documents/templates to authorized folders per access rules (copy = new independent
  record by default; move = re-point `CollectionInstanceId`/`CollectionPath`, same company only).
- [ ] **Delete is soft-delete/archive** (no hard delete; version-history references preserved).
- [ ] **Folder tree mutations** (create/rename/move/delete/copy folder; CollectionInstance/Definition/Baseline
  mutation) are **explicitly deferred** to MOD-0028/follow-up and are **not silently implemented** in FU01
  (disabled placeholders only).
- [ ] Layer 2 enforcement is in place even though a **full folder/document access-management UI may be a
  follow-up**; folder/document ACL stays MOD-0029 domain data (never the central RBAC screen, which is Layer 1
  only).
- [ ] From search results, document/template actions (preview/download/favorite/copy/move/share/delete/upload
  new version) honor permissions; folder-mutation actions are disabled/deferred.

## 17. Test Expectations

Backend tests:
- attach controlled document/template to a valid `CollectionInstance`; missing instance → 404 non-leakage.
- create version, then upload a second version → two immutable rows; duplicate version number → 409.
- active version queryable; previous version readable; overwrite of active version rejected.
- template flags honored; non-shareable/`referenceOnly` template share → 400 `VALIDATION_FAILED`.
- `REFERENCE` share → target uses source active version; `COPY_ON_ADOPT` → independent copy + forked lineage.
- folder-share dry-run reports included folders/templates, skipped, conflicts, permission issues; execute
  shares only the selected branch; unselected branch not exposed; per-item outcomes + honest counts.
- MOD-0220 share-target success / not-found / inactive / unavailable → fail-closed; cancellation preserved.
- binary storage unavailable → controlled failure, no metadata orphan.
- missing permission 403; cross-tenant 404 non-leakage; correlation id preserved across the flow.
- no physical folder/filesystem write; no MOD-0030/0031 side effect.
- Explorer structures: only active instantiated structures returned for the company; non-instantiated published
  baselines and other companies' structures excluded.
- search scope `currentFolder` vs `subtree` vs `structure` returns the correct node set; empty query → current
  folder only.
- permission-filtered search: a folder/document/template the user lacks Layer 1 OR Layer 2 access to is **absent**
  from results (no name/path leakage); cross-company item appears only via an explicit share.
- mixed result DTO carries `ResultType` + path + correct per-result permission flags.

Frontend tests/smoke:
- library opens in TenantShell (`_LayoutTenantShell`); add document/template form renders; upload + new version
  works; version history panel renders.
- Explorer: company + Documentation Structure selector resolves active structures; folder tree renders; empty
  search shows current-folder contents; active search shows structure-wide authorized results with breadcrumb;
  `Search in` scope dropdown switches folder / subtree / structure; folder result navigates, doc/template result
  opens the detail panel.
- share document/template controls gated by permission; folder-share wizard: source branch → target company →
  include templates → share mode → dry-run preview → execute disabled until a valid dry-run → results.
- no direct service-port call; no client `TenantId`/`X-Tenant-Id`; controlled `reason_code`/`correlation_id`
  display; no stack traces.

Build/verify: `dotnet build` Platform API + Diten.Web; relevant Platform tests; DataTable verifier (if Python
available); RESX parity for tenant languages; `git diff --check`; protected-path verification.

> **Known environment caveat (carried from FU03/FU04/FU05):** a running local fleet can lock service DLLs
> (use `--no-build` tests or an isolated build), the browser smoke needs a permissioned tenant session, and
> the DCP-002 module-id preflight (`verify_module_id.py`) needs a working Python — deferred runtime/preflight
> checks are recorded as validation debt, not silently skipped.

> **Permission validation note:** the Layer 1 catalog seed is **DONE** (14 keys in AuthService `DataSeeder.cs`,
> SuperAdmin auto-granted, tests green). A browser smoke / E2E run may still show `PARTIAL` **only if** the
> seeded keys have not been granted to the specific tenant role/user under test — that is a **runtime
> entitlement/grant step, not a missing permission-catalog seed**. With the grant applied, gated actions
> succeed; without it, the backend correctly returns `403 PERM_DENIED`.

L10n validation (TenantShell):
- **RESX parity** across `ar, en, es, fr, ru, tr, zh` — all 7 `ControlledDocumentsIndex.{lang}.resx` share the
  identical key set (RESX parity verifier).
- **`requiredKeys` sync** between `_IndexL10n.cshtml` and `index.l10n.js` (no missing-key `[L10N WARNING]`,
  no undefined `window.L10n.*`).
- **No approval/review/e-signature labels** (FU01 approval workflow is out-of-scope).
- **No hardcoded frontend text** — all UI strings resolve through `window.L10n.*` / RESX.
- **`reason_code` / `correlation_id` display localized** (incl. `ReasonStorageUnavailable`); no raw codes,
  no stack traces shown.

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved the FU01 controlled-document / SOP / work-instruction /
  versioning / template-sharing foundation scope; status set to `approved` on 2026-06-22.
- [x] **DCP-002 module-identity gate for `MOD-0029` + `MOD-0029-FU01`:** Blueprint canonical name
  `MOD-0029 — Controlled Documents (SOPs/Work Instructions)` confirmed; registry rows for `MOD-0029` and
  `MOD-0029-FU01` added to `execution/registries/module-id-registry.md`; preflight `verify_module_id.py` run
  green (`OK MOD-0029`, `OK MOD-0029-FU01`, `--check-all` 0 HARD violations). **GATE PASSED**
- [x] **Binary/content storage abstraction decision documented:** `APPROVED AS PHASED STORAGE ARCHITECTURE`
  via the `IContentStorageGateway` seam (see §8 *Storage Architecture Decision*), with the "no metadata orphan
  on failure" contract (storage-first commit + best-effort delete + orphan-cleanup follow-up).
  - Phase 1: `LocalFileSystemContentStorageGateway` allowed **only through the seam** (config-driven root,
    never under `wwwroot`, no public URL, backend-gated download, sanitized key/path, SHA-256, no raw bytes in
    Mongo, no direct controller filesystem code).
  - Phase 2: external provider migration planned (MinIO / S3-compatible / Azure Blob / dedicated storage server
    / MOD-0266) behind the **same interface** — provider impl changes only; migration/reconciliation plan
    required; **no domain/application rewrite**.
  - Provider migration and orphan-sweep reconciliation remain **follow-up items** (§20). External-provider
    integration stays a **controlled gate** until a provider exists. **CONTROLLED GATE (Phase 2)**
- [x] **CollectionInstance read-only consumption seam confirmed:** `APPROVED WITH READ-ONLY READER CONTRACT`.
  `CollectionInstance` entity exists (`Diten.Platform.Domain/Entities/DocumentManagement/CollectionInstance.cs`,
  metadata-only) and exposes all fields FU01 needs (Id, TenantId, CompanyId, ScopeBindings, CanonicalId,
  ParentCanonicalId, BaselineReleaseId, Name, FullPath, InstanceStatus, CreatedAt/CreatedBy). Implementation
  **must create/use `ICollectionInstanceReferenceReader`** (read-only; see §10) and **must NOT inject the mixed
  read/write `ICollectionInstanceRepository` directly** (it has `CreateAsync`/`CreateManyAsync`/`ArchiveManyAsync`/
  `ReactivateManyAsync`). Consumes id / path / company binding / folder scope only; **no FU05 mutation**;
  branch/descendants derived read-only from `FullPath` prefix / `ParentCanonicalId`. **GATE PASSED**
- [ ] **Versioning + sharing contract documented:** immutable versions, active-version resolution,
  `REFERENCE` vs `COPY_ON_ADOPT` lineage, folder-share dry-run/execute outcome shape. **CONTROLLED GATE**
- [x] **Permission / access-control ownership gate:** `APPROVED AS TWO-LAYER AUTHORIZATION MODEL` (see §14).
  - **Layer 1 = central RBAC / global module permission** (catalog `[HasPermission]` keys); owned by central
    Permission/RBAC + MOD-0018/security.
  - **Layer 2 = MOD-0029 `FolderDocumentAccessPolicy` / `DocumentAccessPolicy`** (tenant/company/resource-scoped
    domain data); owned by MOD-0029.
  - **Backend rule: Layer 1 AND Layer 2** (both must pass; resource grant never substitutes for global
    permission). The **central permission screen never manages per-document/per-folder ACL data** (rejected to
    avoid catalog explosion).
- [x] **Permission keys finalized (Layer 1) — `DONE / PASS`:** the MOD-0018/security task seeded all **14 §14
  Layer 1 canonical keys** into the AuthService `DataSeeder.cs` (MOD-0028 pattern) and added 14 `InlineData`
  seed contract tests (`DocumentManagementPermissionSeedTests.cs`). `PermissionAliasMap` not needed (new
  canonical keys; no legacy alias). **SuperAdmin granted automatically** (full-catalog role template). Tests
  green: `34/34` seed+role-template, `167/167` full AuthService suite, `19/19` Platform alias resolver,
  `git diff --check` clean. No per-folder/per-document keys (Layer 2 domain data). **GATE PASSED.** Remaining:
  a **runtime tenant role/user grant** for browser smoke/E2E (runtime entitlement step, not a missing seed —
  see §20). FU01 may proceed with Platform-local constants + `[HasPermission]`.
- [ ] **Gateway route compatibility verified** (existing catch-all GET/POST is sufficient; no new route).
  **CONTROLLED GATE**
- [x] **TenantShell L10n key set prepared — `PASS`:** the MOD-0028 (QmsBaselines/Instantiations) TenantShell
  L10n pattern is confirmed (`_IndexL10n.cshtml` JSON → `index.l10n.js` `toPascalCase` → `window.L10n`, marker
  class, `SharedResource` reuse). **7-language parity required: `ar, en, es, fr, ru, tr, zh`.** The
  ControlledDocuments surface will add its own `_IndexL10n.cshtml`, `index.l10n.js`, `ControlledDocumentsIndex.cs`
  marker, and 7 `ControlledDocumentsIndex.{lang}.resx` files (identical key sets); generic DataTable/toast/common
  labels reuse `SharedResource`. `reason_code`, access-denied, storage and validation messages are localized.
  **Approval-workflow labels must not be added** (FU01 approval workflow is out-of-scope). Key groups + file
  plan recorded in §11. **GATE PASSED**
- [x] **Approval-workflow boundary confirmed — `PASS`:** approval workflow, approver/reviewer assignment,
  formal review state machine, e-signature, approval routes/notifications, and MOD-0023 workflow-engine
  integration are all **out of FU01 scope** (unless the MOD-0029 parent approves a later wave). FU01's `ACTIVE`
  is a **technical activation** (current-version resolution), not a formal approval. Fail-safe approval guard +
  MOD-0023 boundary recorded in §19; approval lifecycle stays a §20 follow-up. **GATE PASSED**
- [ ] `golden_reference: compact` + `form_field_count: 10` accepted (multi-field add-document form).
- [ ] `entity_base: TenantScopedEntity` accepted (confirmed by FU01/FU02/FU05).
- [ ] FU01 test matrix and protected paths accepted.

### Ready-for-implementation summary

**All FU01 implementation-precheck controlled gates are now satisfied:**

| Gate | Status |
|---|---|
| DCP-002 / registry | ✅ PASS (MOD-0029 + MOD-0029-FU01 reserved; verifier green) |
| Storage architecture | ✅ PASS (`IContentStorageGateway` phased; Phase 1 local seam; Phase 2 later wave) |
| CollectionInstance read-only consumption seam | ✅ PASS (`ICollectionInstanceReferenceReader` required; mixed repo not injected) |
| Two-layer authorization model | ✅ PASS (Layer 1 RBAC AND Layer 2 MOD-0029 AccessPolicy) |
| Layer 1 permission seed | ✅ PASS (14 keys seeded; tests green; SuperAdmin auto-grant) |
| Approval-workflow boundary | ✅ PASS (`ACTIVE` = technical activation; no approval/e-sign/MOD-0023) |
| TenantShell L10n key set | ✅ PASS (MOD-0028 pattern; 7-language parity; key groups + file plan) |

**Remaining items are implementation-time / runtime follow-ups (not preconditions):**

- **Tenant role/user runtime grant** of the 14 Layer 1 keys for browser smoke / E2E (runtime entitlement step;
  `platform.*` keys are not auto-granted to tenant roles — escalation boundary).
- **Phase 2 external storage provider** (MinIO / S3 / Azure Blob / MOD-0266) — a later wave behind the
  unchanged `IContentStorageGateway`.
- **Translation quality/parity** for `ar/en/es/fr/ru/tr/zh` RESX during implementation.
- **Tenant session / browser-smoke availability** (permissioned tenant session needed for the UI smoke).

## 19. Implementation Notes

- FU01 is the MOD-0029 foundation: it consumes MOD-0028-FU05 `CollectionInstance` nodes read-only and attaches
  controlled documents / SOPs / work instructions / templates with immutable versions and controlled sharing.
  It never edits folder structure, `CollectionDefinition`, or FU05 instantiation.
- MOD-0028 owns folder/tree/structure metadata; **MOD-0029 owns controlled document records, SOP/work
  instruction files, template files, file references, versions, sharing, and document-control metadata.**
- Files go only through the approved binary/content storage abstraction (`FileRef`/`ContentRef`); FU01 never
  creates physical folders, never writes the filesystem directly, and never re-implements the storage provider.
- Versioning is append-only: a new upload is a new immutable version; the active version is resolved by
  `CurrentVersionId`; previous versions stay readable per permission.
- Sharing defaults to `REFERENCE` for templates; `COPY_ON_ADOPT` is opt-in and feature-flag gated until copy
  lineage is verified. Folder/branch share reuses the FU05 dry-run → execute pattern and the same
  flow-correlation discipline.
- MOD-0220 is consumed read-only via its contract for share-target company validation; FU01 never modifies
  `Diten.MdmService`.
- Reuse FU01/FU02/FU05 contracts directly (`Response<T>` `reason_code`/`correlation_id`, route family,
  `[HasPermission]`, `PermissionAliasMap`, typed feature flags, the jsTree folder renderer). Do not fork them.

### Approved Implementation Handoff (effective only after the user sets the pack to approved/ready-for-dev)

- Next executable action after approval: the orchestrator may implement **MOD-0029-FU01 (backend +
  TenantShell) only**.
- Allowed: `ControlledDocument`/`ControlledDocumentVersion`/`TemplateDocument`/`TemplateVersion`/
  `DocumentSharePolicy`/`FolderShareOperation`/`FolderShareOutcome`, attach/version/share/folder-share
  endpoints, the content-storage abstraction seam consumption, the TenantShell Controlled Documents surface,
  permission-gated controls, localization, frontend/backend tests.
- Storage: the **Phase 1 `LocalFileSystemContentStorageGateway` IS allowed** as an `IContentStorageGateway`
  implementation in Infrastructure (config-driven root, behind the seam, per §8 *Storage Architecture
  Decision*). What stays **not allowed** is inline controller/handler filesystem code, physical business-folder
  creation, raw bytes in Mongo, GridFS, and re-implementing/forking an **external** provider (that is the
  Phase 2 controlled-gate follow-up).
- Not allowed: editing MOD-0028 structure / `CollectionDefinition` / FU05 instantiation, an **external** binary
  storage provider implementation (Phase 2 controlled gate), physical business-folder creation, direct
  controller filesystem storage, OCR/indexing, e-signature, approval
  workflow (unless parent-approved), retention/legal hold (MOD-0030), evidence export (MOD-0031), external
  portal / public sharing / email, browser-based editing, gateway `ocelot.json` changes (unless a new route is
  unexpectedly required and a separate integration-agent task is opened), or AuthService seed/alias edits via a
  protected path.

**Approval guard (fail-safe — FU01 implementation must NOT create any of these):**

- an approval aggregate;
- an approval state machine entity;
- an approval request endpoint;
- an approval decision endpoint;
- a reviewer queue;
- an approver assignment;
- an e-signature integration;
- an approval notification;
- a workflow engine / **MOD-0023 integration**;
- approval-specific UI / review-approve screens;
- approval-specific audit beyond the generic MOD-0021 seam.

**MOD-0023 workflow-engine boundary:** **MOD-0029-FU01 does not integrate with the MOD-0023 workflow engine.**
Any reference to workflow-candidate-style grantee keys (`user:{id}` / `role:{id}` / `position:{id}` / …) is
**only a naming/modeling analogy** for access-policy grantees (§14 Layer 2), **not** a workflow integration.

## 20. Follow-up Items

1. **CollectionDefinition template binding follow-up:** binding template files at the `CollectionDefinition`
   (template/baseline) level so newly instantiated companies inherit templates — only if the MOD-0028 parent
   approves it; FU01 attaches templates to `CollectionInstance` only.
2. **Approval workflow follow-up (separate FU / later wave):** controlled-document review/approve lifecycle —
   a formal review/approve **state machine**, **approvers** / approver decisions, **e-signature if required**,
   and **workflow engine (MOD-0023) integration if required**. Out of FU01; implemented only **if and when the
   MOD-0029 parent approves it**. FU01's `ACTIVE` is technical activation only, never a formal approval gate.
3. **Retention / legal hold (MOD-0030):** retention enforcement over controlled documents/versions remains
   MOD-0030-owned, never FU01.
4. **Evidence export (MOD-0031):** evidence-pack export over controlled documents remains MOD-0031-owned.
5. **Content services follow-up:** OCR / full-text indexing / preview rendering / browser-based editing.
6. **Notification follow-up:** email/in-app notification on share/version events.
7. **Share governance follow-up:** revoke-share, share expiry, and reconciliation of `REFERENCE` shares when a
   source version is superseded.
8. **Retry follow-up:** retry of a failed folder-share subset (mirror of the FU05 retry pattern) once the
   synchronous flow is proven.
9. **Phase 2 external storage provider migration:** implement an external `IContentStorageGateway` provider
   (MinIO / S3-compatible / Azure Blob / dedicated storage server / MOD-0266) behind the unchanged interface,
   plus a content **migration/reconciliation plan** to move Phase 1 local content — no domain/application
   rewrite. **Controlled gate.**
10. **Orphan-cleanup / reconciliation sweep:** a background sweep that detects and removes stored content whose
    metadata commit failed (best-effort delete fallback), reconciling `ContentRef` ↔ stored objects.
11. **Storage production-hardening:** storage-root backup, path security/hardening, and a malware-scanning
    policy before Phase 1 local storage is treated as production-ready.
12. **External/limited-user single-document share:** giving access to a user **without** the Layer 1 global
    module permission (the edge case FU01 deliberately denies). Must be designed as a separate scope; FU01 never
    silently supports it.
13. **Self-access-explain diagnostic:** a "why can't I access this document?" endpoint / UI helper that explains
    the effective Layer 1 + Layer 2 decision. Not required for FU01; future usability follow-up.
14. **MOD-0029 Layer 1 permission seed — DONE:** the 14 §14 Layer 1 keys were seeded into the AuthService
    `DataSeeder.cs` (+ 14 `DocumentManagementPermissionSeedTests.cs` entries); SuperAdmin is granted
    automatically (full-catalog role template); AuthService `167/167` + Platform alias `19/19` green. **The
    AuthService catalog seed is no longer missing.** Remaining follow-up: a **tenant role/user runtime grant**
    of these keys for browser smoke / E2E validation — a runtime entitlement step (`platform.*` keys are not
    auto-granted to tenant roles by the escalation boundary, same as MOD-0028). Until that grant is applied, the
    backend correctly fails closed with `403 PERM_DENIED`. **Runtime grant follow-up.**
15. **Company-instance folder-management (folder tree operations) — separate MOD-0028 extension:** create /
    rename / move / delete / copy-paste folder and any `CollectionInstance` hierarchy change are **MOD-0028-owned
    structure mutations**. FU01 consumes the tree read-only and must **not** implement folder mutation; the
    Explorer may surface **disabled placeholders** only. Requires a separately approved MOD-0028/structure (or
    company-instance-folder-management) extension scope.
16. **Full folder/document access-management UI — phased:** Layer 2 **enforcement** ships in FU01 (with
    defaults/inheritance), but the full UI to manage `FolderDocumentAccessPolicy` / `DocumentAccessPolicy` may be
    a follow-up. Candidate locations: a MOD-0029 **Access Control** page, a folder-details **access tab**, or a
    baseline/structure admin integration. This is **MOD-0029 Layer 2 domain data**, never the central RBAC
    permission screen (Layer 1 only).
17. **Office/browser-render preview:** DOCX / XLSX / PPTX inline render is a follow-up (FU01 supports PDF/image
    inline; unsupported types fall back to download) unless an existing viewer is available.
18. **Document/template copy reference-mode & paste UX:** richer copy semantics (reference vs independent copy),
    multi-select copy/paste, and cross-folder paste affordances beyond the FU01 default (independent copy of the
    current active version) are a follow-up.

Each follow-up requires its own approved or ready-for-dev scope. FU01 does not authorize any later wave, and
does not authorize approval workflow, retention, or evidence export.
