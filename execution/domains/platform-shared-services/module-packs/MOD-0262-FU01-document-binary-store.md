---
id: MOD-0262-FU01
name: Document Binary Store
parent: MOD-0262
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: TenantScopedEntity
status: ready-for-dev
status_note: "CT promoted draft → ready-for-dev 2026-09-07 after DCP-008 was approved and OD-1 / OD-3 / OD-4 / OD-A were resolved. Runtime implementation not started."
approved_by: control-tower
approved_on: 2026-09-07
owner: platform-shared-services
branch: feature/structured-content-messaging
started: 2026-09-07
target: TBD
form_field_count: 0
capability_pack: DCP-008
runtime_implementation: in-progress
---

# MOD-0262-FU01 — Document Binary Store

> **Status guard.** `ready-for-dev` (CT, 2026-09-07). Both CAP-001 §7 conditions are met: **DCP-008 is `approved`** and this pack has passed its own gate. Implementation starts **only** through `@orchestrator` `/add-module` with this pack as input — this document does not itself start code, and the branch/worktree decision is taken at that moment, not here.
>
> **Scope guard.** This authorization covers **`MOD-0262-FU01` only**. FU02…FU07 remain planned and unreserved.
>
> **Identity.** Canonical child of `MOD-0262` (Internal Document Repository Service). DCP-002 preflight: `verify_module_id.py --check-id MOD-0262-FU01 --name "Document Binary Store" --parent MOD-0262` → **exit 0**. No new `MOD-xxxx` is minted.

---

## 1. Module Summary

FU01 makes MOD-0262 **real as a bounded store**: it takes the existing MOD-0029-FU01 content-storage seam, moves it out of a consumer's feature folder into a shared contract layer, replaces its in-process-shaped contract with a stream-based one, and puts **authorization, tenant isolation and audit at the repository boundary** — where a service boundary will later be drawn.

It is deliberately the **narrowest useful slice**. Per DCP-008 **AD-1**, FU01 depends on **neither** MOD-0030 (Records Management, W-3), **nor** MOD-0031 (Evidence Linking, W-4), **nor** the undefined `DOC-REPO-BUNDLE` contract. That is what allows a W-1 / `R1 - PPM MVP` / RC=Y module with two HARD dependants (MOD-0028, MOD-0313) to ship without waiting on a W-4 module and an open EA item.

**Target users:** none directly — FU01 delivers **no page**. Its consumers are MOD-0028 / MOD-0029 (today), MOD-0162 and MOD-0313 (later FUs).

---

## 2. Ownership and Boundaries

**In scope**
- The `IContentStorageGateway` / `ContentRef` abstraction, relocated to a shared contract layer and owned by MOD-0262.
- Provider implementation(s) behind that abstraction (R1: local filesystem, carried over).
- Deterministic, **tenant-isolated** object keys.
- Checksum (SHA-256), media-type / extension / size validation.
- **Authorization enforced by the store itself** (DCP-008 AD-5).
- Audit emission to MOD-0021 for store / read / delete-compensation.
- Stream-based upload and download (DCP-008 AD-4).

**Out of scope (each owned elsewhere)**
- Document metadata, versioning, controlled-document lifecycle → MOD-0028 / MOD-0029.
- Evidence packages → MOD-0262-FU04.
- Physical destruction / disposition execution → MOD-0262-FU05.
- Repository Admin, Repository Health, Document Binary Store **pages** → MOD-0262-FU02 / FU03.
- CRM pointer resolution → MOD-0262-FU06.
- Extraction into a standalone `Platform Document Service` process → MOD-0262-FU07 (DCP-008 AD-3).
- `DOC-REPO-BUNDLE` payload definition → ⛔ blocked on **EA-BP-003**; FU01 must not define it.
- `EnterpriseStrategyService/UploadsController` → CT decision **D-5**, separate security lane.

**Owned by this FU:** document binary objects and their pointers. Nothing else.

---

## 3. Owned Objects

> Concrete class/endpoint names are **proposals for the `ready-for-dev` gate**, not frozen contracts. Names are finalised against the Golden Reference and existing MOD-0029 code at that gate.

