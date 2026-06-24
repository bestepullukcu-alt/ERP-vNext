---
id: MOD-0028-FU03
name: Documentation Management Tenant Structure Baselines UI
parent: MOD-0028
previous: MOD-0028-FU02
domain: platform-shared-services
service: Diten.Platform
shell: tenant
golden_reference: compact
entity_base: none
status: approved
owner: platform-shared-services
branch: feature/pss/mod-0028-fu03-tenant-qms-baseline-ui
started: 2026-06-16
target: 2026-06-30
form_field_count: 0
---

# MOD-0028-FU03 - Documentation Management Tenant Structure Baselines UI

## 1. Module Summary

MOD-0028-FU03 is a **frontend-only** TenantShell UI follow-up to `MOD-0028 Documentation Management`. It is the next
step after `MOD-0028-FU01 Backend Contract Foundation` and `MOD-0028-FU02 QMS Workbook Import Profile for Structure Baselines`. FU03 implements
the tenant-facing screens that consume the **already validated, stable FU02 backend contract** — it adds **no backend,
gateway, or permission changes**.

FU03 delivers the tenant governance UI to:

- view structure baselines (list + detail);
- import a QMS workbook via **dry-run**, review the validation summary, then **commit** using the QMS import profile;
- view the imported nested definition tree;
- publish a DRAFT baseline into an immutable snapshot manifest.

### Naming reconciliation

This FU03 pack is semantically renamed from **Tenant QMS Baseline UI** to **Tenant Structure Baselines UI**. Tenant UI
labels should use:

- Menu: `Documentation Structures`
- List title: `Structure Baselines`
- QMS-specific action: `Import QMS Workbook`

QMS is a source/import profile, not the product-wide route or menu concept. Existing view/controller folders such as
`QmsBaselines` may remain as transitional implementation names only if already created; preferred future frontend
folder/module names are `StructureBaselines` or `DocumentationStructureBaselines`.

### Previous-foundation status context (consumed, not re-opened)

- **FU02 backend final validation: PASS.** The QMS workbook import profile / structure-baseline backend contract is
  stable.
- Gateway already routes `GET, POST, OPTIONS` for `/api/v1/document-management/{everything}`.
- Canonical workbook source format is the `last version` sheet with a dotted outline code; the backend parser builds
  the correct nested tree (never a flat tree) and preserves slash-containing atomic folder names.
- Backend responses use `Response<T>` with `reason_code` and body-level `correlation_id`.
- The five required permission keys are implemented and seeded.
- **Frontend must call Gateway port `5000` or a same-origin proxy, never Platform API `5057`.**
- FU03 does not modify FU01/FU02; it only **consumes** their HTTP contract.

### Approval Scope

- This pack is `status: approved` for the exact FU03 frontend TenantShell Structure Baselines UI scope only.
- The user explicitly approved the FU03 frontend scope: structure-baseline list page; QMS import wizard; workbook file
  select/upload UI; dry-run call; dry-run validation summary; error/warning/conflict/hierarchy findings display; commit
  action after a valid dry-run; baseline detail page; nested definition-tree viewer; publish confirmation flow;
  permission-gated buttons/screens; controlled `reason_code` error display; `correlation_id` support detail;
  localization/resources if repo convention requires; frontend smoke/specs if repo convention exists.
- The approval is **not** for: backend code changes, gateway changes, permission seed/alias changes, company
  instantiation, `CollectionInstance`, MOD-0220 LegalEntity adoption, MOD-0029 document lifecycle, MOD-0030
  retention/legal-hold, MOD-0031 evidence export, physical folder creation, document upload/storage beyond sending the
  workbook to the FU02 import API, or direct Platform API `5057` calls.
- FU03 does not re-open or expand FU01/FU02, and does not authorize any later MOD-0028 wave.

### FU03 boundary note — valid future needs that are NOT FU03

Manual structure create/edit, company adoption, and controlled document lifecycle are **valid, real business
requirements** — but they are deliberately **out of FU03** and must not be folded into the import UI:

