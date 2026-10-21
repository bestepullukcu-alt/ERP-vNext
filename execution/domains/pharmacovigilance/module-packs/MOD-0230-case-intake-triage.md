---
id: MOD-0230
name: Case Intake & Triage
domain: pharmacovigilance
service: Diten.PvgService
service_port: 5011
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: ready-for-dev
build_gate: open
operational_runtime_gate: closed
owner: PVG paket sahibi
branch: feature/pvg/mod-0230-case-intake-triage
started: 2026-08-04
approved: 2026-08-09
target: 2026-08-28
form_field_count: 16
slice: "Slice 1 - intake + triage + route. Archive/void and export are out of scope."
---

# MOD-0230 - Case Intake & Triage

> **Status change 2026-08-09.** DCP-004 is `approved` and this pack carries `status: ready-for-dev`. Implementation
> may proceed **under the build/test gate only.**
>
> **Revision 2026-10-21 (independent review 01).** Review 01 of pack version `f1b56737` returned "UYGUN DEĞİL" with
> seven blocking findings F-1 ... F-7 (`docs/records/reviews/MOD-0230/2026-10-21-f1b56737.md`). The Control Tower
> comparison note (`docs/records/reviews/MOD-0230/2026-10-21-f1b56737-ct-karsilastirma-01.md`) confirmed all seven and
> added further items. This revision replaces claims the code does not support with facts measured at commit
> `ce3be60cf`, returns unsigned owner contracts to open state, and lists every owner decision under "Open Decisions
> (Owner Decision Pending)". It approves nothing. A version-bound approval row (CT-APR-01 / CT-APR-02) is still
> required for this pack version; none exists (`scripts/pack_approval.py check` exit 1, measured 2026-10-21).
>
> **Two gates, not one:**
>
> | Gate | State | Authorizes |
> |---|---|---|
> | **Build / test** | **OPEN** since 2026-08-09 | Backend code and tests in `services/Diten.PvgService/**`; starting the API host in local / dev / CI on its configured URL to run tests and smoke measurements; the single Gateway route family; the tenant UI. Local / dev / CI only. |
> | **Operational runtime** | **CLOSED** | Production or production-like use, supplier qualification, validation, real MOD-0019 / MOD-0023 / MOD-0031 adapters. |
>
> Not authorized under either gate today: Mongo collections, indexes, or migrations (DCP-004 §10); seed data;
> permission seed or grant; background jobs; archive, void, export, delete, bulk-delete; AI behaviour.
>
> The operational runtime gate stays closed until real MOD-0019, MOD-0023, and MOD-0031 ship, the owner contracts in
> "MOD-0230 Owner-Approval Evidence Intake Template" are signed by their owners, a retention / legal-hold owner is
> named, and the operational runtime authorization row is approved. That row is `[ ]` and must not be checked.
> See DCP-004 §10 "Build gate vs operational runtime gate".

## Build Authorization (Non-Production) - 2026-08-09

**Authorized (build/test gate; local / dev / CI only):**