| Kind | Item | Note |
|---|---|---|
| Contract (relocated) | `IContentStorageGateway` | Moved **out of** `Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/` into a shared contract location (see §5). Signature changes per AD-4/AD-5. |
| Contract (relocated) | `ContentStoreRequest` / `ContentStoreResult` / `ContentStreamResult` / `ContentStorageScope` | Same move. `ContentStoreRequest.Content: byte[]` is **removed**. |
| Domain | `ContentRef` | Existing embedded pointer — **preserved unchanged** (`ContentId`, `StorageProvider`, `ObjectKey`, `FileName`, `MediaType`, `ByteSize`, `Checksum`, `CreatedAt`, `CreatedBy`, `VersionId`). |
| Domain (new) | `RepositoryObject` | The store's own record of a stored object; the SoR row behind a `ContentRef`. Enables FU03 orphan reconciliation. |
| Provider | `LocalFileSystemContentStorageGateway` | Carried over; re-homed behind the relocated contract. |
| Application | Store / OpenRead / TryDelete use cases + authorization + audit decoration | Replaces the "caller authorises" assumption. |
| API | `POST …/repository/objects` (multipart upload → `ContentRef`) | AD-4: no `byte[]` in a JSON body. |
| API | `GET …/repository/objects/{contentId}/content` (stream) | Authorised by the store. |
| API | `GET …/repository/objects/{contentId}` (pointer metadata; never `ObjectKey`) | `ObjectKey` is internal and never returned. |
| Permission | `platform.document-repository.read` | See §14 and OD-3. |
| Permission | `platform.document-repository.manage` | |
| Frontend route | **none** | `shell: none`, `golden_reference: none`. |

---

## 4. Entity Fields

**`ContentRef` (embedded pointer) — unchanged, listed for completeness**

| Field | Type | Required | Rule |
|---|---|---|---|
| `ContentId` | Guid | yes | Store-assigned. |
| `StorageProvider` | string | yes | Provider discriminator (`local-filesystem` in R1). |
| `ObjectKey` | string | yes | **Internal only** — never serialised to any client. |
| `FileName` | string | yes | Sanitised. |
| `MediaType` | string | yes | Must pass the allow-list. |
| `ByteSize` | long | yes | Must be > 0 and ≤ configured maximum. |
| `Checksum` | string | yes | SHA-256, computed by the store, never accepted from the caller. |
| `CreatedAt` / `CreatedBy` | DateTimeOffset / string | yes | |
| `VersionId` | Guid | yes | Owning item's version. |

**`RepositoryObject` (new, `TenantScopedEntity`)**

| Field | Type | Required | Rule |
|---|---|---|---|
| `TenantId` | Guid | yes | From `TenantScopedEntity`; **mandatory** isolation dimension. |
| `ContentId` | Guid | yes | Unique per tenant. |
| `StorageProvider` / `ObjectKey` | string | yes | Resolution pair. |
| `Scope` | enum | yes | Extends today's `Documents` / `Templates`; extension is additive and backward-compatible. |
| `OwningItemId` / `OwningVersionId` | Guid | yes | Back-reference to the consumer's record. |
| `Checksum` / `ByteSize` / `MediaType` / `FileName` | — | yes | Mirrors `ContentRef`. |

**Mongo index needs**
- `{ TenantId: 1, ContentId: 1 }` — unique.
- `{ TenantId: 1, StorageProvider: 1, ObjectKey: 1 }` — unique; backs FU03 orphan detection.
- `{ TenantId: 1, OwningItemId: 1, OwningVersionId: 1 }`.
- ⚠️ Partial-index filters must not use `$ne` / `$not` (unsupported → startup crash); use `$type`. See `mongo-indexing.md`.
- ⚠️ Never index or sort **two** `DateTimeOffset` fields together (BSON-array "parallel arrays" failure).

---

## 5. Repo Scope

Concrete paths to be touched at implementation time (not now):