- **FU03 is import-UI only:** an import wizard plus list/detail/tree/publish **consumer** screens over the FU02
  contract. It builds no manual structure, no company adoption, and no document lifecycle.
- **Manual create/edit** (build a structure without Excel, edit/reorder/move/soft-delete draft nodes) is tracked as
  **MOD-0028-FU04** (separate backend+frontend pack).
- **Company adoption / `CollectionInstance` provisioning** (apply a published baseline to a company/LegalEntity) is
  tracked as **MOD-0028-FU05** (separate pack).
- **Controlled document lifecycle** (document create/draft/review/approve/effective/archive, versioning, workflow) is
  owned by **MOD-0029**, never by MOD-0028. MOD-0028 owns documentation *structure/baseline governance* only.
- See §20 for the proposed scope of each follow-up. Each requires its own approved/ready-for-dev pack.

## 2. Ownership and Boundaries

### In scope

- TenantShell pages/components for Structure Baselines list, QMS import wizard (dry-run → review → commit), baseline detail,
  nested definition-tree viewer, and the publish flow.
- Permission-gated buttons/screens using the existing lowercase effective keys.
- API integration with the FU02 endpoints **exclusively through Gateway `5000` or a same-origin proxy**.
- Loading states, success toasts, controlled `reason_code` error messages, and `correlation_id` surfacing in an error
  detail/support area.
- Tenant localization for all required tenant languages and a DataTable v2 list surface where applicable.
- Frontend smoke/spec coverage per repo convention.

### Consumed, not owned

- FU02 endpoints, `Response<T>` envelope (`reason_code` + `correlation_id`), and validation-summary shape.
- The five effective permission keys (no new keys minted; no seed/alias change).
- The Gateway `5000` route family and the TenantShell layout/navigation conventions.
- MOD-0021 correlation surfaced from the response body for support/troubleshooting only.

### Explicitly out of scope

- Any backend change (`services/Diten.Platform/**`), gateway change (`ocelot.json`), or permission seed/alias change.
- Company instantiation, `CollectionInstance` provisioning, MOD-0220 LegalEntity adoption.
- Template master/version/variant, exception workflow, local-node management.
- MOD-0029 document lifecycle, MOD-0030 retention/legal-hold, MOD-0031 evidence export.
- **Physical file-system folder creation.**
- **Document upload/storage beyond sending the workbook bytes to the FU02 import API** (the workbook is governance
  metadata sent to dry-run/commit; it is never stored as a document or written to disk by FU03).
- Minting new permissions, new routes, or new backend fields.

## 3. Owned Objects

FU03 owns only frontend artifacts (no persisted entity, no backend object):

- A TenantShell **navigation/menu entry** for Documentation Management → Documentation Structures (only if repo convention
  requires a registered nav entry).
- **Pages/views:** baseline **list**, **import wizard** (upload + dry-run review + commit), baseline **detail**, and a
  **definition-tree viewer** (may be a panel within detail or a dedicated view per repo convention).
- **Client scripts:** the page JavaScript modules that call the FU02 endpoints, render the validation summary, render
  the nested tree, and drive the publish confirmation.
- **View models / DTOs** for the pages, if the repo's MVC convention uses them.
- **Localization resources** (`.resx` / L10n bridge payloads) for the tenant languages.
- Frontend smoke/spec files where the repo has a frontend test convention.

FU03 must **not** introduce any backend type, repository, command/handler, route, or permission.

## 4. Entity Fields

`entity_base: none` — FU03 creates **no persisted entity**. All data is read from, or written through, the FU02
backend endpoints. `TenantId` is never sent from the client (the backend resolves it from tenant context); the UI must
never expose or accept a `TenantId` field, override, or query parameter.

The UI renders the FU02 response shapes only (illustrative, confirm against the live FU02 DTOs during dev):