- `services/Diten.PvgService/**` - dedicated service (OD-7). The API host may be started in local / dev / CI on its
  default URL `http://127.0.0.1:5011` (`Program.cs`) or an `ASPNETCORE_URLS` override, only to run tests and E2/E3
  smoke measurements. Port 5011 is **not** allocated in the `AGENTS.md` §3 port table (see "Service Port And
  Appsettings Policy"); starting the host locally is not a port allocation.
- The tenant UI paths listed in "Repo Scope", including the MVC proxy controller
  `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs`.
- `gateway/Diten.ApiGateway/ocelot.json` - the one MOD-0230 route family, via integration-agent only.
- Tests for the above.

**Slice 1 scope - commands and queries:**

| Surface | In slice 1 | Note |
|---|---|---|
| Create, Update | Yes | - |
| GetList, GetById | Yes | - |
| Triage, Route | Yes | Gated by `IPvgWorkflowTransitionGate` |
| **Archive / Void** | **No** | Requires `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1`; no compliance / legal-hold owner exists |
| **Export (incl. masked export)** | **No** | Requires a real MOD-0019 approval; masked-only export cannot be proven without a masking owner |
| Delete, BulkDelete | **Never** | Permanently excluded |
| Any AI behaviour | **No** | Governed-AI gates absent |

**Still forbidden:** production deployment, supplier qualification, validation approval, Mongo collections / indexes /
migrations, seed data, permission seed / grant, background jobs, `.antigravity/**` edits, edits to the `AGENTS.md` §3
port table without an Enterprise Architecture decision, other domains' packs, and any MedDRA data.

**Historical prerequisite (2026-08-09):** the stale ignored `services/Diten.PvgService/bin` and `obj` folders were to be
deleted before scaffolding. The tracked scaffold exists since commit `0860632f1` (2026-08-16).

**Implementation work packs:** `execution/domains/pharmacovigilance/work-packs/WP-0230-01` ... `WP-0230-07` carry
`status: ready`; `WP-0230-08` (module manifest / catalog) is `blocked` pending a governance decision. Two WP files
disagree with this pack and this pack wins by authority order (they are not changed by this revision):
`WP-0230-03` is titled "Mongo persistence" while persistence is in-memory only under the build/test gate, and
`WP-0230-07` names the controller `CaseIntakeTriageController.cs` and the key family `pvg.case-intake-triage.*`
while the code uses `PharmacovigilanceCaseIntakeTriageController.cs` and `pvg.mod0230.intake.*`. Work is assigned
through "Teslim dilimleri".

> DCP-002 gate (2026-08-04): `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"` -> `OK MOD-0230: proven against Blueprint/registry.`

## Build-Gate Gaps (Measured 2026-10-21)

Static facts at commit `ce3be60cf`, plus the E2/E3 measurements of the PVG-M1-02 inspection
(`docs/records/audits/2026-10/pvg-m1-02-inspection-01/INSPECTION-NOTE.md`, measured 2026-10-20). Two statements in
that note are wrong and are corrected here: `ocelot.json` carries `AuthenticationOptions` on 2 routes (the
`/api/v1/uploads` pair), not 4; and the service does check permission keys (`pvg.mod0230.intake.*`). Each gap names
the delivery slice that owns it. Row G-16 and the 2026-10-22 additions to G-5 and G-15 are static facts measured at
commit `8e13f3787` (independent review 02, `docs/records/reviews/MOD-0230/2026-10-21-b48500cf.md`).

| # | Gap | Measured fact | Owning slice |
|---|---|---|---|
| G-1 | No JWT validation in PvgService | `Program.cs` registers routing and the PVG host only; no authentication middleware. PvgService projects reference only their own projects; no `*.csproj` references `Diten.Platform.Common`. | DL-4 |
| G-2 | Tenant and actor come from request headers | `PvgCaseIntakeTriageEndpoints.cs` reads `X-Diten-Tenant-Context`, `X-Diten-Actor-Id`, `X-Diten-Actor-Kind`. The Gateway resolves the tenant into `X-Tenant-Id` (`gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs`) and neither writes nor strips `X-Diten-*`. The MVC proxy fills both headers from the session token, but a caller that reaches the Gateway or the service directly chooses its own tenant and actor. `multi-tenancy.md` names `X-Tenant-Id` via `TenantResolutionMiddleware` as the single tenant source, and `security-jwt.md` requires it to match the token's tenant claim. | DL-4 |
| G-3 | Permission keys differ from this pack | The service checks `pvg.mod0230.intake.{read,create,update,triage,route}` (`PvgIntakeDraftApplicationService.cs` lines 7-11), pinned by `PvgApplicationContractShapeTests.cs`. This pack proposes `pvg.case-intake-triage.*`. Neither family exists in `Diten.AuthService` or `Diten.Web`. Target family selected 2026-10-21 on the PVG side (OD-PVG-1): `pvg.case-intake-triage.*`; not applied until MOD-0018 / AuthService approves catalog-grant ownership and the `pvg.*` namespace. | DL-6 |
| G-4 | MVC controller carries `[Authorize]` only | `PharmacovigilanceCaseIntakeTriageController.cs`: class-level `[Authorize]`, zero `[HasPermission]` across its 10 actions; `Index`, `Create`, `Edit`, `Details` return views without a permission check (UAS-001 gap). | DL-7 |
| G-5 | No audit write | Mutations return a `PvgAuditIntent` built after the store write; the application service has no audit sink or outbox dependency; the host registers none; the API response drops the intent. `tests/architecture/audit-ledger/Diten.PvgService.md` records no trace and lists the four MOD-0230 write commands as known debt. No read audit either (added 2026-10-22): `GetDraftByIdAsync` (`PvgIntakeDraftApplicationService.cs` line 337) opens one safety case and writes no record; AUD-001 §7 requires one ("Audited Events", BG-18). | DL-5 |
| G-6 | Startup is not fail-closed for missing dependencies | `PvgServiceApiRuntimeOptions.ThrowIfUnsafe()` checks two flags only; the four ports and the store are registered only when `Pvg:Runtime:UseNonProductionAdapters` is true; `/health/ready` reports flags, not dependencies. E3 B3: production-like + `Pvg:Runtime:OperationalRuntimeAuthorized=true` + no adapters -> host starts, `/health/ready` 200, list call with full headers -> HTTP 500 with empty body. Test `Production_like_authorized_startup_does_not_register_non_production_adapters` asserts the five missing registrations as success. | DL-3 |
| G-7 | Operational runtime authorization is a configuration flag | `Pvg:Runtime:OperationalRuntimeAuthorized` is read from environment variables or command line (`Program.cs`, `PvgServiceApiHost.cs`); it is not bound to any approval evidence. | DL-3 |
| G-8 | Error model | Endpoints map `Succeeded` to 200, `Invalid` to 400, and every other outcome to 409 - including not-found (`IntakeDraftNotFound` is returned as `Blocked`), permission denied, and missing tenant / actor / correlation context. This pack requires 404 / empty for not-found and cross-tenant and 401/403 for unauthorized; `multi-tenancy.md` requires 400 for a missing tenant header. | DL-4 |
| G-9 | Validation runs before the permission gate | `CreateDraftAsync` and `UpdateDraftAsync` validate first; E3 f: an unauthorized caller receives field validation reason codes (safe codes only). Small. | DL-4 |
| G-10 | Persistence is in-memory | `InMemoryPvgIntakeDraftRepository` (process-local dictionary keyed by tenant); no Mongo driver, collection, index, or migration. `PvgIntakeDraftIndexCatalog` declares a collection name and two index definitions as data only. | DL-2 |
| G-11 | Backend layout differs from Golden Reference | Code: `Application/RegPvBase/*` with one `PvgIntakeDraftApplicationService`; commands `CreateIntakeDraftCommand`, `UpdateIntakeDraftCommand`, `TriageIntakeDraftCommand`, `RouteIntakeDraftCommand`; queries `GetIntakeDraftListQuery`, `GetIntakeDraftByIdQuery`; no `Features/` folder; no `*Handler` classes; no MediatR. | DL-2 |
| G-12 | Port-conformance names are not in code | `docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md` is committed and defines C-01 ... C-17 against `PvgStaticFieldPolicy`, `PvgStaticTransitionGate`, `PvgPendingEvidenceStore`, `IPvgSafetyPartnerAdapter`, `PvgPortResult`, `AddRegPvBasePorts`, and key `Pvg:RegPvBase:UseNonProductionAdapters`; none occurs in `services/Diten.PvgService` (`git grep`, 0 files) and no test names a C-id. Code uses `DenyAll*` adapters, a fourth port `IPvgPermissionGate` not in the document, and `Pvg:Runtime:*` keys. | DL-1 |
| G-13 | Port 5011 | Not in the `AGENTS.md` §3 table; `AGENTS.md` lists 5011 (`PvgService`) among ports in use awaiting an Enterprise Architecture decision. `.claude/launch.json` entry `web-verify` starts Diten.Web on 5011, so both cannot run locally on defaults. | DL-3 |
| G-14 | DataTable verifier gaps | `python3 .antigravity/scripts/verify_datatable_page.py . --area Pharmacovigilance --module CaseIntakeTriage --reference compact --api-profile proxy --format gaps` printed 18 gaps: data mode undeclared; old skeleton instead of `_TableSkeleton`; `_Filter` and `_IndexL10n` partial includes not found; form view model not found under `frontend/Diten.Web/Models`; the remaining 13 items (bulk delete, export, import, select-all, reload-with-toast and clear-selection wiring) are tied to bulk / export features this pack excludes. Since this revision the frontmatter declares `data_mode: server` (OD-PVG-8); re-run after this edit (2026-10-21) still prints 18 gaps - the data-mode item names `_DataTable.cshtml`, which carries no `data-dt-data-mode` attribute yet (DL-7). | DL-7 |
| G-15 | No concurrency check on update | `InMemoryPvgIntakeDraftRepository.ReplaceAsync` overwrites without a version check (last write wins). Added 2026-10-22: no record version exists in any layer. `IPvgIntakeDraftStore.ReplaceAsync` (line 12) takes no expected version and returns `bool`; `PvgIntakeDraftApplicationService` reads `false` as `IntakeDraftNotFound` at all three call sites (update line 157, triage line 239, route line 321); the API request records in `PvgCaseIntakeTriageEndpoints.cs`, the MVC form inputs in `PharmacovigilanceCaseIntakeTriageController.cs`, and the JS carry no version (`git grep -i 'version\|etag\|rowversion\|concurrency'` over these paths finds only the `asp-append-version` cache-busting attribute in the views). Ownership: store contract and service calls DL-2 (BG-12); API transport DL-4 (BG-19); MVC / JS transport DL-7 (BG-20). | DL-2 |
| G-16 | The list shows at most the first 100 records | `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js` `buildListUrl` (lines 28-35) sets `pageNumber=1` and `pageSize=100` on every request; the DataTable is not server-side (no `serverSide` option, no total fields); the MVC proxy `List` action clamps `pageSize` to 1 ... 100; `IPvgIntakeDraftStore.ListAsync`, `PvgIntakeDraftQueryResult`, and `PvgCaseIntakeApiResponse` return items only - no total or filtered count. A tenant with more than 100 intake drafts cannot reach the 101st: a functional defect of the build/test screen. `data_mode: server` and the table attribute alone do not fix it. WP-0230-07 has no acceptance record and is not counted as done while this holds. API-side counts: DL-4 (BG-19); screen: DL-7 (BG-20). | DL-7 |

Gateway note: route-level `AuthenticationOptions` is absent on most routes by repo norm; the Gateway applies
authentication and `TenantResolutionMiddleware` pipeline-wide. E3 G1 / G4 (dev profile, 2026-10-20) observed anonymous
and forged-Bearer requests reaching the API; production-profile Gateway behaviour was not measured. The open security
gap is on the service side (G-1, G-2), not in the route entries.

## Open Decisions (Owner Decision Pending)

The owner marked all ten rows "Evet" (yes) on 2026-10-21, adopting the recommended option of every row. Record:
`docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md@4fe62fdc3`. The last column states the result. Four
rows are fully decided (OD-PVG-4, OD-PVG-5, OD-PVG-8, OD-PVG-10). Six rows are decided on the PVG side only and wait
for another owner's approval named in that column (OD-PVG-1, OD-PVG-2, OD-PVG-3, OD-PVG-6, OD-PVG-7, OD-PVG-9); they
are not actionable until that approval is recorded, and the acceptance criteria that depend on them stay open. This
section records decisions only: it is not the version-bound pack approval row (CT-APR-01 / CT-APR-02), not another
module owner's approval, and it does not open the operational runtime gate.

| ID | Decision | Facts | Recommended option | Who decides | Owner decision (2026-10-21) |
|---|---|---|---|---|---|
| OD-PVG-1 | Single permission-key family | G-3. No key of either family is seeded, granted, emitted in a JWT, or enforced by a shipped `[HasPermission]`, so neither is "published" under PKS-001 §2. The `pvg.*` namespace is not yet listed in PKS-001 §4. | Adopt `pvg.case-intake-triage.{read,create,update,triage,route}`; change the five service constants and the shape test (DL-6); no alias is needed because no key is published. `triage` and `route` are registered here as PKS-001 Tier-3 actions. Reason: PKS-001 §1 makes the middle segment a resource, not a module ID (module code is not a permission namespace); the name matches the Gateway route family and MVC route; cost is five constants and one test. | PVG paket sahibi; MOD-0018 owner confirms catalog / grant ownership and the `pvg.*` namespace | PVG side decided 2026-10-21 (owner): target family `pvg.case-intake-triage.*`; awaiting MOD-0018 / AuthService approval of catalog-grant ownership and the `pvg.*` namespace - not actionable until then. |
| OD-PVG-2 | Gateway-service tenant / actor context contract | G-1, G-2 | The service validates the JWT itself (issuer / audience as for other tenant services), takes the actor from the validated subject claim, takes the tenant from `X-Tenant-Id` as written by the Gateway and requires it to equal the token's tenant claim; `X-Diten-Tenant-Context` and `X-Diten-Actor-*` stop being authoritative and are ignored. Reason: one tenant source per `multi-tenancy.md`, claim match per `security-jwt.md`, and no change to Gateway code outside the route family (protected). | PVG paket sahibi with Enterprise Architecture; MOD-0018 owner for claim names | PVG side decided 2026-10-21 (owner); awaiting Enterprise Architecture approval of the contract and MOD-0018 owner approval of the claim names - not actionable until then. |
| OD-PVG-3 | Error-model mapping | G-8 | Not-found and cross-tenant -> 404 with a safe reason code and no record data; missing or invalid token -> 401; permission denied -> 403; missing tenant context -> 400; validation -> 400; duplicate, workflow-blocked, evidence-blocked, concurrency conflict -> 409; unexpected failure -> 500 with a non-empty safe envelope. Reason: matches `multi-tenancy.md`, `security-jwt.md`, and "Failure Path to Verify". | PVG paket sahibi; MOD-0041 / Ops for the regulated envelope | PVG side decided 2026-10-21 (owner): status-code mapping decided; awaiting MOD-0041 / Ops approval of the regulated error envelope - not actionable until then. |
| OD-PVG-4 | Backend file convention | G-11 | Record the `Application/RegPvBase/*` application-service layout as a documented deviation (technical debt) for slice 1; do not rename. Reason: PvgService does not use MediatR; renaming the four commands would add new names to the audit ledger's known-debt list, which may only shrink (AUD-001); the API and RegPvBase suites pin the current names. Revisit when DL-5 lands. | PVG paket sahibi | Decided 2026-10-21 (owner): recommended option adopted. |
| OD-PVG-5 | Form of startup fail-closed | G-6 | When the host is production-like, refuse to start unless every constructor dependency of `PvgIntakeDraftApplicationService` resolves (checked during service registration or provider build); in every environment `/health/ready` returns 503 when a required port or the store is missing. Reason: refusal at start is the strongest form and matches the two existing flag refusals; the readiness check covers non-production hosts. | PVG paket sahibi; startup policy approval also sits in "Required Approval Gates" | Decided 2026-10-21 (owner): recommended option adopted. |
| OD-PVG-6 | Binding the operational-runtime flag to evidence | G-7 | Until the operational runtime authorization row is approved, `Pvg:Runtime:OperationalRuntimeAuthorized=true` is set only inside tests; a static CI scan fails when any committed configuration, launch profile, workflow, or deployment file sets it. When runtime is later approved, the host also requires a configured reference to that approval row. Reason: a flag cannot prove approval; the scan is cheap and measurable now. | PVG paket sahibi with Enterprise Architecture | PVG side decided 2026-10-21 (owner): CI scan decided; awaiting Enterprise Architecture approval of how the flag binds to the approval row - not actionable until then. |
| OD-PVG-7 | Port allocation | G-13 | Enterprise Architecture allocates 5011 to `Diten.PvgService` in the `AGENTS.md` §3 table, and the `web-verify` entry in `.claude/launch.json` moves to another port. Reason: 5011 is already in `Program.cs`, the four Gateway routes, domain-config (OD-7), and DCP-004; moving `web-verify` touches one local launch entry. | Enterprise Architecture; repo owner for `.claude/launch.json` | PVG side decided 2026-10-21 (owner): PVG takes the request to Enterprise Architecture; awaiting the Enterprise Architecture port-allocation decision - not actionable until then. The `.claude/launch.json` change is a separate repo-owner approval; this decision does not give it. |
| OD-PVG-8 | `data_mode` for the list | G-14; the list endpoint is server-paged (`pageNumber`, `pageSize`); intake records are unbounded. | `data_mode: server` in the frontmatter (declared there since this revision) and `data-dt-data-mode="server"` on the table (DL-7). Reason: `module-pack-standard.md` §3 maps an unbounded entity list to `server`. | PVG paket sahibi | Decided 2026-10-21 (owner): recommended option adopted. |
| OD-PVG-9 | Audit write path for K2-class commands | G-5. AUD-001 §4.3 K2: for GxP records, no audit record means no operation. AUD-001 §5 K4: PVG has no audit trace; new write commands are blocked until the shared forwarder package exists. | Wait for the K4 shared forwarder (path `b`) and the MOD-0021 owner contract, then deliver DL-5; until then, keep every MOD-0230 mutation unable to succeed outside tests (deny-all permission gate). Reason: path `a` is Platform-only and copying the MDM forwarder is forbidden by AUD-001 §5. | Control Tower (K4 package); MOD-0021 owner; PVG paket sahibi | PVG side decided 2026-10-21 (owner); awaiting the Control Tower K4 shared forwarder package and the MOD-0021 owner contract - not actionable until then; the deny-all permission gate stays. |
| OD-PVG-10 | Verifier items tied to excluded features | G-14 | Record the bulk-delete, export, import, select-all, and related wiring items as a "known deviation" for this module (they contradict the permanent exclusions); fix the other five items in DL-7. Reason: the list-screen touch protocol requires an owner answer; the exclusions are owner decisions already recorded in this pack. | PVG paket sahibi | Decided 2026-10-21 (owner): recommended option adopted. |

## Module Summary

MOD-0230 Case Intake & Triage is the first PVG member module in DCP-004. Its purpose is to define the
regulated Pharmacovigilance intake baseline: Safety Case intake records, intake artifacts, triage state,
routing decisions, and the evidence-pack boundary required before downstream Case Processing, MedDRA Coding,
and Signal Management planning can proceed.

Blueprint context:

| Field | Value |
|---|---|
| Domain / Landscape | Regulated Life-Sciences Extensions |
| Suite / Platform | Pharmacovigilance (PV) |
| Capability Group | Safety Case Operations |
| Blueprint Wave | W-3 |
| Placement | Domain App (Regulated Workflow) |
| Delivery Outcome / Value | PV intake baseline; controlled routing |
| Soft Pages | PV Case Inbox; Intake Forms; Triage Rules; Evidence Pack |
| Minimum Integration Contract | REG-PV-BASE |
| SoR Primary Object | Safety Cases |
| AI Enablement Tier / Risk | Governed-AI / High |

## Ownership and Boundaries

In scope for this build/test-ready pack:

- Safety Case intake contract boundary.
- Intake artifact contract boundary.
- Triage state and routing decision contract boundary.
- Evidence-pack handoff boundary.
- W-3A0 dependency map and production blockers.
- Future module-pack readiness questions.

Out of scope for this build/test-ready pack:

- Operational runtime, production deployment, supplier qualification, validation, seed data, Mongo collections /
  indexes / migrations (the build/test gate uses the in-memory store only), background jobs, permission seed or grant,
  archive/void, export, delete, bulk-delete, and any surface outside slice 1.
- W-3A0 foundation remediation development.
- Full Case Processing, MedDRA Coding, Signal Management, reporting/submission, or PV quality runtime.
- AI summarization, extraction, recommendation, or routing implementation.

## Owned Objects

| Object | Ownership | Build/test status (measured 2026-10-21) |
|---|---|---|
| Safety Case Intake | MOD-0230 SoR for intake-stage Safety Case creation boundary | Domain type `SafetyCaseIntake` exists; stored in the in-memory store only |
| Intake Artifact | MOD-0230 SoR for intake-stage submitted artifacts and source references | Represented only as `SourceReference` and `EvidenceLinkReferences` fields |
| Triage State | MOD-0230 SoR for initial triage status and routing outcome | `PvgIntakeStatus`, `PvgTriageOutcome` exist |
| Routing Decision | MOD-0230 SoR for controlled routing decision metadata | Route command exists; `DenyAllWorkflowTransitionGate.EvaluateAsync` denies every route transition |
| Evidence Pack Boundary | MOD-0230 produces intake evidence-link requirements; MOD-0031 owns evidence-link service | Port `IPvgEvidenceLinkPort` with deny-all adapter only |

Write commands (each appears once in "Audited Events"): `CreateIntakeDraftCommand`, `UpdateIntakeDraftCommand`,
`TriageIntakeDraftCommand`, `RouteIntakeDraftCommand`. Queries: `GetIntakeDraftListQuery`, `GetIntakeDraftByIdQuery`
(the single-record read `GetIntakeDraftByIdQuery` also has a read row in "Audited Events", AUD-001 §7).
API endpoints: see "Gateway / API Routing Decision". Frontend route: `/Pharmacovigilance/CaseIntakeTriage`.
Permission keys: see "Authorization Convention" (OD-PVG-1).

Slice 1 runtime objects, endpoints, controllers, commands, queries, DTOs, frontend routes, and permissions may be
defined only inside the build/test gate. Operational runtime remains unauthorized.

## Entity Fields

Create/edit user-entered field count recorded for draft planning: `16`.

Golden Reference decision: `compact`, because the create/edit form has more than 8 user-entered fields and
regulated intake requires separate Create/Edit/Details review surfaces rather than Index-hosted offcanvas panels.

Excluded from field count: `Id`, `TenantId`, audit fields, correlation id, workflow instance id, created/updated
metadata, system-generated case number, and computed SLA/status timestamps.

| Field | Required | Sensitivity class | User-entered | Notes / blocker |
|---|---|---|---|---|
| IntakeChannel | Yes | public-metadata | Yes | Allowed values / source of options still requires approval. |
| SourceType | Yes | public-metadata | Yes | Allowed source taxonomy still requires approval. |
| SourceReference | No | confidential | Yes | External source ID policy must align with Blueprint MOD-0040 / TRACE-BUNDLE. |
| ReceivedAtUtc | Yes | regulated-safety | Yes | Must be UTC-normalized; client timestamp trust policy still requires approval. |
| ReporterType | Yes | public-metadata | Yes | Allowed values still require approval. |
| ReporterContactSummary | No | PII | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| PatientSubjectCode | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| EventOnsetDate | No | PHI | Yes | Requires MOD-0019 masking / row-field policy before runtime. |
| AdverseEventNarrative | Yes | PHI | Yes | Raw narrative prohibited in logs/traces/audit payloads. |
| SuspectProductText | No | regulated-safety | Yes | Terminology/reference policy still requires approval. |
| Seriousness | Yes | regulated-safety | Yes | Allowed values still require approval. |
| IntakePriority | Yes | regulated-safety | Yes | Priority taxonomy and SLA linkage still require approval. |
| TriageOutcome | Yes | regulated-safety | Yes | Allowed states and transition rules require Workflow/Inbox contract. |
| TriageReason | No | PHI | Yes | Raw reason text prohibited in logs/traces/audit payloads. |
| RouteTargetQueue | Yes | confidential | Yes | Requires Workflow/Inbox route target contract and permission-filtered visibility. |
| EvidenceLinkReferences | No | confidential | Yes | Requires MOD-0031 Evidence-Link contract; no fake evidence pack fallback. |

Every field included in create/edit/list/detail/export surfaces must receive masking behavior, row/field access
rules, audit payload rules, evidence-link rules, and fail-closed tests before slice 1 acceptance. Export remains
out of slice 1 and still requires a real MOD-0019 approval before it can be added.

## Repo Scope

Authorized for the build/test gate:

- `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`
- `services/Diten.PvgService/**` - MOD-0230 slice 1 code lives in `src/*/RegPvBase/**`, `src/Diten.PvgService.Api/**`,
  `tests/Diten.PvgService.Api.Tests/**`, and `tests/Diten.PvgService.RegPvBase.Tests/**`. The MOD-0231 / MOD-0232 /
  MOD-0234 folders in the same service belong to their own packs.
- `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs` - the same-origin MVC proxy
  controller (actual file name; WP-0230-07 still names it `CaseIntakeTriageController.cs`).
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`
- `frontend/Diten.Web/wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/**`
- `frontend/Diten.Web/Resources/Views/Pharmacovigilance/CaseIntakeTriage/**`
- `frontend/Diten.Web/Models/CaseIntakeTriage/**` - listed in WP-0230-07; does not exist today (G-14).
- `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js`
- `gateway/Diten.ApiGateway/ocelot.json` - only the four MOD-0230 route entries (upstream `/api/pv-case-intake-triage*`),
  integration-agent only.
- `tests/architecture/audit-ledger/Diten.PvgService.md` - removal of a known-debt row only, by DL-5, when that command
  is audited (the list only shrinks).

Record of an earlier scope breach: the controller (commit `0860632f1`) and the UI test (commit `b00093ea6`) were
committed on 2026-08-16 while this pack's Repo Scope did not list them. This revision lists them; it does not
retroactively approve that commit.

Still blocked:

- Production deployment, supplier qualification, and validation approval (operational runtime gate).
- Archive, void, and export surfaces (out of slice 1).
- Mongo collections, indexes, migrations; seed data; permission seed / grant (MOD-0018 / AuthService own seed/grant).
- Port allocation. The port source is the `AGENTS.md` §3 table, changed only by Enterprise Architecture decision
  (OD-PVG-7). `.antigravity/rules/ports.md` is not the port source.

## Protected Paths

- `.antigravity/**`
- `AGENTS.md` (port table changes only by Enterprise Architecture decision)
- `services/**` **except `services/Diten.PvgService/**`**
- `frontend/**` **except the frontend paths listed in "Repo Scope"**
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/**` except the four MOD-0230 route entries in `ocelot.json`, via integration-agent; this includes
  `gateway/Diten.ApiGateway/Middleware/**`
- `tests/architecture/audit-ledger/**` except the shrink-only row removal named in "Repo Scope"
- `.claude/launch.json` (repo owner)
- `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` (higher authority) and
  other domain module packs
- Runtime internals of other domains unless explicitly authorized by the user.

## Dependencies

W-3A0 dependencies are production blockers, not waived:

| Dependency | Owning module / source | Status for MOD-0230 |
|---|---|---|
| REG-PV-BASE | DCP-004 minimum integration contract | BLOCKER |
| SSO + RBAC/ABAC | MOD-0018 RBAC / permissions plus Platform/Auth foundations | BLOCKER |
| PHI/PII masking hooks | MOD-0019 Data Masking & Row/Field Security | BLOCKER |
| AuditEvent v1 | MOD-0021 Audit Trail Service | BLOCKER |
| Workflow/Inbox v1 | MOD-0023 Workflow Designer | BLOCKER |
| Evidence-Link | MOD-0031 Evidence Linking Service | BLOCKER |
| TRACE-BUNDLE: canonical ID, Correlation-ID, trace stitching, regulated error model | Blueprint MOD-0040 / platform trace standards | BLOCKER |
| OTel / operational telemetry | Platform observability foundations | BLOCKER |
| Audit forwarder for services without an audit trace | AUD-001 §5 K4 shared forwarder package (Control Tower) | BLOCKER for DL-5 |
| DCP-004 | PVG Urgent W-3 Development Block | **`approved` 2026-08-09**; build/test gate open, operational runtime gate closed |

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not direct MOD-0230 runtime
blockers unless this module's approved scope emits signal analytics, semantic metric IDs, or data-product outputs.
They remain downstream DCP-004 / MOD-0234-facing gates for Signal Management. If MOD-0230 later adds signal
analytics or data-product output, the pack must be revised to promote the relevant MOD-0004/MOD-0063 contracts
from downstream gates to direct MOD-0230 blockers.

### Required Interface Contracts for Build/Test and Operational Runtime

MOD-0230 separates build/test substitution from owner approval. A substitute satisfies the local build/test gate only;
it does not approve a contract and does not open operational runtime. No row in this table is closed.

| Owner | Required contract for MOD-0230 | Required MOD-0230 decision | Status |
|---|---|---|---|
| MOD-0018 RBAC / permissions | canonical permission keys, seed/grant ownership, actor context, tenant authorization context, optional data-scope shape | actor roles and permission matrix for read/create/update/triage/route; archive/export out of slice 1; delete and bulk-delete permanently excluded | **OPEN - owner contract pending.** Build/test substitute only: `IPvgPermissionGate` + `DenyAllPermissionGate`, registered only when `Pvg:Runtime:UseNonProductionAdapters` is true. No JWT validation in the service; tenant and actor come from request headers (G-1, G-2). PvgService references no `Diten.Platform.Common` project, so nothing is consumed from `Diten.Platform.Common/Authorization`. Real RBAC - JWT validation, Gateway-resolved tenant / actor context (OD-PVG-2), `pvg` permission evidence and tests - is required before operational runtime. Key family: OD-PVG-1. |
| MOD-0019 masking / row-field security | field sensitivity vocabulary, masking/omit/deny behavior, row-scope and field-scope evaluation, unavailable-policy behavior | per-field sensitivity matrix and fail-closed behavior for list/detail/create/update/audit; export out of slice 1 | **PORTED** - `IPvgFieldSecurityPolicy` + `DenyAllFieldSecurityPolicy`. Build gate satisfied by the substitute; **operational runtime still BLOCKER**. MOD-0019 registry row exists for planning traceability only; it is still unowned, missing a module pack, and has no runtime (see Implementation Notes) |
| MOD-0021 AuditEvent v1 | append/event shape, safe metadata envelope, redaction rules, critical audit failure policy, correlation propagation | audited operations, payload allow-list, failure behavior when audit append/outbox is unavailable | **OPEN - owner contract pending; no substitute.** The code builds a `PvgAuditIntent` only; no sink or outbox; the API response drops it (G-5). Nothing is consumed from the `Diten.Platform` audit feature. The audit-write path that blocks the mutation when the record cannot be written is a separate delivery (DL-5) and acceptance condition (BG-11). The single-record read audit (AUD-001 §7) depends on the same contract and the same K4 forwarder (OD-PVG-9); its write-failure behaviour is a required item of this contract (BG-18). |
| MOD-0023 Workflow/Inbox v1 | transition gate or inbox handoff API/event, assignment semantics, routing state, fail-closed behavior | triage states, routable states, route targets, transition reason codes, blocked/allowed behavior | **PORTED** - `IPvgWorkflowTransitionGate` + `DenyAllWorkflowTransitionGate`. Build gate satisfied by the substitute; **operational runtime still BLOCKER**. Queue registry, assignment, and SLA remain MOD-0023's and are not ported |
| MOD-0031 Evidence-Link | object reference shape, link/query API, evidence requirement/completeness rule, evidence-pack boundary | whether artifacts require evidence links at create, triage, route, and downstream handoff | **PORTED** - `IPvgEvidenceLinkPort` + `DenyAllEvidenceLinkPort`. Build gate satisfied by the substitute; **operational runtime still BLOCKER**. Today: `DenyAllEvidenceLinkPort.EvaluateAsync` returns `EvidenceLinkDenied` for every request and records nothing. Proposed, not in code: a non-production adapter that records a requirement as `Pending` (never as satisfied), only if the MOD-0031 owner approves a durable pending-evidence state |
| Blueprint MOD-0040 / TRACE-BUNDLE | canonical/external ID semantics, `X-Correlation-Id`, trace stitching, regulated error model | generated/manual intake ID policy, external source ID policy, correlation propagation, error reason-code policy | **OPEN - owner contract pending.** PvgService references no `Diten.Platform.Common` Observability or Tenancy code. Local behaviour only: `X-Correlation-Id` is required and checked (at most 128 characters, no whitespace) into `PvgCorrelationContext`; draft IDs are generated by the store; request DTOs carry no `Id` or `TenantId`; no shared correlation middleware and no trace stitching. Error model: G-8, OD-PVG-3. |

### MOD-0230 Owner-Approval Evidence Intake Template

This template records the evidence required to convert a governance-only packet or approval gate into an
owner-approved MOD-0230 input. Every approval requires owner/team, approver, approval date, evidence artifact/link,
approved version, fail-closed proof, required test evidence, and caveats/exclusions. Empty or placeholder values mean
the approval remains blocked. The approver must be the owner of the contract named in the row; the PVG package owner
cannot approve another module's contract (no self-approval). Recording this template does not approve operational
runtime and does not authorize any surface outside the MOD-0230 build/test gate.

| Approval | Owner/team | Approver | Approval date | Evidence artifact/link | Approved version | Fail-closed proof | Required test evidence | Caveats / exclusions | Readiness decision |
|---|---|---|---|---|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | MOD-0018 owner role - **Not approved - owner contract pending.** The 2026-08-09 entry named the PVG package owner as approver; that is not an owner approval. | Open | Build/test substitute only: `IPvgPermissionGate` + `DenyAllPermissionGate` (`services/Diten.PvgService/src/Diten.PvgService.Infrastructure/RegPvBase/DenyAllPermissionGate.cs`), registered only when `Pvg:Runtime:UseNonProductionAdapters` is true. PvgService projects reference no `Diten.Platform.Common` project (`*.csproj`), so the `Diten.Platform.Common/Authorization` types listed on 2026-08-09 are not consumed by this module. | n/a - no owner-approved version | Missing - required proof: deny on missing actor, tenant, permission, scope, seed/grant catalog, or auth context; JWT validated in the service; tenant and actor taken only from validated server context (OD-PVG-2); cross-tenant reads return 404/empty and mutations deny. | Missing - required tests: role/action allow-deny matrix, missing-permission denial, missing or invalid token 401, forged `X-Diten-*` headers without effect, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, and no delete/bulk-delete surface. | **Not owner-approved.** The 2026-08-09 wording "Approved for the build/test gate" applies only to the deny-all substitute: it denies every call, so no permission is ever granted. Real RBAC - JWT validation, Gateway-resolved tenant / actor context, `pvg` permission evidence and tests - is required before operational runtime. Key family: OD-PVG-1; seed/grant ownership stays with MOD-0018 / AuthService. `archive` and `export` keys are out of slice 1; delete and bulk-delete keys are permanently excluded. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build/test gate satisfied by the deny-all substitute only (the box checked on 2026-08-09 covered only that substitute); operational runtime blocked** |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgFieldSecurityPolicy` with `DenyAllFieldSecurityPolicy` | n/a - MOD-0019 contract does not exist | Missing - required proof: deny or omit/mask when field policy is missing or unavailable; raw PHI/PII/free text cannot enter UI/API output, logs, traces, metrics, audit metadata, validation errors, or exports. | Missing - required tests: all 16 fields across list/detail/create/update/export/audit, missing-policy denial, raw-value leak scans, and cross-tenant checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllFieldSecurityPolicy` denies unconditionally. This row continues to **block operational runtime** until MOD-0019 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | MOD-0021 owner role - **Not approved - owner contract pending.** The 2026-08-09 entry named the PVG package owner as approver; that is not an owner approval. | Open | Code fact: mutations return a `PvgAuditIntent` (operation, status, permission, actor kind, timestamp) built after the store write; `PvgIntakeDraftApplicationService` has no audit sink or outbox dependency; the host registers none; the API response drops the intent. `tests/architecture/audit-ledger/Diten.PvgService.md`: no trace; the four MOD-0230 write commands are known debt. Nothing is consumed from the `Diten.Platform` audit feature. | n/a - no owner-approved version | Missing - required proof: no unaudited regulated mutation succeeds; an audit write failure blocks the mutation (AUD-001 K2) or uses only an owner-approved durable outbox in the same unit of work; payload redaction happens before persistence/export. | Missing - required tests: create, update, triage, route, denial/failure events; audit-writer failure leaves the store unchanged and the result not `Succeeded`; single-record read audit (AUD-001 §7) and its write-failure behaviour as this contract fixes it (BG-18); redaction; correlation propagation. | **Not owner-approved and not substituted.** No audit record is written today. Mutations cannot succeed outside tests only because the deny-all permission gate denies every call; that is not audit evidence. The audit-write path that blocks the mutation when the record cannot be written is a separate delivery (DL-5) and acceptance condition (BG-11). Planned audited operations: create, update, triage, route, denial/failure; archive and export events are out of slice 1; the payload allow-list excludes every PHI/PII field. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **open; no build/test substitute** |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgWorkflowTransitionGate` with `DenyAllWorkflowTransitionGate` | n/a - MOD-0023 contract does not exist | Missing - required proof: gate runs before commit; blocked, unavailable, missing queue, missing assignment policy, missing reason code, tenant/object mismatch, or unapproved `NotApplicable` prevents lifecycle mutation. | Missing - required tests: gate-before-commit, allowed/blocked/not-applicable, outage, missing queue/assignment policy, cross-tenant denial, reason-code validation, correlation propagation, and no-PHI workflow event/log/error checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllWorkflowTransitionGate` denies unconditionally. This row continues to **block operational runtime** until MOD-0023 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Not approved - no registered owner contract | Open | Build-gate substitute: `IPvgEvidenceLinkPort` with `DenyAllEvidenceLinkPort` | n/a - MOD-0031 contract does not exist | Missing - required proof: missing required evidence or unavailable Evidence-Link blocks triage, route, archive/void, or handoff unless MOD-0031 owner approves a durable pending-evidence state; no fake pack or duplicated content. | Missing - required tests: link/query shape, completeness, outage, cross-tenant denial, link/unlink audit, correlation propagation, workflow handoff blocked on missing evidence, no duplicated document content, and no-PHI evidence-content checks. | **Not owner-approved. Satisfied for the build/test gate only** by a deny-by-default port: every behaviour this row requires is a denial behaviour, and `DenyAllEvidenceLinkPort` denies unconditionally. This row continues to **block operational runtime** until MOD-0031 ships and its owner signs. The port stores no policy data, hosts no engine, and persists nothing. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **build gate satisfied by fail-closed port; operational runtime still blocked** |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | Enterprise Architecture / trace authority role - **Not approved - owner contract pending.** The 2026-08-09 entry was recorded by the PVG package owner against `Diten.Platform.Common` types that PvgService does not reference. | Open | Code fact: correlation is a PVG-local read of `X-Correlation-Id` into `PvgCorrelationContext`; draft IDs are generated by the store; request DTOs carry no `Id` or `TenantId` (E3 f2: a body `tenantId` is not accepted); no shared correlation middleware; no trace stitching. | n/a - Blueprint MOD-0040 standard version not approved for MOD-0230 | Missing - required proof: no untraceable regulated mutation succeeds; external IDs are non-authoritative; duplicate or mismatch ambiguity rejects, conflicts safely, or routes only through owner-approved durable duplicate review/outbox. | Missing - required tests: server-generated canonical IDs, client-supplied ID rejection, external-ref non-authority, duplicate/mismatch handling, missing/valid/invalid `X-Correlation-Id`, and trace propagation through intake, audit, workflow, evidence, error, and outbox/events. | **Not owner-approved.** MOD-0230 pack rules that bind this module's code (not an approval of the trace contract): canonical IDs are server-generated; client-supplied `Id` and `TenantId` are rejected; `SourceReference` is non-authoritative; duplicate handling returns 409 with no silent overwrite. The repo registry row for `MOD-0040` is a deprecated alias to MOD-0288 and is **not** the authority here - the Blueprint MOD-0040 standard is. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **open; local correlation check only; operational runtime blocked** |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | MOD-0041 / Ops owner role - **Not approved - owner contract pending.** The 2026-08-09 entry was recorded by the PVG package owner against `Diten.Platform.Common/Observability` types that PvgService does not reference. | Open | Code fact: `Program.cs` registers no log enrichment, redaction, OTel, or health response writer; API responses carry safe reason codes; one measured path returns HTTP 500 with an empty body (E3 B3, G-6). | n/a - no owner-approved version | Missing - required proof: raw PHI/PII/free text never enters logs, traces, metrics, validation errors, or error payloads; missing approved telemetry/error policy blocks regulated mutation or uses an explicitly approved degraded path. | Missing - required tests: trace/log/error redaction, correlation propagation across UI/API/service/audit/workflow/evidence/outbox, invalid/missing correlation behavior, safe metric labels, and telemetry outage behavior. | **Not owner-approved.** MOD-0230 pack rules that bind this module's code: reason codes are taxonomy values only; raw exception text, field values, narratives, patient codes, and reporter details are excluded from logs, traces, metrics, validation errors, and error payloads. Leak-scan tests are mandatory before slice 1 is accepted. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption - **open; operational runtime blocked** |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, with MOD-0019, MOD-0021, trace, workflow, and evidence owner alignment where applicable | Not approved - no compliance / legal-hold owner assigned | Missing | Missing | Missing | Missing - required proof: archive/void remains unavailable before approval; legal hold blocks archive and void; missing retention, legal-hold, masking, audit, trace, workflow, or evidence policy denies or blocks with a regulated safe error and no fallback mutation. | Missing - required tests: archive/void blocked before approval, blocked under legal hold, denied for unauthorized actors, denied or masked when MOD-0019 is unavailable, blocked or queued only if MOD-0021 approves durable audit behavior, required metadata captured on allowed paths, evidence/trace references preserved, and hard delete/bulk delete absent. | **Not owner-approved. Removed from slice 1 scope instead:** archive and void surfaces are not implemented in slice 1, so this approval is not on the critical path. It becomes required the moment archive/void is added. No market-specific PV retention period is accepted. | [ ] Owner-approved for MOD-0230 `ready-for-dev` consumption |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Missing | Missing | Missing | Missing | Missing - required proof: approved runtime scope, service boundary, port allocation in `AGENTS.md` §3, appsettings policy, tenant isolation, no client `TenantId`, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void absent or approved, all exposed-surface contracts fail closed, and `Pvg:Runtime:OperationalRuntimeAuthorized` bound to this approval (OD-PVG-6). | Missing - required tests: startup/config fail-closed checks, no port/appsettings/Gateway/frontend/collection/seed/job without approval, tenant isolation, no-PHI telemetry/errors, RBAC/masking/audit/workflow/evidence/trace outage behavior, and phase-gate evidence for every authorized surface. | **NOT AUTHORIZED.** Build/test gate only: local, dev, and CI. Not operational runtime, not production use, not supplier qualification, not validation approval. Requires real MOD-0019, MOD-0023, MOD-0031, signed owner contracts, and a named retention / legal-hold owner. A configuration flag set to `true` is not this authorization. | [ ] Operational runtime authorized - **remains closed** |

### External Owner-Evidence Submission Checklist

External reviewers must answer against the exact MOD-0230 approval artifact named below. A design basis, draft
packet, generic platform capability, or informal note can support review, but it cannot approve the MOD-0230
contract unless the owner explicitly approves the named artifact/version and supplies fail-closed and test evidence.

| Required approval artifact | Who must approve | Evidence reviewers must supply | What cannot count as approval | Exact condition to mark approved |
|---|---|---|---|---|
| `PVG-MOD0230-RBAC-Contract v1` | MOD-0018 / AuthService / Platform access governance | Approver, approval date, artifact/link, approved version, RBAC deny proof, role/action allow-deny tests, cross-tenant denial, platform/partner/tenant actor behavior, seed/grant ownership proof, no delete/bulk-delete proof | Packet recorded only, draft matrix, informal email, unversioned notes, partial permission list, approval by the PVG package owner | All evidence fields are supplied and owner explicitly approves MOD-0230 `ready-for-dev` consumption |
| `PVG-MOD0230-FieldSecurity-Contract v1` | MOD-0019 masking / row-field security owner | Approver, approval date, artifact/link, version, all 16 field rules, missing-policy fail-closed proof, raw PHI/PII/free-text leak tests, list/detail/create/update/export/audit tests | Sensitivity vocabulary alone, draft field matrix, generic masking standard, untested policy | Owner-approved artifact proves allow/mask/omit/deny behavior and tests pass for every required surface |
| `PVG-MOD0230-AuditEvent-v1` | MOD-0021 AuditEvent / audit owner | Approver, approval date, artifact/link, version, append/event shape, outage fail-closed/outbox proof, redaction proof, create/update/triage/route/archive/export/denial tests, single-record read audit test and its write-failure behaviour (AUD-001 §7) | Generic audit capability, unapproved event names, audit notes without failure-mode proof, an in-memory audit intent, approval by the PVG package owner | Owner approves the PVG event contract and required audit/outage/redaction tests are supplied |
| `PVG-MOD0230-WorkflowTransitionGate-v1` | MOD-0023 Workflow/Inbox owner | Approver, approval date, artifact/link, version, gate-before-commit proof, queue/assignment/reason-code behavior, outage and blocked transition tests, cross-tenant denial, no-PHI workflow event/log/error proof | Draft workflow states, queue names without owner approval, untested `NotApplicable` behavior | Owner approves the transition gate contract and all blocked/unavailable/missing-policy paths fail closed |
| `PVG-MOD0230-EvidenceLink-v1` | MOD-0031 Evidence-Link owner | Approver, approval date, artifact/link, version, link/query shape, completeness rules, outage behavior, cross-tenant denial, link/unlink audit, no duplicated content, no-PHI evidence-content tests | Fake evidence pack, duplicated documents, live URL references as authority, generic evidence-link notes | Owner approves the EvidenceLink contract and evidence completeness/outage behavior is test-proven |
| `PVG-MOD0230-TraceBundle-v1` | Enterprise Architecture / platform trace authority for Blueprint MOD-0040 / TRACE-BUNDLE | Approver, approval date, artifact/link, version, canonical ID policy, external ID non-authority proof, duplicate/mismatch handling, correlation header behavior, trace propagation tests | External IDs as primary keys, unversioned trace notes, generic correlation convention only | Owner approves trace identity/correlation behavior and tests prove no untraceable regulated mutation |
| `PVG-MOD0230-ObservabilityErrorModel-v1` | MOD-0041 / Ops / platform observability and regulated error-model owner | Approver, approval date, artifact/link, version, safe reason-code taxonomy, no-PHI telemetry/error policy, redaction tests, correlation propagation, safe metric labels, telemetry outage behavior | Raw exception logging, generic OTel policy, untested error envelope, PHI/PII-bearing logs/traces | Owner approves error/telemetry model and tests prove no PHI/PII/free-text leakage |
| `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` | Compliance / legal-hold / records-retention owner, aligned with MOD-0019, MOD-0021, trace, workflow, evidence owners as applicable | Approver, approval date, artifact/link, version, retention/legal-hold/archive/void rules, archive/void blocked-before-approval proof, legal-hold block proof, no hard delete/bulk-delete proof | Market-generic retention assumption, draft retention matrix, unapproved archive/void wording, missing legal-hold proof | Owner approves class-specific policy and tests prove archive/void/delete paths fail closed as required |
| MOD-0230 operational runtime authorization | User / PVG system owner / Enterprise Architecture, with platform operations and validation approval where required | Approver, approval date, artifact/link, version, approved runtime scope, service boundary, port allocation, appsettings policy, tenant isolation, safe telemetry/errors/audit metadata, no delete/bulk-delete, archive/void status, all exposed-surface fail-closed tests | Local scaffold approval, docs-only planning approval, supplier paper, draft service boundary, untested startup/config behavior, a configuration flag | Explicit operational runtime authorization is granted and the row can be marked approved |

### MOD-0040 / MOD-0288 Identity Clarification

- Use **Blueprint MOD-0040 / TRACE-BUNDLE** for canonical ID, external ID, correlation header, trace stitching, and
  regulated error-model decisions.
- Use **MOD-0288 Organization, Person & Position Directory** only if MOD-0230 routing, assignment, or search
  explicitly consumes organization/person/position references.
- Do **not** reference legacy deprecated repo `MOD-0040` as the organization/person source. In this repo,
  organization/person/position ownership is canonicalized to MOD-0288.

## Runtime Constraints

- **Gate boundary (single statement).** Under the build/test gate the API host may be started in local / dev / CI on
  its configured URL (default `http://127.0.0.1:5011`, or an `ASPNETCORE_URLS` override) to run tests and E2/E3 smoke
  measurements, behind the single Gateway route family and the tenant UI. Operational runtime - production or
  production-like use, supplier qualification, validation, real MOD-0019 / MOD-0023 / MOD-0031 adapters - is closed.
  "MOD-0230 Runtime Authorization Packet (Draft / Not Approved)" governs only operational runtime.
- **Persistence (single statement).** The build/test gate uses the in-memory store only
  (`InMemoryPvgIntakeDraftRepository`). No Mongo collection, index, or migration is authorized under the build/test
  gate (DCP-004 §10). `PvgIntakeDraftIndexCatalog` and the `DB-level` column of "Validation Rules" are planned
  declarations only. A Mongo collection, index, or migration needs the approved runtime authorization packet.
- Seed data, permission seed / grant, and background jobs are unauthorized.
- **Configuration keys (single source: the code).** The host reads `Pvg:Runtime:ProductionLike` (default `false`; the
  environment name `Production` also counts as production-like), `Pvg:Runtime:OperationalRuntimeAuthorized` (default
  `false`), and `Pvg:Runtime:UseNonProductionAdapters` (default `true` when the host is **not** production-like,
  `false` when it is). Configuration comes only from environment variables and the command line; no
  `appsettings*.json` exists. When `ASPNETCORE_ENVIRONMENT` is unset, `Program.cs` uses `Production`, so a default
  start is refused (E3 B1). The key `Pvg:RegPvBase:UseNonProductionAdapters` and its "defaults to false" rule in the
  port-contract document are not what the code reads; for this pack the `Pvg:Runtime:*` keys are the reference.
- **Operational runtime flag.** `Pvg:Runtime:OperationalRuntimeAuthorized` is a configuration value, not an
  authorization. It must be bound to approval evidence before it can mean anything (OD-PVG-6, BG-10).
- All REG-PV-BASE consumption ports are deny-by-default. The host refuses a production-like start with
  non-production adapters (E3 B2). It does not yet refuse a start with missing adapters (G-6).
- BE-01 downstream service registrations for MOD-0231 / MOD-0232 / MOD-0234 application services are
  **composition-only** host wiring for local/dev/CI health-host construction. They do not map endpoints, expose
  routes, authorize downstream runtime, create downstream data, or relax the separate MOD-0231 / MOD-0232 /
  MOD-0234 operational gates. Domain-config states that those modules have no API host and does not carry this
  composition-only exception; this pack is the higher authority, and the difference is recorded, not resolved.
- Archive, void, export, delete, and bulk-delete surfaces must not exist in slice 1.
- The service boundary is a dedicated `Diten.PvgService` with a hybrid partner-aware integration
  posture. The service is expected to own the Diten-controlled intake contract, tenant workflow boundary, audit /
  evidence / workflow integration, and partner adapter boundary if a PV safety partner system is selected.
- `service` is `Diten.PvgService`. `service_port: 5011` records the OD-7 proposal; 5011 is not allocated in the
  `AGENTS.md` §3 table and is also used by the local `web-verify` launch entry (G-13, OD-PVG-7).
- `entity_base: EntityBase` remains correct for Diten-owned tenant records, projections, archive/void metadata, and
  outbox/audit-linked records under the future PVG boundary. Partner-native records remain outside the repo entity
  model unless an adapter contract later maps them.
- Runtime must resolve tenant isolation, regulated data masking, audit, evidence links, workflow/inbox handoff, OTel,
  correlation ID, and error model before acceptance.
- Tenant-owned data must carry server-resolved `TenantId`; client payloads must not accept `TenantId`. Cross-tenant
  reads or mutations must return 404 / empty result with no metadata leak.
- Any sensitive field whose MOD-0019 policy cannot be evaluated must fail closed: deny the field, omit/mask the
  field, or deny the operation according to the field matrix. No implementation may invent a permissive fallback.
- Raw PHI/PII, reporter identifiers, patient identifiers, source document content, free-text safety narratives,
  and unrestricted search/export payloads must not be written to logs, traces, metrics, or audit payloads.
- Delete policy is locked: no hard delete, no bulk delete, and no normal user delete. Archive/void may be introduced
  only after retention/legal-hold approval; it requires reason, actor, UTC timestamp, correlation id, and AuditEvent.
  Archive/void is blocked under legal hold.

## Service Port And Appsettings Policy

- **Port source.** The single port source is the `AGENTS.md` §3 table. 5011 is not in it; `AGENTS.md` lists 5011
  (`PvgService`) among ports in use that await an Enterprise Architecture decision. `.antigravity/rules/ports.md` is
  not the port source and is not edited by this module.
- **Build/test gate.** The host may bind 5011 (default) or an `ASPNETCORE_URLS` override in local / dev / CI. The
  `web-verify` entry in `.claude/launch.json` also binds 5011, so only one of them can run on defaults; this pack
  makes no claim that 5011 is free.
- **Operational runtime gate.** Port allocation in `AGENTS.md` §3 is required (OD-PVG-7, "Required Approval Gates").
  OD-PVG-7: PVG side decided 2026-10-21; awaiting the Enterprise Architecture allocation decision.
  Until then the port decision blocks operational runtime only; it does not stop build/test work.
- **Appsettings.** No `appsettings*.json` or `launchSettings.json` exists for PvgService today. Approved
  `appsettings*.json` content must be service-local, local/dev/CI scoped, and minimum necessary. It may carry
  non-secret feature flags and safe endpoint placeholders only if missing or disabled configuration fails closed. It
  must not contain secrets, partner credentials, PHI/PII, sample patient/reporter data, permissive fallback flags,
  `Pvg:Runtime:OperationalRuntimeAuthorized=true`, or policy values that bypass RBAC, masking, audit, workflow,
  evidence, trace, or observability gates.

## Startup Fail-Closed Requirements

Today (G-6): a production-like host with `Pvg:Runtime:OperationalRuntimeAuthorized=true` and no adapters starts,
`/health/ready` returns 200, and a list call with full headers returns HTTP 500 with an empty body (E3 B3). The
existing test treats the missing registrations as success.

Measurable acceptance (BG-8). Form decided by OD-PVG-5 (owner, 2026-10-21; recommended option adopted). Both parts
are binding:

1. **Production-like host:** the host refuses to start (exception during service registration or provider build,
   non-zero process exit) when any constructor dependency of `PvgIntakeDraftApplicationService` - field security,
   workflow gate, evidence link, permission gate, intake store - does not resolve; **and**
2. **Every environment:** `/health/ready` returns 503, naming only safe dependency codes, when a required port or the
   intake store is missing.

An empty-body 500 from a business endpoint stays unacceptable in every environment ("Audit / Workflow / Evidence /
Trace / Error Requirements").

Test rules:

- A test must fail when a required dependency is missing. The test
  `Production_like_authorized_startup_does_not_register_non_production_adapters` must be replaced or extended so that
  asserting the missing registrations is no longer counted as success.
- Sabotage evidence: removing one required registration turns a named test red.
- The two existing refusals (production-like without authorization; production-like with non-production adapters)
  keep their tests.
- The operational runtime flag is not approval: BG-10 (OD-PVG-6) must hold before any production-like start is
  treated as authorized.

No degraded path may silently allow a regulated create, update, triage, or route mutation unless the owning authority
has approved that degraded path and the behavior is covered by tests.

## Tenant Isolation Rules

Applies to the build/test code now and to any later runtime.

- Tenant context must be server-resolved. Today it is not: the service reads `X-Diten-Tenant-Context` from the
  request (G-2). The Gateway writes the resolved tenant into `X-Tenant-Id` and leaves `X-Diten-*` untouched, so a
  client header is authoritative today. Closing this is a pre-runtime requirement and a build-gate gap owned by DL-4
  (contract: OD-PVG-2; PVG side decided 2026-10-21, awaiting Enterprise Architecture and MOD-0018 claim names).
- Client payloads, query strings, route values, or headers must not supply authoritative `TenantId` or actor identity.
- Cross-tenant reads must return safe 404/empty/not-found style behavior with no existence leak (today: 409, G-8).
- Cross-tenant mutations must deny before mutation and must not disclose the target tenant, object existence, or raw
  request values.
- Tenant values must not appear in validation errors, regulated error payloads, logs, traces, metrics, audit metadata,
  or safe result shapes.

## PHI/PII And Regulated Safety Controls

- Raw patient identifiers, reporter identifiers, contact details, safety narratives, triage free text, source document
  content, and external reference values must not enter logs, traces, metrics, validation errors, regulated errors, or
  audit metadata.
- Allowed responses must use safe reason codes, metadata-only envelopes, redacted hashes, precision indicators,
  or controlled taxonomy codes where approved.
- Missing FieldSecurity, ObservabilityErrorModel, AuditEvent redaction, or trace policy must block regulated mutation or
  use only an explicitly approved fail-closed/degraded behavior.

## Audit / Workflow / Evidence / Trace / Error Requirements

- Audit: create, update, triage, route, denial, and failure paths need owner-approved audit append behavior.
  An audit write failure must block the mutation (AUD-001 K2) or use only an owner-approved durable outbox written in
  the same unit of work. Today only an in-memory `PvgAuditIntent` exists (G-5); the write path is DL-5 (BG-11).
- Read audit: opening one safety case (`GetIntakeDraftByIdQuery`) is audited through the central path only (AUD-001
  §7; "Audited Events" read row, BG-18). Today no read is audited (G-5).
- Workflow: triage and route transitions must call the gate before commit. Missing queue, assignment, reason code,
  tenant/object mismatch, unavailable gate, or blocked transition must prevent mutation.
- Evidence: required evidence links and completeness rules must be owner-approved. Missing or unavailable EvidenceLink
  must block required lifecycle gates; no fake evidence pack or duplicated content is allowed.
- Trace: canonical IDs must be server-generated; external references are non-authoritative; missing/invalid correlation
  must block regulated mutation; duplicate or mismatch ambiguity must fail safely.
- Errors/observability: regulated errors, logs, traces, and metrics must carry only safe codes and safe metadata. Raw
  exception messages, stack traces, PHI/PII, tenant values, and free text must not be emitted. An empty-body 500 is
  not an acceptable error (G-6, G-8).

## Layout & Shell Contract

`shell: tenant`

MOD-0230 is a tenant/domain operational workflow surface, not a platform-admin configuration module.

- Razor layout: every `.cshtml` page explicitly sets `Layout = "_LayoutTenantShell";` (measured 2026-10-21 in
  `Index`, `Create`, `Edit`, `Details`).
- MVC route: `/Pharmacovigilance/CaseIntakeTriage`, served by `PharmacovigilanceCaseIntakeTriageController`
  (`[Route("Pharmacovigilance/CaseIntakeTriage")]`).
- View root: `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`.
- Every controller action carries `[HasPermission("<key>")]` with a key of the family chosen in OD-PVG-1 (target
  `pvg.case-intake-triage.*`; not applied until MOD-0018 / AuthService approval), in addition to `[Authorize]`. Today the controller carries `[Authorize]` only (G-4).
- UAS-001: a user without the read permission receives no page skeleton of this module (no DataTable, form, or filter
  markup) and is not redirected into the module; the shared unauthorized response applies.
- Frontend API profile: same-origin MVC proxy. Browser JavaScript calls the MVC proxy surface, never the Gateway
  directly and never a service port. The proxy calls the Gateway server-side with the session bearer token.

Frontend implementation is authorized for the build/test gate. It remains unauthorized for production until the
operational runtime gate opens.

## Backend File Convention

`service: Diten.PvgService` (port proposal 5011, see "Service Port And Appsettings Policy")

Boundary resolved by OD-7: dedicated `Diten.PvgService` with a hybrid partner-aware integration posture.

- Do not host MOD-0230 inside `Diten.Platform`, `Diten.AuthService`, `Diten.DevEnablementService`, or
  `Diten.EnterpriseStrategyService`.
- If a buy/partner PV safety system is selected, `Diten.PvgService` should act as the controlled wrapper /
  orchestration layer for Diten tenant UI, intake contract, audit, evidence, workflow, correlation, and adapter
  semantics. It must not become a direct frontend-to-partner bridge.
- Internal build scope is limited to the Diten-controlled contract, tenant UI boundary, workflow/audit/evidence
  integration, and adapter layer after approval; it must not become a full standalone PV safety platform under
  MOD-0230.

**Measured layout (G-11).** The code does not follow the Golden Reference CQRS shape:

```text
services/Diten.PvgService/src/
├── Diten.PvgService.Api/            Program.cs, PvgServiceApiHost.cs, PvgCaseIntakeTriageEndpoints.cs
├── Diten.PvgService.Application/RegPvBase/
│   ├── CreateIntakeDraftCommand.cs, UpdateIntakeDraftCommand.cs, TriageIntakeDraftCommand.cs, RouteIntakeDraftCommand.cs
│   ├── GetIntakeDraftListQuery.cs, GetIntakeDraftByIdQuery.cs
│   ├── PvgIntakeDraftApplicationService.cs   (one service; no *Handler classes; no MediatR)
│   ├── PvgIntakeDraftValidator.cs
│   └── IPvg*Port / IPvgPermissionGate / IPvgIntakeDraftStore and request/result records
├── Diten.PvgService.Domain/RegPvBase/        SafetyCaseIntake and field/status/outcome types
└── Diten.PvgService.Infrastructure/RegPvBase/ DenyAll* adapters, InMemoryPvgIntakeDraftRepository, PvgIntakeDraftIndexCatalog
```

The Golden Reference shape this pack originally prescribed:

```text
Features/CaseIntakeTriage/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CaseIntakeTriageModels.cs
```

with commands `CreateCaseIntakeTriageCommand`, `UpdateCaseIntakeTriageCommand`, `TriageCaseIntakeTriageCommand`,
`RouteCaseIntakeTriageCommand`, queries `GetCaseIntakeTriageListQuery`, `GetCaseIntakeTriageByIdQuery`, handlers
`*Handler`, validators `*Validator`.

**Decision OD-PVG-4 (owner, 2026-10-21; recommended option adopted):** the measured `Application/RegPvBase/*`
application-service layout is a **documented deviation (technical debt) from the Golden Reference for slice 1**. The
four commands, the two queries, and the application service are not renamed; the Golden Reference shape above stays
recorded as the reference, not as a slice 1 requirement. Revisit when DL-5 lands. Reason: see "Open Decisions (Owner
Decision Pending)".

Rules that hold regardless of the deviation:

- **Out of slice 1:** archive, void, and any export command.
- Permanently forbidden: delete and bulk-delete commands, DELETE endpoints, and bulk-delete endpoints.
- No `CommandHandler`, `QueryHandler`, or `RequestHandler` suffix; no `CommandValidator` suffix.

## Frontend File Contract

`golden_reference: compact` · `data_mode: server` (OD-PVG-8, decided 2026-10-21; the table attribute
`data-dt-data-mode="server"` is DL-7 work)

Declaring `data_mode: server` and the table attribute does not make the list server-paged. Today `index.js`
`buildListUrl` sends `pageNumber=1` and `pageSize=100` on every request and the API returns no total counts, so records
beyond the first 100 are unreachable (G-16). The fix is measured by BG-20 (screen, DL-7) and BG-19 (API counts, DL-4).

The create/edit field count is 16, so MOD-0230 follows Golden Reference Compact:

- `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs` (MVC proxy; 10 actions).
- `Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`,
  `_Form.cshtml`, `_Filter.cshtml`, `_DataTable.cshtml`, `_IndexL10n.cshtml`, `CaseIntakeTriageIndex.cs`.
- `wwwroot/assets/js/Pharmacovigilance/CaseIntakeTriage/index.js`, `index.l10n.js`, `form.js`, `details.js`.
- `Resources/Views/Pharmacovigilance/CaseIntakeTriage/CaseIntakeTriageIndex.{en,tr,fr,es,zh,ar,ru}.resx` - tenant
  module, all seven languages.
- `Models/CaseIntakeTriage/**` form view model (missing today, G-14).
- `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` (vitest).

Compact must not include Slim-only `_CreateEditOffcanvas.cshtml` or `_DetailsQuickView.cshtml`.

DataTable verifier (G-14): 18 gaps at `ce3be60cf`. Five are real gaps fixed in DL-7 (data mode, `_TableSkeleton`,
the `_Filter` and `_IndexL10n` partial includes, form view model). The other 13 concern bulk delete, export, import,
select-all, and their wiring (reload-with-toast, clear-selection), which this pack permanently excludes; by OD-PVG-10
(owner, 2026-10-21) they are recorded as this module's **known deviation** (list-screen touch protocol answered: no
silent fix and no silent skip).

Frontend API calls use the same-origin MVC proxy profile. Direct browser-to-Gateway calls and direct service-port calls
are forbidden for this regulated tenant surface.

## Validation Rules

Recorded 2026-08-09 for slice 1 as the PVG package owner's rules for this module's own fields; binding on
implementation. `Pre-check` is server-side validation before persistence. `DB-level` records a planned Mongo
index/constraint decision only; no collection or index is authorized under the build/test gate ("Runtime
Constraints"). The code's `PvgIntakeDraftIndexCatalog` declares two different index definitions (tenant + draft id
unique; tenant + status + received) as data only.

Order of checks (G-9): tenant / actor / correlation context and the permission check run **before** field validation,
so an unauthorized caller never receives validation reason codes. Today validation runs first.

**Route is intentionally unsatisfiable in slice 1.** Today (code at `8e13f3787`): `IPvgWorkflowTransitionGate`
defines one member, `EvaluateAsync`; `RouteDraftAsync` calls it for the route transition and passes `null` as the route
target queue (`PvgIntakeDraftApplicationService.cs` line 302); `DenyAllWorkflowTransitionGate.EvaluateAsync` returns
`WorkflowTransitionDenied` for every request, so Route never succeeds. Proposed, not in code: a route-target resolution
against the MOD-0023 queue registry - named `ResolveRouteTargetAsync` in
`docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md` and WP-0230-01 (G-12) - whose shape (separate member
or part of `EvaluateAsync`) is set by `PVG-MOD0230-WorkflowTransitionGate-v1`. The queue registry belongs to MOD-0023
and does not exist. The `Route` command, endpoint, permission key, audit event, and fail-closed test are all built in
slice 1 and become functional only when a real MOD-0023 client replaces the deny adapter. Any implementation that makes
Route succeed by inventing a queue list must be rejected in review.

| Field | Required | Rule | DB-level | Pre-check | Sensitivity / fail-closed requirement |
|---|---|---|---|---|---|
| IntakeChannel | Yes | Controlled option set; no free text | Planned index `(TenantId, IntakeChannel)` | Value must exist in the PVG lookup; unknown value rejected 400 | unknown channel fails closed for routing decisions |
| SourceType | Yes | Controlled option set; no free text | Planned index `(TenantId, SourceType)` | Value must exist in the PVG lookup; unknown value rejected 400 | unknown source fails closed for routing decisions |
| SourceReference | No | Max 128 chars; trimmed; non-authoritative external ID | Planned non-unique index `(TenantId, SourceType, SourceReference)` | Duplicate `(SourceType, SourceReference)` in the same tenant returns 409 with a duplicate-candidate reason code; never a silent overwrite | confidential; never authoritative, never a primary key, never raw in logs/audit |
| ReceivedAtUtc | Yes | UTC instant; rejected if later than server time + 5 min skew or earlier than 1900-01-01 | Planned index `(TenantId, ReceivedAtUtc desc)` | Server re-normalizes to UTC; a client-supplied offset is converted, never trusted as-is | regulated-safety; server-side UTC normalization is mandatory |
| ReporterType | Yes | Controlled option set | Planned index `(TenantId, ReporterType)` | Value must exist in the PVG lookup | public-metadata |
| ReporterContactSummary | No | Max 256 chars; summary only - no full address, no national ID, no account number | Not indexed | Minimum-necessary check: rejected if it exceeds 256 chars | PII; deny/mask/omit when field policy is unavailable |
| PatientSubjectCode | No | Max 64 chars; pseudonymous code only; no whitespace; must not contain the at-sign character or more than 2 consecutive digit groups resembling a national identifier | Not indexed | Rejected if it fails the pseudonymity pattern | PHI; never returned or audited raw without an approved policy |
| EventOnsetDate | No | Date-only; not in the future; not before 1900-01-01; must be on or before `ReceivedAtUtc` date | Not indexed | Cross-field check against `ReceivedAtUtc` | PHI; mask/omit by actor policy |
| AdverseEventNarrative | Yes | Max 8000 chars; required non-empty after trim | Not indexed; **excluded from any text index** | Length and non-empty check | PHI; raw free text prohibited in logs/traces/metrics/audit payloads and error responses |
| SuspectProductText | No | Max 512 chars | Not indexed | Length check only until a terminology contract exists | regulated-safety; omit/deny if unapproved |
| Seriousness | Yes | Controlled option set | Planned index `(TenantId, Seriousness)` | Value must exist in the PVG lookup | regulated-safety |
| IntakePriority | Yes | Controlled option set; SLA linkage deferred to MOD-0023 | Planned index `(TenantId, IntakePriority)` | Value must exist in the PVG lookup; no SLA is computed in slice 1 | regulated-safety |
| TriageOutcome | Yes at Triage, absent at Create | Controlled option set: `Triaged`, `Rejected`, `Duplicate` | Planned index `(TenantId, TriageOutcome)` | Transition must return `Allowed` from `IPvgWorkflowTransitionGate.EvaluateAsync` **before** commit (today the deny-all gate denies every triage) | invalid or unapproved transition fails closed |
| TriageReason | Required when `TriageOutcome` is `Rejected` or `Duplicate`, otherwise optional | Max 1000 chars; a taxonomy reason code is mandatory, free text is supplementary only | Not indexed | Reason code must exist in the taxonomy; free text alone is rejected | PHI; raw reason text prohibited in logs/traces/metrics/audit payloads |
| RouteTargetQueue | Yes at Route | Today: the value is not passed to the gate (`null`, line 302) and `DenyAllWorkflowTransitionGate.EvaluateAsync` denies every route. Proposed, not in code: the value is resolved against the MOD-0023 queue registry through the route-target resolution described above | Not indexed | **Denied for every request in slice 1** - the queue registry is MOD-0023's and does not exist | confidential; visibility must be permission-filtered; no hardcoded queue list is permitted |
| EvidenceLinkReferences | No | Max 20 references; each is an object reference only, never document content | Not indexed | Today: `DenyAllEvidenceLinkPort.EvaluateAsync` returns `EvidenceLinkDenied` and records nothing, so a create or update that carries references, and every triage and route, is blocked. Proposed, not in code: requirements recorded as `Pending`, only if the MOD-0031 owner approves a durable pending-evidence state. Completeness never returns `Allowed` in slice 1 | confidential; no fake evidence pack when the evidence service is unavailable |

Update concurrency (G-15): an update, triage, or route based on a stale read must return 409 with no silent
overwrite. Today no record version exists in any layer; the in-memory store overwrites without a version check and the
application service reads every `ReplaceAsync` `false` as not-found. Required shape and ownership:

| Layer | Required change | Slice | Prerequisite |
|---|---|---|---|
| Store contract (`IPvgIntakeDraftStore.cs`, `InMemoryPvgIntakeDraftRepository.cs`) | Each stored draft carries a version; `ReplaceAsync` takes the expected version and returns a result that separates not-found from version conflict (not a `bool`) | DL-2 | none |
| Application (`PvgIntakeDraftApplicationService.cs` `ReplaceAsync` call sites at lines 157, 239, 321; command, request, and result types; `PvgApplicationReasonCodes.cs`) | Update, triage, and route commands carry the expected version; a conflict maps to its own reason code, never to `IntakeDraftNotFound`; read results return the current version | DL-2 | none |
| API (`PvgCaseIntakeTriageEndpoints.cs` request and response records) | PUT, `/triage`, and `/route` bodies carry the expected version; item and list responses return it; a conflict is HTTP 409 | DL-4 | DL-2 (BG-12) |
| MVC proxy and JS (`PharmacovigilanceCaseIntakeTriageController.cs` form inputs, `form.js`, `details.js`, Edit / Details views) | The version read with the record is sent back on save, triage, and route; a conflict shows the controlled-failure alert, not success | DL-7 | DL-4 (BG-19) |

Acceptance: BG-12 (DL-2), BG-19 (DL-4), BG-20 (DL-7).

### Option-Set Ownership

Option-set ownership recorded for draft planning for future validation and UI contracts:

| Option set | Owner / source | Ownership type |
|---|---|---|
| IntakeChannel | PVG-controlled lookup | Static controlled PVG lookup |
| SourceType | PVG-controlled lookup, partner/source-system extensible | Static controlled PVG lookup with controlled extensions |
| ReporterType | PVG-controlled lookup | Static controlled PVG lookup |
| Seriousness | PVG/compliance-controlled lookup | Static controlled PVG lookup |
| IntakePriority | PVG lookup plus workflow/SLA policy owner | Workflow/reference-owned |
| TriageOutcome | MOD-0023 Workflow/Inbox transition contract plus PVG state model | Workflow-owned |
| RouteTargetQueue | MOD-0023 Workflow/Inbox queue registry, possibly backed by reference service | Workflow/reference-owned |

Static controlled PVG lookups may be planned as PVG-owned reference data only after the approved reference-data
publication path is selected. Workflow/reference-owned option sets must fail closed when the owning contract is
unavailable; no hardcoded UI fallback is allowed.

Every field included in the final create/edit/list/detail/export surface must have at least one matching test
expectation proving unauthorized, cross-tenant, missing-policy, and masking-denied behavior.

## Failure Path to Verify

Each path lists the required behaviour and, where measured, today's behaviour (E3, 2026-10-20). Status codes follow
OD-PVG-3 (PVG side decided 2026-10-21; awaiting MOD-0041 / Ops approval of the regulated error envelope - not applied
until then).

- **Missing required intake field**
  - Expected: 400 validation response; no intake record created. Today: 400 - but returned before the permission
    check (G-9).
- **Duplicate or conflicting intake identifier**
  - Expected: 409 or approved duplicate-handling response; no silent overwrite.
- **Concurrent update on a stale version**
  - Expected: 409 with a version-conflict reason code distinct from not-found; no silent overwrite. Today: last write
    wins; no version exists in any layer (G-15; BG-12, BG-19, BG-20).
- **Unauthorized actor / missing or invalid token**
  - Expected: 401 for missing or invalid token, 403 for missing permission; no data leakage. Today: no token
    validation; 409 `PVG_PERMISSION_DENIED` from the deny-all gate (G-1, G-8).
- **Client-supplied tenant or actor header**
  - Expected: `X-Diten-Tenant-Context` / `X-Diten-Actor-*` sent by a caller have no effect; tenant and actor come from
    validated server context. Today: they are authoritative (G-2).
- **Missing tenant context**
  - Expected: 400 per `multi-tenancy.md`. Today: 409 `PVG_TENANT_CONTEXT_REQUIRED`.
- **Cross-tenant access / unknown id**
  - Expected: 404 with no record data. Today: 409 (`IntakeDraftNotFound` returned as `Blocked`).
- **Unmasked sensitive field**
  - Expected: request/response blocked or masked according to MOD-0019 contract.
- **Missing MOD-0019 policy for a sensitive field**
  - Expected: field omitted/masked or operation denied according to the field matrix; no permissive fallback.
- **Unauthorized field-level read**
  - Expected: non-sensitive envelope may return only if approved; restricted field is masked/omitted or request is denied.
- **Sensitive input appears in audit/log/trace**
  - Expected: test fails; raw PHI/PII/free text must not be persisted to audit payloads, logs, traces, metrics, or error details.
- **Evidence-link unavailable**
  - Expected: fail-closed or explicitly degraded behavior; no fake evidence pack.
- **Workflow/Inbox unavailable**
  - Expected: triage/routing transition blocked; no untraceable routing.
- **Audit write fails**
  - Expected: regulated mutation not committed; result not `Succeeded`; store unchanged (AUD-001 K2). Today: no audit
    write exists (G-5).
- **Read audit for a single-record open cannot be written**
  - Expected: the behaviour fixed by the MOD-0021 contract `PVG-MOD0230-AuditEvent-v1` (OD-PVG-9; BG-18). AUD-001 §7
    requires the read audit but does not state this behaviour; this pack does not invent one. Today: no read audit
    exists (G-5).
- **Required dependency missing at startup**
  - Expected (OD-PVG-5, decided 2026-10-21): a production-like host refuses to start; in every environment readiness
    returns 503 with safe dependency codes; no business endpoint returns an empty-body 500.
    Today: ready 200 and HTTP 500 with empty body (G-6).
- **Correlation/trace context missing**
  - Expected: regulated request blocked with a safe reason code; no untraceable regulated state change. Today: blocked
    with `PVG_CORRELATION_CONTEXT_REQUIRED` / `PVG_CORRELATION_CONTEXT_INVALID` (409).
- **Delete/archive/export attempted**
  - Expected: operation absent. Today: reserved segments return 404; `DELETE` returns 405 (direct) / 404 (Gateway).

## Authorization Convention

**Fact (G-3):** the service checks `pvg.mod0230.intake.{read,create,update,triage,route}` today; this pack proposes
`pvg.case-intake-triage.*`. The single key family is **OD-PVG-1**: PVG side decided 2026-10-21 (owner) - target family
`pvg.case-intake-triage.*`; awaiting MOD-0018 / AuthService approval of catalog-grant ownership and the `pvg.*`
namespace. Until that approval is recorded the keys below are the implementation target only and are not applied
(DL-6 does not start).
Catalog, seed, and grant ownership stay with MOD-0018 / AuthService; MOD-0230 only defines and consumes keys.

Policy and enforcement (PKS-001 format, tenant shell):

- MVC controller: `[Authorize]` plus `[HasPermission("<key>")]` on every action (10 of 10). Today: `[Authorize]` only.
- Service: the permission check runs in the application service before validation and before any port or store call,
  using validated actor and tenant context (OD-PVG-2). Today: deny-all permission gate, header-supplied context.
- UAS-001: no page skeleton for a user without the read key.
- Actor types: tenant_user (UI), system integration actor (approved intake contract only); platform_admin has no
  special access to PHI fields without MOD-0019 policy.

Permission keys - **target** (OD-PVG-1 PVG side decided; not applied until MOD-0018 / AuthService approval):

```text
pvg.case-intake-triage.read
pvg.case-intake-triage.create
pvg.case-intake-triage.update
pvg.case-intake-triage.triage
pvg.case-intake-triage.route
pvg.case-intake-triage.archive   (out of slice 1)
pvg.case-intake-triage.export    (out of slice 1)
```

`triage` and `route` are PKS-001 Tier-3 domain actions registered by this pack. The `pvg.*` namespace is not yet
listed in PKS-001 §4; listing it belongs to MOD-0018.

Explicitly excluded permission keys:

```text
pvg.case-intake-triage.delete
pvg.case-intake-triage.bulk-delete
```

Initial role / permission matrix - **proposal** (not approved; MOD-0018 owner approval required):

| Role | read | create | update | triage | route | archive | export |
|---|---:|---:|---:|---:|---:|---:|---:|
| PVG Intake Agent | Assigned / own queue | Yes | Pre-triage only | No | No | No | No |
| PVG Triage Lead | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | No |
| PVG Safety Manager | Yes | Yes | Yes | Yes | Yes | Only after retention/legal-hold approval | Masked export only |
| PVG Compliance Auditor | Read-only | No | No | No | No | No | Masked export only |
| PVG System Integration | Approved intake contract only (none exists; see REQ-PVG-CRM-01 below) | Yes | No | No | No | No | No |

PVG System Integration: no system-to-system intake contract is written. The CRM visit-report adverse-event request
`execution/domains/pharmacovigilance/work-packs/REQ-PVG-CRM-01-visit-report-adverse-event-msl.md` (2026-10-21, open,
deferred) asks for one; this pack only references it and defines no contract.

Open authorization decisions:

- Key family: OD-PVG-1 - PVG side decided (`pvg.case-intake-triage.*`); awaiting MOD-0018 / AuthService.
- Final actor role names, actor type mapping, and seed/grant ownership require MOD-0018 / AuthService approval.
- Archive permission is unusable until retention/legal-hold policy is approved.
- PHI/PII field-level authorization must align with MOD-0019 before runtime.
- Export is masked-only unless a later field policy approval explicitly permits more.

No permission seed is authorized by this pack.

## Audited Events

Rule: `.antigravity/rules/audit-trail-standard.md` (AUD-001). Ledger: `tests/architecture/audit-ledger/Diten.PvgService.md`
records **no audit trace** in PvgService and lists all four commands below as known debt. PvgService is one of the
services without a trace; AUD-001 §5 K4 blocks new write commands there until the shared forwarder package exists.
These four commands predate that rule (commit `0860632f1`, 2026-08-16); this pack adds no write command.

All four are **K2 class** (GxP regulated safety record, PHI-bearing): if the audit record cannot be written, the
command must fail (AUD-001 §4.3). No fail-closed path exists today. Mutations cannot succeed outside tests only because
the deny-all permission gate denies every call; any change that lets a MOD-0230 mutation succeed before DL-5 lands
breaks K2. Path: **OD-PVG-9** - PVG side decided 2026-10-21 (owner): wait for the K4 shared forwarder (path `b`)
and the MOD-0021 owner contract, keep the deny-all permission gate until then; not actionable until both exist.
Event names below are proposals.

| Command | Event name (proposal) | Object (type) | Path | Before/after or changed fields | Exception class + reason |
|---|---|---|---|---|---|
| `CreateIntakeDraftCommand` | `pvg.case-intake.created` | SafetyCaseIntake | none today - dependency: K4 forwarder (path `b`) + MOD-0021 contract | After: status, IntakeChannel, SourceType, ReporterType, Seriousness, IntakePriority (taxonomy codes only) | - (K2 class) |
| `UpdateIntakeDraftCommand` | `pvg.case-intake.updated` | SafetyCaseIntake | none today - same dependency | Before/after of changed taxonomy-coded fields only | - (K2 class) |
| `TriageIntakeDraftCommand` | `pvg.case-intake.triaged` | SafetyCaseIntake | none today - same dependency | Before/after: status, TriageOutcome, triage reason code | - (K2 class) |
| `RouteIntakeDraftCommand` | `pvg.case-intake.routed` | SafetyCaseIntake | none today - same dependency | Before/after: status, route target queue code | - (K2 class) |
| `GetIntakeDraftByIdQuery` (**read**; `GetDraftByIdAsync`) | `pvg.case-intake.viewed` | SafetyCaseIntake | none today - dependency: the same K4 forwarder (path `b`) + MOD-0021 contract (OD-PVG-9); central path `a` / `b` only, path `c` (equivalent trail) is not allowed for reads (AUD-001 §7) | Read: object type and id, actor, server-resolved tenant, correlation id, UTC time, outcome; no field value | - (AUD-001 §7: a patient-safety record - a pharmacovigilance case - opened singly) |

**Read row (added 2026-10-22, independent review 02 F-1).** AUD-001 §7 audits no read by default, except three
classes; one of them is a pharmacovigilance case opened singly. `GetIntakeDraftByIdQuery` is that open (today it
returns the draft id and status only; the class does not depend on how many fields the detail returns). The read row
adds no write command, so AUD-001 §5 K4 is not breached by it; it shares the write rows' dependency and is not
actionable until OD-PVG-9's two dependencies exist - no separate decision is opened for it. Not audited:
`GetIntakeDraftListQuery` (`ListDraftsAsync`) - a list page is not a single-record open, and it is not a bulk export of
personal data (export is out of slice 1), so none of the three §7 classes applies.

- Today: `GetDraftByIdAsync` (`PvgIntakeDraftApplicationService.cs` line 337) writes no audit record. The deny-all
  permission gate stops every read outside tests; that is the absence of reads, not a read audit, and it does not stand
  in for one. Any change that lets a single-record open succeed outside tests before BG-18 holds breaks AUD-001 §7.
- Write-failure behaviour: AUD-001 §7 requires the read audit and its central path; it does not state what happens when
  the read record cannot be written. §4.3 states fail-closed for the K2 class in terms of commands and for bulk
  personal-data export only. This pack does not extend either to reads on its own authority: the behaviour is a
  required item of `PVG-MOD0230-AuditEvent-v1` (MOD-0021 owner), reached through OD-PVG-9, and BG-18 pins it with a test.
- No architecture test measures read audit (AUD-001 §9: "§7 Okuma denetimi - Hiçbir test"); BG-18 therefore names
  PVG's own tests.

Never written to the audit record: `ReporterContactSummary`, `PatientSubjectCode`, `EventOnsetDate`,
`AdverseEventNarrative`, `SuspectProductText`, `TriageReason` free text, raw `SourceReference`, tokens, tenant values
beyond the server-resolved tenant key.

## Gateway / API Routing Decision

Decision: one Gateway route family is **authorized for the build/test gate** since 2026-08-09, integration-agent-owned.

**Correction 2026-08-09 (NET-001).** Upstream `/api/{resource}`; the `v1` prefix is on the downstream template only.

| | Template |
|---|---|
| Upstream (Gateway, port 5000) | `/api/pv-case-intake-triage` and `/api/pv-case-intake-triage/{intakeDraftId}` (+ `/triage`, `/route`) |
| Downstream (`Diten.PvgService`, `localhost:5011`) | `/api/v1/pv-case-intake-triage` and `/api/v1/pv-case-intake-triage/{intakeDraftId}` (+ `/triage`, `/route`) |

Measured 2026-10-21: four route entries in `gateway/Diten.ApiGateway/ocelot.json` - collection (GET, POST), item
(GET, PUT), `/triage` (POST), `/route` (POST). No OPTIONS method: browsers never call the Gateway for this module
(same-origin MVC proxy), so none is needed while that profile holds. No route-level `AuthenticationOptions`, which is
the repo norm; authentication and `TenantResolutionMiddleware` run pipeline-wide. The Gateway writes `X-Tenant-Id`
and neither writes nor strips `X-Diten-*` (G-2); the service-side contract is OD-PVG-2 and needs no Gateway change.

Slice 1 sub-resources:

```text
/api/pv-case-intake-triage
/api/pv-case-intake-triage/{id}
/api/pv-case-intake-triage/{id}/triage
/api/pv-case-intake-triage/{id}/route
```

Out of slice 1 - must not be routed:

```text
/api/pv-case-intake-triage/{id}/archive
/api/pv-case-intake-triage/export
```

Permanently excluded:

```text
DELETE /api/pv-case-intake-triage/{id}
/api/pv-case-intake-triage/bulk-delete
```

Changes to `ocelot.json` are made only by integration-agent. Frontend consumption uses the same-origin MVC proxy;
direct browser-to-Gateway and direct service-port calls are forbidden for MOD-0230.

## Acceptance Criteria

A checked box records a fact; it is not acceptance evidence (AUDIT-FU-01). Acceptance is measured by the REP-001
acceptance record.

**Original planning pack:**

- [x] Pack exists at `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`.
- [x] Status started as `draft`; later set to `ready-for-dev` for the build/test gate only.
- [x] Canonical name is exactly `Case Intake & Triage`.
- [x] DCP-002 preflight passed for MOD-0230.
- [x] W-3A0 dependencies are recorded as production blockers, not waived.
- [x] No operational runtime implementation is authorized.
- [x] Form field count recorded as `16`.
- [x] Golden Reference recorded as `compact`.
- [x] Shell recorded as `tenant`.
- [x] Entity base recorded as `EntityBase` for the dedicated PVG service boundary.
- [x] Delete and bulk-delete are explicitly excluded; archive/void is blocked until retention/legal-hold approval.
- [x] Service boundary recorded as dedicated `Diten.PvgService`.
- [x] Build/buy/partner strategy recorded as hybrid, partner-aware internal control wrapper.
- [x] Tenant MVC route, view root, same-origin MVC proxy profile, and Gateway route are recorded.
- [x] Option-set ownership is recorded for the field model.

**Build / test gate state (recorded 2026-08-09; corrected 2026-10-21 - not all items are closed):**

- [x] DCP-004 is `approved` / `ready-for-execution`.
- [x] Frontmatter `status: ready-for-dev` recorded 2026-08-09.
- [ ] Version-bound approval row for the current pack version (CT-APR-01 / CT-APR-02) - none exists;
      `scripts/pack_approval.py check` exit 1 (measured 2026-10-21). Requires an independent review with no open
      blocking finding and an approval by someone other than the author.
- [x] `service` resolved to `Diten.PvgService` (OD-7).
- [ ] Port 5011 allocated in `AGENTS.md` §3 - open (OD-PVG-7: PVG side decided 2026-10-21; awaiting the Enterprise
      Architecture allocation decision; the `.claude/launch.json` change needs the repo owner's separate approval).
- [x] Three consumption ports exist with deny-by-default adapters (`DenyAllFieldSecurityPolicy`,
      `DenyAllWorkflowTransitionGate`, `DenyAllEvidenceLinkPort`), plus a fourth `DenyAllPermissionGate`.
- [ ] Port conformance suite C-01 ... C-17 mapped to tests - not mapped (G-12).
- [ ] Interface contracts for MOD-0018, MOD-0021, and Blueprint MOD-0040 / TRACE-BUNDLE - open; not closed against
      any runtime (PvgService references no `Diten.Platform.Common` project).
- [x] Interface contracts for MOD-0019, MOD-0023, and MOD-0031 substituted for the build gate by fail-closed ports
      (not approved).
- [x] Retention / legal-hold removed from the critical path by dropping archive/void from slice 1.
- [x] Build/buy/partner integration boundary decided as hybrid, partner-aware internal control wrapper (OD-7).
- [x] Gateway route family in NET-001 upstream/downstream form (four entries measured).

**Build/test slice acceptance (measurable; added 2026-10-21).** Each item names its slice and stays open until its
command or test shows the stated result. Items that depend on an open decision cannot be checked before it is decided.

- [ ] BG-1 (DL-7) - Every `.cshtml` under `Views/Pharmacovigilance/CaseIntakeTriage/` that renders a page sets
      `Layout = "_LayoutTenantShell";` (`grep -L` over `Index`, `Create`, `Edit`, `Details` prints nothing).
- [ ] BG-2 (DL-7; after OD-PVG-1 and MOD-0018 / AuthService approval of catalog-grant and the `pvg.*` namespace) -
      `PharmacovigilanceCaseIntakeTriageController` carries `[HasPermission("<key>")]` on 10 of 10 actions, every key
      from the target family `pvg.case-intake-triage.*` (not applied until that approval).
- [ ] BG-3 (DL-7) - UAS-001: a signed-in tenant user without the read key requesting
      `/Pharmacovigilance/CaseIntakeTriage` receives the shared unauthorized response; the body contains no
      `dt-pvg-case-intake-triage` table, form, or filter markup; a named test proves it.
- [ ] BG-4 (DL-6; after OD-PVG-1 and MOD-0018 / AuthService approval of catalog-grant and the `pvg.*` namespace) -
      Scan limited to the MOD-0230 surface (narrowed 2026-10-22, review 02 F-2):
      `git grep -n 'pvg\.' -- 'services/Diten.PvgService/src/*/RegPvBase/*' services/Diten.PvgService/src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/PvgApplicationContractShapeTests.cs`
      prints only keys of the target family `pvg.case-intake-triage.*` (today it prints the five `pvg.mod0230.intake.*`
      constants and the five shape-test strings, measured 2026-10-22); the shape test pins the same family, and
      "Authorization Convention" lists the same strings. The MOD-0231 keys `pvg.mod0231.case-processing.*`
      (`Application/CaseProcessing/PvgCaseProcessingApplicationService.cs` lines 7-10) are outside this scan: they are
      the MOD-0231 pack's subject, not this pack's (XMC-001).
- [ ] BG-5 (DL-4; after OD-PVG-2 and Enterprise Architecture approval of the contract and MOD-0018 approval of the
      claim names) - No bearer or an invalid bearer -> 401; a caller-supplied `X-Diten-Tenant-Context`
      or `X-Diten-Actor-Id` that differs from validated context has no effect (test reads no data of the injected
      tenant); tenant header and token tenant claim mismatch -> denied.
- [ ] BG-6 (DL-4; after OD-PVG-3 and MOD-0041 / Ops approval of the regulated error envelope) - Target mapping:
      unknown id and cross-tenant id -> 404 with no record data; missing or invalid token -> 401; permission denied ->
      403; missing tenant context and validation -> 400; duplicate, workflow-blocked, evidence-blocked, concurrency
      conflict -> 409; unexpected failure -> 500 with a non-empty safe envelope; no endpoint returns an empty-body 500;
      tests in `Diten.PvgService.Api.Tests`. Not applied until that approval.
- [ ] BG-7 (DL-4) - An unauthorized caller with an invalid body receives the context / permission outcome, never a
      validation reason code (test).
- [ ] BG-8 (DL-3; form decided by OD-PVG-5, 2026-10-21) - "Startup Fail-Closed Requirements" holds: a production-like
      host refuses to start when a dependency of `PvgIntakeDraftApplicationService` does not resolve, and in every
      environment `/health/ready` returns 503 when a required port or the store is missing; removing one required
      registration turns a named test red.
- [ ] BG-9 (DL-3) - A static test fails if any committed `appsettings*.json`, launch profile, workflow, or deployment
      file sets `Pvg:Runtime:OperationalRuntimeAuthorized` to `true` or sets `Pvg:Runtime:UseNonProductionAdapters`
      for a production-like environment.
- [ ] BG-10 (DL-3; after OD-PVG-6 and Enterprise Architecture approval of the binding form) - A production-like start with `Pvg:Runtime:OperationalRuntimeAuthorized=true` is
      refused unless the approval-evidence binding is satisfied; a test proves the refusal.
- [ ] BG-11 (DL-5; after OD-PVG-9 and the Control Tower K4 shared forwarder package plus the MOD-0021 owner contract) -
      No MOD-0230 mutation succeeds without its audit record written in the same unit
      of work; with the audit writer forced to fail, the result is not `Succeeded` and the store is unchanged (test);
      the ledger's known-debt list shrinks by the audited commands.
- [ ] BG-12 (DL-2) - Stale update at the store and application layer ("Validation Rules", update concurrency table):
      `IPvgIntakeDraftStore.ReplaceAsync` takes the expected version and its result separates not-found from version
      conflict; update, triage, and route map a conflict to their own reason code, never to `IntakeDraftNotFound`, and
      the store is unchanged; read results return the current version. Named tests in
      `tests/Diten.PvgService.RegPvBase.Tests` (names required): `ReplaceAsync_with_stale_version_returns_conflict_not_not_found`,
      `Update_with_stale_version_returns_version_conflict_and_leaves_store_unchanged`,
      `Triage_and_route_with_stale_version_return_version_conflict` (with allow-stub ports in the test only).
- [ ] BG-13 (DL-2) - No Mongo driver reference in PvgService under the build/test gate (existing architecture tests
      stay green).
- [ ] BG-14 (DL-7; OD-PVG-8 and OD-PVG-10 decided 2026-10-21) - The DataTable verifier shows no gap other than the
      recorded known deviation: the 13 items tied to bulk delete, export, import, select-all, and their wiring
      (reload-with-toast, clear-selection) (G-14, OD-PVG-10). The other five - data mode (`data-dt-data-mode="server"`
      on the table), `_TableSkeleton`, the `_Filter` and `_IndexL10n` partial includes, form view model - are fixed in
      DL-7. Open: the verifier still prints 18 gaps (2026-10-21).
- [ ] BG-15 (DL-7) - The seven `CaseIntakeTriageIndex.*.resx` files exist and carry the same key set.
- [ ] BG-16 (DL-8) - `ocelot.json` holds exactly the four MOD-0230 route entries and no archive, export, delete, or
      bulk-delete route.
- [ ] BG-17 (DL-1) - Every command in "Test Expectations" reports 0 failed and `known_failures.py check` exit 0.
- [ ] BG-18 (DL-5; after OD-PVG-9 and the Control Tower K4 shared forwarder package plus the MOD-0021 owner contract;
      added 2026-10-22, review 02 F-1) - Single-record read audit (AUD-001 §7, "Audited Events" read row): every
      `GetDraftByIdAsync` call that returns the record (`Succeeded`) writes exactly one `pvg.case-intake.viewed` record
      through the central path (`b`) carrying object type and id, actor, server-resolved tenant, correlation id, UTC
      time, and outcome, and no field value from the never-written list - test
      `GetById_success_writes_one_read_audit_record_without_field_values` (name required); with the read-audit writer
      forced to fail, the result is the behaviour fixed by `PVG-MOD0230-AuditEvent-v1` - test
      `GetById_when_read_audit_cannot_be_written_follows_the_MOD0021_contract` (name required). This item cannot be
      checked before that contract is signed. The deny-all permission gate does not satisfy it.
- [ ] BG-19 (DL-4; after DL-2 BG-12; does not wait for OD-PVG-2 / OD-PVG-3 - 409 is today's mapping for every
      non-success and also OD-PVG-3's target for a concurrency conflict; added 2026-10-22, review 02 F-3 and F-5) -
      API: (a) PUT, `/triage`, and `/route` bodies carry the expected version, GET item and list items return it, and a
      stale PUT returns 409 with the version-conflict reason code and leaves the store unchanged - test
      `Put_with_stale_version_returns_409_version_conflict`; (b) the list response carries a total count (all of the
      tenant's drafts) and a filtered count (after the status filter): with 101 drafts, `pageNumber=2&pageSize=100`
      returns 1 item, total 101, filtered 101 - test `List_with_101_drafts_returns_second_page_and_total_counts`; with a
      status filter the filtered count equals the matching drafts - test `List_with_status_filter_returns_filtered_count`
      (names required; tests in `Diten.PvgService.Api.Tests`). DL-4 owns the whole API-side count: the store count
      member, `PvgIntakeDraftQueryResult`, `ListDraftsAsync`, and the API response.
- [ ] BG-20 (DL-7; after DL-4 BG-19; added 2026-10-22, review 02 F-3 and F-5) - Screen, measured in
      `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js` (names required): (a) page parameters are passed - the
      request for page n carries `pageNumber=n` and the table's page size (at most 100, the proxy clamp) instead of the
      fixed `pageNumber=1` / `pageSize=100` - test `pvg list sends the requested page number and page size`; (b) with
      at least 101 records the next page is reachable - given a proxy response with 100 items and total 101, moving to
      page 2 requests `pageNumber=2` and renders the 101st record - test `pvg list reaches the 101st record on page 2`;
      (c) the totals are correct - the table's total and filtered counts come from the API's total and filtered counts,
      not from the number of rows received - test `pvg list shows the API total and filtered counts`; (d) the version
      read with the record is sent back on save, triage, and route, and a 409 version conflict shows the
      controlled-failure alert - test `pvg form sends the record version and shows a conflict as a controlled failure`.

**Operational runtime gate (all open - do not check without owner evidence):**

- [ ] MOD-0018 owner signs `PVG-MOD0230-RBAC-Contract v1`.
- [ ] MOD-0019 Data Masking & Row/Field Security ships and its owner signs `PVG-MOD0230-FieldSecurity-Contract v1`.
- [ ] MOD-0021 owner signs `PVG-MOD0230-AuditEvent-v1` and the audit-write path (BG-11) is in place.
- [ ] MOD-0023 Workflow/Inbox v1 ships and its owner signs `PVG-MOD0230-WorkflowTransitionGate-v1`.
- [ ] MOD-0031 Evidence Linking Service ships and its owner signs `PVG-MOD0230-EvidenceLink-v1`.
- [ ] The trace authority signs `PVG-MOD0230-TraceBundle-v1`; MOD-0041 / Ops signs `PVG-MOD0230-ObservabilityErrorModel-v1`.
- [ ] A retention / legal-hold owner is named and signs `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1` before archive/void is added.
- [ ] Port allocated in `AGENTS.md` §3.
- [ ] All non-production port adapters and the `Pvg:Runtime:UseNonProductionAdapters` switch are removed from the environment.
- [ ] MOD-0230 operational runtime authorization is granted and the runtime flag is bound to it.

## MOD-0230 Runtime Authorization Packet (Draft / Not Approved)

This packet records the minimum evidence required before MOD-0230 can enter **operational runtime**. It authorizes
nothing. Build/test work - including starting the host locally, the Gateway route family, and the tenant UI - is
authorized by "Build Authorization (Non-Production) - 2026-08-09", not by this packet. The "Allowed Paths" and
"Forbidden" lists below apply only to the first operational-runtime slice.

Current decision: **NO-GO for PVG operational runtime**. The only future runtime path that may be requested first is
MOD-0230 backend-only, fail-closed runtime. MOD-0231, MOD-0232, and MOD-0234 operational runtime remain blocked until
MOD-0230 runtime handoff evidence and their own downstream gates are approved.

### Current Handoff Evidence - `MOD0230HandoffReference v0.1` Build/Test

The current implemented handoff shape is **`MOD0230HandoffReference v0.1` for build/test only**. It is a narrow,
non-operational reference used to prove local class-library handoff shape and downstream fail-closed behavior. It is
not the future owner-approved operational handoff contract.

Implemented v0.1 fields:

| Field | Status | Notes |
|---|---|---|
| `IntakeDraftId` | Implemented in v0.1 | Local build/test intake draft reference only. |
| `IntakeNumber` | Implemented in v0.1 | Trace/display reference; not a client authority for canonical identity. |
| `ReceivedAtUtc` | Implemented in v0.1 | Timestamp handoff context only. |
| `TriageOutcomeCode` | Implemented in v0.1 | Triage outcome code reference only; workflow owner approval still required for operational routing. |
| `RouteTargetQueueCode` | Implemented in v0.1 | Queue code reference only; MOD-0023 queue/assignment authority still required. |
| `EvidenceLinkReferenceIds` | Implemented in v0.1 | Evidence link references only; MOD-0031 evidence completeness remains blocked. |

External server context, not client-supplied handoff fields:

- `TenantId` is server-resolved from tenant context. It must not be supplied by client payload, query string, or
  handoff message body as authoritative data.
- Correlation / trace context is server-resolved from Blueprint MOD-0040 / TRACE-BUNDLE behavior, including the
  approved correlation header path. It is not a client-owned handoff business field.

Future `MOD0230HandoffReference v1` operational handoff remains blocked until owner approval. The following planned
operational fields are **not produced by BE-01 or BE-02** and must not be treated as available downstream:

- intake/source context such as `IntakeChannel`, `SourceType`, `SourceReference`, and `ReporterType`
- restricted PHI/PII context such as `ReporterContactSummary`, `PatientSubjectCode`, `EventOnsetDate`,
  `AdverseEventNarrative`, and `SuspectProductText`
- regulated safety context such as `Seriousness`, `IntakePriority`, `TriageReason`, and workflow lifecycle metadata
- workflow instance / assignment metadata beyond the v0.1 `RouteTargetQueueCode` reference
- evidence completeness status or evidence-pack content beyond v0.1 reference IDs

Downstream modules may consume v0.1 only as build/test evidence. They must continue to block operational runtime
until the v1 owner-approved handoff contract, masking, workflow, evidence, audit, tenant, and trace proofs are closed.

### Required Approval Gates

All gates below must be owner-approved and evidence-backed before MOD-0230 operational runtime work starts:

- [ ] MOD-0230 operational runtime authorization explicitly granted by the user / PVG system owner / Enterprise Architecture.
- [ ] MOD-0018 RBAC owner approval for runtime permission enforcement, JWT validation, actor context, seed/grant ownership, tenant scope, and deny behavior.
- [ ] MOD-0019 FieldSecurity / masking owner approval for all MOD-0230 fields and every exposed surface.
- [ ] MOD-0021 AuditEvent owner approval for append/event shape, outage behavior, redaction, and correlation propagation.
- [ ] MOD-0023 Workflow/Inbox owner approval for gate-before-commit triage/route behavior.
- [ ] MOD-0031 EvidenceLink owner approval for required evidence links, completeness rules, outage behavior, and no fake packs.
- [ ] TRACE-BUNDLE / Blueprint MOD-0040 trace authority approval for canonical IDs, external-reference non-authority, and correlation.
- [ ] MOD-0041 / Ops observability and regulated error-model owner approval for telemetry/error redaction and outage behavior.
- [ ] Retention / legal-hold / archive-void decision approved, even if archive/void remain absent in the first runtime slice.
- [ ] Port for `Diten.PvgService` allocated in the `AGENTS.md` §3 table by Enterprise Architecture, and the local `web-verify` collision on 5011 resolved (OD-PVG-7).
- [ ] Service-local `appsettings*.json` policy approved.
- [ ] Startup fail-closed policy approved (OD-PVG-5) and the runtime flag bound to approval evidence (OD-PVG-6).
- [ ] No-PHI/PII logs, traces, metrics, audit metadata, validation errors, and regulated errors proof supplied.
- [ ] Tenant isolation proof supplied, including the Gateway-service context contract (OD-PVG-2).

### Allowed Paths After Explicit Runtime Approval

Only these paths may be considered for the first approved MOD-0230 operational runtime slice:

- `services/Diten.PvgService/src/Diten.PvgService.Api/**`
- `services/Diten.PvgService/src/Diten.PvgService.Application/RegPvBase/**`
- `services/Diten.PvgService/src/Diten.PvgService.Domain/RegPvBase/**`
- `services/Diten.PvgService/src/Diten.PvgService.Infrastructure/RegPvBase/**`
- `services/Diten.PvgService/tests/**`
- service-local solution/project files required to wire the approved backend runtime slice
- service-local `appsettings*.json` only when it contains no secrets, no PHI/PII, no partner credentials, and no permissive fallback

### Forbidden Until Separately Approved

For the operational runtime slice (build/test work under the build/test gate is unaffected):

- `frontend/**`
- `gateway/**`
- seeds, fixtures, sample PHI/PII, or sample regulated safety data
- jobs, collections, migrations, repositories, Mongo, or persistence paths unless specifically approved in the runtime packet
- partner integration
- AI behavior
- archive, void, export, delete, or bulk-delete
- MOD-0231, MOD-0232, or MOD-0234 operational runtime
- production use, supplier qualification, CSV validation, or deployment

### Validation Required Before Runtime Work Can Be Accepted

- `git diff --check`
- focused `Diten.PvgService` build and tests
- API startup fail-closed tests
- no client-supplied `TenantId` tests
- cross-tenant read/write denial tests
- missing RBAC, FieldSecurity, AuditEvent, WorkflowTransitionGate, EvidenceLink, trace/correlation, and error-model block tests
- no sensitive echo tests for results, logs, traces, metrics, validation errors, regulated errors, and audit metadata
- appsettings policy tests proving no permissive fallback and no production use of non-production adapters
- no forbidden path changes
- no archive, void, export, delete, or bulk-delete runtime surface

## Test Expectations

Slice 1 test expectations - **required before slice 1 is accepted**. Thresholds: 0 failed in every listed suite and
`known_failures.py check` exit 0 (there are no PVG rows in `tests/known-failures.tsv`, so any red fails).

| What | Command (repo root) | Threshold |
|---|---|---|
| API suite (CI suite `pvg-api`) | `dotnet test services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/Diten.PvgService.Api.Tests.csproj -c Debug --logger "trx;LogFileName=pvg-api.trx"` | 0 failed; baseline 36/36 (E2, 2026-10-20) |
| RegPvBase suite (CI suite `pvg-regpv-base`) | `dotnet test services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/Diten.PvgService.RegPvBase.Tests.csproj -c Debug --logger "trx;LogFileName=pvg-regpv-base.trx"` | 0 failed; baseline 88/88 |
| Other PVG suites sharing the host (`pvg-case-processing`, `pvg-meddra-coding`, `pvg-signal-management`) | `dotnet test` on each project under `services/Diten.PvgService/tests/` | 0 failed |
| Known failures | `python3 scripts/known_failures.py check <trx> --suite <suite>` for each TRX | exit 0 |
| UI test | `npx vitest run tests/pvg-case-intake-triage-ui.test.js` in `frontend/Diten.Web` | 0 failed |
| DataTable verifier | `python3 .antigravity/scripts/verify_datatable_page.py . --area Pharmacovigilance --module CaseIntakeTriage --reference compact --api-profile proxy --format gaps` | only recorded known deviations (BG-14) |
| DCP-002 identity | `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"` | exit 0 |
| Whitespace | `git diff --check` | clean |

Behaviour that the suites must cover (each with at least one named test):

- Tenant isolation: cross-tenant read and write return 404 with no data (BG-6); caller-supplied tenant / actor
  headers have no effect (BG-5).
- Authorization: 401 / 403 paths; permission before validation (BG-7); one key family (BG-4); controller
  `[HasPermission]` and UAS-001 (BG-2, BG-3).
- Per-field PHI/PII sensitivity, masking, row/field deny, and missing-policy fail-closed tests.
- Audit write failure blocks the mutation (BG-11); single-record read audit and its write-failure behaviour (BG-18);
  correlation/TRACE-BUNDLE, evidence-link, workflow/inbox failure-path tests.
- Raw PHI/PII/free text absent from logs, traces, metrics, audit payloads, validation errors, and regulated error
  responses.
- Startup fail-closed (BG-8) and configuration scans (BG-9, BG-10). The configuration key for scans is the code's
  `Pvg:Runtime:*` family.
- Gate-before-commit: no state-changing path commits without an `Allowed` transition result in the same correlation
  scope.
- Concurrency 409 (BG-12 store and application, BG-19 API, BG-20 screen).
- List paging beyond 100 records with correct total and filtered counts (BG-19 API, BG-20 screen).
- REG-PV-BASE port conformance C-01 ... C-17: the document `docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md`
  is committed, but its type names and configuration key are not in the code (G-12). **Until the type names are
  aligned with the code, or a C-id-to-test mapping is recorded, this criterion is unmapped and cannot be checked.**
- Gateway route smoke through `http://localhost:5000/api/pv-case-intake-triage` after any route change (E3 G-series).
- Browser smoke of the tenant UI (Diten.Web -> proxy -> Gateway -> service): not run yet; requires Auth, Mongo, and a
  tenant login.

## Ready-for-dev Checklist

- [x] Required governance files read.
- [x] DCP-002 preflight passed.
- [x] Pack status is `ready-for-dev` (2026-08-09) - **build/test gate only**.
- [ ] Version-bound approval row for the current pack version (CT-APR-01 / CT-APR-02) - none; independent reviews 01
      and 02 returned "UYGUN DEĞİL"; this revision (2026-10-22) needs a new review.
- [x] DCP-004 promoted to `approved` / `ready-for-execution` (2026-08-09).
- [x] W-3A0 scope per OD-2: W-3A0-Lite gates build/test; W-3A0-Full gates operational runtime.
- [ ] MOD-0018 RBAC / permission contract - **open**. Build gate substituted by `DenyAllPermissionGate` only; no JWT
      validation; header-supplied context (G-1, G-2). Corrected 2026-10-21 from "resolved against merged runtime".
- [~] MOD-0019 masking / row-field security - **ported**, not resolved. Build gate satisfied by `DenyAllFieldSecurityPolicy`; operational runtime blocked.
- [ ] MOD-0021 AuditEvent v1 - **open**; no audit write and no substitute (G-5). Corrected 2026-10-21 from "resolved".
- [~] MOD-0023 Workflow/Inbox v1 - **ported**, not resolved. Build gate satisfied by `DenyAllWorkflowTransitionGate`; operational runtime blocked.
- [~] MOD-0031 Evidence-Link - **ported**, not resolved. Build gate satisfied by `DenyAllEvidenceLinkPort`; operational runtime blocked.
- [ ] Blueprint MOD-0040 / TRACE-BUNDLE - **open**; local correlation header check only. Corrected 2026-10-21 from "resolved".
- [ ] MOD-0041 observability / regulated error model - **open** (G-6, G-8).
- [x] `service` resolved to `Diten.PvgService`.
- [ ] Port allocated in `AGENTS.md` §3 - **open** (OD-PVG-7: PVG side decided 2026-10-21; awaiting Enterprise
      Architecture); 5011 is a proposal.
- [x] `shell` resolved as `tenant`.
- [x] `entity_base` recorded as `EntityBase` for the dedicated PVG service boundary.
- [x] Create/edit user-entered fields defined and marked required/optional.
- [x] PHI/PII/sensitive-field matrix recorded for every intake field.
- [x] `form_field_count` recorded as `16`; `golden_reference` recorded as `compact`.
- [x] `data_mode` declared - `server` in the frontmatter (OD-PVG-8, decided 2026-10-21).
- [x] Validation rules recorded (max lengths, cross-field rules, planned index decisions, fail-closed behaviour).
- [x] Option-set ownership recorded for intake, source, reporter, seriousness, priority, triage outcome, and route queue.
- [ ] Permission-key family and actor/role matrix approved - **open** (OD-PVG-1: PVG side decided
      `pvg.case-intake-triage.*` 2026-10-21; MOD-0018 owner approval pending). Corrected 2026-10-21 from "approved".
- [ ] Backend File Convention matches Golden Reference - **no**; the measured deviation (G-11) is a documented
      deviation (technical debt) for slice 1 by owner decision OD-PVG-4 (2026-10-21).
- [ ] Audited Events table covers every write command with a path - table present; no path exists (OD-PVG-9: PVG
      side decided 2026-10-21; awaiting the K4 shared forwarder package and the MOD-0021 owner contract). The
      single-record read row (AUD-001 §7) was added 2026-10-22 under the same dependency (BG-18).
- [n/a] Retention, archive/void activation, and legal-hold policy - **removed from slice 1 scope**; required before archive/void is ever added.
- [x] Build/buy/partner integration boundary recorded as hybrid, partner-aware internal control wrapper.
- [x] Gateway route family defined per NET-001 and assigned to integration-agent.
- [ ] Test expectations concrete - commands and thresholds listed; C-01 ... C-17 unmapped (G-12).
- [ ] Page help texts (UXH-001) - not measured in this revision.

## Implementation Notes

- **2026-08-09 promotion.** DCP-004 is `approved`; this pack is `ready-for-dev` for the **build/test gate only**.
  The operational runtime gate remains closed. Do not read `ready-for-dev` as production authorization.
- **2026-10-21 revision after independent review 01.** Findings F-1 ... F-7 and the Control Tower comparison note
  items are addressed in this text: RBAC, AuditEvent, TRACE-BUNDLE, and observability contracts returned to open;
  self-approval entries withdrawn as owner approvals; permission-key mismatch, controller scope, startup
  fail-closed, configuration key, gate boundary, port source, error model, check order, persistence, file layout, and
  conformance-suite mapping recorded as facts with open decisions; delivery slices rebuilt.
- **2026-10-21 owner decisions OD-PVG-1 ... OD-PVG-10.** Recorded in "Open Decisions (Owner Decision Pending)" from
  `docs/records/decisions/2026-10/pvg-mod0230-open-decisions-01.md@4fe62fdc3`. Applied in this revision: frontmatter
  `data_mode: server` (OD-PVG-8), the documented backend deviation (OD-PVG-4), the binding startup form (OD-PVG-5),
  the verifier known deviation (OD-PVG-10). The six rows that wait for another owner change no code target until that
  approval is recorded.
- **2026-10-22 revision after independent review 02.** Review 02 of pack version `b48500cf` returned "UYGUN DEĞİL"
  with F-1 ... F-5 (`docs/records/reviews/MOD-0230/2026-10-21-b48500cf.md`; Control Tower comparison note
  `docs/records/reviews/MOD-0230/2026-10-21-b48500cf-ct-karsilastirma-02.md`). Addressed in this text: single-record
  read audit row bound to OD-PVG-9 with BG-18 (F-1); BG-4 scan narrowed to the MOD-0230 surface (F-2); stale-update
  ownership across DL-2 / DL-4 / DL-7 with BG-12, BG-19, BG-20 (F-3); "today" vs "proposed, not in code" for the route
  target and evidence `Pending` wording (F-4); list paging gap G-16 with BG-19 and BG-20 (F-5); the comparison note's
  §3 items 2-4 (read row tied to OD-PVG-9, G-16, REQ-PVG-CRM-01 reference in the role matrix). No decision, approval, or
  frontmatter field changed.
- **Platform.Common is not consumed.** The 2026-08-09 text said several contracts were "consumed directly from merged
  `Diten.Platform.Common`". No PvgService project references `Diten.Platform.Common`; authorization, correlation,
  tenancy, and observability are PVG-local or absent.
- **W-3A0-Lite ports.** MOD-0019, MOD-0023, and MOD-0031 do not exist as runtime. MOD-0230 consumes them through
  three PVG-owned ports with deny-by-default adapters, plus a PVG-owned `IPvgPermissionGate`. A port is an interface
  plus a deny default and nothing else - it stores no policy data, hosts no workflow engine, and persists no evidence.
  If a port starts making regulated decisions on its own authority it has become an unauthorized reimplementation and
  must be rejected in review.
- **Port-contract document.** `docs/reference/architecture/pvg-reg-pv-base-port-contracts-v1.md` is committed. Its
  type names, project path (`tests/Diten.Pvg.Application.Tests/RegPvBase/`), and configuration key differ from the
  code (G-12). DCP-004 §10 still says this material is "pending ... not normative until committed"; DCP-004 is not
  changed by this pack.
- **Registry defect - MOD-0019.** As of 2026-08-09, `MOD-0019 Data Masking & Row/Field Security` is
  Blueprint-canonical (W-3, Build, `SEC-DATA-BUNDLE`) but had **no row** in
  `execution/registries/module-id-registry.md`. A row has been added, but MOD-0019 still has no owner and no module
  pack, which is why `PVG-MOD0230-FieldSecurity-Contract v1` could never have been signed.
- **Registry defect - MOD-0040.** The repo registry row for `MOD-0040` is a **deprecated alias to MOD-0288**
  (Organization, Person & Position Directory). The Blueprint MOD-0040 is `Canonical ID & Correlation Standard`.
  This pack means the Blueprint one. Do not resolve `MOD-0040` through the registry row.
- **Route correction.** The earlier `/api/v1/pharmacovigilance/case-intake-triage` **upstream** proposal violated
  NET-001. Corrected to upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage`.
- **Port.** The 2026-08-09 text called 5011 "verified free" and pointed to `.antigravity/rules/ports.md`. Both are
  withdrawn: the port source is `AGENTS.md` §3, where 5011 awaits an Enterprise Architecture decision, and
  `.claude/launch.json` uses 5011 for `web-verify` (G-13).
- **Inspection note corrections.** The PVG-M1-02 inspection note says 4 Gateway routes carry `AuthenticationOptions`
  (actual: 2) and that no `pv.*` key exists in the repo (the service checks `pvg.mod0230.intake.*`). This pack uses
  the corrected facts; correcting the note itself is Control Tower work.
- **Domain-config wording.** Domain-config says MOD-0231 / MOD-0232 / MOD-0234 have no API host; the MOD-0230 host
  registers their application services as composition-only wiring ("Runtime Constraints"). This pack is the higher
  authority; the wording difference is recorded only.
- Frontmatter decisions: `shell: tenant`, `entity_base: EntityBase`, `form_field_count: 16`,
  `golden_reference: compact`, `data_mode: server` (OD-PVG-8), `service: Diten.PvgService`, `service_port: 5011` (proposal; not allocated).
- Route/API profile: tenant MVC route `/Pharmacovigilance/CaseIntakeTriage`, view root
  `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**`, same-origin MVC proxy, Gateway family
  `/api/pv-case-intake-triage` upstream to `/api/v1/pv-case-intake-triage` downstream.
- Option-set ownership reconciled 2026-08-04: static PVG controlled lookups cover IntakeChannel, SourceType,
  ReporterType, and Seriousness; IntakePriority, TriageOutcome, and RouteTargetQueue require workflow/reference
  ownership contracts as recorded in Validation Rules.
- Delete policy reconciled 2026-08-04: no hard delete, no bulk delete, no normal user delete. Archive/void only
  after retention/legal-hold approval, with reason, actor, UTC timestamp, correlation id, and AuditEvent; blocked
  under legal hold.
- MOD-0230 is Blueprint W-3. MOD-0231/MOD-0232/MOD-0234 urgent W-3 slice handling is governed by DCP-004 and does not change this pack's canonical identity.
- REG-PV-BASE is the minimum integration contract for this module: SSO+RBAC/ABAC, PHI/PII masking hooks, AuditEvent v1, Workflow/Inbox v1, Evidence-Link, OTel, Correlation-ID, and Error Model.
- MOD-0004 and MOD-0063 remain downstream DCP-004 / MOD-0234-facing gates unless MOD-0230 explicitly emits
  signal analytics, semantic metric IDs, or data-product outputs in a later approved scope.
- Governed-AI / High-risk Blueprint markers are recorded as blockers for AI behavior, not implementation permission.

## Follow-up Items

MOD-0230's own follow-ups (other owners' work is listed under "Teslim dilimleri" as outside every slice):

- Close build-gate gaps G-1 ... G-16 through slices DL-2 ... DL-8.
- Record the owner answers to OD-PVG-1 ... OD-PVG-10 in this pack - done in this revision (2026-10-21). Still open:
  the other owners' approvals named for OD-PVG-1, 2, 3, 6, 7, and 9.
- Align the port-contract document's type names and configuration key with the code, or record a C-id-to-test map (G-12).
- After real MOD-0019, MOD-0023, and MOD-0031 ship: replace the three non-production adapters and remove the
  `Pvg:Runtime:UseNonProductionAdapters` switch.
- Declare `data_mode` in the frontmatter (OD-PVG-8) - done in this revision (`data_mode: server`).

Out of slice 1, requiring their own approval:

- Archive / void surfaces, export surfaces, background jobs, seed data.
- Any AI-assisted intake, extraction, summarization, or routing behaviour.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | ortak çerçeve ve kapsam | Build Authorization (Non-Production) - 2026-08-09, Build-Gate Gaps (Measured 2026-10-21), Open Decisions (Owner Decision Pending), Module Summary, Ownership and Boundaries, Repo Scope, Protected Paths, Dependencies, Required Interface Contracts for Build/Test and Operational Runtime, MOD-0230 Owner-Approval Evidence Intake Template, External Owner-Evidence Submission Checklist, MOD-0040 / MOD-0288 Identity Clarification, Runtime Constraints, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items |
| DL-2 | sözleşme alanları ve doğrulama | Owned Objects, Entity Fields, Validation Rules, Option-Set Ownership, Backend File Convention |
| DL-3 | host açılışı ve yapılandırma | Service Port And Appsettings Policy, Startup Fail-Closed Requirements |
| DL-4 | kimlik doğrulama, kiracı bağlamı ve hata modeli | Tenant Isolation Rules, Failure Path to Verify, PHI/PII And Regulated Safety Controls |
| DL-5 | denetim yazımı | Audited Events, Audit / Workflow / Evidence / Trace / Error Requirements |
| DL-6 | izin anahtarı sözleşmesi | Authorization Convention |
| DL-7 | tenant UI ve MVC proxy | Layout & Shell Contract, Frontend File Contract |
| DL-8 | Gateway route ailesi | Gateway / API Routing Decision |
| DL-9 | operasyonel runtime kapısı (kapalı) | MOD-0230 Runtime Authorization Packet (Draft / Not Approved), Current Handoff Evidence - `MOD0230HandoffReference v0.1` Build/Test, Required Approval Gates, Allowed Paths After Explicit Runtime Approval, Forbidden Until Separately Approved, Validation Required Before Runtime Work Can Be Accepted |

Dilim ayrıntıları (sahip olunan yollar · önkoşul · kapsam dışı · ölçülebilir bitiş koşulu):

- **DL-1** — Yollar: yalnız bu paket dosyası; kod yok. Önkoşul: yok.
  Kapsam dışı: her kod yolu, WP dosyaları, DCP-004. Bitiş: OD-PVG-1…10 için sahip yanıtı pakete yazılmış ya da
  "açık" kalmış; bağımsız inceleme açık engelsiz; `scripts/pack_approval.py check` çıkış 0; BG-17.
- **DL-2** — Yollar: `services/Diten.PvgService/src/Diten.PvgService.Domain/RegPvBase/**`, `Application/RegPvBase/`
  içindeki komut, sorgu, istek ve sonuç tipleri (`PvgIntakeDraftSummary.cs` dahil), `PvgApplicationReasonCodes.cs` ve
  `PvgIntakeDraftValidator.cs`, `Application/RegPvBase/IPvgIntakeDraftStore.cs` (`ReplaceAsync` beklenen sürümü alır,
  sonuç bulunamadı ile sürüm çakışmasını ayırır), `PvgIntakeDraftApplicationService.cs` içindeki üç `ReplaceAsync` çağrısı
  (satır 157, 239, 321) ve sonuçlarının yorumu, `Infrastructure/RegPvBase/InMemoryPvgIntakeDraftRepository.cs`,
  `PvgIntakeDraftIndexCatalog.cs`, `tests/Diten.PvgService.RegPvBase.Tests/**`. Önkoşul: OD-PVG-4 kapandı (2026-10-21; belgelenmiş sapma);
  başka dilim önkoşulu yok — DL-4 (BG-19) ve DL-7 (BG-20) sürüm taşımasında DL-2'yi bekler.
  Kapsam dışı: Mongo koleksiyonu, indeks, migration; komut adlarının değiştirilmesi; API ve MVC/JS sürüm taşıması
  (DL-4, DL-7); liste toplam sayısı (DL-4). Bitiş: BG-12, BG-13; `pvg-regpv-base` 0 kırmızı.
- **DL-3** — Yollar: `src/Diten.PvgService.Api/Program.cs`, `PvgServiceApiHost.cs`, `tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs`.
  Önkoşul: OD-PVG-5 kapandı (2026-10-21); BG-10 için OD-PVG-6 (PVG tarafı karar 2026-10-21; onay satırına bağlama biçimi
  Enterprise Architecture onayını bekliyor). Kapsam dışı: port tahsisi (OD-PVG-7), üretim açılışı, sırlı appsettings.
  Bitiş: BG-8, BG-9, BG-10 yeşil; bir kayıt silinince adı belli test kırmızı (sabotaj kanıtı).
- **DL-4** — Yollar: `src/Diten.PvgService.Api/PvgCaseIntakeTriageEndpoints.cs` (istek ve yanıt kayıtlarında sürüm
  alanı, liste yanıtında toplam ve süzülmüş sayı dahil), `Program.cs` içindeki kimlik doğrulama kaydı
  (DL-3 ile aynı dosya: DL-3 önce ya da aynı WP), `PvgIntakeDraftApplicationService.cs` içindeki kontrol sırası ve
  `ListDraftsAsync`, liste sayımı için `IPvgIntakeDraftStore.cs` / `InMemoryPvgIntakeDraftRepository.cs` sayım üyesi ve
  `PvgIntakeDraftQueryResult.cs` (DL-2 ile aynı dosyalar: DL-2 önce), API testleri. API tarafı toplam sayının tamamı bu dilimdedir.
  Önkoşul: BG-5 için OD-PVG-2 (PVG tarafı karar 2026-10-21; Enterprise Architecture ve MOD-0018 claim adları onayı bekleniyor),
  BG-6 için OD-PVG-3 (PVG tarafı karar 2026-10-21; düzenlenmiş hata zarfı MOD-0041 / Ops onayını bekliyor); BG-19 için
  DL-2 (BG-12) — BG-19 OD-PVG-2 / OD-PVG-3'ü beklemez. Kapsam dışı: Gateway ara katmanı, izin tanımı/grant, MOD-0018 sözleşmesi;
  MVC/JS sayfalama ve sürüm taşıması (DL-7). Bitiş: BG-5, BG-6, BG-7, BG-19; `pvg-api` 0 kırmızı.
- **DL-5** — Yollar: `PvgIntakeDraftApplicationService.cs` içindeki denetim çağrıları (dört yazma komutu ve
  `GetDraftByIdAsync` okuma denetimi), `Application/RegPvBase/` altında denetim yazıcı portu, ilgili testler, defterde yalnız
  borç satırı silme. Önkoşul: OD-PVG-9 (PVG tarafı karar 2026-10-21;
  K4 ortak iletici paketi (Control Tower) + MOD-0021 sahip sözleşmesi bekleniyor); okuma satırı aynı bağımlılığa bağlıdır, ayrı karar açılmadı.
  Kapsam dışı: ileticinin ya da merkezi günlüğün yapımı; liste okumasının denetimi (AUD-001 §7 sınıfı değil).
  Bitiş: BG-11, BG-18 (tekil açılış okuma denetimi); `AuditTrailStandardTests` yeşil ve borç listesi küçülmüş.
- **DL-6** — Yollar: `PvgIntakeDraftApplicationService.cs` satır 7–11'deki izin sabitleri,
  `tests/Diten.PvgService.RegPvBase.Tests/PvgApplicationContractShapeTests.cs`. Önkoşul: OD-PVG-1 (PVG tarafı karar 2026-10-21,
  hedef `pvg.case-intake-triage.*`; MOD-0018 / AuthService katalog-grant ve `pvg.*` ad alanı onayı bekleniyor).
  Kapsam dışı: katalog, seed, grant (MOD-0018 / AuthService); denetleyici öznitelikleri (DL-7); MOD-0231'in
  `pvg.mod0231.case-processing.*` anahtarları (MOD-0231 paketinin konusu, XMC-001). Bitiş: BG-4 (MOD-0230 yüzeyine daraltılmış tarama).
- **DL-7** — Yollar: `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs` (form girdilerinde
  sürüm alanı, liste vekilinde sayfa parametreleri dahil), görünüm kökü, JS (`index.js` `buildListUrl` sayfalaması,
  `form.js` / `details.js` sürüm taşıması dahil), resx,
  `Models/CaseIntakeTriage/**`, `frontend/Diten.Web/tests/pvg-case-intake-triage-ui.test.js`. Önkoşul: DL-6; OD-PVG-8 kapandı (2026-10-21);
  OD-PVG-10 kapandı (2026-10-21); BG-20 için DL-4 (BG-19: API'de sürüm ve toplam/süzülmüş sayı alanları).
  Kapsam dışı: `_Layout.cshtml`, ortak partial'lar, menü/manifest (WP-0230-08 blocked); servis yolları (API tarafı toplam sayı DL-4).
  Bitiş: BG-1, BG-2, BG-3, BG-14, BG-15, BG-20; vitest 0 kırmızı.
  Bilinen sapma (OD-PVG-10): toplu silme, dışa aktarma, içe aktarma, tümünü seç ve bağlantıları (reload-with-toast,
  clear-selection) - doğrulayıcının 13 maddesi; kalan beş madde (veri modu, `_TableSkeleton`, `_Filter` ve `_IndexL10n`
  partial'ları, form ViewModel'i) DL-7'de düzeltilir.
- **DL-8** — Yollar: `gateway/Diten.ApiGateway/ocelot.json` içindeki dört MOD-0230 route girdisi (yalnız integration-agent).
  Önkoşul: bugünkü durum için yok. Kapsam dışı: Gateway ara katmanı ve diğer route'lar.
  Bitiş: BG-16; Gateway üzerinden smoke çağrısı servisin sonucunu döndürür.
- **DL-9** — Yollar: operasyonel runtime paketi onaylanana kadar yok. Önkoşul: "Required Approval Gates"in tamamı.
  Kapsam dışı: bugün her şey; hiçbir WP bu dilimi şimdi alamaz. Bitiş: operasyonel runtime yetki satırı sahip kanıtıyla onaylı.

Başka sahiplerin işi (XMC-001) — hiçbir dilimde değil; bu paket bunları yapmaz, yalnız bekler:

- MOD-0019 sahibi (platform-shared-services): modül paketi, sahip ve runtime; 16 alanlı hassasiyet matrisi ilk tüketici gereksinimi olarak verilir.
- MOD-0023 sahibi: triage durum kümesi ve route hedefi gereksinimi.
- MOD-0031 sahibi: kanıt tamlığı gereksinimi.
- MOD-0018 sahibi / AuthService: seçilen anahtar ailesinin katalog ve grant'ı; `pvg.*` ad alanının PKS-001 §4 tablosuna eklenmesi.
- MOD-0021 sahibi ve Control Tower (AUD-001 K4 ortak iletici paketi): denetim yolu.
- MOD-0041 / Ops ve iz otoritesi (Enterprise Architecture): hata modeli, gözlemlenebilirlik ve TRACE-BUNDLE sözleşmeleri.
- Enterprise Architecture: `AGENTS.md` §3 port tahsisi; depo sahibi: `.claude/launch.json` `web-verify` portu.
- Uyum / yasal saklama sahibi: arşiv/iptal eklenmeden önce saklama politikası.
- Portföy sahibi: `execution/portfolio/master-development-plan.md` ve teslim panosuna MOD-0230 satırları.
- Control Tower: WP-0230-03 başlığı ile WP-0230-07 denetleyici adı ve anahtar ailesinin bu paketle uzlaştırılması; PVG kuyruğundaki sahip alanının rol etiketiyle hizalanması; PVG-M1-02 inceleme notundaki iki yanlışın düzeltilmesi; WP-0230-08 manifest kararı.