```text
services/Diten.Platform/src/Diten.Platform.Application/
    Features/DocumentManagementControlledDocuments/Services/IContentStorageGateway.cs   ← MOVED FROM
    (shared contract location INSIDE Diten.Platform.Application — OD-A resolved;         ← MOVED TO
     NOT Diten.Common; exact folder/namespace settled at implementation time)
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/     ← NEW
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentManagement/ContentRef.cs   ← unchanged
services/Diten.Platform/src/Diten.Platform.Domain/Entities/DocumentRepository/          ← NEW
services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/DocumentManagement/
    LocalFileSystemContentStorageGateway.cs                                             ← re-homed
services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs    ← NEW
services/Diten.Platform/tests/Diten.Platform.Application.Tests/DocumentRepository/       ← NEW
gateway/Diten.ApiGateway/ocelot.json                                                    ← route (see §15)
```

**Call sites that must keep compiling unchanged in behaviour** (AD-2 — the seam is preserved, not rewritten): `DocumentManagementControlledDocumentsController`, `DocumentManagementTemplatesController`, `DocumentManagementTemplateMastersController`, `DocumentVersioningService`, `ControlledDocumentRegistrationService`, `TemplateService`, `TemplateMasterService`, `TemplateVariantService`. **Eight** call sites (measured at implementation; the pack originally said six). All binary I/O funnels through `DocumentVersioningService`, whose single `StoreAsync` caller is what makes AD-4 surgical.

---

## 6. Protected Paths