| FU02 response | Principal fields the UI renders |
|---|---|
| Baseline summary (list/detail) | id, baselineReleaseId, baselineVersion, status (DRAFT/PUBLISHED), snapshotHash, manifestId, definitionCount, createdAt, publishedAt |
| Import summary (dry-run/commit) | totalRows, importedDefinitionsCount, skippedRows, errors[], warnings[], duplicatePathConflicts[], invalidHierarchyFindings[], dryRun, committed |
| Definition (tree node) | canonicalId, parentCanonicalId, name, fullPath, displayOrder, status, + available metadata (purposeScope, requiredByScope, allowedDocClass, classification, retention hint, flags) |

## 5. Repo Scope

### Authorized FU03 implementation scope (after approval)

- `frontend/Diten.Web/Views/**` — TenantShell views for the Structure Baselines surfaces (exact module folder confirmed
  against the live tenant convention during dev; preferred `Views/DocumentManagement/StructureBaselines/`;
  transitional `Views/DocumentManagement/QmsBaselines/` only if already implemented).
- `frontend/Diten.Web/Controllers/**` — thin MVC controller(s) for the pages (and same-origin API proxy if the chosen
  API profile requires it).
- `frontend/Diten.Web/wwwroot/assets/js/**` — page JavaScript modules for the Structure Baselines pages.
- `frontend/Diten.Web/**` localization resources / L10n bridge for the tenant languages.
- Frontend test/smoke specs where the repo provides a convention.

### Out of FU03 repo scope

- All backend, gateway, and security-seed paths (see Protected Paths).

## 6. Protected Paths

- `.antigravity/**`
- `services/Diten.Platform/**` (all backend code — FU01/FU02 consumed read-only via HTTP)
- `gateway/Diten.ApiGateway/**` including `ocelot.json`
- `services/Diten.Platform.Common/**`
- `services/Diten.AuthService/**` (permission seed/alias)
- `services/Diten.MdmService/**`, `services/Diten.DevEnablementService/**`, `services/Diten.EnterpriseStrategyService/**`
- MOD-0029, MOD-0030, and MOD-0031 implementation files
- Binary storage internals and any physical file-system folder creation
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` and any frozen/legacy layout
- Parent pack and FU01/FU02 packs unless a separate governance reconciliation authorizes an update

## 7. Dependencies

| Dependency | FU03 usage |
|---|---|
| MOD-0028-FU02 | Supplies the QMS import profile / structure-baseline endpoints, `Response<T>` envelope, validation-summary shape — consumed, not modified |
| MOD-0028-FU01 | Supplies the `api/v1/document-management` family, `reason_code`/`correlation_id`, permission keys |
| MOD-0028 parent | Supplies tenant-facing governance UX direction and TenantShell placement rules |
| MOD-0032 / Gateway | Owns the `5000` routes already in place; FU03 calls them but changes nothing |
| MOD-0018 | Owns the permission keys FU03 gates on; FU03 adds none |
| TenantShell | Supplies `_LayoutTenantShell`, navigation, toast/confirm, and the tenant API/proxy convention |

## 8. Runtime Constraints

- **API host:** all calls go through Gateway `http://localhost:5000` or a same-origin MVC proxy. **Direct calls to
  Platform API `5057` are prohibited.** No hardcoded service port or absolute service URL in client JS.
- **Dry-run before commit is mandatory:** the commit action is enabled only after a dry-run returns a valid summary
  for the current workbook; the UI never commits an un-dry-run or invalid import.
- **No fabricated success:** if the backend returns a non-success `Response<T>`, the UI shows the controlled error and
  never renders a success state or empty-but-"done" view.
- **Controlled errors only:** error messages are derived from `reason_code` (mapped to localized copy); the raw
  `correlation_id` is shown only in an error detail/support area. Stack traces and internal exception text are never
  displayed.
- **Tenant isolation is server-enforced:** the UI sends no `TenantId`; a cross-tenant detail request returns 404 and
  the UI shows a generic not-found, never a leaked identifier.
- **Publish is DRAFT-only in the UI:** the publish control is shown/enabled only for a `DRAFT` baseline and always goes
  through a confirmation modal; a controlled failure (e.g. non-DRAFT, stale version) is surfaced, not hidden.
- **Governance metadata only:** the workbook bytes are sent to the import API; FU03 never writes a file to disk,
  creates a physical folder, or stores a document.
- **Auth handling:** if the chosen API profile is same-origin proxy, the HttpOnly token is read server-side by the MVC
  proxy and forwarded to the Gateway; browser JS never generates `document.cookie`, `access_token`, or
  `Authorization: Bearer`. If the tenant shell uses `direct-gateway-profile`, the shared `window.API.{service}` SSOT
  object is used.

## 9. Layout & Shell Contract

- Primary shell: `shell: tenant`.
- Primary Razor layout: every FU03 page declares `Layout = "_LayoutTenantShell";` explicitly
  (`frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml`).
- Primary actor type: `tenant_user`.
- No FU03 page may use `_LayoutPlatformAdmin.cshtml`, the frozen `_Layout.cshtml`, or a legacy backbone layout.
- The exact tenant module view/controller/route folder is confirmed against the live TenantShell convention during dev
  (preferred `Views/DocumentManagement/StructureBaselines/`; transitional `Views/DocumentManagement/QmsBaselines/`
  only if already implemented).
- PlatformAdminShell is not used; structure-baseline governance is a tenant surface.

## 10. Backend File Convention

Not applicable — FU03 writes **no backend code**. It consumes the FU02 backend contract over HTTP only. No
controller-side `ViewModel` data loading (No-ViewModel rule): data is fetched via AJAX/Fetch from the Gateway/proxy.

## 11. Frontend File Contract

`golden_reference: compact` because the module is multi-page and route-based (list + import wizard + detail + tree),
not a single slim offcanvas CRUD form. `form_field_count: 0` because FU03 has **no standard entity create/edit form**;
the import is an action/route-based wizard, and publish is a confirmed action — there is no slim/compact entity
offcanvas. The compact list conventions apply to the baseline list surface; the import wizard and tree viewer follow
spec-specific UX (mirroring the parent pack §11, which is compact but explicitly "not a simple CRUD DataTable").

Proposed surfaces (final folder/names confirmed against the live tenant convention; partials prefixed `_`):