- `.antigravity/**`
- `docs/System Capability & Implementation Blueprint - master 8.1.xlsx` — **EA/SoT-owned, never edited**
- `services/Diten.CrmService/**` — FU06's lane
- `services/Diten.EnterpriseStrategyService/**` — CT decision **D-5**, separate lane
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.DevEnablementService/**`, `services/Diten.PvgService/**`
- MOD-0029 retention/disposition: `…/Features/DocumentManagementRetention/**` — read as input only; FU01 changes **no** disposition behaviour

---

## 7. Dependencies

| Dependency | Type | State |
|---|---|---|
| MOD-0018 RBAC / ABAC Authorization | **HARD** | ✅ available |
| MOD-0021 Audit Trail Service | **HARD** | ✅ ~98% |
| MOD-0029-FU01 existing seam + local provider | reuse basis (AD-2) | ✅ in repo |
| `Diten.Platform` service + Mongo + gateway | infrastructure | ✅ |
| MOD-0030 Records Management | **NOT a dependency of FU01** (AD-1) | ❌ absent — deliberately excluded |
| MOD-0031 Evidence Linking | **NOT a dependency of FU01** (AD-1) | ⚠️ planned — deliberately excluded |
| `DOC-REPO-BUNDLE` contract | **NOT a dependency of FU01** (AD-1) | ⛔ EA-BP-003 open — deliberately excluded |

Reverse: **MOD-0028** and **MOD-0313** are Blueprint-recorded **HARD** dependants of MOD-0262.

---

## 8. Runtime Constraints

- Service: `Diten.Platform`, port **5057**; gateway **5000**.
- `entity_base: TenantScopedEntity` — repository objects are **tenant-owned**, not a cross-tenant catalog, so `GlobalEntity` is wrong here. (`TenantScopedEntity` extends `BaseEntity` and is what `TenantRepository<T>` requires; the frontmatter had followed the standard's table, which lagged the code.)
- Tenant isolation is **mandatory and structural**: `TenantId` participates in the object key, not only in the query filter.
- Soft delete on `RepositoryObject` metadata. **Physical deletion is not implemented in FU01** — see AD-6 and FU05.
- `TryDeleteAsync` remains **best-effort orphan compensation only**. ⛔ It is not a purge path and must never be reused as one.
- Binary bytes are never stored in Mongo; GridFS remains banned.
- Storage root stays outside `wwwroot` and is never served by a static/public URL.
- Standalone-Mongo guard: multi-document atomic writes must check `SupportsTransactionsAsync` before `StartTransaction`, with a compensation path.
- `Diten.Platform` uses a **global** `GuidSerializer(GuidRepresentation.Standard)` (`DependencyInjection.cs`) — there is **no** `RegisterClassMaps` in this service. The binary-Guid class-map trap is CrmService-specific; a round-trip test still proves Guid FKs persist queryably, not silently empty.

---

## 9. Layout & Shell Contract

`shell: none` — backend-only. No Razor view, no layout, no `wwwroot` asset, no RESX. The Blueprint soft pages (Repository Admin, Document Binary Store, Repository Health, Evidence Package Storage) belong to **FU02 / FU03 / FU04**, whose packs set their own `golden_reference` and `form_field_count`.

⚠️ Blueprint's `Module Pages` sheet still lists MOD-0262's **old** external-provider pages and contradicts `Blueprint_Data`'s four soft pages (**EA-BP-001**). FU01 is unaffected because it delivers no page; FU02/FU03/FU04 must not be authored until that contradiction is resolved.

---

## 10. Backend File Convention

`golden_reference: none`, so the Slim/Compact **frontend** contract does not apply. The backend layout still follows `module-pack-standard.md` §4:

```text
services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/
├── Commands/
│   ├── StoreRepositoryObjectCommand.cs
│   └── DeleteRepositoryObjectCommand.cs          (compensation only — NOT purge)
├── Queries/
│   ├── GetRepositoryObjectByIdQuery.cs
│   └── OpenRepositoryObjectContentQuery.cs
├── Handlers/
│   ├── CommandHandlers/                          ← separate folder (mandatory)
│   └── QueryHandlers/                            ← separate folder (mandatory)
├── Validators/
└── DocumentRepositoryModels.cs                   ← all DTOs in ONE file
```

Handler/validator names carry **no** `Command` / `Query` / `Request` suffix. One public type per file.

---

## 11. Frontend File Contract

**Not applicable** — `shell: none`, `golden_reference: none`, `form_field_count: 0`. No `verify_datatable_page.py` run and no `quality-gate-datatable` step is required for FU01 (they become mandatory for FU02/FU03).

---

## 12. Validation Rules

| Rule | Behaviour |
|---|---|
| File name | Sanitised before use; path separators and traversal sequences rejected. |
| Extension | Must be in the configured allow-list → `400` + `ValidationFailed`. |
| Media type | Must be in the configured allow-list → `400`. |
| Empty content | Rejected → `400`. |
| Size | `> MaxFileSizeBytes` → `400`. |
| Checksum | Computed by the store; a caller-supplied checksum is **ignored**, never trusted. |
| `TenantId` | Required and taken from the **authenticated context**, never from the request body. |
| `ObjectKey` | Never accepted as input and never returned in any response. |
| Scope | Must be a known `ContentStorageScope` value. |

---

## 13. Failure Path to Verify

| Scenario | Expected |
|---|---|
| Unauthenticated upload | `401` — enforced by the store, not by the caller (AD-5). |
| Authenticated but missing `…repository.manage` | `403`. |
| Tenant A requests tenant B's `ContentId` | `404` (not `403` — existence is not disclosed). |
| Tenant A supplies tenant B's raw `ObjectKey` | `404`; the key is not an addressable input. |
| Disallowed extension / media type / empty / oversize | `400` + reason code. |
| Metadata write fails after the bytes land | Compensation calls `TryDeleteAsync`; on `false`, an orphan-cleanup follow-up is recorded (never silently swallowed). |
| Physical object missing on read | `404`; the orphan is reportable (consumed by FU03). |
| Standalone Mongo (no replica set) | Transaction guard engages; no `-532462766` crash. |
| Attempt to reach a purge/destroy path | **No such path exists in FU01.** Asserted by test. |

---

## 14. Authorization Convention

Per **PKS-001** (`module.resource.action`, lowercase-dotted, ≥3 segments, hyphen-in-segment, closed action dictionary). `Diten.Platform` owns the `platform.*` namespace.

| Key | Action tier | Purpose |
|---|---|---|
| `platform.document-repository.read` | Tier 1 | Read pointer metadata / stream content. |
| `platform.document-repository.manage` | Tier 3 (`manage`, already in use) | Store objects, run compensation delete. |

**Reserved for later FUs — declared here so the namespace is not fragmented:** `platform.document-repository.health.read` (FU03), `platform.document-repository.evidence-package.read` / `.manage` (FU04), `platform.document-repository.purge.execute` (FU05).

- ⚠️ **AD-7:** `purge.execute` is a **separate** key; `manage` never implies it (SoD — destroying records is not administration).
- ⚠️ **AD-5:** the seam's current XML contract (*"Caller is responsible for all permission checks BEFORE invoking this"*) is **revoked**. Every entry point authorises independently; a remote caller's claim is never trusted.
- ⚠️ **OD-3 (open):** `platform.document-repository.*` vs extending MOD-0029's `platform.document-management.*`. PKS-001 §4 requires exactly one owning module per namespace, which favours a distinct namespace — but this is CT's call before `ready-for-dev`.
- Actor type: Platform actor. `Platform.*` PascalCase keys are **not** used (superseded by PKS-001).
- No permission **seed** is authorised by this pack; seeding is an implementation-time task with its own record.

---

## 15. Gateway / API Routing Decision

- A new Ocelot route is required for `…/document-repository/*` → `Diten.Platform` (5057). Integration-agent task at implementation time.
- ⚠️ Upload/download routes must be verified for **multipart and streaming** pass-through; the existing document path base64s through a proxy, which AD-4 retires.
- ⚠️ This repository's gateway performs **no** authentication (`AuthenticationOptions` count = 0 across 157 routes); auth is downstream. FU01 must therefore not assume any gateway-level protection.
- ⚠️ Proxy `204`/bodiless-status handling: the known `ForwardAsync` defect turns an upstream `204` into `500` by writing `"{}"`. Any new proxy action must use the `IsBodilessStatus` guard.

---

## 16. Acceptance Criteria

- [ ] `IContentStorageGateway` and its request/result records no longer live under `Features/DocumentManagementControlledDocuments/`; they resolve from a shared contract location owned by MOD-0262.
- [ ] All **eight** existing consumers (3 controllers + `DocumentVersioningService` + `ControlledDocumentRegistrationService` + `TemplateService` + `TemplateMasterService` + `TemplateVariantService`) compile and behave identically against the relocated seam (AD-2).
- [ ] `ContentStoreRequest` no longer exposes `byte[] Content`; upload is multipart/stream and returns a `ContentRef` (AD-4). A test asserts **no** binary payload is carried in a JSON body.
- [ ] Every repository entry point authorises independently; a test proves an unauthenticated and an under-privileged call are rejected **by the store** even when the caller claims prior authorisation (AD-5).
- [ ] Two tenants storing an identically named file receive distinct object keys; cross-tenant read by `ContentId` **and** by raw `ObjectKey` both return `404`.
- [ ] `ObjectKey` does not appear in any API response payload (asserted by serialisation test).
- [ ] SHA-256 checksum is computed by the store; a caller-supplied checksum is ignored.
- [ ] Store / read / compensation-delete each emit a MOD-0021 audit event.
- [ ] `RepositoryObject` round-trips under `Diten.Platform`'s global `GuidSerializer(Standard)` (no `RegisterClassMaps` in this service); a test proves Guid FKs are queryable (not silently empty).
- [ ] Mongo indexes created; no partial-index filter uses `$ne` / `$not`; the service starts cleanly on a standalone Mongo.
- [ ] **No purge, destroy, scheduler or cascade path exists in FU01**; a test asserts `TryDeleteAsync` is reachable only from the compensation path (AD-6).
- [ ] No `DOC-REPO-BUNDLE` payload is defined anywhere in this FU (EA-BP-003 guard).
- [ ] `verify_module_id.py --check-id MOD-0262-FU01 --parent MOD-0262` exit 0 and `--check-all` exit 0 with 0 HARD violations.
- [ ] No file under `frontend/` is touched; `services/Diten.CrmService/` and `services/Diten.EnterpriseStrategyService/` untouched.

---

## 17. Test Expectations

| Layer | Expectation |
|---|---|
| Unit | Validation matrix (§12), object-key determinism + tenant separation, checksum computation, compensation-on-metadata-failure. |
| Integration (Mongo) | Must use `MongoIntegrationHarness`. Index creation, class-map round-trip, standalone-Mongo transaction guard. |
| Authorization | Unauthenticated `401`; under-privileged `403`; cross-tenant `404` by `ContentId` and by raw `ObjectKey`. |
| Contract | Serialisation test: `ObjectKey` absent from responses; no `byte[]` in any request/response DTO. |
| Negative-capability | Assert the absence of a purge/destroy code path. |
| Frontend smoke | **N/A** (`shell: none`). |
| Verifier | `verify_module_id.py` (both modes). `verify_datatable_page.py` **N/A** for FU01. |
| RESX | **N/A** — no UI surface. |
| Build | Full solution build green; the **eight** existing consumers unchanged in behaviour. |

---

## 18. Ready-for-dev Checklist

**✅ CLOSED — CT, 2026-09-07.** Every decision gate below is resolved. The remaining unticked items are **kickoff-infrastructure confirmations**, not decisions: they are verified during `/add-module` **Phase 0** (orchestrator's infra check) and **do not block `ready-for-dev`**.

**Decision gates — all closed**

- [x] **DCP-008 is `approved` / `ready-for-execution`** (CAP-001 §7 — hard gate). — **approved 2026-09-07.**
- [x] **OD-A** — target location for the relocated seam. → **A shared contract location inside `Diten.Platform.Application`, NOT `Diten.Common`.** Cross-service contract extraction is deferred to FU07. The exact namespace/folder name is settled at implementation time against the Golden Reference and the existing MOD-0029 code.
- [x] **OD-3** — permission namespace. → **`platform.document-repository.*`, LOCKED.** PKS-001 §4 allows exactly one owning module per namespace, so MOD-0029's `platform.document-management.*` is **not** extended. The MOD-0018 owner confirms key registration at implementation time; the namespace choice is not reopened.
- [x] **DCP-008 OD-1** — seam relocation. → **Stays inside FU01** (AD-8). Splitting it would leave FU01 unable to start.
- [x] **DCP-008 OD-4** — R1 scope. → **FU01 alone is the `R1 - PPM MVP` commitment.** FU02/FU03 are R1 **stretch**, gated on EA-BP-001.
- [x] **Maximum object size** → **config-driven, default greater than 50 MB.** The legacy 50 MB `RequestSizeLimit` was a buffered-upload symptom; **AD-4's streaming contract removes that memory constraint.** The exact per-environment value is set at implementation time.

**Kickoff-infrastructure confirmations — verified in `/add-module` Phase 0 (non-blocking)**

- [x] Storage-root configuration and allow-lists confirmed for each environment. → *Phase 0, 2026-09-08: `ContentStorageOptions` SectionName preserved; appsettings unchanged.*
- [x] Gateway route + multipart/stream pass-through confirmed with the integration agent. → *Phase 0, 2026-09-08: ocelot route added (surgical, 40-line addition; `IsBodilessStatus` guard).*
- [x] Audit event taxonomy for repository operations agreed with the MOD-0021 owner. → *Phase 0, 2026-09-08: `AuditCategory.DocumentManagement`. Live emission still pending smoke (see §16 / §19 ⚠️ partial closure).*
- [x] Confirmation that FU01 introduces **no** permission seed, menu entry, or migration without its own record. → *Phase 0, 2026-09-08: no seed; keys auto-register via `PlatformPermissionAutoRegistrationWorker` at startup.*

---

## 19. Implementation Notes

- **Decisions are locked.** CONTROL TOWER closed OD-1, OD-3, OD-4 and OD-A on **2026-09-07** and promoted this pack to `ready-for-dev` (see §18). Implementation must not reopen them: the seam relocation stays inside FU01, the permission namespace is `platform.document-repository.*`, FU01 alone carries the R1 commitment, the relocated seam lands inside `Diten.Platform.Application` (not `Diten.Common`), and the maximum object size is config-driven with a default above 50 MB.
- **Why this FU exists in this shape.** Blueprint puts MOD-0262 at W-1 / R1 / RC=Y, yet its own `Dependency Gate` names MOD-0030 (W-3) and MOD-0031 (W-4). CT decision **D-3** refused to move the module's wave and instead sliced the FUs. FU01 is the slice that survives that inversion.
- **The seam is preserved deliberately.** `IContentStorageGateway` / `ContentRef` already abstract **eight** consumers; DCP-008 **AD-2** keeps them. This FU fills in behind the seam and re-homes it — it is not a rewrite.
- **In-process by decision, HTTP-shaped by design.** DCP-008 **AD-3**: R1 stays inside `Diten.Platform`; the contract is nevertheless designed for a boundary so FU07's extraction does not force a redesign.
- **The two hard risks this FU closes** (measured in `docs/analysis/mod-0262-internal-document-repository-prd-and-impact-analysis.md`): `ContentStoreRequest.Content` as `byte[]`, and the seam's written instruction that callers perform authorisation.
- **Retention boundary is untouched.** `DocumentDispositionService` states in code that it never deletes and that destruction is "a deliberate future task". FU01 does not become that task — FU05 does, and only once MOD-0030 has an owner.
- **`docs/platform/master-plan.md` still describes MOD-0262 as an external provider that may be skipped** (3 locations, out of `execution/` scope and therefore not corrected in the canonicalization pass). Treat this pack and the registry as authoritative; the legacy plan is stale.
- **Pack↔code reconciliation (2026-09-08, from FU01 `/add-module` Phase 0).** Three record drifts corrected against the implemented code: (a) **eight** consumers, not six — `TemplateMasterService` and `TemplateVariantService` also route through `DocumentVersioningService`; (b) `entity_base` is **`TenantScopedEntity`**, not `BaseEntity` (`TenantRepository<T>` requires it; the standard's table lagged the code); (c) **no `RegisterClassMaps`** in `Diten.Platform` — it uses a global `GuidSerializer(Standard)`, and the class-map trap is CrmService-specific (correctly not invented).
- **⚠️ KAPANIŞ (KISMİ) — 2026-09-08 (DK#10).** Code complete; static verification green — 13/14 §16 boxes (audit-event box is code-wired, **not** live-verified), full solution build green, the eight consumers behaviourally unchanged (ControlledDocument 49/49, Template 222/222), `verify_module_id` `--check-id`/`--check-all` exit 0 / 0 HARD. **Runtime smoke NOT run** (fleet down, 5057/5000 closed) → per DK#10 this is **not** ✅. Full suite 3758 pass / 155 pre-existing-or-environmental FAIL (77 macOS-mongod-path, 49 BSON-Timestamp, ~26 MOD-0029 domain + BL-279, 3 MOD-0029 attribution — none FU01's; the 3 attribution warrant a pre-FU01 baseline confirmation). Live-verification list owned by CONTROL TOWER: multipart `POST …/document-repository/objects`→201+ContentRef · streamed `GET …/{contentId}/content` · cross-tenant 404 (ContentId **and** raw ObjectKey) · 401/403 authz · audit events reaching MOD-0021 · four indexes present in the live DB · MOD-0029 upload/download unbroken. Keys `platform.document-repository.read|manage` auto-register at Platform startup (`PlatformPermissionAutoRegistrationWorker`) and land on the full-catalog SuperAdmin role → smoke uses a **platform-admin** token (no manual grant, no raw-Mongo rolePermissions write).
- **Branch reconciliation (2026-09-08).** Frontmatter `branch:` was the proposed `feature/pss/mod-0262-…`; CT declined a worktree and kept work on `feature/structured-content-messaging` (the active multi-workstream branch). Field updated to match. Commit/push deferred by CT until all workstreams are done.

---

## 20. Follow-up Items

| Item | Target |
|---|---|
| Repository Admin + Document Binary Store page | MOD-0262-FU02 (after EA-BP-001) |
| Repository Health / two-way orphan reconciliation | MOD-0262-FU03 |
| Evidence Package Storage | MOD-0262-FU04 — ⛔ blocked on EA-BP-003 + MOD-0031 |
| Physical disposition execution (AD-6 rules carried verbatim) | MOD-0262-FU05 — blocked on MOD-0030 identity |
| MOD-0162 consumer cutover (four untyped pointers) | MOD-0262-FU06 — ⛔ blocked on EA-BP-003 |
| Extraction to standalone `Platform Document Service` | MOD-0262-FU07 |
| Additional storage providers (S3 / Azure Blob) | post-R1, behind the unchanged seam |
| Antivirus scan + encryption-at-rest policy | post-R1 |
| ⚠️ `EnterpriseStrategyService/UploadsController` authentication gap | **CT decision D-5 — separate security lane, not a MOD-0262 follow-up** |