- **Baseline list:** `Index.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `{Module}Index.cs`,
  `index.js`, `index.l10n.js` — DataTable v2 (`data-dt-standard="v2"`, skeleton loader, inline filter, Gateway/proxy
  API). Columns: baselineReleaseId, version, status badge (DRAFT/PUBLISHED), definitionCount, createdAt, publishedAt.
- **Import wizard:** route-based `Import.cshtml` (+ `import.js`): file picker (`.xlsx` only) → dry-run → render
  validation summary (counts, errors, warnings, duplicate-path conflicts, invalid-hierarchy findings) → commit enabled
  only on a valid dry-run.
- **Baseline detail:** `Details.cshtml` (+ `details.js`): metadata, status, snapshot hash / manifest info when present,
  and the publish control (DRAFT only) behind a shared confirm modal.
- **Definition-tree viewer:** a panel/partial within detail (or a dedicated view) rendering the nested tree from
  `.../{id}/definitions`, preserving `displayOrder`, showing folder name/full path and available metadata; nodes drill
  to `.../{id}/definitions/{canonicalId}` for detail.

Shared conventions: `_LayoutTenantShell`, shared toast/`window.showConfirm`, loading/skeleton states, the L10n bridge
(`_IndexL10n.cshtml` JSON payload → `index.l10n.js` → `window.L10n`), and the tenant API profile (Gateway `5000` /
same-origin proxy). No inline `onclick`; no page-embedded styles (reusable styles live in `backbone-custom.css`).

## 12. Validation Rules

| UI input / action | Required | Rule | Failure handling |
|---|---|---|---|
| Workbook file | Yes (import) | `.xlsx` extension/MIME; non-empty; size within UI cap | Block dry-run; inline message |
| Source baseline key | Per FU02 contract | Non-empty; passed to dry-run/commit | Block dry-run; inline message |
| Baseline version (commit) | Per FU02 contract | Non-empty | Block commit; inline message |
| Dry-run gate | Yes | Commit disabled until a valid dry-run summary exists for the current file | Commit stays disabled |
| Publish gate | Yes | Publish shown/enabled only for `DRAFT`; requires confirm modal | Hidden/disabled otherwise |
| TenantId | Never | UI sends no `TenantId` anywhere | N/A (server-resolved) |
| Correlation id | Display only | Shown in error detail/support area; never editable | N/A |

All controlled backend failures (`reason_code`) map to localized copy; the UI never invents success and never shows raw
exception text.

## 13. Failure Path to Verify

- **Invalid workbook/schema/input:** backend 400 `VALIDATION_FAILED` → show the validation summary + localized error;
  commit stays disabled.
- **Duplicate sibling:** backend 409 `CONFLICT` → show the conflict finding; no commit.
- **Invalid hierarchy / missing canonical sheet:** backend 400 `VALIDATION_FAILED` → show finding; no commit.
- **Publish of a non-DRAFT or stale baseline:** backend 400 `VALIDATION_FAILED` / 409 `CONFLICT` → controlled message;
  no fabricated published state.
- **Missing permission:** backend 403 `PERM_DENIED` → the gated control is hidden/disabled; a direct call shows a
  controlled access message (no silent success).
- **Cross-tenant detail:** backend 404 `NOT_FOUND_NON_LEAKAGE` → generic not-found; no leaked identifier.
- **Expired session / 401:** route through the shared unauthorized/refresh flow; never mask an expired JWT as a generic
  error toast.
- **Any error:** `correlation_id` is available in the error detail/support area; stack traces are never shown; the
  client never calls `5057`.

## 14. Authorization Convention

- Tenant-facing pages assume server-side `[Authorize]` + `[HasPermission]` already enforced by FU02; FU03 adds the
  matching **frontend gates** using the same lowercase effective keys.
- Actor type: `tenant_user`.
- Permission-gated UI:

| UI element | Required effective key |
|---|---|
| Import button / wizard (dry-run + commit) | `platform.document-management.structure-baselines.import` |
| Baseline list + detail screens | `platform.document-management.structure-baselines.view` |
| Publish button / flow | `platform.document-management.structure-baselines.publish` |
| Definition tree list / node detail | `platform.document-management.collection-definitions.list` / `.view` (if the frontend gate convention supports per-surface keys; otherwise gate the tree under `structure-baselines.view`) |

Transitional aliases, if FU02/FU03 runtime already uses them: `platform.document-management.qms-baselines.import`,
`.view`, and `.publish` resolve to the preferred `structure-baselines.*` keys through a MOD-0018/security-owned alias
plan. FU03 must not mint a second independent permission model.

- FU03 mints **no** permission and changes no seed/alias. Frontend gates and backend policy resolve to the **same**
  lowercase effective key. A hidden/disabled control is the UI expression of the server's 403 `PERM_DENIED`.

## 15. Gateway / API Routing Decision

- **No gateway change.** FU02's Gateway routes already cover the FU03 calls: `/api/v1/document-management/{everything}`
  supports `GET, POST, OPTIONS`, and the root `/api/v1/document-management` supports `GET, OPTIONS`.
- All FU03 calls go through Gateway `5000` or a same-origin MVC proxy; **never `5057`**.
- Preferred FU03 API calls (consumed as-is):

| Method | Path | Used by |
|---|---|---|
| GET | `/api/v1/document-management/structure-baselines` | list page |
| GET | `/api/v1/document-management/structure-baselines/{id}` | detail page |
| POST | `/api/v1/document-management/structure-baselines/import/qms/dry-run` | QMS import wizard (validate) |
| POST | `/api/v1/document-management/structure-baselines/import/qms/commit` | QMS import wizard (commit) |
| POST | `/api/v1/document-management/structure-baselines/{id}/publish` | publish flow |
| GET | `/api/v1/document-management/structure-baselines/{id}/definitions` | tree viewer |
| GET | `/api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}` | node detail |

Backward-compatible alias plan: `/api/v1/document-management/qms-baselines/**` may remain as a temporary alias to the
preferred `/structure-baselines/**` routes if already consumed by implemented FU03 pages. New UI labels and future
feature packs must use `Structure Baselines` terminology.

**API profile decision (closed):**

- FU03 **must use a same-origin MVC proxy OR the shared Gateway API SSOT according to the existing repo convention** —
  whichever the live tenant shell already uses. It does not introduce a new API access pattern.
- **Direct Platform API `5057` calls are prohibited**, and there is **no hardcoded service port or absolute service URL
  in page JS**.
- **If the existing frontend provides a central `window.API` object or shared API client, FU03 must reuse it**; it must
  not fork or re-implement an API client.
- Which concrete profile applies (same-origin proxy vs `direct-gateway` `window.API`) is read from the live tenant
  convention during the inspection precheck (§18); both satisfy the Gateway-`5000`-only rule.

## 16. Acceptance Criteria

- [x] FU03 pack is `status: approved` for the exact frontend TenantShell Structure Baselines UI scope only.
- [ ] Scope is frontend TenantShell UI only; no backend, gateway, or permission-seed/alias change.
- [ ] Every FU03 page uses `Layout = "_LayoutTenantShell";`.
- [ ] All API calls go through Gateway `5000` or a same-origin proxy; no client code references `5057`.
- [ ] List, import wizard, detail, definition-tree viewer, and publish flow are delivered.
- [ ] Dry-run before commit is mandatory; commit is disabled until a valid dry-run summary exists.
- [ ] Validation summary (counts, errors, warnings, duplicate/hierarchy findings) is displayed.
- [ ] Controlled errors are shown from `reason_code`; `correlation_id` is surfaced in an error/support area; no stack
  traces; no fabricated success.
- [ ] Definition tree renders nested (not flat), preserves order, shows name/full path and available metadata.
- [ ] Publish is DRAFT-only, behind a confirmation modal, and surfaces controlled publish failures.
- [ ] Import/view/publish controls are permission-gated on the correct effective keys; frontend and backend resolve the
  same key.
- [ ] No physical folder creation and no document upload/storage beyond sending the workbook to the import API.
- [ ] FU03 does not authorize company adoption or `CollectionInstance`.
- [ ] Tenant localization is complete for all required tenant languages.

## 17. Test Expectations

- Frontend build: `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`.
- DataTable verifier on the list surface:
  `python3 .antigravity/scripts/verify_datatable_page.py . --area {Area} --module {Module} --reference compact`.
- RESX parity for all tenant languages on the new resources.
- UI behavior specs/smoke (where the repo has a frontend test convention):
  - dry-run renders the validation summary; commit disabled until a valid dry-run.
  - commit only proceeds after a valid dry-run; success toast on commit; no commit on invalid summary.
  - publish visible/enabled only for DRAFT; confirm modal required; controlled failure surfaced.
  - tree renders nested and ordered; node drill-down works.
  - controlled `reason_code` errors render; `correlation_id` visible in error detail; no stack trace.
- Static checks: no `5057` literal in client code; no fabricated-success path; no `TenantId` sent from client.
- Browser smoke through frontend `5001` and Gateway `5000` only (never a direct service port).
- `git diff --check` and protected-path verification (no backend/gateway/common/security changes).

## 18. Ready-for-dev Checklist

- [x] User reviewed this pack and explicitly approved only the FU03 frontend TenantShell Structure Baselines UI scope.
- [x] **API profile decision is closed:** FU03 reuses the existing repo convention (same-origin MVC proxy or shared
  Gateway `window.API` SSOT), reuses any central API client, hardcodes no service port, and never calls `5057`. The
  concrete profile is read from the live tenant convention during the inspection precheck below.
- [x] FU03 test matrix and protected paths are accepted.
- [x] Golden Reference (compact) list conventions and the spec-specific wizard/tree UX are accepted.
- [ ] **TenantShell route/view convention (controlled gate):** implementation must inspect the live frontend convention
  before coding; the illustrative paths in this pack are replaced by the actual repo convention; every page uses
  `_LayoutTenantShell`. **CONTROLLED GATE**
- [ ] **FU02 response DTO confirmation (implementation precheck):** before UI binding, inspect the actual FU02 DTO field
  names (baseline summary, import summary, definition node); if expected fields differ, adapt the **UI mapping only** —
  never change the backend. **CONTROLLED GATE**
- [ ] **AuthService runtime seed note (controlled runtime note):** the preferred `structure-baselines.*` permission
  seeds and any transitional `qms-baselines.*` aliases apply only after an AuthService restart; this affects
  end-to-end RBAC smoke only, **not** frontend pack approval or build.
  **CONTROLLED GATE**
- [ ] Parent reference `MOD-0028`, previous foundations `MOD-0028-FU01`/`MOD-0028-FU02`, and follow-up identity
  `MOD-0028-FU03` are recorded in `execution/registries/module-id-registry.md` (FU01/FU02 rows also still pending).
  **CONTROLLED GATE**
- [ ] DCP-002 module-identity preflight is run when Python/openpyxl is available; FU03 is an FU/child of MOD-0028, so no
  new MOD ID is minted. **CONTROLLED GATE**
- [ ] The required tenant language set and the L10n bridge keys are listed (business-analyst/l10n-agent) before UI work.
  **CONTROLLED GATE**
- [x] Status set to `approved` for frontend implementation of the FU03 scope.

## 19. Implementation Notes

- FU03 is the parent pack's "TenantShell UI follow-up" (parent §20 item 3). It is purely a consumer of the stable FU02
  contract; if a needed field or behavior is missing from FU02, FU03 **stops and reports** rather than adding backend
  code.
- The workbook is governance metadata: the UI reads the chosen `.xlsx`, sends its bytes to the import API (dry-run then
  commit), and renders the returned summary/tree. FU03 never persists the file, creates a folder, or implements a
  document lifecycle.
- Reuse shared TenantShell building blocks: DataTable v2 list template, inline filter, shared toast/confirm, the L10n
  bridge, and the tenant API profile. Do not fork these.
- The definition tree must render the **nested** structure returned by the backend (parent/child via
  `parentCanonicalId`, ordered by `displayOrder`); it must not flatten or re-derive hierarchy on the client, and must
  display atomic folder names verbatim (names may contain `/`).
- Errors are controlled: map `reason_code` to localized copy, surface `correlation_id` for support, never show stack
  traces, and never call `5057`.
- The route family existing in the gateway is not proof of capability; the UI must reflect real backend responses and
  never fabricate a success or an empty-but-done state.

### Approved Implementation Handoff (in effect — pack is `approved`)

- Next executable action: the orchestrator may implement **FU03 frontend only**.
- Allowed:
  - TenantShell pages (baseline list, import wizard, baseline detail, definition-tree viewer, publish flow)
  - frontend controller/routes if the repo convention requires them
  - frontend JS modules
  - API integration through Gateway `5000` / same-origin proxy only (reusing the central API client/SSOT)
  - localization / resources
  - frontend tests / smoke
- Not allowed:
  - `services/Diten.Platform` backend code
  - gateway `ocelot.json`
  - AuthService permission seed/alias
  - `services/Diten.Platform.Common`
  - `services/Diten.MdmService`
  - physical folder creation
  - document lifecycle / document upload-storage beyond sending the workbook to the FU02 import API
  - company adoption / `CollectionInstance`
  - direct Platform API `5057` calls
- First steps after handoff (precheck, not coding): inspect the live TenantShell route/view convention and the tenant
  API profile, confirm the FU02 response DTO field names, then build the UI mapping accordingly.

## 20. Follow-up Items (post-FU03 MOD-0028 roadmap)

These are valid business requirements that are intentionally **separate from FU03**. Each is a future, separately
approved pack with its own scope/AC/tests. FU03 authorizes none of them.

### MOD-0028-FU04 — Manual Structure Baseline Builder (backend + frontend)

**Goal:** let a user build/edit a documentation structure (folder tree) **without** Excel import, working on a DRAFT
baseline. PUBLISHED baselines remain immutable.

**Proposed scope:**

- Create a **manual DRAFT** `BaselineRelease` (no workbook).
- Add a manual `CollectionDefinition` node; edit node metadata; **reorder/move** a node; **soft-delete** a draft node.
- Parent-child validation; duplicate-sibling validation (consistent with FU02 rules); deterministic keys preserved.
- Operates only on a DRAFT baseline; a PUBLISHED baseline stays immutable; pre-publish validation reused/extended.
- Audit + correlation on every mutation; tenant isolation; optimistic concurrency.
- Permission model extends the `structure-baselines.*` / `collection-definitions.*` family (new edit/create keys via
  MOD-0018/security; no reverse/dynamic aliases).

**Candidate backend endpoints (under the existing versioned family):**

- `POST   /api/v1/document-management/structure-baselines/manual`
- `POST   /api/v1/document-management/structure-baselines/{id}/definitions`
- `PUT    /api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}`
- `PATCH  /api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}/move`
- `DELETE /api/v1/document-management/structure-baselines/{id}/definitions/{canonicalId}`
- `POST   /api/v1/document-management/structure-baselines/{id}/validate`

**Candidate frontend:** manual baseline designer, tree editor, add-child-node, edit-node-metadata, move/reorder,
delete-draft-node, validate-draft, publish handoff (reuses the FU03 publish flow).

**Boundary:** manual create/edit is valid but **not** FU03. FU03 stays import-UI only. New POST/PUT/PATCH/DELETE
methods will need a Gateway method-widening integration-agent task (the catch-all is currently GET/POST/OPTIONS).

### MOD-0028-FU05 — Company Adoption / CollectionInstance Provisioning (backend + frontend)

**Goal:** apply a PUBLISHED baseline to a company / LegalEntity, creating company-scoped instances.

**Proposed scope:**

- **MOD-0220 LegalEntity selection** (fail-closed validation, bearer/tenant propagation — the FU01-confirmed seam).
- PUBLISHED baseline → company `CollectionInstance` creation; company-specific adoption.
- Provisioning / reconciliation; instance status; strict tenant/company isolation.
- **No document lifecycle** at this stage.

**Candidate backend endpoints:**

- `POST /api/v1/document-management/structure-baselines/{id}/adoptions`
- `GET  /api/v1/document-management/adoptions`
- `GET  /api/v1/document-management/adoptions/{id}`
- `POST /api/v1/document-management/adoptions/{id}/reconcile`

**Candidate objects:** `CollectionInstance`, `ScopeBinding` / `CollectionBinding` (if the parent pack requires),
`ProvisioningJob` (if needed). **Boundary:** company adoption is required for real tenant/company use but is separate
from FU03 **and** from FU04 (manual builder).

### MOD-0029 — Controlled Document Lifecycle Integration (boundary note)

After the folder/baseline structure exists, **MOD-0029** owns the actual controlled-document lifecycle: document
create/draft/review/approve/effective/archive, versioning, workflow integration, controlled-document status, document
ownership, lifecycle audit, and possible binary/content-reference integration.

**Boundary (firm):** document lifecycle must **not** be implemented inside MOD-0028 (incl. FU03/FU04/FU05). MOD-0028
owns documentation *structure/baseline governance*; **MOD-0029 owns controlled document lifecycle.** Likewise MOD-0030
(retention/legal-hold) and MOD-0031 (evidence export) remain their owners.

### Remaining UI follow-ups (smaller, after FU04/FU05 backends land)

1. **Corporate root UI:** corporate-root initialization/lock screens.
2. **Local governance UI:** local nodes and exception request/queue screens.
3. **Template governance UI:** template master/version/variant management screens.
4. **Accessibility / observability / release-gate:** full a11y pass, UI telemetry, and release checklist for the
   documentation-management tenant surfaces.
5. **FU03 L10n closure:** author the remaining 6 tenant-language `.resx` for the FU03 surfaces (EN shipped; ar/es/fr/
   ru/tr/zh outstanding via l10n-agent).

Each follow-up requires its own approved or ready-for-dev scope. FU03 does not authorize any later wave, and does not
authorize manual create/edit, company adoption, `CollectionInstance`, or document lifecycle.
