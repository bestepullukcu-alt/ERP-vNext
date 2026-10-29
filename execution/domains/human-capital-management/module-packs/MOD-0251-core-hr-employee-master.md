---
id: MOD-0251
name: Core HR / Employee Master
domain: human-capital-management
service: Diten.HcmService
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: approved
owner: ali.tufanoglu
branch: feature/hr/platform-backbone-hr-source-readiness-new
started: 2026-06-17
target: 2026-06-17
form_field_count: 18
---

# MOD-0251 - Core HR / Employee Master

> **Revision 5 (draft, 2026-10-29) - not approved.** Changes: (1) front matter: the second approval field
> `approval_status` (the conditional approval of the P2 draft / reference-validation slice) is removed, because the
> dispatch gate (`scripts/prompt_gate.py`, CT-FIX-04) does not dispatch a pack whose `approval_status` is not
> `approved`; (2) this note; (3) the one acceptance line that stated that field as a current fact; (4) a "Revision 5
> (testable)" acceptance item and Revision 5 items in the "Ready-for-dev Checklist"; (5) short Revision 5 markers on
> four current-scope sentences ("Module Summary", "Authoritative Baseline After Containment", "Approval Readiness",
> "Teslim dilimleri"). The rest of the Revision 4 body is unchanged.
>
> **What an approval of this version makes dispatchable (pre-review F-27).** Without `approval_status` the pack has no
> machine-readable scope limit: an approval row binds the whole file by its sha, and the gate checks a prompt's
> "Kapsanan dilim" only against the rows of "Teslim dilimleri" (CT-APR-02), not the slice's prerequisite. The limit is
> this rule: a slice DL-1 to DL-7 is dispatchable only when its own prerequisite in "Slice details" is met and
> recorded - DL-1: none; DL-2: the recorded owner decision on permission breadth (F-13); DL-3: the owner decision F-5;
> DL-4 to DL-7: the prerequisites their "Slice details" entry names (earlier slices accepted, contracts, decisions) and
> every decision of "Open Decisions (Owner)" whose "Blocks" column names that slice. A slice whose prerequisite is not
> recorded is not dispatchable even with the approval row; the dispatching CT checks the record and the prompt cites it.
>
> **How older statements are read.** Sentences that say the approved scope is only the P2 draft / reference-validation
> slice, that nothing added is approved or executable, that the pack is conditionally approved, or that the front
> matter carries `approval_status` (the Revision 2, 3 and 4 notes below, "Revision 2 Review Reconciliation",
> "Conditional Promotion Decision", "Approval Readiness") describe the scope of Revisions 1 to 4. They are kept as
> history and this note supersedes them. The conditional P2 approval stays as history in the body text. Like Revision
> 4, this revision needs its own independent review of this exact version and an approval row.

> **Revision 4 (draft, 2026-10-29) - not approved.** Draft revision written under the owner word `ayri-dilim`
> (2026-10-29). It follows independent review 03 (`docs/records/reviews/MOD-0251/2026-10-29-03d9e80f.md`, verdict
> UYGUN DEĞİL, open F-26) and its CT comparison (`docs/records/audits/2026-10/hcm-emp-03-review-03/CT-COMPARISON.md`,
> option 1 "Ayrı dilim", chosen by the owner). The draft-write central audit (GWT-A12) moves out of DL-2 into its own
> slice DL-7 "Draft-write central audit (HCM → MOD-0021 via shared forwarder)" at level E5 (SOP §23.1, cross-module
> seam); DL-2 keeps its code fixes at E4. The approved runtime scope is still **only** the P2 draft/reference-validation
> slice; nothing added here is approved or executable. Approval of this revision needs a new independent review of this
> exact version and an approval row. Revision 2 and 3 text below is kept; where it disagrees with a "Revision 4" note,
> the Revision 4 note is the current statement. Interim checks (subagent, not official):
> `docs/records/audits/2026-10/hcm-emp-03-interim-01/INTERIM-REVIEW-01.md`, labels AK-27…AK-50 ("ara kontrol"); they do
> not use the official review numbering, which continues at F-27 in review 04. Official findings F-1…F-26 keep their labels.

> **Revision 3 (draft, 2026-10-29) - not approved.** Draft revision written under the owner word `kayit-ve-revizyon3`
> (2026-10-29). It follows independent review 02 (`docs/records/reviews/MOD-0251/2026-10-29-47968562.md`, verdict
> UYGUN DEĞİL, open F-18 to F-25) and its CT comparison (`docs/records/audits/2026-10/hcm-emp-03-review-02/CT-COMPARISON.md`).
> It adds `## Audited Events` (F-18), front-matter `data_mode: server` (F-19), detail-read acceptance in DL-3 (F-20),
> the DL-6 → DL-5 dependency (F-21), draft-write central audit in DL-2 (F-22), one meaning for GWT-B07 and the D03 test
> (F-23), the split MOD-0299 checklist item (F-24) and the DL-2 permission-breadth decision record (F-25). The approved
> runtime scope is still **only** the P2 draft/reference-validation slice; nothing added here is approved or executable.
> Approval of this revision needs a new independent review of this exact version and an approval row. Revision 2 text
> below is kept; where it disagrees with a "Revision 3" note, the Revision 3 note is the current statement.

> **Revision 2 (draft, 2026-10-27) - not approved.** Draft revision written under the owner word `paket-revizyon`
> (2026-10-27). It follows independent review 01 (`docs/records/reviews/MOD-0251/2026-10-27-edb2c321.md`, verdict
> UYGUN DEĞİL, 10 blockers) and its CT comparison (`docs/records/audits/2026-10/hcm-emp-03-review-01/CT-COMPARISON.md`).
> The approved runtime scope is still **only** the P2 draft/reference-validation slice; the front-matter `status` and
> `approval_status` describe that earlier conditional approval and are unchanged. Nothing this revision adds (slices
> DL-2 to DL-6, new fields, events, open decisions) is approved or executable. Approval of this revision needs a new
> independent review of this exact version (SOP §20.4) and an approval row (`scripts/pack_approval.py`). Sections written
> before this revision are kept as history; where they disagree with "Revision 2 Review Reconciliation" below, that
> section is the current statement.

## Blueprint Alignment

| Blueprint Field | Value |
|---|---|
| Domain / Landscape | 4) Enterprise Application Ecosystem |
| Suite / Platform | Human Capital Management Foundation |
| Capability Group | Core HR Master |
| Wave | W-1 |
| Placement | Domain App (HCM Foundation) |
| Deployment Unit / Product | HCM Foundation App |
| Build / Buy / Partner | Build |
| SLO Tier | Tier 2 |
| L1 Queue | Service Desk |
| L2 Owning Ops | People Ops / IAM Ops |
| L3 Engineering | HCM Product Engineering |
| Implementation Phase | HR Foundation MVP |
| Minimum Integration Contract Bundle | HCM-CORE-BUNDLE |
| SoR Applicability | Y |
| SoR Notes | Internal HCM master with native HCM ownership. |

Identity gate: `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0251 --name "Core HR / Employee Master"` returns `OK  MOD-0251: proven against Blueprint/registry.` Registry collision check is clear because no active registry row maps `MOD-0251` to a different capability.

## Module Summary

`MOD-0251 Core HR / Employee Master` is the internal authoritative employee and employment master for the HCM Foundation MVP. It owns employee legal profile, employment record, employment status, and the employee-side job assignment / employment assignment snapshot needed by downstream HCM modules.

This pack was conditionally approved only for the reduced draft/reference-validation slice *(Revisions 1 to 4; Revision 5: the scope statement of the Revision 5 note at the top replaces this paragraph's approval and authorization sentences)*. That P2 slice is implemented in code for create draft, save/update with ETag, reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. The earlier "browser-validated" claim is narrative; no recorded evidence in the repository refs examined (review 01, F-7). Review 01 also found code defects inside that slice (see "Known Defects in the Approved P2 Slice (Review 01)"). Employee list/detail read APIs, pages and a menu entry exist in code outside the approved scope (review 01, F-5); their existence is not approval. The HCM service scaffold exists under `services/Diten.HcmService`, but this pack does not authorize submit/approval/activation, full MOD-0251 lifecycle implementation, employee registry/detail lifecycle surfaces, evidence upload/link, export/status/Data Quality Queue, government identifier capture/tokenization, or employee Active-state APIs.

## Authoritative Baseline After Containment

Prompt ID: `MOD0251-SPEC-PACK-AUTHORITY-ALIGN-M1`.

Authority decision:

- Current active spec: `docs/reference/architecture/MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md` (moved from `docs/specs/`; Revision 2 path correction, review 01 F-17).
- Active internal spec version: `v1.10 P2 runtime smoke closure status`, including the v1.9 registry read-only scope contract for later `MOD0251-P4-REGISTRY-READ-M1`.
- Standalone v1.4 spec status: not present as a separate governing file in the current repository; v1.4 is superseded lineage recorded in the active spec changelog.
- Implementation evidence named earlier: `docs/records/acceptance-reports/MOD-0251-scope-containment-rework-2026-06-21.md` - **missing**: not present in any repository ref examined by review 01 (F-7); the name appears only in a commit message. No acceptance record for P2 exists in the repository today.

Current approved runtime scope remains the contained P2 draft/reference-validation baseline *(Revisions 1 to 4; Revision 5: what an approval of this version makes dispatchable is stated in the Revision 5 note)*:

- Draft create.
- Draft save/update with ETag.
- Draft reload.
- Person, organization-unit, position, and legal-entity reference validation.
- Non-submit review/readiness state.

Registry/detail scope decision:

- Employee Registry is not a P2 support surface.
- Employee Detail is not a P2 support surface.
- P2 runtime smoke is limited to the Create Employee Draft surface and draft API/proxy flow.
- Active employee records must not be used as P2 evidence.
- Draft-only records must not be surfaced through Employee Registry in P2.
- Registry read-only behavior moves to a later approved registry sequence.
- Detail read-only behavior moves to a later approved detail sequence.

Containment state:

- Submit and workflow-decision routes are blocked by design with controlled failure responses until later lifecycle authorization.
- MOD-0023 workflow start is not runtime-enabled for MOD-0251.
- Active employee materialization is not runtime-enabled.
- `employee.activated` is not emitted, and `employee.created.v1` is not implemented.
- Detail status/evidence/audit placeholder UI and registry export affordance are removed from the contained baseline.
- Gateway remains unchanged; backend-level route containment is the current control. *(Revision 2 correction, review 01 F-6: Gateway already carries four HCM routes, including `/api/v1/hcm/employees` GET and `/api/v1/hcm/employees/{everything}` GET/POST/PATCH/DELETE, `gateway/Diten.ApiGateway/ocelot.json:2788-2847`; backend containment is still the control for submit/workflow-decisions.)*
- Evidence/status/export/data-quality and government identifier capture remain deferred.

## Revision 2 Review Reconciliation

This section is the current statement of code and evidence state after review 01. It grants nothing; the approved
runtime scope stays the P2 draft/reference-validation slice. Sources: review 01 findings
(`docs/records/reviews/MOD-0251/2026-10-27-edb2c321.md`), CT comparison
(`docs/records/audits/2026-10/hcm-emp-03-review-01/CT-COMPARISON.md`), inspection note
(`docs/records/audits/2026-10/hcm-emp-02-inspection-01/INSPECTION-NOTE.md`), discovery record
(`docs/records/audits/2026-10/hcm-employee-discovery-01/RECORD.md`).

### Corrected Current State

- **P2 evidence (F-7):** the P2 browser runtime, authenticated replay and "PASS" closure statements in this pack are
  narrative; no recorded evidence in the repository refs examined (review 01, F-7). The named acceptance report is
  missing. P2 closure must be re-proven at the DL-2 evidence level against this pack version. *(Revision 4, F-26: P2
  closure needs both DL-2 (E4) and DL-7 (E5, draft-write central audit, GWT-A12).)*
- **Build and tests (2026-10-27):** HCM solution build 0 warnings / 0 errors; `Diten.HcmService.Application.Tests`
  holds 52 test methods / 60 cases (2026-10-27 run 60/60, build 0 warnings). That run is E2 (build/test); TRX was not
  kept; no runtime (API/UI) test was run. Earlier counts in this pack ("5 passed", "Vitest 3", "Vitest 13") are other
  evidence periods and are historical. Frontend: three HCM Vitest files (`frontend/Diten.Web/tests/hcm-employee-*.test.js`,
  15 `it` cases by static count) were not run on 2026-10-27.
- **Permission seed (F-13):** implemented, not pending - `services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs:33-55`
  seeds all 14 `mod0251.*` keys (Admin gets all; Viewer gets search/view/view_status_history). This is wider than P2
  (submit, approve, export, change_status are seeded). Seed code does not prove the keys are applied to a database or
  that those actions are open. Keep or narrow: open decision.
- **Audit (F-4):** draft writes use a log-only `DraftAuditService`; `GovernedHcmAuditAppendClient` is registered but no
  production handler calls it; actor and correlation are sent as null. GWT-A12 is not met.
  *Revision 3 (review 02 F-22):* the central audit of the existing draft writes moves to DL-2. Target is path `b`
  through the shared forwarder `Diten.BuildingBlocks.CentralAudit` (on `main`: 528cd415d, 2026-10-14, BL-501; last
  111c60c46, 2026-10-23), used today by InventoryService and SupplyChainService (`AddCentralAudit<…>(…, RequireEnlistment
  = true)` in their `Program.cs`). `Diten.HcmService` does not register it and has no Mongo transaction today; the HCM
  ledger (`tests/architecture/audit-ledger/Diten.HcmService.md`) lists its trail as `aday` and all eight commands as
  known debt. Whether AUD-001 K4 is lifted for HCM is an open decision (AE-1). See "Audited Events".
  *Revision 4 (review 03 F-26):* this work leaves DL-2 for its own slice DL-7 (E5): HCM → MOD-0021 Audit Trail through
  the shared forwarder is a cross-module seam (SOP §23.1), and a local outbox row does not prove delivery to the
  Platform central log (`PlatformCentralAuditClient.cs:49-60`: `Retry` when not configured, `Rejected` for an unknown
  category / operation / actor type).
- **Gateway (F-6, F-17):** four HCM routes exist (`ocelot.json:2788-2847`). The general `/employees/{everything}` route
  passes GET/POST/PATCH/DELETE. A broad route does not mean those lifecycle operations exist. Gateway changes are
  `integration-agent` only.
- **List/detail UI and API (F-5, F-17):** `GET /api/v1/hcm/employees` and `GET /api/v1/hcm/employees/{id}`
  (`services/Diten.HcmService/src/Diten.HcmService.Api/Controllers/Hcm/EmployeesController.cs:24,55`), the
  `/HCM/Employees` and detail pages (`frontend/Diten.Web/Controllers/HCM/EmployeesController.cs:36,44`) and a menu
  manifest entry (`services/Diten.Platform/src/Diten.Platform.Application/Features/HumanCapital/SelfRegistration/HcmEmployeeMasterManifestProvider.cs:36`)
  exist outside the approved scope. Decision in DL-3.
- **Smoke fixture (F-6):** `EmployeeSmokeFixturesController` requires JWT and two permissions; "local only" rests only
  on the environment name (Development/Testing/Local); the repository rewrites an existing employee number and revives a
  soft-deleted row (`EmployeeSmokeFixtureRepository.cs:34`). Boundary fix is in DL-2.
- **Workflow callback (F-14):** `POST .../drafts/workflow-decisions` carries only class-level `[Authorize]`, no
  `[HasPermission]`; today it returns 409 and changes nothing.
- **Submit/activation containment (F-10):** confirmed - submit and workflow-decisions return 409; no workflow start
  client is registered; no Active employee is materialized; no `employee.activated` / `employee.created.v1` is emitted.
- **MOD-0299 (F-11):** `execution/registries/module-id-registry.md:199` keeps the old SaaS Billing & Invoicing meaning
  only as a non-executable deprecated alias that migrates to MOD-0364 SaaS Billing & Invoicing, and states that
  canonical MOD-0299 stays Position & Organization Assignment (Blueprint 8.7; Blueprint 8.8 `Blueprint_Data` row 300 per
  review 01). There is no unresolved identity conflict. The de-scope of the MOD-0299 assignment workflow from P2 stays
  valid on scope grounds only.
- **Workspace state:** the earlier "no-valid-`HEAD` / all-untracked workspace" blocker is not a current blocker; the
  repository has a valid `HEAD`. (The same stale sentence in `domain-config.md:66` is outside this revision's file
  scope.)
- **Registry row:** `module-id-registry.md` still has no MOD-0251 row; the identity is proven from the Blueprint
  (`verify_module_id.py --check-id` exit 0). Row addition is a shared-file change planned in the joint registry PR
  (discovery record).

### Known Defects in the Approved P2 Slice (Review 01)

Code defects inside the conditionally approved P2 slice. Each is open; fixing them is DL-2 (code work, separate WP and
approval). Paths are under `services/Diten.HcmService/src/` unless shown in full; line numbers are from the findings file.

| Finding | Defect | Code location (file:line, review 01) | Criteria affected |
|---|---|---|---|
| F-1 | Reference validation treats every HTTP 2xx as referenceable without reading the provider envelope; a provider `referenceable=false` in a 200 response (not found / other tenant) counts as valid; malformed JSON does not block. | `Diten.HcmService.Infrastructure/ReferenceValidation/GatewayReferenceValidationClient.cs:125`; `services/Diten.Platform/src/Diten.Platform.Application/Features/TenantOrganization/Handlers/QueryHandlers/ValidatePersonReferencesQueryHandler.cs:33` | GWT-A03 to GWT-A06, GWT-B07 |
| F-2 | Validated references are not bound to the stored draft: validation checks the ids in the request, not the draft's ids; a later patch does not invalidate the validation summary; review checks only `CanReview` and non-empty fields. | `Diten.HcmService.Application/Features/CoreHrEmployeeMaster/Handlers/ValidateDraftReferencesHandler.cs:53`; `.../Handlers/PatchEmployeeDraftHandler.cs:50`; `.../Handlers/EmployeeDraftHandlerHelpers.cs:28` | GWT-A03 to GWT-A07 |
| F-3 | Draft PII protection: payload stored without encryption or recursive masking; mapper masks top-level keys only; `personal_email` and `phone` are not in the sensitive-key list; government-identifier check covers `StepPayload` only, `ClientValidationState` is stored unchecked. | `.../CoreHrEmployeeMaster/EmployeeDraftPayloadGuard.cs:49`; `Diten.HcmService.Persistence/Repositories/EmployeeDraftSessionRepository.cs:37`; `.../CoreHrEmployeeMaster/EmployeeDraftMapper.cs:62`; `.../Validators/PatchEmployeeDraftValidator.cs:29` | GWT-A11, GWT-B05 |
| F-15 | Unauthorized users get the page skeleton (title, start button, form cards; list filter and table) - UAS-001 violation; client-side disabling and API 403 do not close it. | `frontend/Diten.Web/Views/HCM/Employees/Create.cshtml:10`; `frontend/Diten.Web/Views/HCM/Employees/Index.cshtml:13` | GWT-A10 |
| F-16 | Create idempotency is find-then-insert with no unique index on `TenantId` + `CreateIdempotencyKeyHash`; two concurrent requests with one key can create two drafts and two audit records. | `.../Handlers/CreateEmployeeDraftHandler.cs:33`; `Diten.HcmService.Persistence/Repositories/EmployeeDraftSessionRepository.cs:37` | GWT-A01 |

Related findings outside the five: F-4 (log-only audit; DL-4 in Revision 2, DL-2 in Revision 3 / F-22, DL-7 since
Revision 4 / F-26), F-6
(smoke-fixture boundary; DL-2), F-14 (workflow-decisions authorization; DL-2).

## Open Decisions (Owner)

Open owner decisions, plus OWN (decided 2026-10-29). None other is decided; each blocks the slice named. K-1 to K-9 come from the
HCM-CORE-BUNDLE link contract proposal (`docs/analysis/contracts/drafts/hcm-core-employee-link-v0.3-proposal.md` §6,
proposal - not approved). Decided already, not reopened: `bag-kisi` (User → Person → Employee; no `UserId` on
`Employee`; `docs/records/decisions/2026-10/hcm-employee-user-link-owner-decision-01.md`).

| ID | Decision | Owner role | Blocks |
|---|---|---|---|
| K-1 | Which module and service hold the two-way user ↔ person link: MOD-0360 Tenant User & Identity Management or MOD-0288 Organization, Person & Position Directory (other module; XMC-001). | Repository owner with MOD-0360 and MOD-0288 owners | DL-5 |
| K-2 | Event names: `hcm.employee.activated.v1` (proposal) or `employee.created.v1` (this pack's earlier name). | HCM pack owner with HCM consumer CT owners | DL-4, DL-6 |
| K-3 | Self-read of own employment summary: new key `mod0251.employee.view_self` or a session-owner rule. | HCM pack owner with RBAC owner | DL-5 |
| K-4 | Service identity for scheduled sync (service-to-service identity on the Auth side; read-only `employment-summaries`). | RBAC / Auth owner (other module; XMC-001) | DL-5, DL-6 |
| K-5 | Add `seniorityDate` and `ftePercent` to the MOD-0251 Employment Record. | HCM data owner | DL-4 |
| K-6 | Source of `workLocationId`: MOD-0288 organization unit or a separate work-location vocabulary (holiday calendar via MOD-0362 Working Calendar & Public Holidays). | HCM data owner with MOD-0288 owner | DL-4 |
| K-7 | `employmentType` vocabulary and its management (MOD-0048 Reference Data Management or other). | HCM data owner with MOD-0048 owner | DL-4 |
| K-8 | Approval-chain source: proposal MOD-0288 `PositionAssignment`; relation to MOD-0251 `EmploymentRecord.PositionId` and duplication rule. | HCM pack owner with MOD-0288 and MOD-0023 Workflow Designer owners | DL-4, DL-5 |
| K-9 | `asOf` default and day boundary: tenant time zone or legal-entity time zone. | HCM pack owner | DL-5 |
| F-5 | Employee list/detail read code (API, pages, menu entry) outside P2: retro-approve as DL-3 scope or close it (code work, separate WP). | HCM pack owner (repository owner) | DL-3 |
| F-12 | Boundary with MOD-0298 Employee Profile Workspace and the `EmployeeProfileProjection` in `Diten.HumanCapitalService` (HRIS source id, external employee reference): source mapping, update authority, feed contract from MOD-0251. Changes to another service need separate scope approval. | HCM data owner with MOD-0298 owner (MOD-0298 has no owner today) | DL-5 |
| F-13 | Permission breadth: keep the 14 seeded `mod0251.*` keys or narrow the seed to the approved slices. Revision 3 (F-25): either choice needs a recorded owner decision before DL-2 starts; keeping the seed is not a default. | HCM pack owner with RBAC owner | DL-2 |
| AE-1 | AUD-001 K4: the rule text (`audit-trail-standard.md` §5 K4, and `module-pack-standard.md` §10.1) still says new write commands in HCM are blocked "until the shared forwarder arrives", although the forwarder `Diten.BuildingBlocks.CentralAudit` is on `main` (528cd415d, 111c60c46). Confirm that K4 is lifted for `Diten.HcmService`. This pack does not claim it is lifted. | AUD-001 rule owner (Control Tower) | DL-7 (central audit; Revision 4, was DL-2), DL-4, DL-6 |
| AE-2 | The shared forwarder records only commands that succeed (`CentralAuditBehavior`: a failed envelope or exception leaves no record), and a `[HasPermission]` 403 never reaches MediatR. AUD-001 §3 wants refused commands recorded (`Outcome = Failed / Denied`); this pack's catalog has `employee.access_denied` and `employee.conflict`. Decide how denials and conflicts are recorded (forwarder change, other path) or accept the gap. Revision 4 (AK-45): two refusal paths must be decided separately - (1) authorization refusals that never reach MediatR (a `[HasPermission]` 403; needs an authorization-hook entry point), and (2) in-handler refusals that return a failed envelope (409 conflict in `PatchEmployeeDraftHandler.cs:39-42,77-81`, `ValidateDraftReferencesHandler.cs:42-45,86-90`, `ReviewEmployeeDraftHandler.cs:39-42,72-75`; the behaviour drops failed envelopes, `CentralAuditBehavior.cs:59-63`), which need a handler-callable refusal-record API. | Control Tower with Building Blocks owner (other module; XMC-001) | DL-7 (GWT-A10, GWT-A11, GWT-A13 audit part; Revision 4, was DL-2) |
| AE-3 | The forwarder record (`CentralAuditOutboxRecord`) has no before/after or changed-field-names field; AUD-001 §3 item 10 asks for one of them on a change. Decide: extend the shared record, or accept entity + operation only for HCM. Revision 4 (AK-27): if the decision records "entity + operation only" as accepted, the DL-7 acceptance record names that gap (event name and changed-field names not in the central log) instead of measuring them - the same exit rule as AE-2; the minimum content for both outcomes is in "Audited Events". | Control Tower with Building Blocks owner (other module; XMC-001) | DL-7 (Revision 4, was DL-2), DL-4 |
| AE-4 | Platform `AuditCategory` (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs`) has no HCM category; proposed: `MasterData` (proposed, not decided). The `SourceModule` code must be the one the HCM manifest registers with Platform (not verified). | HCM pack owner with Platform audit owner | DL-7 (Revision 4, was DL-2) |
| BL-324 | HCM tenant source (Revision 4, AK-37): `Diten.HcmService` takes the tenant from the `X-Tenant-Id` header and reads the JWT only to refuse a contradiction (`services/Diten.HcmService/src/Diten.HcmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs:34-59`); the forwarder files the central record under the scope's tenant and Platform does not re-check it (`ICentralAuditScope.cs:7-10`). Decide that the validated JWT is the tenant source for HCM, at least for the central-audit scope. | HCM pack owner with security / Auth owner | DL-7 |
| AE-5 | Revision 4 (AK-48): whether the employee number (`employeeNumber`, an identifier of a person) may be written to the central audit record (fixture rows of "Audited Events"), or only the entity id. | Data-protection owner with HCM pack owner (owner role not verified) | DL-7 |
| OWN | Pack owner: front matter now says `ali.tufanoglu` (repository owner's decision in chat, 2026-10-29; interim AK-44 closed). Written before the version that review 04 targets, so the reviewed version carries it; a later change makes that version stale. A dedicated HR data owner can replace it later through a new revision and approval row. | Repository owner | decided 2026-10-29 |
| HRF | Identity clash: `origin/hr-future` carries `platform-shared-services/module-packs/MOD-0251-hris-external-sor.md` ("HRIS External SoR", `Diten.Platform`) with the same number and a different meaning; it does not block `main`, but a merge of that branch makes the clash real. This queue does not write to that branch (XMC-001). | Owner of the `hr-future` branch with Enterprise Architect | merge of `hr-future`; none of DL-1 to DL-7 |

## Ownership and Boundaries

The following ownership lists describe MOD-0251 product ownership and future SoR responsibility. They do not broaden the current approved runtime scope beyond the contained P2 draft/reference-validation baseline above.

### In Scope

- Employee master creation, activation, search, detail, status history, and governed lifecycle contracts.
- Employment Record ownership for the employee master.
- Job Assignment / Employment Assignment Snapshot ownership as part of the employee master.
- Employee draft session for the Create Employee wizard.
- Employee lifecycle events and safe audit payload policy.
- HCM-CORE-BUNDLE interface bundle metadata.

### Out of Scope

- Assignment workflow/orchestration, owned by `MOD-0299`.
- Organization, person, and position reference directories, owned by `MOD-0288`.
- Approval engine ownership, owned by `MOD-0023`.
- Binary document storage, owned by `MOD-0028`.
- Evidence link registry, owned by `MOD-0031`.
- Retention/legal hold policy, owned by `MOD-0030`.
- HR sensitive access and masking policy engine, owned by `MOD-0314`.
- Payroll, time, leave, compensation, benefits, performance, offboarding execution, employee relations, and Talent Ecosystem modules.

## Owned Objects

- Domain entities:
  - Employee.
  - Employment Record.
  - Job Assignment / Employment Assignment Snapshot.
  - Employee Status History.
  - Employee Identifier token reference.
  - Employee Draft Session.
  - Employee Document Link metadata.
  - Employee Data Quality Case.
- Commands:
  - Create employee draft.
  - Update employee draft.
  - Submit employee draft.
  - Apply workflow approval decision.
  - Update employee profile.
  - Update employment record.
  - Request status change.
  - Link employee evidence.
  - Export employee list.
- Queries:
  - Employee registry list.
  - Employee detail.
  - Employee status history.
  - Employee data-quality case list.
- API/interface stubs:
  - Employee Draft APIs.
  - Employee Query APIs.
  - Employee Status Command.
  - Employee Evidence Link API.
  - HCM-CORE-BUNDLE.
- Frontend planning surfaces:
  - Employee Registry.
  - Create Employee Wizard.
  - Employee Master Detail.
  - Employee Status History.
  - Employee Data Quality Queue.

## Entity Fields

Field-level details are inherited from `docs/reference/architecture/MOD-0251-Core-HR-Employee-Master-Spec-v1.5-BLUEPRINT-ALIGNED.md` and must be contract-closed in `MOD0251-P1-CONTRACTS-M1` before implementation.

Revision 2 proposed fields (DL-4; not approved; open decisions K-5 and K-6): `seniorityDate` (date, null means
`hireDate` is used) and `ftePercent` (decimal 0-100, null allowed) on the Employment Record; `workLocationId` (uuid,
null allowed; source open in K-6). They feed the HCM-CORE-BUNDLE `EmploymentSummary` (link contract v0.3 §2). The
canonical consumer key is (`TenantId`, `employeeId`); `employeeNumber` is a display code, not a key (v0.3 §1.1).

| Entity | Required Key Fields / Notes |
|---|---|
| Employee | `EmployeeId`, server-side `TenantId`, `PersonId`, legal names, employee number, worker type, employment type, hire date, status, sensitivity level, concurrency `version` / `etag`. |
| Employment Record | `EmploymentRecordId`, `EmployeeId`, server-side `TenantId`, company/legal-entity reference, start/end dates, contract type, probation fields, employment status, approval status, concurrency `version` / `etag`. |
| Job Assignment / Employment Assignment Snapshot | Employee-side assignment snapshot only; orchestration stays with MOD-0299. |
| Employee Draft Session | Wizard draft state, step status JSON, redacted/encrypted draft payload, expiration, submit timestamp, concurrency. |
| Employee Status History | Append-only status transition log with workflow/audit/evidence references. |
| Employee Document Link | Link metadata only; binary content stays with MOD-0028 and evidence links with MOD-0031. |
| Employee Data Quality Case | Duplicate/missing-data queue item, assignment, resolution status, related employee/draft references. |

## Repo Scope

Governance/documentation scope for this reconciliation prompt:

- `execution/domains/human-capital-management/README.md`.
- `execution/domains/human-capital-management/domain-config.md`.
- `execution/domains/human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md`.

Completed P2 runtime scope is limited to the draft/reference-validation slice. Additional MOD-0251 business implementation remains blocked until a later approved prompt authorizes it and confirms:

- HCM backend service/project path. Scaffold exists at `services/Diten.HcmService`, but employee business implementation remains unapproved.
- HCM persistence/migration location.
- HCM frontend route/view convention.
- Gateway route ownership and downstream service port.
- Test project paths.

## Protected Paths

- `.antigravity/**`.
- `services/**` outside the approved `services/Diten.HcmService` scaffold and explicitly approved MOD-0251 draft/reference-validation implementation prompts.
- `frontend/**` outside the approved MOD-0251 P2 draft wizard/proxy work and later separately approved frontend prompts.
- `gateway/**` route files until an approved integration-agent task exists.
- `tests/**` outside the approved MOD-0251 P2 focused test scope and later separately approved test prompts.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`.
- `frontend/Diten.Web/Controllers/Archive/**`.
- `frontend/Diten.Web/Views/Archive/**`.
- Other domain module packs and domain configs unless separately approved.
- Workbook and registry files except read-only inspection.

## Dependencies

### Blueprint Dependency Gate

| Dependency | Purpose | Fail-Closed Rule |
|---|---|---|
| `MOD-0018 RBAC / Authorization` | HR role and permission control | Missing permission seed/enforcement blocks all protected endpoints. |
| `MOD-0021 Audit Trail` | Access/change/export/approval audit | Missing audit emit/query contract blocks critical writes and sensitive reads. |
| `MOD-0023 Workflow` | Activation, sensitive update, suspension, termination, rehire approval workflow | Missing workflow contract blocks activation approval and approval-decision implementation. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention and legal hold policy | Missing retention/legal hold contract blocks evidence integration and destructive lifecycle flows. |
| `MOD-0048 Reference Data / Lookups` | Employment statuses, worker types, contract types, reason categories | Missing controlled values blocks validation and create/submit. |
| `MOD-0057 Taxonomy / Semantic Tagging` | Classification, role/job categories, tags | Missing taxonomy blocks tag visibility only; optional for first approved draft/reference-validation slice unless required by contract. |

### Additional Implementation Dependencies

| Dependency | Purpose | Fail-Closed Rule |
|---|---|---|
| `MOD-0028 Documentation / Evidence Management` | Document/evidence storage reference | Missing contract blocks evidence attachment/integration. |
| `MOD-0031 Evidence Linking Service` | Evidence link registry | Missing contract blocks evidence completion. |
| `MOD-0288 Organization, Person & Position Directory` | Person, organization, position references | Missing person reference blocks employee creation; missing company/legal-entity SoR blocks employment record validation. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | HR-sensitive masking and visibility policy | Missing masking hook blocks sensitive response shaping. |
| Tokenization/security service | Government identifier token/hash capture | Missing tokenization contract blocks government identifier capture. |
| Shared central-audit forwarder `Diten.BuildingBlocks.CentralAudit` (BL-501; Revision 3) | Path `b` for every MOD-0251 write command ("Audited Events") | Blocker today: the HCM ledger (`tests/architecture/audit-ledger/Diten.HcmService.md`) has no accepted trail (only `hcm-taslak-denetimi`, path `aday`, log-only) and lists all eight commands as known debt. No new write command is coded until DL-7 (Revision 4, was DL-2) registers the forwarder, Control Tower accepts the HCM trail, and AE-1 confirms K4 for HCM. |

## Runtime Constraints

- First approved runtime slice is draft/reference-validation only: Create Employee Draft -> Save Draft Steps -> Validate Person/Organization/Position/Legal-Entity References -> Review Draft -> Reload Persisted Draft.
- Submit for approval, MOD-0023 approval decision processing, Active transition, `employee.created` event/outbox behavior, and approval audit events are excluded from the first approved slice.
- Evidence integration, termination/status changes, Data Quality Queue, and export are later sequences.
- No shell UI is allowed: UI pages must be backed by real API, persistence, RBAC, audit, validation, and failure handling.
- Tenant isolation is mandatory; `TenantId` is server-side and not accepted from client payload.
- Employee data is tenant-owned; `entity_base: EntityBase` is provisional and must be confirmed against the HCM service base class during P0/P1.
- Optimistic concurrency is mandatory for editable records.
- Idempotency is mandatory for create/submit/approval/status/document/export commands.

## Layout & Shell Contract

- `shell: tenant`.
- Frontend route planning from the spec: `/HCM/Employees`, `/HCM/Employees/Create`, `/HCM/Employees/{employeeId}`.
- Razor layout for future frontend pages: `Layout = "_LayoutTenantShell";`.
- Frontend expansion beyond the approved P2 draft wizard/proxy slice is blocked until a later approved scope confirms the route, service/API, and UI contracts.

## Backend File Convention

Backend service owner is `Diten.HcmService`. The scaffold exists at `services/Diten.HcmService`, and P2 draft/reference-validation runtime is implemented there. Full Employee Master lifecycle implementation remains unapproved.

Once confirmed, use repo-standard CQRS/MediatR conventions:

```text
services/{ConfirmedHcmService}/src/{ConfirmedHcmService}.Application/Features/CoreHrEmployeeMaster/
├── Commands/
├── Queries/
├── Handlers/CommandHandlers/
├── Handlers/QueryHandlers/
├── Validators/
└── CoreHrEmployeeMasterModels.cs
```

Naming must follow Golden Reference conventions: commands end in `Command`, queries end in `Query`, handlers end only in `Handler`, validators end only in `Validator`, and DTO/view records live in `{Module}Models.cs`.

## Frontend File Contract

Golden Reference decision: `compact`.

Justification: Create/edit user-entered fields exceed eight. Counted planning fields include person reference, legal first/middle/last/preferred names, date of birth, nationality, work email, personal email, phone, worker type, employment type, hire date, company/legal-entity reference, contract type, probation fields, sensitivity level, and related employment metadata. Government identifier capture is excluded until tokenization is confirmed but the form remains above the compact threshold.

Applicability:

- Employee Registry DataTable uses DataTable v2 and the Compact reference for table/list structure.
- Create Employee uses a wizard pattern, not a direct GoldenReferenceCompact create form clone.
- Employee Master Detail uses a single detail / section page pattern, not a simple CRUD details clone.
- Compact Golden Reference still governs backend CQRS naming and DataTable list page structure where applicable.

Future registry/detail frontend compact file planning, after a later approved scope:

- `frontend/Diten.Web/Views/HCM/Employees/Index.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/Create.cshtml` for wizard host.
- `frontend/Diten.Web/Views/HCM/Employees/Details.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_Filter.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_DataTable.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/_IndexL10n.cshtml`.
- `frontend/Diten.Web/Views/HCM/Employees/EmployeeMasterIndex.cs`.
- `frontend/Diten.Web/wwwroot/assets/js/HCM/Employees/index.js`.
- `frontend/Diten.Web/wwwroot/assets/js/HCM/Employees/index.l10n.js`.

No new frontend behavior may be created by this governance reconciliation prompt.

## Validation Rules

| Field / Rule | Requirement |
|---|---|
| `PersonId` | Required; must resolve to same-tenant MOD-0288 person before employee creation. |
| Legal first name | Required before activation; max 100; PII redaction in logs/audit. |
| Legal last name | Required before activation; max 100; PII redaction in logs/audit. |
| Employee number | Generated by MOD-0251 policy; unique per tenant. |
| Work email | Optional; if present, valid email and unique per tenant. |
| Worker type | Required controlled value from MOD-0048. |
| Employment type | Required controlled value from MOD-0048. |
| Hire date | Required before activation. |
| Company/legal-entity reference | Required before employment record validation; SoR must be confirmed by P0 inspection. |
| Contract type | Required controlled value from MOD-0048. |
| Government identifier | Raw value prohibited; token/hash only after tokenization contract exists. |
| Sensitivity level | Required; drives MOD-0314 masking policy. |
| TenantId | Server-side only; reject/ignore client-provided values. |
| Version/ETag | Required for editable updates; stale writes return controlled conflict. |

## Failure Path to Verify

| Failure Path | Expected Behavior |
|---|---|
| Missing dependency | Relevant runtime slice blocks with controlled error; no inferred contract. |
| Unauthorized access | 403 or masked response; audit access-denial event emitted. |
| Tenant mismatch | 404/fail-closed; no cross-tenant data disclosure. |
| Duplicate employee | Duplicate warning or Data Quality Case; no silent duplicate activation. |
| ETag/concurrency conflict | 409 with reload/review-current-version path. |
| Idempotency replay | No duplicate employee, workflow, audit, status history, or event side effect. |
| Workflow unavailable | Activation submit/approval cannot complete; employee state remains unchanged. |
| Audit unavailable | Critical writes blocked; sensitive read/export audited or denied. *(Revision 4, AK-28: on path `b` "unavailable" means the local outbox row cannot be written in the command's transaction - then the write is blocked. Platform not configured or down does not block the write; the row stays `Pending` in the outbox (delivery outcome `Retry` when Platform is down; AK-35). See "Audit Fail-Closed Rules".)* |
| Tokenization unavailable | Government identifier capture disabled/blocked. |

## Authorization Convention

Tenant HCM permission namespace is provisional and must be confirmed during P1 contract closure. Planned permission codes:

- `mod0251.employee.search`.
- `mod0251.employee.view`.
- `mod0251.employee.view_sensitive`.
- `mod0251.employee.create_draft`.
- `mod0251.employee.submit`.
- `mod0251.employee.approve`.
- `mod0251.employee.edit_legal`.
- `mod0251.employee.edit_employment`.
- `mod0251.employee.change_status`.
- `mod0251.employee.attach_evidence`.
- `mod0251.employee.export`.
- `mod0251.employee.view_status_history`.
- `mod0251.data_quality.view`.
- `mod0251.data_quality.resolve`.

Actor types: HR Admin, HR Manager, HR Contributor, HR Data Steward, Audit Admin, and service actors from MOD-0023/MOD-0021/MOD-0031 as applicable.

## MOD-0251 RBAC Contract

Prompt ID: `MOD0251-P1-RBAC-AUDIT-CONTRACT-M1`.

This section closes MOD-0251 RBAC planning only. It does not create permission seeds, roles, policies, controllers, UI guards, tests, gateway routes, or runtime code. Existing repo evidence shows executable permission enforcement patterns through `HasPermissionAttribute`, Auth role/permission controllers, and Platform controller permission usage; MOD-0251-specific seed and endpoint implementation remain blocked until this pack is promoted and an approved implementation prompt authorizes employee runtime behavior.

*Historical text above. Revision 2 (review 01 F-13): the permission seed is implemented in Auth `DataSeeder` (14 keys,
wider than P2); see "Corrected Current State". The workflow-decisions endpoint has no `[HasPermission]` (F-14).*

### Permission Key Catalog

| Permission Key | Purpose |
|---|---|
| `mod0251.employee.search` | Search and list tenant-scoped employee registry rows. |
| `mod0251.employee.view` | View non-sensitive employee detail. |
| `mod0251.employee.view_sensitive` | View sensitive employee fields when MOD-0314/tokenization policy also permits. |
| `mod0251.employee.create_draft` | Start and save employee draft sessions before submit. |
| `mod0251.employee.submit` | Submit a complete employee draft to the approval dependency. |
| `mod0251.employee.approve` | Receive/process an approval or rejection decision through the approved MOD-0023 adapter contract. |
| `mod0251.employee.edit_legal` | Edit legal profile fields after draft or approved controlled edit flow. |
| `mod0251.employee.edit_employment` | Edit employment record fields after draft or approved controlled edit flow. |
| `mod0251.employee.change_status` | Request or execute status transitions such as suspend, terminate, rehire, or leave. |
| `mod0251.employee.attach_evidence` | Link evidence to an employee/draft through MOD-0028/MOD-0031 contracts. |
| `mod0251.employee.export` | Request and download governed employee exports. |
| `mod0251.employee.view_status_history` | View immutable employee status history. |
| `mod0251.data_quality.view` | View duplicate/data-quality queue items for MOD-0251. |
| `mod0251.data_quality.resolve` | Resolve duplicate/data-quality cases for MOD-0251. |

### Role-Permission Matrix

| Role | Permissions |
|---|---|
| HR Admin | All MOD-0251 permissions listed in the catalog. |
| HR Manager | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.view_sensitive`, `mod0251.employee.create_draft`, `mod0251.employee.submit`, `mod0251.employee.edit_employment`, `mod0251.employee.change_status`, `mod0251.employee.attach_evidence`, `mod0251.employee.export`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`. |
| HR Contributor | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.create_draft`, `mod0251.employee.submit`, `mod0251.employee.attach_evidence`, `mod0251.employee.view_status_history`. |
| HR Data Steward | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.edit_legal`, `mod0251.employee.edit_employment`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`, `mod0251.data_quality.resolve`. |
| Department Manager | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.view_status_history` limited to permitted reporting scope. |
| Employee | `mod0251.employee.view` limited to self-service scope only; no sensitive, export, approval, evidence, status-change, or data-quality permissions. |
| Audit Admin | `mod0251.employee.search`, `mod0251.employee.view`, `mod0251.employee.export`, `mod0251.employee.view_status_history`, `mod0251.data_quality.view`; audit event access remains governed by MOD-0021/platform audit permissions. |
| Platform Admin | No implicit tenant HCM business access from platform role alone. Break-glass access requires explicit MOD-0251 tenant-scoped permission assignment and audit policy. |

### Protected Action / Endpoint Map

| Protected Action | Future API Surface | Required Permission | Fail-Closed Dependency |
|---|---|---|---|
| Registry search/list | `GET /api/v1/hcm/employees` | `mod0251.employee.search` | MOD-0314 masking unavailable returns non-sensitive rows only or denies. |
| Detail view | `GET /api/v1/hcm/employees/{employeeId}` | `mod0251.employee.view` | Tenant mismatch returns not found or denied. |
| Sensitive view | `GET /api/v1/hcm/employees/{employeeId}/sensitive` or sensitive field expansion | `mod0251.employee.view_sensitive` | MOD-0314 and tokenization contracts must also permit; otherwise denied. |
| Draft create/update | `POST /api/v1/hcm/employees/drafts`, `PATCH /api/v1/hcm/employees/drafts/{draftSessionId}` | `mod0251.employee.create_draft` | MOD-0288/MOD-0048 validation blockers prevent review readiness, not draft session creation unless required fields are validated at that step. |
| Draft reference validation | `POST /api/v1/hcm/employees/drafts/{draftSessionId}/validate-references` | `mod0251.employee.create_draft` | Missing person, organization-unit, position, or legal-entity provider/Gateway path blocks review readiness. |
| Draft review | `POST /api/v1/hcm/employees/drafts/{draftSessionId}/review` | `mod0251.employee.create_draft` | Non-submit review only; MOD-0023 unavailable keeps submit/approval/Active out of scope. |
| Submit | `POST /api/v1/hcm/employees/drafts/{draftId}/submit` | `mod0251.employee.submit` | MOD-0023 unavailable blocks submit/approval. |
| Approval adapter | `POST /api/v1/hcm/employees/{employeeId}/approval-decisions` or event consumer | `mod0251.employee.approve` for user/API command; service actor contract for MOD-0023 event | MOD-0023 unavailable blocks activation decisions. |
| Legal profile edit | `PATCH /api/v1/hcm/employees/{employeeId}/legal-profile` | `mod0251.employee.edit_legal` | Sensitive fields denied when masking/tokenization unavailable. |
| Employment record edit | `PATCH /api/v1/hcm/employees/{employeeId}/employment-record` | `mod0251.employee.edit_employment` | MOD-0288/MOD-0048 validation required. |
| Status change | `POST /api/v1/hcm/employees/{employeeId}/status-changes` | `mod0251.employee.change_status` | Workflow/evidence/retention blockers keep R1 status changes disabled. |
| Evidence link | `POST /api/v1/hcm/employees/{employeeId}/evidence-links` | `mod0251.employee.attach_evidence` | MOD-0028/MOD-0031 unavailable disables evidence link. |
| Export | `GET /api/v1/hcm/employees/export` | `mod0251.employee.export` | MOD-0314/audit/export policy unavailable disables export. |
| Status history | `GET /api/v1/hcm/employees/{employeeId}/status-history` | `mod0251.employee.view_status_history` | Tenant mismatch returns not found or denied. |
| Data quality view | `GET /api/v1/hcm/employee-data-quality` | `mod0251.data_quality.view` | Queue workflow remains deferred if P10 not approved. |
| Data quality resolve | `POST /api/v1/hcm/employee-data-quality/{caseId}/resolve` | `mod0251.data_quality.resolve` | Full resolution workflow remains deferred unless approved. |

### RBAC Fail-Closed Rules

- Missing MOD-0251 permission seed blocks endpoint enablement; no endpoint may silently use a broader or legacy permission.
- Unknown role, missing role mapping, or absent permission claim grants no access.
- Tenant mismatch returns not found or denied and must not disclose cross-tenant identifiers, counts, or existence.
- UI hiding is insufficient; every protected action requires server-side permission enforcement.
- Direct browser/front-end service-port bypass is prohibited; tenant HCM calls must enter through Gateway `5000` / approved same-origin MVC proxy patterns.

## Gateway / API Routing Decision

- Gateway route implementation is deferred. *(Historical. Revision 2, review 01 F-6/F-17: four HCM routes exist in `gateway/Diten.ApiGateway/ocelot.json:2788-2847` - `/api/v1/hcm/employees` (GET), `/api/v1/hcm/employees/{everything}` (GET/POST/PATCH/DELETE), `/api/v1/hcm/employees/drafts`, `/api/v1/hcm/employees/drafts/{everything}`. The general route is wider than P2; narrowing it is an `integration-agent` task in DL-2/DL-3.)*
- Frontend must call Gateway `5000` only.
- Browser JS must not call service ports directly.
- Gateway route changes must be assigned to `integration-agent` after service/API route ownership is confirmed.
- Expected API surface from the spec remains `/api/v1/hcm/employees...`, but the exact route must be confirmed by P1 contracts and gateway standards before implementation.

## API / Interface Registry Stubs

| Contract | Owner | Consumer | Type | Notes |
|---|---|---|---|---|
| HCM-CORE-BUNDLE | MOD-0251 | HCM Foundation consumers | Contract bundle | Minimum integration contract bundle from Blueprint v7. |
| Employee Draft APIs | MOD-0251 | MOD-0251 UI | API | Create/update/submit draft; idempotent submit. |
| Employee Query APIs | MOD-0251 | MOD-0298, MOD-0299, MOD-0305, MOD-0314 | API | Search/detail with masking. |
| Employee Status Command | MOD-0251 | HR UI, MOD-0305 | API | Status transitions after workflow policy. |
| Workflow Start / Decision | MOD-0023 | MOD-0251 | API/Event | MOD-0023 owns approval workflow. |
| Audit Emit | MOD-0021 | MOD-0251 | API/Event | Critical writes fail closed on audit failure. *(Revision 4, AK-28: on path `b` only a local outbox write failure blocks the write; Platform delivery is asynchronous - see "Audit Fail-Closed Rules".)* |
| Evidence Document | MOD-0028 | MOD-0251 | API | Evidence attachment dependency. |
| Evidence Link | MOD-0031 | MOD-0251 | API | Evidence link dependency. |
| Person Reference Lookup | MOD-0288 | MOD-0251 | API | Employee creation blocker. |
| Sensitive Access Policy | MOD-0314 | MOD-0251 | API/Service | Masking and visibility dependency. |

## Audit Event List and Safe Payload Policy

Planned events:

- `employee_draft.created`.
- `employee_draft.updated`.
- `employee.submitted_for_approval`.
- `employee.approved`.
- `employee.rejected`.
- `employee.profile.updated`.
- `employee.employment_record.updated`.
- `employee.status.changed`.
- `employee.evidence_linked`.
- `employee.export.requested`.
- `employee.export.completed`.
- `employee.access_denied`.
- `employee.conflict`.
- `employee.idempotency_replayed`.

Safe payload policy: audit payloads may include tenant ID, actor ID, employee ID, draft ID, workflow reference, correlation ID, event type, status, and version. They must not include full PII payloads, raw government identifiers, secrets, document content, unredacted comments, or exported row data.

## MOD-0251 Audit Contract

Prompt ID: `MOD0251-P1-RBAC-AUDIT-CONTRACT-M1`.

This section closes MOD-0251 audit payload planning only. It does not wire `IAuditService`, add audit behavior registrations, create event emitters, add tests, or create audit queries. Existing repo evidence shows executable audit primitives through `IAuditService`, `AuditBehavior`, `AuditAppendResult`, `AuditService`, redaction, and `PlatformAuditController`; MOD-0251 event wiring remains blocked until `Diten.HcmService` implementation is explicitly approved.

### Audit Event Catalog

| Event | Trigger | Criticality |
|---|---|---|
| `employee_draft.created` | Draft session created. | Critical write. |
| `employee_draft.updated` | Draft legal/employment/evidence metadata saved. | Critical write. |
| `employee.submitted_for_approval` | Complete draft submitted to MOD-0023. | Critical write. |
| `employee.approved` | Approved decision activates employee. | Critical write. |
| `employee.rejected` | Rejected decision returns draft/employee to revisable state. | Critical write. |
| `employee.profile.updated` | Legal/profile section changed. | Critical write. |
| `employee.employment_record.updated` | Employment record section changed. | Critical write. |
| `employee.status.changed` | Employee lifecycle status changed. | Critical write. |
| `employee.evidence_linked` | Evidence link attached to employee/draft. | Critical write when evidence is required. |
| `employee.export.requested` | Export requested before generation. | Sensitive access/export. |
| `employee.export.completed` | Export generated or completed with row count metadata. | Sensitive access/export. |
| `employee.access_denied` | RBAC, tenant, sensitive access, or dependency policy denies a protected action. | Security audit. |

### Safe Payload Matrix

| Payload Field Group | Allowed | Prohibited |
|---|---|---|
| Common envelope | `tenant_id`, `actor_id`, `correlation_id`, event name, source module, request type, outcome. | Actor email/display name unless masked by the audit infrastructure. |
| Target identifiers | `employee_id`, `draft_id`, `employment_record_id`, `status_history_id`, `evidence_link_id`, `data_quality_case_id`, workflow reference ID. | Raw document content, exported row data, or third-party payload bodies. |
| Change metadata | Entity version/ETag, previous and next status codes, changed field names, section name, idempotency key hash/reference. | Before/after values for PII or confidential fields, full object snapshots, raw validation payloads. |
| Sensitive identity | Token reference presence flag and token provider reference when safe. | Government identifier token value, raw government identifier, secrets, emails, phone numbers, date of birth, address, bank/tax values. |
| Denial/export metadata | Permission key, action name, dependency name, denial reason code, export format, bounded row count, filter hash/reference. | Free-text reason notes unless redacted, search text containing PII, unbounded filter details, generated file contents. |

### Audit Fail-Closed Rules

- Critical writes block or roll back if audit emit fails or returns a critical rejected result.
  *(Revision 4, AK-28 / AK-35 / AK-39 - meaning on path `b`, the shared forwarder: "audit emit fails" and "critical
  rejected result" mean only that the local outbox row cannot be written inside the command's own Mongo transaction
  (`CentralAuditTransaction.EnlistAsync` → `InsertInTransactionAsync`, `CentralAuditRecorder.cs:123-134`); that aborts
  the transaction, so the write does not happen and the user receives a **controlled failure**. Controlled failure
  (defined once here, used throughout this pack): a `Response<T>` error envelope with a stable error code that says
  "cannot be done now, try again" (`audit-trail-standard.md` §4.3: "şu an yapılamadı"; code name *proposed*, decided in
  DL-7), no partial write, no stack trace. Today the outbox exception is not that: `CentralAuditBehavior.cs:49-53`
  abandons the event and rethrows, and `ExceptionHandlingBehavior.cs:22-26` logs and rethrows, so the caller gets a
  generic 500; DL-7 changes this and measures it. Delivery to Platform happens later and asynchronously
  (`CentralAuditDispatcher.cs:62-109`) and never stops or rolls back the command. Platform address / key not
  configured: the dispatcher sends nothing, the row stays `Pending` with `Attempts` unchanged, a warning is logged each
  `UnconfiguredWarningInterval`, and the health check is Degraded at once. Platform down: the delivery outcome is
  `Retry` - that is an outcome, not a state; the row stays `Pending` with `Attempts`, `NextAttemptAtUtc` and
  `LastError` updated (`CentralAuditOutboxStore.cs:150-157`), and the health check turns Degraded only when the oldest
  pending row is older than `StaleAfter` (10 min; `CentralAuditOptions.cs:33-34`, `CentralAuditHealthCheck.cs:43-56`).
  Category / operation / actor type unknown to Platform: the row's state becomes `Rejected`, an error is logged and
  the health check is Degraded while a `Rejected` row exists. Outbox state model (AK-46): a row written in the command
  transaction starts `AwaitingCommand`; the command's success confirms it (`Pending`, then `Delivered`); a failed
  command whose transaction committed keeps it as `Abandoned`, never delivered; a row the command never confirmed
  (process death between commit and confirmation) is released after `OrphanAfter` (10 min; `CentralAuditOptions.cs:36-37`,
  `CentralAuditOutboxStore.cs:121-131`) as `Pending` with `ConfirmedByCommand = false` and is sent as outcome
  Unconfirmed; `Rejected` follows ANY Platform 400 (unknown enum name, unresolvable target tenant or other validation;
  `PlatformCentralAuditClient.cs:107-109`), other failures are `Retry`. HCM enlists only on a successful write: a 409
  path returns before any write and leaves no row. This is the accepted forwarder behaviour (BL-501); DL-7 measures it
  as outage, recovery, rejection and orphan release, not as a blocked write.)*
- Sensitive view paths must audit when sensitive fields are returned; if audit cannot be emitted, sensitive fields are denied. *(Revision 4, AK-39: on path `b` "cannot be emitted" means the local outbox row cannot be written in the read's transaction; Platform delivery is asynchronous. MOD-0251 has no audited read today - "Audited Events", AK-30.)*
- `employee.access_denied` payloads must not include PII, raw identifiers, request bodies, or sensitive search parameters.
- Export requires both `employee.export.requested` and `employee.export.completed`; failed export completion records safe status/error code only.
- Audit event payloads must be deterministic, redacted, tenant-scoped, correlation-aware, and safe for replay/idempotency checks.

## P0 Inspection Findings

P0 read-only inspection verdict: `BLOCKED` for approval/implementation.

Inspection evidence recorded that no implementation occurred and no files were changed during the inspection. The pack was later conditionally promoted for the draft/reference-validation slice only; runtime work outside that approved slice remains blocked until topology, service ownership, gateway, frontend, RBAC, audit, workflow, evidence, masking, tokenization, and dependency contracts are explicitly closed.

Key inspection findings:

- No HCM/HR backend service exists under `services/`.
- No HCM API, persistence, indexes, gateway routes, frontend routes, HCM RBAC permissions, HCM audit events, tokenization contract, MOD-0314 masking contract, MOD-0023 workflow contract, or MOD-0031 evidence contract is executable for MOD-0251.
- Platform audit capability exists, but MOD-0251 audit event wiring does not.
- Current MOD-0023 workflow and MOD-0031 evidence dependencies are mock/disabled or not implemented for executable MOD-0251 use.
- Gateway route work remains deferred to `integration-agent`.
- Frontend must use tenant shell and Gateway `5000` / same-origin MVC proxy patterns; browser code must never call service ports directly.

## Governance Closure Findings

Governance closure inspections GC-01, GC-02, and GC-03 are now recorded in this draft pack. These findings document blockers only; they do not approve employee business implementation, gateway routing, migrations, tests, workbook edits, registry edits, or status promotion.

### GC-01 MOD-0299 Reconciliation Finding

Blueprint master 5 and master 7 both map `Blueprint_Data` row 300 to:

| Field | Blueprint Value |
|---|---|
| Module ID | `MOD-0299` |
| Module Name | `Position & Organization Assignment` |
| Suite / Platform | `Human Capital Management Foundation` |
| Capability Group | `Foundation / Platform` |
| Wave | `W-2` |
| Placement | `Domain App (HCM Foundation)` |
| Minimum Integration Contracts | `HCM-CORE-BUNDLE (assignment context schema, position binding contract, manager hierarchy view contract, AuditEvent v1, Correlation-ID, OTel)` |

The current module ID registry maps `MOD-0299` to deprecated SaaS Billing & Invoicing and replacement candidate `CAND-CAP-0005`. That registry row is marked deprecated / non-executable and is not compatible with the Blueprint HCM meaning of `MOD-0299`.

*Historical. Revision 2 (review 01 F-11, row re-read 2026-10-27): `execution/registries/module-id-registry.md:199`
records the SaaS Billing & Invoicing meaning as a non-executable deprecated alias whose legacy meaning migrates to
MOD-0364 SaaS Billing & Invoicing, and states that canonical MOD-0299 stays Position & Organization Assignment
(Blueprint 8.7). No identity conflict remains; the decision below stands on scope grounds only.*

Decision:

- MOD-0251 may reference `MOD-0299` only as a deferred/future HCM assignment dependency.
- MOD-0251 must not consume or implement an executable `MOD-0299` assignment workflow/orchestration contract until Enterprise Architect approval reconciles the registry conflict and the approved registry update is applied.
- No placeholder, alias, or runtime literal may be invented to bypass the conflict.

### GC-02 HCM Service Topology Finding

Recommended future HCM service boundary:

```text
services/Diten.HcmService/
├── Diten.HcmService.sln
├── src/
│   ├── Diten.HcmService.Api/
│   ├── Diten.HcmService.Application/
│   ├── Diten.HcmService.Domain/
│   ├── Diten.HcmService.Infrastructure/
│   └── Diten.HcmService.Persistence/
└── tests/
    └── Diten.HcmService.Application.Tests/
```

Recommended future port: `5060`.

This topology is now the approved planning target for MOD-0251 and the dedicated HCM service scaffold exists at `services/Diten.HcmService`. The scaffold must not be treated as approval to implement employee runtime behavior. MOD-0251 must not be placed in `Diten.Platform`, `Diten.AuthService`, `Diten.MdmService`, `Diten.DevEnablementService`, or `Diten.EnterpriseStrategyService`, because those services do not own native HCM employee master SoR data.

## P1 HCM Service Topology Contract

Prompt ID: `MOD0251-P1-HCM-TOPOLOGY-CONTRACT-M1`.

This P1 contract closes the service-boundary decision for planning purposes only. It does not create the service, authorize backend implementation, add gateway routes, create migrations, create tests, or promote this pack out of `draft`.

### Contract Decision

| Decision Point | Contract Value |
|---|---|
| Future service owner | `Diten.HcmService` |
| Future service path | `services/Diten.HcmService` |
| Future solution file | `services/Diten.HcmService/Diten.HcmService.sln` |
| Future API project | `services/Diten.HcmService/src/Diten.HcmService.Api` |
| Future application project | `services/Diten.HcmService/src/Diten.HcmService.Application` |
| Future domain project | `services/Diten.HcmService/src/Diten.HcmService.Domain` |
| Future infrastructure project | `services/Diten.HcmService/src/Diten.HcmService.Infrastructure` |
| Future persistence project | `services/Diten.HcmService/src/Diten.HcmService.Persistence` |
| Future test project | `services/Diten.HcmService/tests/Diten.HcmService.Application.Tests` |
| Recommended future port | `5060` |
| Browser/runtime ingress | Gateway `5000` only |
| Frontend shell | Tenant shell, `Layout = "_LayoutTenantShell";` |

### Scaffold Gate

The HCM service scaffold exists at `services/Diten.HcmService` on local port `5060`. It creates only the service boundary and does not authorize MOD-0251 employee runtime behavior.

## P1 HCM Service Scaffold / Port Decision

Prompt ID: `MOD0251-P1-HCM-SERVICE-SCAFFOLD-PORT-DECISION-M1`.

This decision closes the MOD-0251 HCM service owner and local port planning gate only. The follow-up scaffold prompt created the service boundary. It does not create frontend routes, gateway routes, migrations, MOD-0251 behavior tests, run-script entries, permission seeds, audit wiring, employee entities, employee DTOs, or employee APIs.

### Decision

| Decision Point | Approved Planning Decision |
|---|---|
| Service boundary | Dedicated HCM domain service |
| Service owner | `Diten.HcmService` |
| Service root | `services/Diten.HcmService` |
| API project | `services/Diten.HcmService/src/Diten.HcmService.Api` |
| Application project | `services/Diten.HcmService/src/Diten.HcmService.Application` |
| Domain project | `services/Diten.HcmService/src/Diten.HcmService.Domain` |
| Infrastructure project | `services/Diten.HcmService/src/Diten.HcmService.Infrastructure` |
| Persistence project | `services/Diten.HcmService/src/Diten.HcmService.Persistence` |
| Application test project | `services/Diten.HcmService/tests/Diten.HcmService.Application.Tests` |
| Local downstream port | `5060` |
| Browser/runtime ingress | Gateway `5000` only |
| Gateway route ownership | `integration-agent`, after HCM API route contract exists |
| Frontend service-port rule | No direct frontend calls to `5060` |

### Port Evidence

- Existing local service sequence uses Gateway `5000`, Frontend `5001`, Enterprise Strategy HTTP `5004`, Auth `5056`, Platform `5057`, DevEnablement `5058`, and MDM `5059`.
- Current `run_all.sh`, `run_watch.sh`, and `gateway/Diten.ApiGateway/ocelot.json` evidence reserve/use `5000`, `5001`, `5004`, `5056`, `5057`, `5058`, and `5059`.
- Repo search found no existing `Diten.HcmService`, `HcmService`, or `5060` runtime reservation outside this planning pack.
- `5060` is the next local service port after the approved MDM `5059` runtime decision and is approved as the HCM downstream port for future scaffold planning.

### Scaffold Boundaries

- The scaffold prompt created only the service boundary, solution/projects, local launch settings for `http://localhost:5060`, and minimal build/runtime wiring needed to prove the empty HCM service builds.
- MOD-0251 employee entities, CQRS handlers, controllers, persistence collections/indexes, permission seeds, audit emitters, frontend pages, gateway routes, and migrations remain outside the scaffold decision.
- HCM service code must not be placed under Platform, Auth, MDM, DevEnablement, or EnterpriseStrategy.
- Gateway ingress for MOD-0251 remains blocked until the HCM API route contract exists and an approved `integration-agent` prompt adds routes.

### Placement Guard

MOD-0251 must not be implemented inside:

- `services/Diten.Platform/**`.
- `services/Diten.AuthService/**`.
- `services/Diten.MdmService/**`.
- `services/Diten.DevEnablementService/**`.
- `services/Diten.EnterpriseStrategyService/**`.

Those services may be consumed through explicit contracts only. They do not own the native HCM employee master SoR.

### Topology Closure Impact

This section closes the service-name/path/port recommendation as the pack's planning topology. It does not close the remaining approval blockers:

- MOD-0299 registry reconciliation or de-scope.
- MOD-0288 person/reference contract confirmation.
- MOD-0023 workflow contract closure or activation blocked.
- MOD-0031 evidence contract closure or evidence deferred.
- MOD-0314 masking contract closure or sensitive reads denied.
- Tokenization/security contract closure or government identifier capture disabled.
- MOD-0251 permission seed and audit wiring implementation.
- Route, DTO, validation, and Given/When/Then acceptance criteria.

### GC-03 Dependency Contract Readiness Matrix

| Dependency | Readiness | Blocks Approval | Blocks Implementation | R1 Fail-Closed Position |
|---|---|---:|---:|---|
| `MOD-0018 RBAC / Authorization` | Executable infrastructure exists; MOD-0251 permission seed, role matrix, and endpoint mapping are not closed. | Yes | Yes | Deny protected endpoints until matrix and seeds exist. |
| `MOD-0021 Audit Trail` | Executable audit infrastructure exists; MOD-0251 audit payloads/events are not closed. | Yes | Yes | Block critical writes and sensitive reads until payload contract exists. |
| `MOD-0048 Reference Data / Lookups` | Executable Platform lookup surface exists; HR business controlled values are not closed for MOD-0251. | Yes | Yes | Block create/submit validation until HR lookup source is confirmed. |
| `MOD-0288 Organization, Person & Position Directory` | Conditional executable readiness for person reference now exists: backend + gateway + frontend proxy/picker are implemented; frontend Vitest remains blocked by missing local test dependency. Organization-unit and position providers plus read-only Gateway ingress are planning-closed to MOD-0288 / Platform runtime. | Conditional | Yes | MOD-0251 may consume existing `PersonId`, organization unit, and position references only through approved Gateway/MVC proxy paths; block employee creation/submission or employment validation when lookup-validation, gateway, picker/reference validation, or provider response is unavailable. |
| `MOD-0023 Workflow` | Missing / not executable for MOD-0251; inspected posture is mock/disabled or not implemented. | No for draft/reference-validation approval when submit/activation is de-scoped | Yes for submit, approval, and Active transition | Activation submit and approval decision remain blocked. |
| `MOD-0031 Evidence Linking Service` | Missing / not executable for MOD-0251; evidence dependency is mock/disabled or not implemented. | Yes unless evidence is formally deferred | Yes for evidence flows | Evidence step disabled/skipped when not required; evidence-linked activation blocked. |
| `MOD-0028 Documentation / Evidence Management` | Not executable for MOD-0251 evidence attachment. | No if first slice excludes evidence | Yes for evidence attachment | Evidence attachment deferred and disabled. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention/legal-hold execution contract unavailable for MOD-0251 business records. | No if destructive/legal-hold flows are deferred | Yes for destructive lifecycle and legal hold | Destructive flows blocked; no retention inference. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Missing masking / sensitive access contract. | Yes | Yes for sensitive reads/responses | Deny sensitive reads or return non-sensitive shapes only. |
| Tokenization/security service | Missing government identifier tokenization contract. | Yes for government identifier capture | Yes for government identifier capture | Government identifier capture disabled. |

Approval impact:

- `MOD-0018`, `MOD-0021`, and `MOD-0048` are executable only as platform primitives; each still requires MOD-0251-specific contract closure before approval.
- `MOD-0288` person reference moved from hard blocker to conditional readiness for the person dependency only. MOD-0251-specific use must still consume only existing `PersonId` values through the approved Gateway/MVC proxy path and must not substitute Auth `UserId`.
- Non-person provider and read-only Gateway ingress decisions are planning-closed: organization units and positions are consumed from MOD-0288 / Platform runtime, and legal entity/company validation is assigned to the executable MDM legal-entity lookup-validation provider for R1 unless a later Blueprint/domain decision overrides it. Position assignment workflow remains deferred pending MOD-0299 reconciliation or explicit first-slice de-scope.
- `MOD-0023` no longer blocks approval of a draft/reference-validation slice because submit, approval, and Active transition are explicitly removed from first-slice scope. It still blocks activation implementation.
- `MOD-0031`, `MOD-0314`, and tokenization/security are missing or not executable and block approval unless the affected behavior is formally removed from approval scope or fail-closed.
- `MOD-0028` and `MOD-0030` may remain deferred only when evidence/destructive lifecycle flows are explicitly out of the first slice and fail-closed.
- MOD-0251 RBAC/audit planning contracts and Given/When/Then acceptance criteria are recorded; permission seed implementation, audit wiring implementation, DTO contracts, route contracts, and validation contracts remain blocked before implementation approval.

### MOD-0288 / Reference Contract Finding

Prompt ID: `MOD0251-P1-MOD0288-REFERENCE-PACK-UPDATE-M1`.

The MOD-0288/reference contract inspection originally returned `BLOCKED` for pack approval because no executable
person-reference contract was proven. Follow-up MOD-0288 person-reference work now provides conditional executable
readiness for the person dependency: backend read/search/lookup-validation, Gateway exposure, and frontend MVC
proxy/picker exist. This section records planning findings only; it does not create adapters, backend code, frontend
code, gateway routes, migrations, tests, workbook edits, registry edits, or implementation approval.

#### Reference Readiness Matrix

| Reference | Readiness | Impact | MOD-0251 Rule |
|---|---|---|---|
| Person | Implemented backend + gateway + frontend proxy/picker; conditional frontend test caveat remains because Vitest is not installed locally. | No longer a hard standalone person-contract blocker, but MOD-0251 approval remains blocked by other dependencies and route/DTO/validation/service blockers. | Employee creation requires a pre-existing same-tenant `PersonId`; MOD-0251 must consume person references through Gateway/MVC proxy paths only and must never substitute Auth `UserId`. |
| Organization Unit | Provider planning-closed to MOD-0288 / Platform organization-unit runtime. Executable service API and approved Gateway ingress are closed for read/reference use. | Provider and ingress decision closed; runtime employment validation may consume only the approved Gateway path. | MOD-0251 may consume same-tenant organization unit references only; it must not own organization directories. |
| Position | Provider planning-closed to MOD-0288 / Platform position runtime. Executable service API and approved Gateway ingress are closed for read/reference use. | Provider and ingress decision closed; runtime assignment snapshot validation may consume only the approved Gateway path. | MOD-0251 may validate existing same-tenant positions only; it must not own the position directory. |
| Position Assignment | Platform primitive exists, but MOD-0299 owns assignment workflow/orchestration after registry reconciliation. | Executable primitive only; workflow dependency remains deferred until MOD-0299 is reconciled or explicitly de-scoped. | MOD-0251 may store only employee-side assignment snapshot after valid person/org/position references; no executable assignment workflow dependency is approved. |
| Legal Entity / Company | Provider planning-closed for R1 to executable MDM legal-entity lookup-validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. MDM runtime port, restore/build, and approved Gateway ingress are closed for read/lookup-validation use. | Provider and ingress decision closed for R1; runtime employment record validation may consume only the approved Gateway path. | MOD-0251 consumes the legal-entity provider contract only; it must not own legal entity/company master data. |

#### Owner Decisions

- `MOD-0288` owns person, organization, and position references.
- `MOD-0251` consumes references only and must not own person, organization, position, or legal entity master directories.
- `MOD-0251` may consume MOD-0288 person references only as existing `PersonId` values through the approved Gateway path or frontend same-origin MVC proxy; AuthService `UserId` is not a person-reference substitute.
- MOD-0288 / Platform organization-unit runtime is the provider for organization-unit references.
- MOD-0288 / Platform position runtime is the provider for position references.
- MDM legal-entity lookup-validation is the R1 provider for legal entity/company validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider.
- `MOD-0299` owns assignment workflow/orchestration after registry reconciliation is complete.
- MOD-0251 may store an employee-side assignment snapshot only after valid person, organization, position, and company/legal-entity reference confirmation.

#### MOD-0288 Person Dependency Executable Evidence

| Layer | Evidence |
|---|---|
| Backend routes | `GET /api/v1/platform/persons/{personId}`; `GET /api/v1/platform/persons`; `POST /api/v1/platform/persons/lookup-validation`. |
| Backend permission keys | `platform.person.view`; `platform.person.search`; `platform.person.lookup_validation`. |
| Gateway routes | Upstream `/api/v1/platform/persons`; upstream `/api/v1/platform/persons/{everything}`; downstream Platform service `localhost:5057`. |
| Frontend proxy/picker | MVC proxy `/Platform/PersonReferences/api`; reusable `window.PersonReferenceApi`; reusable `window.PersonReferencePicker`; browser calls same-origin proxy only; proxy forwards to Gateway path only. |
| Backend validation | Platform API build PASS with 0 warnings / 0 errors; focused TenantOrganization tests PASS with 60 passed / 0 failed. |
| Gateway validation | `ocelot.json` JSON validation PASS; route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Frontend validation | `Diten.Web` build PASS with existing warnings / 0 errors; `node --check` for picker PASS. |
| Remaining caveat | Focused Vitest picker test is authored but not executable until the frontend test dependency (`vitest`) is installed/restored in `frontend/Diten.Web/node_modules`. |

#### Non-Person Reference Ingress / Provider Closure

This section closes provider ownership decisions for non-person references in planning terms only. It does not add
Gateway routes, frontend code, backend code, tests, adapters, migrations, workbooks, registries, or implementation
approval.

| Reference | Provider Decision | Readiness | MOD-0251 Behavior | Fail-Closed Rule |
|---|---|---|---|---|
| Organization Unit | MOD-0288 / Platform organization-unit runtime. | Executable service API and approved Gateway/reference ingress are closed through `/api/platform/organization-units` and `/api/platform/organization-units/{everything}`. | Consume existing same-tenant organization-unit references only through Gateway; no organization ownership or local directory copy. | Unresolved, tenant-mismatched, stale/non-referenceable, unavailable provider, or unavailable Gateway route blocks relevant employment validation. |
| Position | MOD-0288 / Platform position runtime. | Executable service API and approved Gateway/reference ingress are closed through `/api/platform/positions` and `/api/platform/positions/{everything}`. | Consume existing same-tenant position references only through Gateway; no position ownership or local directory copy. | Unresolved, tenant-mismatched, stale/non-referenceable, unavailable provider, or unavailable Gateway route blocks employment/assignment snapshot validation. |
| Position Assignment | Platform primitive exists; MOD-0299 owns assignment workflow/orchestration after registry reconciliation. | Executable primitive only; workflow dependency remains deferred. | Store employee-side assignment snapshot only after valid person, organization-unit, and position references; do not depend on executable assignment workflow in the first slice. | No assignment workflow dependency is allowed until MOD-0299 is reconciled or explicitly de-scoped; ambiguous workflow dependency blocks implementation. |
| Legal Entity / Company | MDM legal-entity lookup-validation is the R1 provider unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. | Executable MDM lookup-validation, runtime port `localhost:5059`, MDM restore/build, and approved Gateway/reference ingress are closed through `/api/legal-entities` and `/api/legal-entities/{everything}`. | Consume provider contract only through Gateway; no legal entity/company master ownership in MOD-0251. | Unresolved, inactive, non-referenceable, tenant-mismatched, unavailable provider, or unavailable Gateway route blocks employment record validation. |

Gateway/reference ingress plan:

- Person ingress is conditionally closed through `/api/v1/platform/persons` and `/api/v1/platform/persons/{everything}`.
- Non-person Gateway/reference ingress is closed for organization units, positions, and legal entities through approved read-only Gateway routes.
- Position assignment Gateway/reference ingress is intentionally not exposed for the MOD-0251 first slice and remains deferred pending MOD-0299 reconciliation or explicit de-scope.
- MDM Legal Entity downstream host/port decision is planning-closed for MOD-0251 purposes as `localhost:5059`; evidence is the existing Platform API `MdmService:BaseUrl` configuration in both base and development appsettings.
- MDM runtime launch setup is recorded as available for local development on `localhost:5059` through `Diten.MdmService.Api` launch settings and the local `run_all.sh` / `run_watch.sh` scripts.
- Legal Entity Gateway ingress is closed through downstream `localhost:5059` and the approved paths `/api/legal-entities` and `/api/legal-entities/{everything}`.
- Gateway implementation remains `integration-agent`-only and must not be added from this pack.
- Runtime approval still requires position assignment workflow de-scope/reconciliation and closure or deferral of the remaining non-reference blockers.

#### Non-Person Reference Executable Evidence

| Reference | Gateway Routes | Downstream | Methods | Validation Evidence |
|---|---|---|---|---|
| Organization Unit | `/api/platform/organization-units`; `/api/platform/organization-units/{everything}` | Platform `localhost:5057` | `GET`, `OPTIONS` | `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Position | `/api/platform/positions`; `/api/platform/positions/{everything}` | Platform `localhost:5057` | `GET`, `OPTIONS` | `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Legal Entity / Company | `/api/legal-entities`; `/api/legal-entities/{everything}` | MDM `localhost:5059` | `GET`, `OPTIONS` | MDM focused restore PASS; MDM API build PASS with 0 warnings / 0 errors; `ocelot.json` JSON validation PASS; focused route assertion PASS; Gateway build PASS with 0 warnings / 0 errors. |
| Position Assignment | Not exposed for MOD-0251 first slice. | N/A | N/A | Deferred pending MOD-0299 reconciliation or explicit de-scope. |

Auth convention:

- No anonymous routes were added for non-person reference ingress.
- Gateway auth was not weakened.
- Downstream controllers remain authoritative for `[Authorize]` and permission gates:
  - `platform.organization-units.read`
  - `platform.positions.read`
  - `platform.organization.read-manager-chain`, where relevant
  - `mdm.legal-entities.read`

#### Reference Fail-Closed Rules

- Unresolved reference blocks create/submit or employment validation.
- Tenant mismatch returns not found or denied and must not disclose cross-tenant existence.
- Dependency unavailable blocks the affected runtime slice; no inferred local replacement is allowed.
- Stale, deprecated, archived, inactive, or deleted reference is not referenceable.
- Missing person reference blocks employee creation/submission.
- If MOD-0288 person lookup-validation is unavailable, MOD-0251 employee creation/submission remains blocked.
- If the Gateway person route is unavailable, person selection in the UI remains blocked.
- If the frontend picker/proxy cannot validate the selected `PersonId`, submit remains blocked.
- Auth `UserId` remains an invalid substitute for MOD-0288 `PersonId`.
- If the organization-unit, position, or legal-entity Gateway route is unavailable, the related employment validation must be blocked.
- Organization-unit, position, and legal-entity provider/ingress decisions are planning-closed, but unresolved, inactive, non-referenceable, tenant-mismatched, stale/deprecated, archived/deleted, or unavailable provider responses still block employment validation.
- Position assignment workflow remains deferred until MOD-0299 reconciliation or explicit de-scope.

*Revision 3 (review 02 F-23): in this section, in "Validation Rules" and in GWT-B07, "employee creation" means the
employee record that activation creates, not the draft session. A draft session MAY be created and saved with missing
or not-yet-validated references; Review Draft and Submit REQUIRE a valid same-tenant person and valid
organization-unit, position and legal-entity references (see GWT-B07).*

## Service Topology Blocker

The HCM backend service scaffold exists at `services/Diten.HcmService`. MOD-0251 must not be placed in `Diten.Platform`, `Diten.AuthService`, `Diten.MdmService`, `Diten.DevEnablementService`, or `Diten.EnterpriseStrategyService`, because those services do not own native HCM employee master SoR data.

P1 topology and scaffold/port decisions select a dedicated HCM service boundary at `services/Diten.HcmService` with `Diten.HcmService.Api`, `Application`, `Domain`, `Infrastructure`, `Persistence`, and `Diten.HcmService.Application.Tests` projects. Approved local downstream port is `5060`.

Employee runtime implementation remains fail-closed until a later approved MOD-0251 implementation prompt authorizes employee business behavior.

## Gateway Blocker

No HCM gateway route exists. Gateway work remains deferred to `integration-agent` after the HCM service/API route and downstream port are approved. *(Historical. Revision 2, review 01 F-17: four HCM routes exist, `ocelot.json:2788-2847`; see "Corrected Current State".)*

Frontend and browser traffic must use Gateway `5000` only. Direct calls from frontend code to service ports are prohibited.

## Frontend Topology Blocker

The tenant shell exists and remains the preferred shell for MOD-0251. No HCM Employee UI exists yet. *(Historical. Revision 2, review 01 F-5/F-17: `/HCM/Employees/Create` (P2), plus `/HCM/Employees` list and detail pages and a menu entry outside the approved scope, exist in `frontend/Diten.Web/Views/HCM/Employees/` and `frontend/Diten.Web/Controllers/HCM/EmployeesController.cs`; the pages draw their skeleton for unauthorized users (F-15).)*

Frontend planning remains:

- Employee Registry uses DataTable v2; `GoldenReferenceCompact` applies to registry/list structure only.
- Create Employee uses a wizard pattern, not a direct CRUD create-form clone.
- Employee Detail uses a section/detail page pattern, not a simple CRUD details clone.
- No HCM frontend file may be created until service/API contracts and route topology are approved.

## Dependency Contract Blockers

| Dependency | P0 Finding | Blocker Rule |
|---|---|---|
| `MOD-0023 Workflow` | Contract is not executable for MOD-0251; current inspected adapters are mock/disabled or not implemented. | Activation workflow and approval-decision implementation remain blocked. |
| `MOD-0031 Evidence Linking Service` | Contract is not executable for MOD-0251; evidence dependency is mock/disabled or not implemented. | Evidence completion and evidence-linked activation remain blocked. |
| `MOD-0028 Documentation / Evidence Management` | Evidence/document integration is unavailable for executable MOD-0251 use. | Evidence attachment remains blocked. |
| `MOD-0030 Records Management / Retention / Legal Hold` | Retention/legal hold integration is unavailable for MOD-0251 execution. | Retention-aware destructive flows and legal-hold behavior remain blocked. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Masking contract is unavailable. | Sensitive response shaping and sensitive-read paths remain blocked. |
| Tokenization/security service | Contract is unavailable. | Government identifier capture remains disabled/blocked. |
| `MOD-0288 Organization, Person & Position Directory` | Person backend + Gateway + frontend proxy/picker are conditionally executable; Vitest verification remains blocked by missing frontend test dependency. Organization-unit and position provider decisions plus read-only Gateway ingress are planning-closed to MOD-0288 / Platform runtime. | Employee creation may consume existing same-tenant `PersonId` only through Gateway/MVC proxy validation; employment validation may consume organization-unit and position references only through approved Gateway paths. |
| Legal Entity / Company provider | R1 provider decision is planning-closed to executable MDM legal-entity lookup-validation unless a later Blueprint/domain decision assigns a dedicated MOD-0220/legal-entity provider. MDM runtime port, restore/build, and read-only Gateway ingress are closed. | Employment record validation may consume only the approved provider contract through Gateway; unresolved/inactive/non-referenceable/tenant-mismatched/unavailable legal entity blocks validation. |

## RBAC / Audit Blockers

- RBAC infrastructure exists and the MOD-0251 permission catalog, role-permission matrix, protected action map, and fail-closed rules are now closed for planning.
- MOD-0251 permission seed implementation, policy registration, controller attributes, and endpoint enforcement remain blocked until this pack is promoted and a separate approved implementation prompt authorizes them. *(Historical. Revision 2, review 01 F-13: the seed is implemented in Auth `DataSeeder` with all 14 keys, wider than P2; keep or narrow is an open decision.)*
- Audit infrastructure exists and the MOD-0251 audit event catalog, safe payload matrix, and fail-closed rules are now closed for planning.
- MOD-0251 audit behavior wiring, event emitters, critical category registration, and implementation tests remain blocked until this pack is promoted and a separate approved implementation prompt authorizes them. *(Revision 2, review 01 F-4: today draft writes use a log-only audit service; the governed append client exists but is not called. Real wiring is DL-4. Revision 3, F-22: draft-write wiring moves to DL-2 (path `b`, shared forwarder); DL-4 wires only its own new commands - see "Audited Events". Revision 4, F-26: draft-write wiring is DL-7 (E5), no longer DL-2.)*
- Critical writes, sensitive reads, exports, workflow decisions, and access denials must remain blocked or fail-closed until the implementation wiring exists.

## MOD-0299 Defer / Reconcile Decision

Decision selected: **Option A - first-slice de-scope**.

Current conflict:

- Blueprint master 5 and Blueprint master 7 identify `MOD-0299` as `Position & Organization Assignment`.
- The MOD-0251 specification uses `MOD-0299` as the owner of assignment workflow/orchestration.
- The current module ID registry maps `MOD-0299` to deprecated SaaS Billing & Invoicing alias `CAND-CAP-0005`. *(Historical. Revision 2, review 01 F-11: `module-id-registry.md:199` keeps that meaning only as a non-executable alias migrating to MOD-0364 SaaS Billing & Invoicing and states canonical MOD-0299 stays Position & Organization Assignment; no identity conflict remains.)*
- MOD-0251 must not bind to any executable `MOD-0299` assignment workflow/orchestration contract until the registry is reconciled by Enterprise Architect/user approval. *(Revision 2: the binding stays excluded because assignment workflow is out of scope for P2 and for DL-2 to DL-6, not because of an identity conflict.)*

First activation slice decision:

- Executable `MOD-0299` assignment workflow/orchestration is explicitly excluded from the MOD-0251 first approved draft/reference-validation slice.
- MOD-0251 may store only the employee-side job assignment / employment assignment snapshot after the person, organization-unit, position, and legal entity references are validated through their approved provider/Gateway paths.
- Position assignment workflow remains a later R1 or post-first-slice dependency.
- Position assignment Gateway ingress remains intentionally not exposed for MOD-0251 first-slice work.
- Registry reconciliation is still required before any executable `MOD-0299` workflow/orchestration implementation, dependency binding, Gateway route, or assignment console work.

Exact future registry reconciliation prompt, requiring EA/user approval before use:

`MOD0299-REGISTRY-RECONCILE-POSITION-ORG-ASSIGNMENT-M1`

Scope: reconcile `execution/registries/module-id-registry.md` so Blueprint `MOD-0299 Position & Organization Assignment` is no longer blocked by the deprecated SaaS Billing alias / `CAND-CAP-0005`, without changing MOD-0251 implementation code. *(Historical. Revision 2: the registry already carries this reconciliation in row 199; this prompt is not needed for identity.)*

## Workflow / Evidence / Masking / Tokenization Dependency Decision

This section records first-slice governance decisions only. It does not create backend code, frontend code, gateway routes, tests, migrations, adapters, or implementation approval.

## Workflow First-Slice De-scope Decision

Prompt ID: `MOD0251-P1-WORKFLOW-FIRST-SLICE-DESCOPE-PACK-UPDATE-M1`.

Decision selected: **Option A - de-scope approval from first approved slice**.

The first approved implementation slice is redefined as a draft/reference-validation slice only:

- Create Employee Draft.
- Save Draft steps.
- Validate same-tenant person, organization-unit, position, and legal-entity references through approved provider/Gateway paths.
- Review Draft.
- Reload persisted draft.

Explicitly excluded from the first approved slice:

- Submit for approval.
- MOD-0023 workflow start.
- MOD-0023 approval/rejection decision processing.
- Active transition.
- `employee.created` event/outbox behavior.
- Approval audit events such as `employee.submitted_for_approval`, `employee.approved`, and `employee.rejected`, except draft/review audit events where applicable.

Activation remains a later sequence blocked until an executable MOD-0023 workflow contract exists. MOD-0251 must not implement local approval logic, independent approval state, or an approval bypass. This de-scope moves the pack closer to approval for a draft/reference-validation slice only; it does not approve activation, employee creation as Active, workflow integration, gateway routes, frontend pages, migrations, or employee business implementation.

## P1 Draft / Reference Validation Contract

Prompt ID: `MOD0251-P1-CONTRACTS-M1`.

This section closes contract documentation for the first approved draft/reference-validation slice only. It does not create backend code, frontend code, gateway routes, tests, migrations, employee entities, employee APIs, workbooks, registry entries, or implementation approval.

### First-Slice API Contract Stubs

| Method | Route | Purpose | Scope |
|---|---|---|---|
| `POST` | `/api/v1/hcm/employees/drafts` | Create a tenant-scoped employee draft session. | First slice |
| `PATCH` | `/api/v1/hcm/employees/drafts/{draftSessionId}` | Save one draft step or partial draft payload with ETag and idempotency. | First slice |
| `GET` | `/api/v1/hcm/employees/drafts/{draftSessionId}` | Reload persisted draft, step state, validation summary, and current ETag. | First slice |
| `POST` | `/api/v1/hcm/employees/drafts/{draftSessionId}/validate-references` | Validate person, organization-unit, position, and legal-entity references through approved provider/Gateway paths. | First slice |
| `POST` | `/api/v1/hcm/employees/drafts/{draftSessionId}/review` | Persist non-submit review state and return readiness summary. This route must not submit, activate, or start workflow. | Optional first slice |

Out of scope for this contract: submit, approval, activate, reject, `employee.created` event/outbox behavior, employee `Active` state, evidence link, export, status change, and data-quality workflow beyond a duplicate-warning/readiness placeholder.

### DTO Contract Stubs

| DTO | Direction | Required Shape |
|---|---|---|
| `EmployeeDraftCreateRequest` | Request | Optional `source_context`, optional `client_reference`, `idempotency_key`; no PII payload at creation; no client-supplied `tenant_id`. |
| `EmployeeDraftCreateResponse` | Response | `draft_session_id`, `draft_schema_version`, `current_step`, `step_statuses`, `validation_summary`, `version`, `etag`, `created_at`. |
| `EmployeeDraftPatchRequest` | Request | `step_code`, `payload_schema_version`, `step_payload`, `client_validation_state`, `idempotency_key`; `If-Match` header required; no raw government identifier values. |
| `EmployeeDraftResponse` | Response | `draft_session_id`, `draft_schema_version`, `current_step`, redacted `steps`, `step_statuses`, `reference_validation_summary`, `review_state`, `version`, `etag`, `updated_at`, `expires_at`. |
| `ReferenceValidationRequest` | Request | Optional explicit references to validate: `person_id`, `organization_unit_id`, `position_id`, `legal_entity_id`; no Auth `user_id`; no client-supplied tenant authority. |
| `ReferenceValidationResponse` | Response | Per-reference result with `reference_type`, `reference_id`, `status`, `is_referenceable`, `provider`, `reason_code`, safe display metadata, and aggregate `can_review`. |
| `DraftReviewRequest` | Request | `idempotency_key`, review acknowledgement flags, optional duplicate-warning acknowledgement, current `etag`; no submit or approval fields. |
| `DraftReviewResponse` | Response | `draft_session_id`, `review_state`, `can_submit_later`, `blocking_reasons`, `validation_summary`, `reference_validation_summary`, `version`, `etag`; submit/activation fields omitted. |
| `ProblemDetails` | Error | Standard problem-details shape with `type`, `title`, `status`, `code`, `trace_id`, safe `errors[]` entries containing `field`, `code`, and redacted `message`. |

### Validation Contract

- Draft save may persist incomplete legal/profile/employment steps, but Review Draft requires the draft to have required first-slice fields needed for reference validation: `person_id`, legal name fields needed for duplicate/readiness checks, worker type, employment type, hire date where policy requires it, organization-unit reference, position reference, legal-entity/company reference, and sensitivity level.
- Person reference validation must use the approved MOD-0288 person Gateway/MVC proxy path. MOD-0251 must consume an existing same-tenant `PersonId` only and must never substitute Auth `UserId`.
- Organization-unit validation must use the approved Platform Gateway path `/api/platform/organization-units` or `/api/platform/organization-units/{everything}`.
- Position validation must use the approved Platform Gateway path `/api/platform/positions` or `/api/platform/positions/{everything}`.
- Legal-entity validation must use the approved MDM Gateway path `/api/legal-entities` or `/api/legal-entities/{everything}`.
- Client-supplied `TenantId` is ignored/rejected; tenant authority comes only from server auth/tenant context.
- Government identifier capture is disabled; raw government identifiers are rejected and must not be stored, logged, audited, or returned.
- Sensitive fields are denied, omitted, or masked unless and until an executable MOD-0314 contract permits them.
- Evidence step is skipped/disabled for this slice; no evidence upload, evidence link, document link metadata, or orphan evidence state is allowed.
- Duplicate detection may return a readiness warning/blocking reason only; data-quality assignment/resolution workflow is out of scope.

### Persistence Contract

- First-slice persistence object: `EmployeeDraftSession` only.
- `draft_schema_version`: `employee-create-wizard.v1`.
- Draft payload must be encrypted or redacted according to HCM sensitive-data policy; audit/log payloads must carry safe metadata only.
- `TenantId` is server-side and mandatory on persisted draft/session records.
- Optimistic concurrency is mandatory through `version` and `etag`; stale writes return conflict.
- Idempotency key is mandatory for create, save, validate-references, and review operations.
- Reload persistence must return saved draft steps, reference-validation summary, review state, ETag/version, and safe audit references after service restart/reload. *(Revision 4, AK-32: the safe audit reference part is DL-7, E5; the rest DL-2, E4.)*
- No Active employee record, employment record, status-history activation row, approval state, or employee-created event/outbox record may be created in this slice.

### Audit Contract For This Slice

| Event | Trigger | Safe Payload Only |
|---|---|---|
| `employee_draft.created` | Draft session created. | `tenant_id`, `actor_id`, `draft_session_id`, `correlation_id`, `idempotency_key_hash`, `draft_schema_version`. |
| `employee_draft.updated` | Draft step saved. | `tenant_id`, `actor_id`, `draft_session_id`, `step_code`, `changed_field_names`, `version`, `correlation_id`. |
| `employee_draft.references_validated` | Reference validation completed. | `tenant_id`, `actor_id`, `draft_session_id`, reference types checked, provider names, aggregate result, reason codes. |
| `employee_draft.reviewed` | Non-submit review state persisted. | `tenant_id`, `actor_id`, `draft_session_id`, review result, blocking reason codes, version, `correlation_id`. |
| `employee.access_denied` | RBAC, tenant, masking, dependency, or validation policy denies action. | `tenant_id`, `actor_id`, action, permission key, denial reason code, `correlation_id`; no PII values. |

Prohibited audit payloads: raw PII values, raw government identifiers, token values, full draft payloads, document contents, provider response bodies, unredacted free text, and exported row data.

### Fail-Closed Behavior

- Missing person Gateway/proxy route blocks person validation and draft review readiness.
- Missing organization-unit, position, or legal-entity Gateway route blocks the related reference validation and draft review readiness.
- Dependency unavailable, timeout, ambiguous status, stale/deprecated/inactive reference, or tenant mismatch blocks Review Draft.
- Tenant mismatch returns not found or denied without cross-tenant existence disclosure.
- Stale ETag returns conflict and must not overwrite newer draft data.
- Idempotency replay returns the original create/save/validate/review result or a controlled replay response without duplicate draft, review, validation, or audit effects. *(Revision 4: the "no duplicate audit effect" part is DL-7, E5.)*
- Audit failure for critical draft writes blocks or rolls back the write. *(Revision 4: DL-7, E5. AK-28: on path `b` "audit failure" is a failure to write the local outbox row in the command transaction; Platform delivery `Retry` / `Rejected` does not block the write - see "Audit Fail-Closed Rules".)*
- MOD-0023 unavailable keeps submit/approval/Active unavailable; no local approval substitute is allowed.

### Contract-Test Plan

No implementation tests are created by this prompt. Future implementation prompts must add focused tests for:

- Draft create, save, get/reload, validate-references, and review.
- RBAC denial and tenant isolation.
- ETag conflict and idempotency replay.
- Safe audit payload emission and audit failure behavior.
- Person, organization-unit, position, and legal-entity validation success/failure paths.
- Government identifier rejection and sensitive-field mask/deny behavior.
- Confirming no submit, approval, Active transition, evidence link, export, status change, or data-quality workflow behavior appears in the first slice.

## Conditional Promotion Decision

Prompt ID: `MOD0251-P1-PACK-CONDITIONAL-PROMOTION-M1`.

Decision: `status: approved` with `approval_status: conditionally-approved-for-draft-reference-validation-slice`.

This promotion is scoped only to the reduced draft/reference-validation slice. It is not approval for activation, submit/approval, full lifecycle, evidence/export/status/data-quality flows, or full MOD-0251 implementation.

### Approved Slice Scope

The conditionally approved slice includes only:

- Draft create.
- Draft save/update.
- Draft reload.
- Person, organization-unit, position, and legal-entity reference validation through approved provider/Gateway paths.
- Review Draft as a non-submit, non-activation readiness state.
- Draft-slice audit events.
- RBAC enforcement for draft/reference actions.
- ETag and optimistic concurrency handling.
- Idempotency for create/save/validate/review.
- Tenant isolation and fail-closed tenant mismatch behavior.

### Explicit Exclusions

This conditional promotion does not approve:

- Submit for approval.
- MOD-0023 workflow start.
- Approval/rejection decision processing.
- Active transition.
- `employee.created` event/outbox behavior.
- Evidence upload/link.
- Export.
- Status change.
- Data-quality workflow beyond duplicate-warning/readiness placeholder.
- Employee activation.
- Full HR lifecycle.

### Promotion Conditions / Caveats

- Frontend person picker Vitest test exists but cannot run until `vitest` is installed/restored in `frontend/Diten.Web`.
- MOD-0023 workflow remains non-executable and blocks activation only.
- MOD-0031, MOD-0028, and MOD-0030 evidence/retention remain deferred/fail-closed.
- MOD-0314 masking contract is unavailable; sensitive reads deny or mask by default.
- Tokenization is unavailable; government identifier capture is disabled.
- MOD-0299 assignment workflow remains de-scoped from the first approved slice; registry reconciliation remains a future governance item.
- Permission seed and audit wiring are implementation tasks after promotion and are allowed only for the approved draft/reference-validation slice. *(Revision 2: the seed was implemented with all 14 keys, wider than this slice (F-13); audit wiring is log-only (F-4).)*
- HCM Gateway route for employee APIs does not exist yet and must wait until employee APIs exist. *(Historical. Revision 2: four HCM routes exist, including a general `/employees/{everything}` route (F-6).)*
- This promotion is not authorization for activation or full MOD-0251 implementation.

### Next Implementation Boundary

The next implementation prompt may only implement:

- Draft session persistence.
- Draft create/save/get APIs.
- Reference validation using existing Gateway/provider paths.
- Review-only non-submit endpoint, if included.
- RBAC and audit for those draft actions.

The next implementation prompt must not create:

- Active employee record behavior.
- Submit/approval.
- MOD-0023 workflow integration.
- `employee.created` event/outbox behavior.
- Evidence upload/link.
- Export/status/data-quality workflow.
- Frontend pages or Gateway routes unless separately approved in their own prompts.

| Dependency | Current State | First-Slice Decision | Fail-Closed Behavior | Approval Impact |
|---|---|---|---|---|
| `MOD-0023 Workflow` | Executable workflow start / decision contract for MOD-0251 is not proven. | Approval is removed from the first approved draft/reference-validation slice. Approval remains owned by MOD-0023 for any later activation sequence. MOD-0251 must not replace MOD-0023 with local approval logic or independent workflow state. | Submit/approval is disabled or blocked when MOD-0023 is unavailable. Employee must not become Active without a valid MOD-0023 decision contract. | Does not block approval of the draft/reference-validation slice. Still blocks activation implementation. |
| `MOD-0031 Evidence Linking` | Executable evidence link contract for MOD-0251 is not proven. | Evidence integration is deferred from the first approved draft/reference-validation slice when `activation_evidence_required = false`. Evidence-linked activation remains blocked when tenant policy requires evidence. | Evidence step is disabled/skipped when evidence is not required. Submit/approval is blocked when policy requires evidence but MOD-0031/MOD-0028/MOD-0030 contracts are unavailable. | Does not block first-slice approval only if evidence is formally disabled for that slice and policy is `activation_evidence_required = false`. |
| `MOD-0028 Documentation / Evidence Management` | Document/evidence attachment execution remains deferred for MOD-0251. | No upload/link UI in the first approved draft/reference-validation slice. Evidence attachment is a later R1 sequence. | No shell upload controls. No orphan document-link metadata. No document/content ownership in MOD-0251. | Deferred from first slice; required before evidence attachment implementation. |
| `MOD-0030 Records / Retention / Legal Hold` | Retention/legal-hold execution for employee evidence/destructive lifecycle is not closed. | Destructive lifecycle and legal-hold-sensitive flows remain deferred. No deletion, anonymization, destructive retention, or legal-hold-sensitive behavior in the first approved draft/reference-validation slice. | Destructive or legal-hold-sensitive flows are blocked until the contract exists. No retention inference is allowed. | Deferred from first slice; required before destructive lifecycle, retention, legal hold, and evidence retention behavior. |
| `MOD-0314 HR Governance & Sensitive Access Controls` | Executable masking / sensitive access policy hook is not proven. | Sensitive reads are denied or masked by default until MOD-0314 contract is available. First slice may expose only safe/minimal non-sensitive fields. | No sensitive PII is returned when policy hook is unavailable. `view_sensitive` is blocked or returns a masked/non-sensitive response. | Still blocks sensitive-read implementation; first slice may proceed only with non-sensitive/minimal response shapes. |
| Tokenization/security service | Government identifier tokenization contract is unavailable. | Government identifier capture is disabled/excluded from the first slice. Raw government identifier storage remains prohibited. | Government identifier fields are hidden, disabled, or rejected. No raw identifier or token value may appear in audit, logs, traces, events, or persisted employee data. | Government identifier capture remains blocked until tokenization/security contract is identified and approved. |

Approval gate interpretation:

- MOD-0023 does not block approval of the de-scoped draft/reference-validation slice because submit/approval and Active-state transition are excluded.
- MOD-0023 remains a hard blocker for any later approved slice that includes submit/approval or Active-state transition.
- MOD-0031/MOD-0028/MOD-0030 are formally deferred for first-slice evidence behavior only when `activation_evidence_required = false`; evidence-required tenants remain blocked.
- MOD-0314 absence requires default deny/mask for sensitive reads and non-sensitive-only response shapes.
- Tokenization absence requires government identifier capture to remain disabled and raw identifier storage prohibited.
- These decisions do not remove implementation-time checks; they define fail-closed behavior until executable dependency contracts exist.

## Approval Readiness

This pack was conditionally approved for a draft/reference-validation slice only *(Revisions 1 to 4; Revision 5: see the Revision 5 note)*. The P1 draft/reference-validation API, DTO, validation, persistence, audit, fail-closed, and future contract-test plan are closed in this pack. It cannot be marked `ready-for-dev` until remaining approval caveats are resolved or explicitly accepted.

Approval requires closure or formal fail-closed deferral of every blocker recorded in P0, GC-01/02/03, and P1 topology contract. At minimum, approval remains blocked until:

- HCM service scaffold exists at `services/Diten.HcmService` on approved local port `5060`; MOD-0251 employee runtime behavior still requires a separate approved implementation prompt.
- MOD-0299 assignment workflow/orchestration is explicitly de-scoped from the first approved draft/reference-validation slice. Registry reconciliation remains required before any executable MOD-0299 workflow/orchestration work.
- MOD-0288/reference contract is conditionally ready for person references and non-person provider/ingress decisions are closed for organization unit, position, and legal entity/company. It remains a blocker for approval only to the extent the frontend person-picker Vitest caveat must be accepted/resolved and position assignment workflow remains deferred/de-scoped.
- MOD-0288 person lookup/read/lookup-validation contract is confirmed for same-tenant employee creation and submission through Gateway/MVC proxy paths only; Auth `UserId` substitution remains prohibited.
- Company/legal-entity owner/provider decision is approved for R1 to MDM legal-entity lookup-validation, with future override allowed only by Blueprint/domain decision.
- Gateway/reference ingress is confirmed for organization unit, position, and legal entity validation through approved runtime paths. Position assignment ingress/workflow remains intentionally not exposed and is de-scoped from the first approved draft/reference-validation slice.
- Position assignment workflow remains deferred beyond the first approved draft/reference-validation slice until MOD-0299 registry reconciliation and a separately approved implementation sequence.
- MOD-0023 workflow contract no longer blocks approval of the draft/reference-validation slice because submit/approval and Active-state transition are explicitly removed from first approved scope.
- MOD-0023 workflow contract remains a blocker for any activation implementation, including submit for approval, approval/rejection decision processing, Active transition, `employee.created` event/outbox behavior, and approval audit events.
- P1 draft/reference-validation contract closure is complete for API stubs, DTO stubs, validation rules, persistence rules, audit events, fail-closed behavior, and future contract-test plan.
- MOD-0031 evidence contract remains unavailable, but evidence is formally deferred with disabled/skipped UI/API behavior when `activation_evidence_required = false`; evidence-required activation remains blocked.
- MOD-0028/MOD-0030 evidence, retention, and legal-hold behavior is formally deferred from the first slice with upload/link/destructive/legal-hold-sensitive flows blocked.
- MOD-0314 masking contract remains unavailable, so sensitive reads must be denied or return non-sensitive/masked shapes only.
- Tokenization/security contract remains unavailable, so government identifier capture remains disabled and raw identifier storage is prohibited.
- Given/When/Then acceptance criteria are added in this draft pack.
- MOD-0251 RBAC/audit contract closure planning is complete in this draft pack.
- MOD-0251 permission seed implementation remains pending after pack promotion and implementation approval. *(Historical. Revision 2, F-13: implemented in Auth `DataSeeder`, 14 keys, wider than P2; not pending.)*
- MOD-0251 audit wiring implementation remains pending after pack promotion and implementation approval. *(Revision 2, F-4: still not wired - log-only; DL-4. Revision 3, F-22: draft writes move to DL-2. Revision 4, F-26: draft writes are DL-7.)*
- *Revision 2 approval gate: approval of this revision additionally needs a new independent review of this version with
  no open ENGEL, an approval row, and the open decisions each slice depends on (see "Open Decisions (Owner)").*
- Submit/approval/activation contracts remain deferred and must not be implemented until executable MOD-0023 workflow contract exists.

## P2 Draft / Reference Validation Implementation Evidence

Prompt ID: `MOD0251-P2-EVIDENCE-PACK-UPDATE-M1`.

> **Historical - Revision 2 (review 01, F-7, F-17).** The results in this section and in "P2 Conditional Closure /
> Handback" are narrative; no recorded evidence in the repository refs examined (review 01, F-7). Test counts here
> ("5 passed", "Vitest 3 tests", "Vitest 13 tests") belong to other evidence periods; current count: 52 test methods /
> 60 cases (2026-10-27 run 60/60, build 0 warnings). "Gateway evidence is limited to draft ingress" is incorrect:
> the Gateway also carries the general employees routes (F-6). Current state: "Corrected Current State".

This section records evidence for the conditionally approved MOD-0251 draft/reference-validation implementation slice only. It does not authorize or record submit, approval, activation, Active employee behavior, evidence upload/link, export, status change, Data Quality Queue behavior, workbook changes, registry changes, or `.antigravity` changes.

### Backend Evidence

| Evidence Area | Result |
|---|---|
| Implemented runtime slice | HCM draft/reference APIs for draft create, draft save/update, draft reload, reference validation, and non-submit review scope. |
| Service owner | `Diten.HcmService` under `services/Diten.HcmService`. |
| Approved port | `5060`. |
| Build validation | HCM build PASS with 0 warnings / 0 errors. |
| Focused tests | HCM focused tests PASS: 5 passed / 0 failed. |
| Scope guard | No submit, approval, activation, evidence/export/status/DQ behavior is included in the approved P2 evidence. |

### Gateway Evidence

| Evidence Area | Result |
|---|---|
| Gateway route family | `/api/v1/hcm/employees/drafts`; `/api/v1/hcm/employees/drafts/{everything}`. |
| Downstream service | HCM downstream `localhost:5060`. |
| Gateway validation | Gateway build PASS with 0 warnings / 0 errors. |
| Scope guard | Gateway evidence is limited to draft/reference-validation ingress; no submit/approval/activation routes are recorded as approved. |

### Frontend Evidence

| Evidence Area | Result |
|---|---|
| Page route | `/HCM/Employees/Create`. |
| MVC proxy | Same-origin proxy under `/HCM/Employees/drafts/api...`. |
| Gateway-only path | Frontend proxy forwards to Gateway `/api/v1/hcm/employees/drafts...`; browser code must not call service ports directly. |
| Build validation | `Diten.Web` build PASS with 0 warnings / 0 errors. |
| Focused JS tests | Focused Vitest PASS: 3 tests. |
| Scope guard | UI scope is draft/reference-validation only; submit, approval, activation, evidence upload/link, export, status change, Data Quality Queue, and government identifier controls remain excluded or disabled. |

### HCM Startup Evidence

| Evidence Area | Result |
|---|---|
| Startup blocker | HCM startup hang was isolated to early host-builder startup before normal binding. |
| Fix recorded | HCM API startup uses `WebApplication.CreateEmptyBuilder(...)` with explicit configuration and Kestrel/URL setup instead of the default builder path that hung in local runtime. |
| Health check | HCM bound on `5060`; `/health` returned 200. |
| Runtime rule | Runtime smoke must use the explicit local .NET host rather than `DOTNET_ROLL_FORWARD=Major`, which can roll `net8.0` apps to a later runtime. |

### Unauthenticated Runtime Browser Smoke Evidence

| Evidence Area | Result |
|---|---|
| Services bound | HCM `5060`, Gateway `5000`, and Frontend `5001` bound during smoke retry. |
| Route reachability | `/HCM/Employees/Create` was reachable through Frontend `5001`. |
| Unauthenticated behavior | Route returned controlled redirect to `/account/login?ReturnUrl=%2FHCM%2FEmployees%2FCreate`. |
| Browser page | Login page loaded with title `Sign In - Di10`. |
| Console | No browser console errors or warnings were captured. |
| Direct service-port check | Browser-visible page URLs did not reference service ports `5056`, `5057`, `5059`, or `5060`. |
| Process cleanup | Smoke-started services were stopped cleanly; final listener check found no active listeners on `5000`, `5001`, or `5060`. |

### Final Authenticated Browser Runtime Evidence

Final authenticated browser replay closed the prior runtime evidence gap after the legal-entity helper fix.

| Evidence Area | Result |
|---|---|
| Tenant shell | Visible with `Employee Drafts`; `canCreateDraft=true`. |
| Draft create | PASS; draft session and ETag present. |
| Save/update | PASS; `If-Match` used and version advanced. |
| Reload | PASS; persisted draft reloaded by `draftSessionId`. |
| Reference validation | PASS; person, organization unit, position, and legal entity all valid/referenceable. |
| Legal entity picker | PASS; approved fixture selected and hidden legal entity value retained through save/reload. |
| Review | PASS; review state `reviewed`, `blockingReasons=[]`, and `canSubmitLater=false`. |
| Scope guard | PASS; no actionable submit, approval, activation, evidence upload/link, export/status/Data Quality Queue, or government identifier controls. |
| Direct service-port check | PASS; no direct browser references to service ports `5056`, `5057`, `5059`, or `5060`. |
| Console | PASS; no console errors. |

This evidence does not expand implementation scope and must not be used to add submit, approval, activation, evidence/export/status/DQ behavior, direct service-port calls, or broad seed/test data outside a later approved scope.

## P2 Conditional Closure / Handback

Prompt ID: `MOD0251-P2-CONDITIONAL-HANDBACK-M1`; final browser evidence reconciled by `MOD0251-P2-GOVERNANCE-WORDING-RECONCILE-M1`.

Final P2 verdict: `PASS`. *(Historical. Revision 2: narrative; no recorded evidence in the repository refs examined (review 01, F-7). Review 01 also found code defects in the slice (F-1, F-2, F-3, F-15, F-16). P2 is not closed; its correction and re-proof is DL-2.)*

The MOD-0251 P2 draft/reference-validation implementation slice is closed as PASS for the approved slice only. The pack remains approved only for the draft/reference-validation scope: draft create, draft save/update, draft reload, person/organization-unit/position/legal-entity reference validation, and non-submit review. This handback does not approve submit, MOD-0023 workflow start, approval/rejection processing, Active transition, `employee.created` event/outbox behavior, evidence upload/link, export, status change, Data Quality Queue behavior, or government identifier capture.

### Closed P2 Evidence

| Evidence Area | Closure Status |
|---|---|
| Backend build/tests | Closed. HCM draft/reference API implementation evidence recorded; HCM build passed with 0 warnings / 0 errors; focused tests passed 5 / 0. |
| Gateway routes/build | Closed. HCM draft Gateway route family `/api/v1/hcm/employees/drafts` and `/api/v1/hcm/employees/drafts/{everything}` recorded with downstream `localhost:5060`; Gateway build passed with 0 warnings / 0 errors. |
| Frontend build/Vitest | Closed. `/HCM/Employees/Create`, MVC proxy `/HCM/Employees/drafts/api...`, legal-entity helper retention, and Gateway-only forwarding recorded; `Diten.Web` build passed with 0 warnings / 0 errors; focused Vitest passed 13 tests. |
| HCM startup `/health` | Closed. Startup blocker fixed; HCM bound on `5060`; `/health` returned 200. |
| Unauthenticated browser smoke | Closed. Frontend route was reachable, redirected unauthenticated users to login, login page loaded, no console errors/warnings were captured, and no direct browser calls to service ports were observed. |
| Final authenticated browser replay | Closed. Create, save/update with ETag, reload, all four references valid, review `reviewed`, `blockingReasons=[]`, no out-of-scope controls, no direct service-port references, and no console errors. |

### Runtime Sign-Off Status

*(Historical. Revision 2: the sign-off below is narrative; no recorded evidence in the repository refs examined (review 01, F-7).)*

Authenticated tenant browser smoke is closed for the P2 draft/reference-validation slice. Runtime sign-off remains limited to this slice and must not be used to add submit, approval, activation, evidence/export/status/DQ behavior, direct service-port calls, or broad seed/test data outside a later approved scope.

## P2 Scope Containment Baseline

Prompt ID: `MOD0251-OUT-OF-SCOPE-LIFECYCLE-REWORK-M1`.

Containment verdict: `PASS`. *(Historical. Revision 2: narrative; no recorded evidence in the repository refs examined (review 01, F-7). Submit/workflow-decision containment is confirmed in code (review 01 F-10, inspection note item 1); "Gateway remains unchanged" below is incorrect (F-6); registry/detail read surfaces remain in code outside scope (F-5).)*

The contained implementation baseline after the rework is:

- P2 draft create/save/reload/validate/review remains preserved.
- Submit for approval is controlled-blocked and does not start workflow.
- Workflow decision consumption is controlled-blocked and cannot mutate lifecycle state.
- Workflow start client is not runtime-registered for MOD-0251.
- Active employee materialization and `employee.activated` emission are not present in the runtime path.
- Status/evidence/audit placeholder UI is removed from the detail surface.
- Registry export affordance is removed pending governed export/masking contract closure.
- Gateway remains unchanged; backend route containment is the active control.

### Next-Sequence Decision Table

| Sequence | Status | Owner / Dependency | Required Decision Before Runtime Work |
|---|---|---|---|
| P2 runtime smoke closure | CONDITIONAL PASS; closed only for Create Draft / reference-validation | MOD-0251 / HCM | Preserve draft create/save/reload/reference-validation/review only. Negative submit/workflow proof is test/source-backed rather than browser-fetch-backed and does not authorize lifecycle work. |
| Registry read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 | Approve exact search/list DTO, non-sensitive columns, RBAC, tenant isolation, empty/error/forbidden states, no export/status/evidence/actions, and runtime smoke scope. |
| Detail read-only sequence | Deferred, not P2 support | MOD-0251 + MOD-0314 | Approve exact detail projection, sensitive-field policy, read-only affordance rules, no status/evidence/audit placeholders, and runtime smoke scope. |
| Lifecycle contract authorization | Deferred | MOD-0251 + HCM product owner | Approve exact submit, approval, activation, persistence, audit, idempotency, and rollback contract before code changes. |
| MOD-0023 callback-source authorization closure | Deferred blocker | MOD-0023 Workflow | Define authenticated callback/event source, replay/idempotency, tenant validation, and allowed decision payloads before workflow decisions are consumed. |
| `employee.created.v1` event contract closure | Deferred blocker | MOD-0251 event owner + downstream HCM consumers | Close event name, schema, safe payload, outbox behavior, replay/idempotency, and consumer expectations before any employee-created/activation event is emitted. |
| MOD-0314 masking contract closure | Deferred blocker for sensitive reads/export | MOD-0314 HR Governance & Sensitive Access Controls | Close masking/deny policy evaluation and response shaping before sensitive detail, registry, or export fields are enabled. |
| Evidence/retention contract closure | Deferred blocker | MOD-0028 Documentation, MOD-0031 Evidence Linking, MOD-0030 Records/Retention | Close evidence attachment, evidence-link metadata, retention/legal-hold, and failure behavior before evidence controls or evidence-gated activation are enabled. |
| Export/masking contract closure | Deferred blocker | MOD-0251 + MOD-0314 + MOD-0021 Audit | Close governed export limits, masking, audit requested/completed events, file/job ownership, and retention before export affordances or endpoints are enabled. |
| Data-quality queue sequence | Deferred | MOD-0251 + data governance owner | Approve queue entity, duplicate policy, assignment/resolution workflow, audit, and UI scope before queue runtime behavior is added. |

*Revision 2: this table is historical sequencing. The delivery order is now the "Teslim dilimleri" table (DL-1 to
DL-6). The `employee.created.v1` row is superseded by DL-6 and open decision K-2 (proposal `hcm.employee.activated.v1`
and three further events); the "P2 runtime smoke closure" row is not closed (F-7) and moves to DL-2. Revision 4 (F-26): P2 closure needs both
DL-2 and DL-7.*

## P4 Employee Registry Read-Only Scope Contract

Prompt ID: `MOD0251-P4-REGISTRY-READ-SCOPE-CONTRACT-M1`.

This module pack authorizes governance planning only for the later `MOD0251-P4-REGISTRY-READ-M1` implementation prompt. It does not authorize runtime changes by itself and does not make Employee Registry part of P2 evidence.

*Revision 2: registry/detail read code already exists (F-5); this contract is the basis of DL-3, which starts only after
the owner decides to retro-approve or close that code (open decision F-5). The registry repository today filters by
`TenantId` and `IsDeleted` only and has no approved-record / fixture separation (`EmployeeRepository.cs:127`); "Allowed
Records" below is therefore not met by the current code.*

### Registry Purpose and Boundary

Employee Registry is a tenant-shell, read-only DataTable for authorized HR users to search and scan approved employee records. It is not lifecycle execution, not draft review, not Employee Detail editing, not export, and not a status/evidence/audit panel.

First registry read scope must exclude:

- submit, approval/rejection, activation, and workflow decision processing;
- status mutation and status-history timeline;
- evidence upload/link/retention controls;
- audit panel/query display;
- export/download/print/DataTables local export;
- Data Quality Queue workflow;
- government identifier capture, tokenization, display, or search;
- Employee Detail row action unless a separate detail-read sequence is approved.

### Allowed Records

The first registry read implementation may show only approved registry-safe employee records:

- migrated or seeded read-only records explicitly approved for registry read;
- controlled local/test fixture records created and cleaned up through an approved fixture pattern;
- future Active employee records only after a later lifecycle/activation sequence exists and has its own runtime proof.

Draft-only records and P2 draft sessions must not surface through Employee Registry. If no approved registry-safe records exist, the registry must show an empty state.

### API Boundary

Target API stub: `GET /api/v1/hcm/employees`.

Required contract:

- Permission: `mod0251.employee.search`.
- Server-side tenant resolution only; client `TenantId` is ignored or rejected.
- `Response<T>` envelope.
- Pagination: `page`, `pageSize`, max page size `100`.
- Filters: `search`, `status`, `workerType`, `employmentType`, `organizationUnitId`, `positionId`, `legalEntityId`, `sensitivityLevel`.
- Sorting allowlist: `employeeNumber`, `displayName`, `workerType`, `employmentType`, `status`, `hireDate`, `updatedAt`.
- No government identifier, DOB, personal email, phone, evidence, audit payload, or private-note fields in response or searchable fields.
- Errors must be controlled and must not leak stack traces, secrets, tokens, connection strings, or internal implementation details.
- Search logging/audit uses safe metadata only; raw PII-like search terms remain prohibited unless a later MOD-0021/MOD-0314 contract permits redaction.

Registry row DTO may include only:

- `employeeId`;
- `employeeNumber`;
- `displayName`;
- `workerType`;
- `employmentType`;
- `organizationUnitId` / display;
- `positionId` / display;
- `legalEntityId` / display;
- `status`;
- `hireDate`;
- `sensitivityLevel`;
- `updatedAtUtc`;
- `actions.canView`;
- `actions.canExport=false`.

If MOD-0314 masking is not contract-closed, implementation must either return only non-sensitive fixture-safe values or fail closed for sensitive fields.

### UI Boundary

Target UI route: `/HCM/Employees`.

Required UI contract:

- Tenant layout: `_LayoutTenantShell`.
- Same-origin frontend proxy: `/HCM/Employees/api`.
- Browser JS must not call service ports `5056`, `5057`, `5059`, or `5060`.
- DataTable v2 with `data-dt-standard="v2"` and Compact registry/list structure.
- Columns: Employee Number, Display Name, Worker Type, Employment Type, Organization Unit, Position, Legal Entity, Status, Hire Date, Sensitivity, Last Updated.
- States: loading, empty, filtered empty, permission denied/filtering, controlled error, masked/unavailable sensitive field.
- No export button or hidden export affordance.
- No row detail action unless detail-read scope is separately approved.
- No status/evidence/audit placeholders.

### P4 Acceptance Criteria

| ID | Given | When | Then |
|---|---|---|---|
| REG-GWT-01 | An HR user has `mod0251.employee.search` and approved registry-safe records exist. | The user opens `/HCM/Employees` and searches. | A paged tenant-scoped list returns through Gateway-backed API with approved columns only. |
| REG-GWT-02 | A user lacks `mod0251.employee.search`. | The user opens or queries registry. | Server-side denial returns 401/403 or permission-filtered state with no rows/count leakage. |
| REG-GWT-03 | Tenant A and tenant B both have records. | Tenant B searches registry. | Tenant A rows, identifiers, and counts are not disclosed. |
| REG-GWT-04 | No approved registry-safe records exist. | The user opens registry. | Empty state is shown; no rows are synthesized from draft sessions. |
| REG-GWT-05 | Sensitive fields are governed by policy. | Registry data is returned. | Sensitive values are masked, omitted, or fail-closed; DOB/personal contact/government identifiers never appear. |
| REG-GWT-06 | Registry toolbar/actions render. | User reviews available commands. | Export is absent or disabled. |
| REG-GWT-07 | Detail-read scope is not approved. | Rows render. | Row detail action is absent or disabled. |
| REG-GWT-08 | Filters/sort/page are supplied. | Query executes. | Only allowlisted parameters are accepted; invalid input returns controlled validation errors. |

### P4 Test Expectations

- Backend query tests for pagination, filters, sort allowlist, and DTO shape.
- RBAC tests for `mod0251.employee.search`.
- Tenant isolation tests for rows and counts.
- Sensitive-field/masking tests proving no DOB, personal email, phone, government identifier raw/token/hash, evidence ids, or audit payloads are returned.
- Empty-state tests proving draft sessions do not appear in registry results.
- Frontend tests for loading, empty, filtered empty, error, permission-denied, and absent export/detail/lifecycle controls.
- Gateway route test only if gateway scope is approved in the implementation prompt.
- No lifecycle, activation, export, or Employee Detail dependency.

### Detail Read Acceptance (DL-3)

Revision 3 (review 02 F-20). Detail read stays in DL-3 (not a separate slice) and is accepted at E4 with the criteria
below; DL-3 is not accepted on REG-GWT-01 to REG-GWT-08 alone. This applies only if owner decision F-5 retro-approves
the read code; if F-5 closes it, the detail API and page are removed with their own E4 negative run.

Target API: `GET /api/v1/hcm/employees/{employeeId}` (permission `mod0251.employee.view`); target UI: the detail page
under `/HCM/Employees/{employeeId}` with `Layout = "_LayoutTenantShell";`.

Detail projection (this is the pack's detail DTO; the response carries these keys and no others):

| Group | Fields | Rule in DL-3 |
|---|---|---|
| Employee | `employeeId`, `employeeNumber`, `personId`, `employeeStatus`, `sensitivityLevel`, `version`, `etag`, `updatedAt` | Returned. |
| Legal profile | `legalFirstName`, `legalMiddleName`, `legalLastName`, `preferredName` | Returned. |
| Legal profile, sensitive | `dateOfBirth`, `personalEmail`, `phone` | Always `null`, also for a user with `mod0251.employee.view_sensitive`. |
| Legal profile, policy-governed | `nationalityCode`, `workEmail` | `null` while MOD-0314 HR Governance & Sensitive Access Controls is not executable (today). |
| Masking flag | `sensitiveFieldsMasked` | `true` whenever any field above is `null` by rule. |
| Employment records | `employmentRecordId`, `legalEntityId`, `organizationUnitId`, `positionId`, `startDate`, `endDate`, `contractType`, `probationStatus`, `probationEndDate`, `employmentStatus`, `approvalStatus`, `version`, `etag` | Returned. |
| Never present | government identifier (raw, token, hash, presence flag), evidence ids, audit payload, status history, private notes, `actions.canEditLegal`, `actions.canEditEmployment`, `actions.canChangeStatus`, `actions.canAttachEvidence`, `actions.canExport` | Absent. |

| ID | Given | When | Then |
|---|---|---|---|
| DET-GWT-01 | A user has `mod0251.employee.view` and a registry-safe employee exists in the user's tenant. | The user requests the detail through the Gateway. | 200 with a `Response<T>` envelope whose data keys equal the detail projection above (no extra key, none missing). |
| DET-GWT-02 | A user lacks `mod0251.employee.view`. | The user calls the API or opens the page. | API returns 403 with no employee data; the page shows an in-place access explanation and no page skeleton (UAS-001). |
| DET-GWT-03 | An employee exists in tenant A. | A tenant B user requests its id. | 404, the same response as for an id that does not exist; no tenant A data, id echo or count. |
| DET-GWT-04 | An employee has date of birth, personal email, phone, nationality and work email stored. | Any user, including one with `mod0251.employee.view_sensitive`, requests the detail. | Those five fields are `null`, `sensitiveFieldsMasked` is `true`, and no government identifier field appears. |
| DET-GWT-05 | The detail page renders for a user who holds every `mod0251.*` key. | The user inspects the page and the response. | No edit, status-change, evidence, export or audit control, link or hidden form is present; the response has no edit/status/evidence/export action flag. |
| DET-GWT-06 | A draft session id, a soft-deleted employee id or a non-registry-safe record id is used. | The detail is requested. | 404; draft sessions and soft-deleted rows never surface through detail. |

Required level: E4 (HTTP through the HCM API on a real store with real permission claims, plus the UI denial check).

## Given / When / Then Acceptance Criteria

These scenarios are acceptance-contract closure only. They do not create MOD-0251 behavior tests, employee implementation, gateway routes, migrations, or approval to develop. Every scenario is aligned to one of: `First approved draft/reference-validation slice`, `Deferred R1 / activation sequence`, or `Governance blocker`.

### First Approved Draft / Reference-Validation Slice

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-A01 | Draft creation | An authorized HR user has `mod0251.employee.create_draft`, an approved HCM service/API contract exists, audit is available (Revision 4, AK-32: the audit precondition belongs to DL-7), and tenant context is resolved server-side. | The user starts a Create Employee draft. | A tenant-scoped employee draft session is created with no Active employee record yet; client-provided `TenantId` is ignored/rejected; `employee_draft.created` is audited with safe payload only. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |
| GWT-A02 | Draft update/save | An employee draft exists for the same tenant and the user has `mod0251.employee.create_draft`. | The user saves legal profile and employment draft step data with a current ETag. | The draft is saved, version/ETag advances, incomplete activation-required fields remain visibly incomplete, and `employee_draft.updated` is audited without full PII payload. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |
| GWT-A03 | Person reference validation | A draft has a selected `PersonId` and the approved MOD-0288 person lookup-validation path is available. | The user validates or reviews the draft. | The person reference is confirmed as same-tenant and referenceable; unresolved, tenant-mismatched, inactive, or unavailable person validation blocks draft review/submit readiness without creating a person. |
| GWT-A04 | Organization-unit reference validation | A draft has an organization-unit reference and the approved Platform Gateway route is available. | The user validates or reviews the draft. | The organization-unit reference is confirmed as same-tenant/referenceable; unresolved, stale, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A05 | Position reference validation | A draft has a position reference and the approved Platform Gateway route is available. | The user validates or reviews the draft. | The position reference is confirmed as same-tenant/referenceable; unresolved, stale, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A06 | Legal-entity reference validation | A draft has a legal-entity/company reference and the approved MDM Gateway route is available. | The user validates or reviews the draft. | The legal entity is confirmed as referenceable through MDM lookup-validation; unresolved, inactive, tenant-mismatched, or unavailable provider responses block draft review/submit readiness. |
| GWT-A07 | Draft review state | A draft has saved legal/profile and employment draft steps plus validated references. | The user opens Review Draft. | MOD-0251 returns a reviewable persisted draft shape with validation summary and no submit/approval/Active transition controls in the first approved slice. |
| GWT-A08 | Reload draft persistence | A draft was created, saved, and reference-validated. | The user reloads the wizard/review page or the service restarts. | Draft data, step status, validation summary, ETag/version, and safe audit references remain retrievable from persistence without creating an Active employee. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |
| GWT-A09 | Tenant isolation | Draft data exists in tenant A. | A user from tenant B requests the draft or review state. | MOD-0251 returns 404/fail-closed or an empty tenant-scoped result; no cross-tenant identifiers or counts are disclosed. |
| GWT-A10 | Server-side RBAC denial | A user lacks the required MOD-0251 permission for draft create, save, validate, review, or reload. | The user attempts the protected draft/reference action. | The server denies with 403 or a controlled denial shape, and `employee.access_denied` is audited where audit contract requires it. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |
| GWT-A11 | Sensitive field masking/denial | A user can access draft/review data but lacks sensitive-field permission or MOD-0314 policy clearance. | The user opens draft review or requests sensitive fields. | Sensitive fields are masked/omitted or the request is denied; raw government identifiers are never returned; sensitive read/denial is audited. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |
| GWT-A12 | Audit draft write success/failure | A user performs draft create, draft update, reference validation, or review-state persistence. | Audit emit succeeds or fails. | On success, the draft write commits with safe audit payload; on audit failure for critical draft writes, the write is blocked or rolled back per contract and the user receives a controlled failure. *(Revision 4, AK-28: on path `b` "audit failure" means the local outbox row cannot be written in the draft write's own Mongo transaction; Platform delivery (`Retry` / `Rejected`) is asynchronous, does not block the write, and is measured in DL-7 as outage, recovery and rejection.)* |
| GWT-A13 | ETag conflict | A draft section was changed after the user loaded it. | The user saves with a stale ETag. | Save is rejected with 409/conflict, current version can be reloaded, and `employee.conflict` is audited without overwriting newer data. *(Revision 4: the audit clause is satisfied in DL-7, E5; the other clauses in DL-2, E4.)* |

### Governance Blocker Scenarios

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-B01 | HCM scaffold without broader employee lifecycle approval | `services/Diten.HcmService` exists and P2 draft/reference-validation runtime is implemented, but full MOD-0251 employee lifecycle implementation is not approved. | Any prompt attempts employee Active-state entities/APIs, lifecycle persistence, migrations, gateway routes, frontend registry/detail lifecycle pages, permission seeds, audit wiring, or behavior tests outside the approved P2 slice without separate approval. | Work is blocked outside P2; MOD-0251 remains limited to draft/reference-validation and must not be placed in Platform, Auth, MDM, DevEnablement, or EnterpriseStrategy. |
| GWT-B02 | Missing MOD-0023 workflow contract | MOD-0023 workflow start/decision contract is unavailable, mock-only, disabled, or ambiguous. | A user attempts submit/approval processing. | Submit/approval is disabled or blocked with controlled error; employee remains Draft/Pending as applicable and no local approval engine is invented. |
| GWT-B03 | Missing MOD-0031 evidence contract | MOD-0031 evidence link contract is unavailable. | The wizard reaches Evidence step or policy requires activation evidence. | If evidence is not required for the first slice, the step is disabled/deferred; if policy requires evidence, submit/approval is blocked until evidence contract is executable. |
| GWT-B04 | Missing MOD-0314 masking contract | MOD-0314 masking/sensitive access policy contract is unavailable. | A user requests sensitive registry/detail fields. | Sensitive reads are denied or non-sensitive-only shapes are returned; no inferred masking policy is implemented. |
| GWT-B05 | Missing tokenization/security contract | Tokenization/security service ownership or API is unavailable. | A user attempts to capture government identifier data. | Government identifier capture is disabled; raw government identifiers are not stored, logged, audited, or returned. |
| GWT-B06 | Unresolved MOD-0299 identity conflict | MOD-0299 registry/Blueprint reconciliation remains unresolved. | MOD-0251 needs assignment workflow/orchestration dependency. | Assignment workflow dependency is deferred; MOD-0251 may store only its employee-side assignment snapshot and must not bind to executable MOD-0299 contracts. |
| GWT-B07 | Missing or unavailable MOD-0288 person validation path | Same-tenant person reference lookup-validation, Gateway route, or frontend proxy/picker validation is unavailable or ambiguous, or the draft has no `PersonId`. | A user creates or saves a draft, then requests Review Draft or Submit. | Create and save succeed and keep the reference visibly unvalidated; Review Draft and Submit are refused with a controlled validation/dependency error until a valid same-tenant person (and valid organization-unit, position and legal-entity references) is confirmed; MOD-0251 does not create or own person directory records and never accepts Auth `UserId` as a substitute. *(Revision 3, F-23: one meaning, matching "Protected Action / Endpoint Map" and "Validation Contract".)* |

### Deferred R1 Sequence Scenarios

| ID | Scenario | Given | When | Then |
|---|---|---|---|---|
| GWT-D01 | Evidence attach integration | MOD-0028, MOD-0031, and MOD-0030 contracts are not closed for MOD-0251. | A user attempts to attach evidence. | Evidence upload/link controls remain disabled/deferred; evidence attach is implemented only in a later approved evidence sequence. |
| GWT-D02 | Termination/status-change | Employee is Active and a user attempts termination, suspension, rehire, or non-activation status change. | The status-change flow is requested before its R1 sequence is approved. | Flow is not implemented in the first approved draft/reference-validation slice; it remains blocked/deferred pending workflow/evidence/retention contracts. |
| GWT-D03 | Export | A user attempts restricted employee export before export platform and masking contracts are closed. | Export is requested. | Export remains disabled/deferred; no unbounded or unmasked export is produced. |
| GWT-D04 | Data Quality Queue full workflow | Duplicate/data-quality cases may be identified during create/submit. | A user attempts assignment, resolution, merge, or queue workflow operations before P10. | Full queue workflow remains deferred; only activation-slice duplicate detection/blocking or case creation may be represented by contract. |
| GWT-D05 | Self-service employee profile | Employee self-service/MOD-0319 is not approved for R1 first slice. | An employee attempts self-service profile editing. | Self-service edit is unavailable/deferred; MOD-0251 does not expose employee-owned edit surfaces in the first approved draft/reference-validation slice. |
| GWT-D06 | Submit for approval | A complete reviewed draft exists but executable MOD-0023 workflow contract is unavailable. | The user attempts Submit for Approval before the activation sequence is approved. | Submit is unavailable/blocked; no MOD-0023 workflow is started, no `employee.submitted_for_approval` audit event is emitted, and no Pending Approval or Active employee state is created. |
| GWT-D07 | Approval decision / Active transition | A draft or future submitted employee exists but executable MOD-0023 decision contract is unavailable. | MOD-0251 receives or attempts to process an approval/rejection decision before the activation sequence is approved. | Decision processing and Active transition are blocked; no `employee.approved`, `employee.rejected`, `employee.created`, status-history activation row, or employee-created outbox event is produced. |

### Acceptance Category Summary

| Category | Scenario IDs | Status |
|---|---|---|
| First approved draft/reference-validation slice | GWT-A01 through GWT-A13 | Implemented in code, not accepted. Browser validation is narrative; no recorded evidence in the repository refs examined (review 01, F-7). Per-criterion status: "Acceptance Criteria → Test → Evidence Map" (none is fully implemented and proven; A12 is missing). |
| Governance blocker | GWT-B01 through GWT-B07 | Must fail closed until each blocker is resolved or formally de-scoped. B01 is violated by code scope (F-5). |
| Deferred R1 / activation sequence | GWT-D01 through GWT-D07 | Explicitly out of first approved draft/reference-validation slice; requires later approved prompts. |

### Acceptance Criteria → Test → Evidence Map

Revision 2 (review 01, F-9). Status is the static state of production code and tests at review 01; it is not a test
result. "Test" names existing test methods only (`file:line` of the method under
`services/Diten.HcmService/tests/Diten.HcmService.Application.Tests/` unless a full path is shown); "none" means no test
exists for that criterion. The 2026-10-27 run (60/60) is build/test evidence only; no criterion has runtime evidence.
"Required level" is the evidence level the criterion must reach for acceptance; it equals its slice's level in
"Teslim dilimleri". Deferred behavior is listed as deferred: absence of a behavior is not its completion. Revision 4
(AK-32): a row whose criterion has an audit part names both levels and slices, "E4 (audit part E5)" and "DL-2 (audit
part DL-7)".

| Criterion | Status | Test (static) | Required level | Slice | Note |
|---|---|---|---|---|---|
| GWT-A01 | partial | `EmployeeDraftBackendTests.cs:26` (sequential replay); `EmployeeMasterContractTests.cs:47` (no client `TenantId`) | E4 (audit part E5) | DL-2 (audit part DL-7) | No concurrent-create test; no unique index (F-16); audit actor/correlation null (F-4). Revision 4 split: create, tenant and idempotency parts DL-2, E4; the `employee_draft.created` audit part (actor, correlation, safe payload; F-4) DL-7, E5. |
| GWT-A02 | partial | `EmployeeDraftBackendTests.cs:43` (stale ETag only); `frontend/Diten.Web/tests/hcm-employee-draft-wizard.test.js` (ETag sent on save; not run 2026-10-27) | E4 (audit part E5) | DL-2 (audit part DL-7) | No save-success / version-advance / `employee_draft.updated` test. Revision 4 split: save, version and ETag parts DL-2, E4; the `employee_draft.updated` audit part DL-7, E5. |
| GWT-A03 | partial | `EmployeeDraftBackendTests.cs:77` (positive, fake provider); `GatewayReferenceValidationClientTests.cs:15`, `:52`, `:70` | E4 | DL-2 | Defects F-1 and F-2: a provider `referenceable=false` in a 200 response counts as valid; ids not bound to the draft. No HTTP-200-negative test. |
| GWT-A04 | partial | `EmployeeDraftBackendTests.cs:77` (aggregate positive, fake provider) | E4 | DL-2 | F-1, F-2; no organization-unit negative test. |
| GWT-A05 | partial | `EmployeeDraftBackendTests.cs:77` (aggregate positive, fake provider) | E4 | DL-2 | F-1, F-2; no position negative test. |
| GWT-A06 | partial | `EmployeeDraftBackendTests.cs:77` (aggregate positive, fake provider) | E4 | DL-2 | F-1, F-2; no legal-entity negative test. |
| GWT-A07 | partial | `EmployeeDraftBackendTests.cs:77` (positive non-submit review) | E4 | DL-2 | Review checks only `CanReview` and non-empty fields (F-2). |
| GWT-A08 | partial | `EmployeeDraftSessionRepositoryMongoTests.cs:15` (Mongo round-trip) | E4 (audit part E5) | DL-2 (audit part DL-7) | No service-restart reload; no safe audit reference. Revision 4 split: reload and restart persistence DL-2, E4; the safe audit reference DL-7, E5. |
| GWT-A09 | implemented | `EmployeeDraftBackendTests.cs:168` (handler); `EmployeeDraftSessionRepositoryMongoTests.cs:15` (Mongo tenant) | E4 | DL-2 | Unit level only; no HTTP/runtime cross-tenant evidence. |
| GWT-A10 | partial | none for draft endpoints | E4 (audit part E5) | DL-2 (audit part DL-7) | No real HTTP permission-denial test; page skeleton drawn for unauthorized users (F-15); no `employee.access_denied` audit. Revision 4 split: 403 and UAS-001 parts DL-2, E4; the `employee.access_denied` audit part DL-7, E5 (depends on AE-2). P2 closure needs that audit part unless the AE-2 decision (Control Tower with Building Blocks owner) records the gap as accepted; an accepted gap is then named in the DL-7 acceptance record. |
| GWT-A11 | partial | `EmployeeDraftBackendTests.cs:61` (government identifier in `StepPayload` only) | E4 (audit part E5) | DL-2 (audit part DL-7) | F-3: no recursive masking, `personal_email`/`phone` unmasked, `ClientValidationState` unchecked; no persistence/response negative test. Revision 4 split: masking, omission, denial and no raw government identifier DL-2, E4; the "sensitive read/denial is audited" part DL-7, E5. AK-30: its read half has no audit row by rule (`audit-trail-standard.md` §7: a single read is audited only when KVKK special-category or HR-confidential data is returned; draft review returns none - government identifier rejected, sensitive keys masked or omitted), so DL-7 measures that no such field is returned to a reader without a read row; its denial half is `employee.access_denied` and depends on AE-2. P2 closure needs the denial record unless the AE-2 decision (Control Tower with Building Blocks owner) records the gap as accepted; an accepted gap is then named in the DL-7 acceptance record. |
| GWT-A12 | missing | none for draft writes (`GovernedHcmAuditAppendClientTests.cs`, `EmployeeAuditAdapterContractTests.cs` test the adapter, not the draft write path) | E5 | DL-7 | Log-only audit (F-4). Revision 3 (F-22): moved from DL-4 to DL-2 - central record through the shared forwarder (path `b`), enlisted in the draft write's own Mongo transaction, actor and correlation set, no draft change when the record cannot be written (see "Audited Events"). Revision 4 (F-26): moved from DL-2 to DL-7 at E5 - exit is a real HCM draft write visible in the Platform central log with tenant, actor, correlation, object and safe change info (content per AE-3 outcome, "Audited Events", AK-27), plus measured delivery outage (not configured / Platform down) and recovery, the rejected category/operation path, and a cold start that keeps the outbox. DL-2 alone does not close GWT-A12. Open: AE-1 (K4), AE-2, AE-3 (changed fields), AE-4 (category), AE-5 (employee number), BL-324 (tenant source). |
| GWT-A13 | partial | `EmployeeDraftBackendTests.cs:43` | E4 (audit part E5) | DL-2 (audit part DL-7) | Stale write rejected. `employee.conflict` audit: the shared forwarder records no failed command, so this part depends on open decision AE-2 (Revision 3; was "DL-4 audit wiring"). Revision 4 split: stale-write rejection and reload DL-2, E4; the `employee.conflict` audit part DL-7, E5 (depends on AE-2). P2 closure needs that audit part unless the AE-2 decision (Control Tower with Building Blocks owner) records the gap as accepted; an accepted gap is then named in the DL-7 acceptance record. |
| GWT-B01 | missing | none | E4 | DL-3 | Violated by code scope: list/detail API, pages, menu entry, broad Gateway route, wide seed (F-5, F-6, F-13). |
| GWT-B02 | partial | `EmployeeDraftBackendTests.cs:113`; `EmployeeMasterContractTests.cs:201`; `GatewayWorkflowStartClientTests.cs:11` | E5 | DL-4 | Blocked state proven by handler/route tests only. |
| GWT-B03 | missing | none | not set (later evidence slice) | later, not numbered | Evidence step absent; no acceptance test of the blocked state. |
| GWT-B04 | partial | `EmployeeRegistrySearchTests.cs:20`, `:122` (safe projections of out-of-scope list/detail) | E4 | DL-3 | `WorkEmail` and `NationalityCode` returned unmasked in detail (inspection note item 12). |
| GWT-B05 | partial | `EmployeeDraftBackendTests.cs:61` | E4 | DL-2 | `ClientValidationState` not checked (F-3). |
| GWT-B06 | implemented | none | E1 | DL-1 | Documentation criterion; no MOD-0299 Position & Organization Assignment binding exists in code. Identity conflict closed (F-11). |
| GWT-B07 | partial | `GatewayReferenceValidationClientTests.cs:52` (missing `PersonId` blocks without provider call) | E4 | DL-2 | F-1 breaks the "unavailable/ambiguous" path. Revision 3 (F-23): contradiction resolved - B07 now says create/save MAY proceed with missing or unvalidated references and Review Draft / Submit REQUIRE a valid same-tenant person and the other references, as in "Protected Action / Endpoint Map" and "Validation Contract". Missing tests: create/save succeeds without `PersonId`; review refused without a validated person; submit is 409 today regardless. |
| GWT-D01 | deferred | none | not set (later evidence slice) | later, not numbered | Evidence upload/link. |
| GWT-D02 | deferred | none | E5 | DL-4 | Termination enters DL-4; suspension/rehire/other status changes stay in a later status-change slice. |
| GWT-D03 | deferred | partial static: `frontend/Diten.Web/tests/hcm-employee-registry.test.js:105-111` (registry `index.js` configures no export buttons / export columns; not run 2026-10-27) | not set (later export slice) | later, not numbered | Export affordance removed from registry; endpoint absent. No runtime evidence (Revision 3, F-23: "none" corrected). |
| GWT-D04 | deferred | none | not set (later Data Quality Queue slice) | later, not numbered | Duplicate warning only. |
| GWT-D05 | deferred | none | not set (later self-service slice) | later, not numbered | Self-service edit stays out; `/me` read of the employment summary is DL-5 (K-3). |
| GWT-D06 | deferred | `EmployeeDraftBackendTests.cs:113`; `EmployeeMasterContractTests.cs:201`; `GatewayWorkflowStartClientTests.cs:11` | E5 | DL-4 | Blocked state partly proven (submit returns 409, no workflow start client). |
| GWT-D07 | deferred | `WorkflowApprovalLifecycleTests.cs:11`, `:26`; `EmployeeMasterContractTests.cs:154` | E5 | DL-4 | Blocked state partly proven; callback has no `[HasPermission]` (F-14, fixed in DL-2). |

## Acceptance Criteria

- DCP-002 validation proves `MOD-0251` / `Core HR / Employee Master`.
- Domain scaffold exists under `execution/domains/human-capital-management/**`.
- Module pack status is `approved` (Revision 5: `approval_status` removed; the conditional P2 approval is history).
- Service owner is explicitly `Diten.HcmService`; no MOD-0251 employee business implementation may start from the scaffold alone.
- P0 inspection findings are recorded with verdict `BLOCKED` for approval/implementation.
- Service topology, gateway, frontend, dependency contract, RBAC/audit, and MOD-0299 reconciliation blockers are documented.
- MOD-0251 SoR ownership is explicit for Employee, Employment Record, and Job Assignment / Employment Assignment Snapshot.
- MOD-0299 and MOD-0288 boundaries are explicit.
- Dependencies are split into Blueprint Dependency Gate and Additional Implementation Dependencies.
- First approved runtime slice is draft/reference-validation only; submit, approval, Active transition, and employee-created event/outbox behavior are deferred.
- Gateway and frontend service-port constraints are documented.
- Sensitive/tokenization fail-closed rules are documented.
- Given/When/Then acceptance criteria cover first approved draft/reference-validation slice, governance blockers, and deferred R1/activation sequences.
- Revision 2 (testable):
  - The pack has one `## Teslim dilimleri` table; every row has exactly one evidence level token (E1-E5), and every name in its "kapsadığı bölümler" column is a heading of this pack (`scripts/pack_approval.py` slice parser).
  - Every GWT-A, GWT-B and GWT-D criterion appears once in "Acceptance Criteria → Test → Evidence Map" with status, test or "none", required level and slice.
  - No statement in this pack calls P2 browser-validated or PASS without the marker "narrative; no recorded evidence".
  - Each finding F-1, F-2, F-3, F-15, F-16 is listed with file:line in "Known Defects in the Approved P2 Slice (Review 01)".
  - Each open decision in "Open Decisions (Owner)" names an owner role and the slice it blocks; no person name or e-mail appears.
  - DL-2 acceptance: for each of F-1, F-2, F-3, F-14, F-15, F-16 and the F-6 fixture boundary, a negative test fails on the unfixed code and passes on the fix (sabotage evidence), and an E4 run on the same commit as this pack version is recorded.
  - Layout for every HCM employee page stays `Layout = "_LayoutTenantShell";`, and an unauthorized user receives only an in-place access explanation, no page skeleton (UAS-001).
- Revision 3 (testable):
  - `## Audited Events` lists every write command of "Owned Objects" and every write command in `services/Diten.HcmService/src/Diten.HcmService.Application/Features/CoreHrEmployeeMaster/Commands/` exactly once, each with a path or an `İ` class and a reason.
  - DL-7 acceptance for GWT-A12 (labelled "DL-2" in Revision 3): each of the four draft writes `CreateEmployeeDraftCommand`, `PatchEmployeeDraftCommand`, `ValidateDraftReferencesCommand`, `ReviewEmployeeDraftCommand` and the two smoke-fixture writes `EnsureEmployeeSmokeFixtureCommand`, `CleanupEmployeeSmokeFixtureCommand` leaves exactly one central-audit outbox row with tenant, actor, correlation id and entity id set, committed in the same Mongo transaction as the draft or fixture change; a forced outbox-insert failure leaves the draft or fixture unchanged and returns a controlled failure; removing the enlistment call makes that test fail (sabotage evidence). *(Revision 4, F-26: this item is now part of DL-7 acceptance, not DL-2; see below.)*
  - Front matter carries `data_mode: server`; the pack covers one list screen (Employee Registry, `Views/HCM/Employees/Index.cshtml`).
  - DL-3 is accepted only with DET-GWT-01 to DET-GWT-06 at E4 in addition to REG-GWT-01 to REG-GWT-08 (or the recorded removal under F-5).
- Revision 4 (testable):
  - "Teslim dilimleri" has a DL-7 row at E5; DL-2 stays E4 and its scope and exit name no draft-write central audit.
  - DL-7 acceptance (GWT-A12), E5: the outbox-row item above passes for its six commands (four draft writes, two smoke-fixture writes - the two fixture writes only as the DL-2 F-6 outcome leaves them: if F-6 removes the endpoint, the commands and their rows go; AK-40); submit and workflow decision (marker in DL-7, behaviour and audit row in DL-4; 409 today, no write) leave no row (AK-33); the structural guard of "Test Expectations" (AK-29) passes; and, on one integrated run of `Diten.HcmService` with Platform (MOD-0021 Audit Trail): (1) a real HCM draft write appears in the Platform central log with the minimum content for the recorded AE-3 outcome ("Audited Events", "Central-log content measured by DL-7"; AK-27) and no forbidden field; (2) with the Platform address / internal key not configured, the draft write commits (not blocked, AK-28), its row stays `Pending` with `Attempts` unchanged, and the health check is Degraded; with Platform down, the row stays `Pending` while `Attempts` and `LastError` are updated (delivery outcome `Retry`), and the health check turns Degraded only when the oldest pending row is older than `StaleAfter` (10 min); after configuration / restart of Platform the row is `Delivered` exactly once (AK-35); (3) a record with a category / operation / actor type Platform does not know ends in state `Rejected`, the command is not stopped, and the outcome is visible (outbox state `Rejected`, error log, health Degraded), not silently lost - produced with a test-only command declaration carrying an unknown category, because the structural guard keeps production declarations to Platform names (AK-40); (4) after an HCM cold start with undelivered rows, the outbox still holds them and delivers them; (5) a forced local outbox-insert failure blocks the write and returns the controlled failure defined in "Audit Fail-Closed Rules", not a generic 500 (the only audit failure that blocks, AK-28, AK-39); (6) tenant source (AK-37, AK-42; exact rule after BL-324): a token without a `tenant_id` claim plus an `X-Tenant-Id` header - the write is refused and neither draft data nor a central record is created (today's header-vs-token mismatch 400, `TenantResolutionMiddleware.cs:37-50`, is not the measurement: it already holds and says nothing about the record); `EmployeeDraftHandlerHelpers.TryGetTenantId` / the header-built `ITenantContext` is not the audit scope; (7) correlation (AK-41): the `CorrelationId` in the central record equals the request's correlation id (gateway header), not the event id the recorder falls back to (`CentralAuditRecorder.cs:110`); the actor id is the token's user claim; (8) orphan path (AK-46): with the process stopped between commit and confirmation, the row is released after `OrphanAfter` and appears in the central log as Unconfirmed; (9) submit and workflow decision, dispatched through MediatR in the test (the controller never dispatches them, `EmployeeDraftsController.cs:80-99`; AK-49), leave no row.
  - DL-7 acceptance (audit parts of GWT-A01, GWT-A02, GWT-A08, GWT-A10, GWT-A11, GWT-A13), E5: the central records of `CreateEmployeeDraftCommand` and `PatchEmployeeDraftCommand` (RequestType) carry actor and correlation id and nothing outside the safe content; the reloaded draft's `draftSessionId` finds its records in the central log by EntityId (the "safe audit reference"); an idempotent replay adds no second record ("Fail-Closed Behavior"; with `RequireEnlistment = true` a success that enlisted nothing files none). GWT-A11 read half: no audit row by rule (`audit-trail-standard.md` §7; "Audited Events", AK-30) - DL-7 measures that draft review returns no special-category or HR-confidential field, so no read row is required; this rests on the DL-2 F-3 masking result and is measured after DL-2 is accepted or on the combined AK-31 run (AK-40). Denial and conflict (`employee.access_denied` for GWT-A10 and the GWT-A11 denial half, `employee.conflict` for GWT-A13): measured only after AE-2 decides that refused commands are recorded and the forwarder change named in "Audited Events" (AK-34; other module, operating card §7c) exists; each is then visible in the Platform central log under its own RequestType (mapping table) with tenant, actor, correlation id, a refused outcome and no PII - a reason code only if AE-3 adds a field for it. If the AE-2 decision (Control Tower with Building Blocks owner) records the gap as accepted, the DL-7 acceptance record names that gap instead of measuring them; the same rule applies to AE-3 ("entity + operation only" accepted: event name and changed-field names named as a gap).
  - P2 closure is claimed only when both DL-2 (E4) and DL-7 (E5) are accepted on the same pack version; DL-2 alone does not close GWT-A12, nor the audit parts of GWT-A01, GWT-A02, GWT-A08, GWT-A10, GWT-A11 and GWT-A13 (for the denial and conflict records: unless AE-2 records an accepted gap; for event name and changed fields: unless AE-3 does, as above).
  - Order of DL-2 and DL-7 (AK-31): both change the draft create/patch write path (DL-2 F-16 atomic create with a unique index; DL-7 Mongo transactions in `EmployeeDraftSessionRepository`). Whichever slice lands second reruns the first slice's acceptance tests on its own commit, and P2 closure rests on one combined DL-2 + DL-7 acceptance run on the commit carrying this pack version (cf. operating card S-86: evidence binds to the commit carrying the current pack version).
- Revision 5 (testable):
  - The front matter carries no `approval_status` line, and the dispatch gate's approval-limit check (`scripts/prompt_gate.py` `check_approval_limit`, CT-FIX-04, through the shared reader `scripts/ct_queue.py` `pack_approval_limit`) reports no limit for this version: `pack_approval_limit` returns `(None, None)` and `pack_approval_decision(text, "dispatch")` returns `ok`.

## Test Expectations

No runtime tests are created by this governance reconciliation prompt.

Current and future implementation expectations:

- P2 focused backend/frontend tests and browser replay are the only completed runtime evidence for this approved slice. *(Historical. Revision 2: no runtime evidence is recorded in the repository (F-7). Current: 52 test methods / 60 cases (2026-10-27 run 60/60, build 0 warnings), E2 only; per-criterion coverage in "Acceptance Criteria → Test → Evidence Map".)*
- DL-2 tests (Revision 2): provider HTTP-200 negative/missing/malformed bodies (F-1); validation bound to draft ids and version, invalidated by a patch (F-2); recursive PII guard incl. `personal_email`, `phone` and `ClientValidationState`, persistence and response negatives (F-3); concurrent create with one idempotency key on a real Mongo store with a unique index (F-16); HTTP 403 for each draft endpoint and for workflow-decisions without permission (F-14, GWT-A10); server-rendered access explanation with no skeleton for unauthorized users (F-15); smoke-fixture refusal outside the fixture boundary and no revival of soft-deleted rows (F-6).
- DL-7 audit tests (Revision 4, F-26; were "DL-2 audit tests" in Revision 3, F-22; GWT-A12): on a real Mongo store with the shared forwarder registered (`RequireEnlistment = true`), one outbox row per successful draft write (create, patch, validate-references, review) with tenant, actor, correlation and entity id; forced outbox failure → no draft change, controlled failure; idempotent replay → no second row; failed command → no deliverable row; no forbidden field (see "Audited Events") in the row; smoke-fixture writes covered the same way. Sabotage: drop the enlistment call and the failure test must go red. Structural guard (Revision 4, AK-29 / AK-36), modelled on Inventory's full model (`services/Diten.InventoryService/tests/Diten.InventoryService.Tests/CentralAudit/CentralAuditDeclarationTests.cs:29-109`), measured on the production HCM assemblies and sources: (a) every HCM write command (an `IBaseRequest`, not `*Query`, not in `.Queries`) declares `ICentralAuditDeclaration`, and their number is a fixed count in the test; (b) category and operation are Platform enum names (`CentralAuditPlatformValues.IsKnown`), operation is not `Unknown`, entity type is not empty; (c) source module: Inventory instantiates its own manifest providers, but HCM's provider lives in Platform (`services/Diten.Platform/src/Diten.Platform.Application/Features/HumanCapital/SelfRegistration/HcmEmployeeMasterManifestProvider.cs:26`, module code `HCM-EMPLOYEE-MASTER`) and is not referenced by the HCM test project; instead every declaration's `SourceModule` must equal one HCM constant `HCM-EMPLOYEE-MASTER`, and a source-text check of that Platform file pins the same code (*proposed*; reading the file is not a change to the other module); (d) no MOD-0251 command carries `ICentralAuditOutsideTransaction`; (e) in `Diten.HcmService.Persistence` no `StartTransaction(` appears, the number of `WithTransactionAsync(` call sites is a fixed count in the test, each callback enlists (`CentralAuditTransaction.EnlistAsync(`), and a background job that runs outside any command is a named file exception that must not enlist (none today); (f) every HCM repository write method runs inside an enlisting transaction - today none does (`EmployeeDraftSessionRepository.cs:39,50`, `EmployeeSmokeFixtureRepository.cs:60,89,120` write without a transaction). Sabotage: remove one enlistment, or add a repository write outside a transaction, and the guard goes red. E5 run (Revision 4): `Diten.HcmService` and Platform together - the record is read back from the Platform central log with the AE-3-outcome content; delivery outage then recovery, the write not blocked (not configured: `Pending`, `Attempts` unchanged, health Degraded; Platform down: `Pending` with `Attempts` / `LastError` updated, Degraded only past `StaleAfter`; then `Delivered` once); rejected category/operation through a test-only declaration, the command not stopped and the `Rejected` state visible; controlled failure on a forced outbox-insert failure; token without `tenant_id` plus `X-Tenant-Id` header refused with no data and no record (AK-42); record correlation id equals the request's correlation id (AK-41); orphan release after `OrphanAfter` sent as Unconfirmed (AK-46); submit and workflow decision dispatched through MediatR leave no row (AK-49); HCM cold start with undelivered rows.
- DL-4 tests (Revision 3: own new commands only): the same audit tests for submit, workflow decision, activation, termination, hire-date change and employment change; the draft-write audit is DL-7's (Revision 4; DL-2's in Revision 3).
- DL-5 tests: consumer and producer contract tests against the versioned contract file; producer-side contract test in `Diten.HcmService`.
- Frontend DataTable verifier applies only after registry/list pages are approved.
- Runtime smoke only after the approved implementation slice exists; activation smoke remains deferred until MOD-0023 activation is approved.
- Security/tenant tests for unauthorized access, tenant mismatch, masking, audit denial, and tokenization-disabled flows.

## Ready-for-dev Checklist

- [x] User review/promotion recorded for the draft/reference-validation slice.
- [x] Conditional promotion for draft/reference-validation slice recorded.
- [ ] Activation/full-lifecycle promotion recorded.
- [x] HCM service boundary selected for planning as `services/Diten.HcmService`.
- [x] HCM service scaffold created at `services/Diten.HcmService`; no MOD-0251 employee business implementation was created.
- [x] Future HCM port `5060` accepted as the approved local downstream port for `Diten.HcmService`.
- [x] MOD-0299 assignment workflow/orchestration explicitly de-scoped from the MOD-0251 first approved draft/reference-validation slice.
- [x] MOD-0299 identity reconciled (`module-id-registry.md:199`). *(Revision 3, F-24: row 199 keeps "SaaS Billing & Invoicing" only as a non-executable deprecated alias whose legacy meaning migrates to MOD-0364, and states canonical MOD-0299 stays Position & Organization Assignment; re-read 2026-10-29 in this worktree. No identity conflict remains.)*
- [ ] MOD-0299 Position & Organization Assignment workflow scope approval (assignment workflow/orchestration binding for MOD-0251; out of scope for P2 and DL-2 to DL-7; needs its own scope approval before any binding). *(Revision 3, F-24: separated from the identity item above.)*
- [x] MOD-0288 person/reference contract confirmed for same-tenant employee creation through backend + Gateway + frontend proxy/picker, with frontend Vitest execution caveat.
- [x] Organization-unit provider decision approved for MOD-0251 employment validation: MOD-0288 / Platform organization-unit runtime.
- [x] Position provider decision approved for MOD-0251 assignment snapshot validation: MOD-0288 / Platform position runtime.
- [x] Legal entity/company provider decision approved for R1 MOD-0251 employment validation: MDM legal-entity lookup-validation unless a later Blueprint/domain decision overrides it.
- [x] Gateway/reference ingress plan confirmed for MOD-0288 person route through `/api/v1/platform/persons` and `/api/v1/platform/persons/{everything}`.
- [x] Frontend person picker/proxy exists through `/Platform/PersonReferences/api`, `window.PersonReferenceApi`, and `window.PersonReferencePicker`, with frontend Vitest execution caveat.
- [x] Gateway/reference ingress confirmed for non-person MOD-0288/MDM reference validation paths: organization units, positions, and legal entities.
- [x] Position assignment workflow is explicitly de-scoped from first approved draft/reference-validation slice.
- [x] Workflow approval de-scoped from first approved draft/reference-validation slice.
- [ ] MOD-0023 workflow contract executable.
- [ ] Activation transition approved for implementation.
- [x] DTO/API/validation contract for draft/reference-validation slice closed.
- [ ] Submit/approval/activation contract approved.
- [x] Activation submit/approval remains blocked unless executable MOD-0023 workflow contract exists.
- [ ] MOD-0031 evidence activation contract executable.
- [x] Evidence integration deferred from first slice when `activation_evidence_required = false`; evidence-required activation remains blocked.
- [ ] MOD-0028 evidence attachment contract executable.
- [x] MOD-0028 evidence attachment deferred from first slice; no upload/link UI or orphan document-link metadata.
- [ ] MOD-0030 retention/legal-hold contract executable.
- [x] MOD-0030 retention/legal-hold/destructive lifecycle behavior deferred from first slice; destructive/legal-hold-sensitive flows blocked.
- [ ] MOD-0314 masking contract executable for advanced sensitive flows.
- [x] MOD-0314 unavailable means sensitive reads are denied or return non-sensitive/masked shapes only.
- [ ] Tokenization/security contract identified for government identifier capture.
- [x] Tokenization unavailable means government identifier capture is disabled and raw identifier storage is prohibited.
- [x] MOD-0251 role-permission matrix added.
- [x] MOD-0251 audit payload contract added.
- [ ] MOD-0251 permission seed implementation completed after HCM service scaffold approval. *(Revision 2: seed code exists with 14 keys, wider than P2 (F-13); not accepted until the breadth decision.)*
- [ ] MOD-0251 audit wiring implementation completed after HCM service scaffold approval. *(Revision 2: log-only today (F-4); DL-4. Revision 3: draft writes DL-2, F-22. Revision 4: draft writes DL-7, F-26.)*
- [x] G/W/T acceptance criteria added.
- [ ] HCM domain is added to any required global registries/AGENTS updates in a separate governance prompt if needed.
- [ ] P0 inspection confirms route, service, persistence, migration, test, frontend, RBAC, audit, workflow, evidence, masking, and tokenization contracts.
- [x] P1 draft/reference-validation contract closure produces API stubs, DTO stubs, validation rules, persistence rules, audit payload contracts, fail-closed rules, and future contract-test plan.
- [x] HCM implementation prompt allowed after conditional promotion, limited to draft/reference-validation boundary only.
- [x] P2 backend draft/reference-validation build and focused tests passed. *(Revision 2: re-measured 2026-10-27 - build 0 warnings, 60/60 (E2, inspection note); tests do not cover the F-1/F-2/F-3/F-16 defects.)*
- [ ] P2 HCM draft Gateway route ownership assigned to `integration-agent` and route/build evidence recorded. *(Revision 2: unchecked - routes exist and are wider than draft ingress (F-6); build evidence is narrative (F-7).)*
- [ ] P2 frontend draft wizard/proxy build and focused Vitest passed. *(Revision 2: unchecked - narrative; no recorded evidence (F-7); not run 2026-10-27.)*
- [ ] P2 HCM startup blocker isolated and fixed; HCM `/health` returned 200 on port `5060`. *(Revision 2: unchecked - narrative; no recorded evidence (F-7).)*
- [ ] P2 unauthenticated browser smoke passed with controlled login redirect, no console errors/warnings, and no direct browser calls to service ports. *(Revision 2: unchecked - narrative; no recorded evidence (F-7).)*
- [x] P2 conditional handback recorded for draft/reference-validation slice. *(Revision 2: the handback text is recorded in this pack; its PASS verdict is narrative (F-7).)*
- [ ] P2 authenticated browser smoke completed for draft create/save/reload/reference validation/review: tenant shell visible, `canCreateDraft=true`, all four references valid, review `reviewed`, `blockingReasons=[]`, no out-of-scope controls, no direct browser service-port references, and no console errors. *(Revision 2: unchecked - narrative; no recorded evidence (F-7); "all four references valid" is also unreliable because of F-1.)*
- [x] Frontend shell and route conventions are confirmed for the approved draft/reference-validation slice.
- [ ] DataTable Golden Reference compact applicability is accepted for registry/list pages.
- [ ] Government identifier capture remains disabled until tokenization/security contract is confirmed.
- [ ] Revision 2: "Teslim dilimleri" table present with one evidence level per slice. *(Revision 4: read by reviews 02 and 03; review 03 F-26 was against DL-2's level and is answered by DL-7; left unticked until review 04.)*
- [ ] Revision 2: acceptance-criteria map present for GWT-A01 to GWT-D07. *(Revision 4: read by reviews 02 and 03; changed in Revision 4 (A12 to DL-7, audit parts split); left unticked until review 04.)*
- [ ] Revision 2: independent review 02 of this exact version with result UYGUN and no open ENGEL. *(Revision 3: review 02 recorded UYGUN DEĞİL against revision 2; superseded by the review 03 item below.)*
- [ ] Revision 2: approval row for this version (`scripts/pack_approval.py`).
- [ ] Revision 3: `## Audited Events` present with every write command once. *(Revision 4: review 03 found the eight current commands covered; extended in Revision 4; left unticked until review 04.)*
- [ ] Revision 3: independent review 03 of this exact version with result UYGUN and no open ENGEL. *(Revision 4: review 03 recorded UYGUN DEĞİL against revision 3 (F-26); superseded by the review 04 item below.)*
- [ ] Revision 3: approval row for this version (`scripts/pack_approval.py`).
- [ ] Revision 4: DL-7 row present at E5 and DL-2 free of the draft-write central audit (written; not yet reviewed).
- [ ] Revision 4: independent review 04 of this exact version with result UYGUN and no open ENGEL.
- [ ] Revision 4: approval row for this version (`scripts/pack_approval.py`).
- [ ] Revision 5: front matter has no `approval_status` line (written; not yet reviewed).
- [ ] Revision 5: independent review of this exact version with result UYGUN and no open ENGEL.
- [ ] Revision 5: approval row for this version (`scripts/pack_approval.py`).
- [ ] Revision 5: each slice's prerequisite ("Slice details" and "Open Decisions (Owner)") recorded before that slice's dispatch; the dispatching prompt cites the record.
- [ ] DL-2: P2 defects F-1, F-2, F-3, F-14, F-15, F-16 and the F-6 fixture boundary fixed and proven at the DL-2 level (E4). *(Revision 4, F-26: GWT-A12 draft-write central audit moved to DL-7.)*
- [ ] DL-2: owner decision record on permission breadth (F-13) - required whether the seed is kept or narrowed.
- [ ] DL-7: AUD-001 K4 lift confirmed for `Diten.HcmService` by the rule owner (AE-1); AE-2, AE-3, AE-4, AE-5 decided. *(Revision 4: was a DL-2 item.)*
- [ ] DL-7: BL-324 decided - the validated JWT as the tenant source for the HCM central-audit scope (Revision 4, AK-37).
- [ ] DL-7: Platform-side additions implied by AE-2 to AE-4 handled as other-module work (XMC-001 "(a) write in this work" answer from the module owner recorded first; card §7c / S-90).
- [ ] DL-7: GWT-A12 draft-write central audit proven at E5 (Platform central log, delivery outage and recovery, rejected path, cold start).
- [ ] DL-3: owner decision F-5 recorded (retro-approve or close list/detail read).
- [ ] DL-4 to DL-6: open decisions K-1 to K-9 closed for the slice in question.
- [ ] `data_mode` (and `data_mode_max_rows` if client mode) set for the registry DataTable before DL-3 can be ready-for-dev. *(Revision 3, F-19: front matter now carries `data_mode: server`; review 03 found `server` suitable for the one list screen; left unchecked until review 04.)*

## Implementation Notes

- This module is tenant-shell HCM, not Platform Admin.
- Backend service scaffold now exists at `services/Diten.HcmService`; P2 draft/reference-validation runtime is implemented there. Full MOD-0251 employee lifecycle implementation remains blocked pending later approval and contract closure.
- *(Removed as a current blocker in Revision 2: the earlier "no-valid-`HEAD` / all-untracked workspace" note is resolved; the repository has a valid `HEAD`.)*
- Existing services include Auth, Platform, Platform.Common, Platform.Contracts, DevEnablement, EnterpriseStrategy, and MDM; none is an HCM owner for MOD-0251. *(Revision 2: `Diten.HumanCapitalService` also exists and holds an external-HRIS `EmployeeProfileProjection`; its relation to MOD-0251 is open decision F-12.)*
- `MOD-0299` Position & Organization Assignment: per `module-id-registry.md:199` the SaaS Billing & Invoicing meaning is a non-executable deprecated alias migrating to MOD-0364 SaaS Billing & Invoicing, and canonical MOD-0299 stays Position & Organization Assignment. No identity reconciliation is pending; assignment workflow stays out of scope for P2 and DL-2 to DL-7.
- Registry currently does not contain an active `MOD-0251` row, but the validator proves the ID/name from the Blueprint workbook and no active collision was detected. Row addition goes in the joint registry PR (discovery record).
- Registry row 197 likewise keeps MOD-0298 "Tenant Module Entitlements" only as a deprecated alias; canonical MOD-0298 is Employee Profile Workspace (relevant to F-12).
- Contract handling (owner decision `gecici-sozlesme`, relayed by the leave CT; no decision record found in this worktree): the HCM-CORE-BUNDLE contract stays a versioned file in the repository and both sides carry contract tests (DL-5).
- Missing pack sections against `module-pack-standard.md` were not reconciled in Revision 2 (for example a dedicated "Audited Events" section, AUD-001, and `data_mode`); review 02 should check them. *(Revision 3: both added - "Audited Events" (F-18) and `data_mode: server` (F-19). Other sections of `module-pack-standard.md` were not re-checked in this revision.)*

## Follow-up Items

*Historical. Revision 2: the control plan below is superseded by "Teslim dilimleri" (DL-1 to DL-6). Evidence upload,
status change (other than termination), export and Data Quality Queue remain later slices and are not numbered now.*

Development control plan:

- P0 inspection.
- P1 contracts.
- P2 RBAC/audit.
- P3 draft backend.
- P4 registry read.
- P5 wizard UI.
- P6 approval activation.
- P8 evidence.
- P9 status change.
- P10 Data Quality Queue.
- P11 export.
- P12 closure.

Do not start any follow-up outside the approved draft/reference-validation boundary until a later pack update explicitly approves that scope.

## Audited Events

Revision 3 (review 02 F-18; `module-pack-standard.md` §10.1, AUD-001; `audit-trail-standard.md` §3-§6). One row per
write command, each exactly once: the eight commands in
`services/Diten.HcmService/src/Diten.HcmService.Application/Features/CoreHrEmployeeMaster/Commands/` today, the
commands of DL-4 to DL-6, and the remaining write commands of "Owned Objects" (later slices). Event names come from
"Audit Event Catalog" and "Audit Contract For This Slice"; a name marked *proposed* is not in the catalog and is not
decided. Command names marked *proposed* do not exist in code. This table is not approval to code any row.
Revision 4 (review 03 F-26): the central audit of the eight existing commands is slice DL-7 (E5), no longer DL-2; the
forwarder registration, the Mongo transactions and the ledger change below are DL-7 work.

Path today: none. The HCM ledger lists one trail, `hcm-taslak-denetimi`, path `aday` (`IDraftAuditService.EmitAsync`,
application log only), and all eight current commands under "Bilinen borç". Target path for every audited row: `b`,
the shared forwarder `Diten.BuildingBlocks.CentralAudit` (BL-501; on `main`: 528cd415d, 2026-10-14; 111c60c46,
2026-10-23). How it works (read from `CentralAuditBehavior.cs`, `CentralAuditRecorder.cs`,
`CentralAuditServiceCollectionExtensions.cs`): the command carries `ICentralAuditDeclaration`; `CentralAuditBehavior`
opens one event per marked command; the repository writes it into the service's own outbox inside the command's Mongo
transaction (`CentralAuditTransaction.EnlistAsync`); `CentralAuditDispatcher` delivers it to Platform
`POST /api/internal/audit/append`. A failed command leaves no deliverable record. InventoryService and
SupplyChainService register it with `AddCentralAudit<…>(…, options => options.RequireEnlistment = true)` in their
`Program.cs`. `Diten.HcmService` does not register it and has no Mongo transaction today (DL-7 work). The outbox row
is local; delivery is not proven by it: `PlatformCentralAuditClient.SendAsync` returns `Retry` when the Platform
address / internal key is not configured and `Rejected` when the category, operation or actor type is not a Platform
audit enum name (`PlatformCentralAuditClient.cs:49-60`). DL-7 therefore measures delivery into the Platform central log
(E5).

**Open decision AE-1 (not resolved here):** `audit-trail-standard.md` §5 K4 and `module-pack-standard.md` §10.1 still say
that new write commands in HCM are blocked "until the shared forwarder arrives", although the forwarder exists on `main`.
The rule owner must confirm that K4 is lifted for `Diten.HcmService`. This pack does not claim it is lifted; until it is
confirmed, no row below is coded - neither DL-7's wiring of the eight existing commands (Revision 4; DL-2's in Revision
3) nor any new command of DL-4 or later.

| Komut | Olay adı | Nesne | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateEmployeeDraftCommand (exists; DL-7) | `employee_draft.created` | EmployeeDraftSession | b | AfterState: `draftSchemaVersion`, `currentStep`, `version`; idempotency key as hash only | — |
| PatchEmployeeDraftCommand (exists; DL-7) | `employee_draft.updated` | EmployeeDraftSession | b | `stepCode` and the names of the changed step fields; `version` before/after; no field values | — |
| ValidateDraftReferencesCommand (exists; DL-7) | `employee_draft.references_validated` | EmployeeDraftSession | b | Reference types checked, aggregate result, reason codes, `version` before/after | — (not İ5: it persists the validation summary on the draft) |
| ReviewEmployeeDraftCommand (exists; DL-7) | `employee_draft.reviewed` | EmployeeDraftSession | b | Before/After: `reviewState`; blocking reason codes; `version` | — |
| SubmitEmployeeDraftCommand (exists, blocked: 409 today; marker in DL-7, behaviour and audit row in DL-4) | `employee.submitted_for_approval` | EmployeeDraftSession | b | Before/After: draft and employee status; workflow reference id | — (today it changes nothing and fails, so no record is filed) |
| ConsumeWorkflowDecisionCommand (exists, blocked: 409 today; marker in DL-7, behaviour and audit row in DL-4) | `employee.approved` or `employee.rejected` (by decision) | Employee | b | Before/After: approval status, employee status; workflow reference id, decision code | — (today it changes nothing and fails, so no record is filed) |
| EnsureEmployeeSmokeFixtureCommand (exists; audit in DL-7, F-6 boundary in DL-2) | `employee_smoke_fixture.ensured` *(proposed)* | Employee (fixture) | b | AfterState: `employeeId`, `employeeNumber`; revived-from-soft-delete flag | — (if F-6 removes the endpoint, the row goes with the command) |
| CleanupEmployeeSmokeFixtureCommand (exists; audit in DL-7, F-6 boundary in DL-2) | `employee_smoke_fixture.cleaned_up` *(proposed)* | Employee (fixture) | b | BeforeState: `employeeId`, `employeeNumber`; deleted flag | — (as above) |
| ActivateEmployeeCommand *(proposed name; DL-4)* | `employee.status.changed` | Employee | b | Before/After: employee status (Pending Approval → Active); `employmentRecordId`; workflow reference id | — |
| TerminateEmployeeCommand *(proposed name; DL-4)* | `employee.status.changed` | Employee | b | Before/After: employee status; changed field names `terminationDate`, `endDate` | — |
| ChangeEmployeeHireDateCommand *(proposed name; DL-4)* | `employee.employment_record.updated` | EmploymentRecord *(proposed; where `hireDate` lives is decided in DL-4)* | b | Changed field name `hireDate`; `version` before/after | — |
| UpdateEmploymentRecordCommand *(proposed name; DL-4 "change employment"; Owned Objects "Update employment record")* | `employee.employment_record.updated` | EmploymentRecord | b | Changed field names (for example `legalEntityId`, `organizationUnitId`, `positionId`, `contractType`, `seniorityDate`, `ftePercent`, `workLocationId`); `version` before/after | — |
| UpdateEmployeeProfileCommand *(proposed name; later slice; Owned Objects "Update employee profile")* | `employee.profile.updated` | Employee | b | Changed field names only; no values | — |
| RequestEmployeeStatusChangeCommand *(proposed name; later status-change slice; Owned Objects "Request status change")* | `employee.status.changed` | Employee | b | Before/After: employee status; reason category code | — |
| LinkEmployeeEvidenceCommand *(proposed name; later evidence slice; Owned Objects "Link employee evidence")* | `employee.evidence_linked` | EmployeeDocumentLink | b | AfterState: `evidenceLinkId`, document type code | — |
| ExportEmployeeListCommand *(proposed name; later export slice; Owned Objects "Export employee list")* | `employee.export.requested`, `employee.export.completed` | Employee export | b | Format, filter hash, bounded row count | — (bulk personal-data export: no record, no file - `audit-trail-standard.md` §4.3) |
| PublishEmployeeEventsCommand *(proposed name; DL-6 outbox publisher, only if built as a command)* | — | — | — | — | İ2 - a scheduled technical trigger that only delivers integration events already committed by audited commands; it changes no employee record itself. |

*Nesne* (Revision 4, AK-38): one entity type per row, because a declaration carries one `EntityType`
(`CentralAuditMetadata`, `ICentralAuditDeclaration.cs:30`); other objects a command touches are not in the record.

DL-5 adds no write command: the employment-summary endpoints and the `updatedSince` read are reads, and the user-person
link is written by its owning module (K-1). No read row is needed today: AUD-001 §7 sensitive single reads apply only
when sensitive fields are returned, and DL-3 returns none (DET-GWT-04); enabling `view_sensitive` adds a read row first.
`mod0251.data_quality.resolve` has no command in "Owned Objects"; when one is added it gets a row here first.

**Pack event name → central record (Revision 4, AK-27).** The shared record carries no event name and no before/after
or changed-field field: `CentralAuditOutboxRecord` holds tenant, company, actor (id, display name, type), source
service and module, `Category`, `Operation`, `EntityType`, `EntityId`, `RequestType`, correlation id and time, and
`PlatformCentralAuditClient.cs:63-89` sends only those (plus event id and outcome). `RequestType` is the command type
name (`CentralAuditBehavior.cs:36`). A pack event name is therefore identified in the central log by RequestType +
Operation + EntityType. Operations are Platform enum names (`CentralAuditPlatformValues`); every value below is
*proposed* until DL-7 codes it, and the category for all rows is AE-4 (proposed `MasterData`, a Platform name today).

| Pack event name | RequestType | Operation | EntityType |
|---|---|---|---|
| `employee_draft.created` | `CreateEmployeeDraftCommand` | `Create` | `EmployeeDraftSession` |
| `employee_draft.updated` | `PatchEmployeeDraftCommand` | `Update` | `EmployeeDraftSession` |
| `employee_draft.references_validated` | `ValidateDraftReferencesCommand` | `Update` | `EmployeeDraftSession` |
| `employee_draft.reviewed` | `ReviewEmployeeDraftCommand` | `LifecycleTransition` | `EmployeeDraftSession` |
| `employee_smoke_fixture.ensured` *(proposed)* | `EnsureEmployeeSmokeFixtureCommand` | `Create` | `Employee` |
| `employee_smoke_fixture.cleaned_up` *(proposed)* | `CleanupEmployeeSmokeFixtureCommand` | `Delete` | `Employee` |
| `employee.submitted_for_approval` | `SubmitEmployeeDraftCommand` | `LifecycleTransition` | `EmployeeDraftSession` (DL-4; no row while it returns 409) |
| `employee.approved` / `employee.rejected` | `ConsumeWorkflowDecisionCommand` | `LifecycleTransition` | `Employee` (DL-4; approved and rejected are told apart only if AE-3 adds a field; no row while it returns 409) |
| `employee.access_denied` | `EmployeeAccessDenied` *(proposed; a distinct name filed by the forwarder change below - a `[HasPermission]` 403 never reaches MediatR, and an empty RequestType would be refused by Platform)* | `PermissionDenied` | `EmployeeDraftSession` |
| `employee.conflict` | `EmployeeDraftConflict` *(proposed; distinct from `PatchEmployeeDraftCommand`, so it cannot be mistaken for `employee_draft.updated`)* | `Update` | `EmployeeDraftSession` |

The last two rows have no path today (AK-34): the forwarder records only commands that succeed, sends only the outcomes
Succeeded / Unconfirmed (`CentralAuditPlatformValues.cs:61-67`, `PlatformCentralAuditClient.cs:75`) although Platform
knows Failed / Denied, and the record has no reason-code field (`CentralAuditOutboxRecord.cs:17-67`). They need AE-2
(refused commands are recorded) and a named forwarder change in `Diten.BuildingBlocks.CentralAudit`: a refused outcome
(Failed / Denied) on the record and an entry point for refusals that never reach MediatR. That change is other-module
work (operating card §7c, S-90; the module owner's XMC-001 "(a) write in this work" answer first). Until both exist,
or AE-2 records the gap as accepted, the two rows are not coded and their RequestType names stay proposed. They apply
to GWT-A10, the GWT-A11 denial half and GWT-A13.

**Central-log content measured by DL-7 (Revision 4, AK-27), per AE-3 outcome.** Both outcomes: tenant, actor id and
type, correlation id, `Category`, `Operation`, `EntityType`, `EntityId`, `RequestType`, outcome, source service and
module - and none of the "Never written" items below.

- *AE-3 adds a field to the shared record:* in addition, the pack event name and the content of the "Önceki/sonraki ya
  da değişen alanlar" column above (field names, status codes, version - no personal-data values; `employeeNumber` only if AE-5
  allows it) appear in the central log; DL-7 measures them for each of the six writing commands, and for
  the two refusal rows (once they exist, AK-34) a denial or conflict reason code if the added field carries one.
- *AE-3 accepts entity + operation only:* "safe change information" is Operation + EntityType + EntityId; the pack
  event name is read from the mapping table above; the "Önceki/sonraki ya da değişen alanlar" column is not in the
  central log, nor a reason code for the refusal rows, and the DL-7 acceptance record names that as an accepted gap
  (the same exit rule as AE-2).

**Reads and refusals (Revision 4, AK-30).** After DL-2's F-3 fix (not today, AK-48), draft read (`GET` draft) and review
read are to return no KVKK special-category or HR-confidential field (government identifier rejected, sensitive keys
masked or omitted), so by
`audit-trail-standard.md` §7 they have **no audit row**; this covers the read half of GWT-A11, and DL-7 measures only
that such a field is not returned. Returning one later adds a read row here first. The denial half of GWT-A11 and
GWT-A10 (`employee.access_denied`) and GWT-A13 (`employee.conflict`) are the two refusal rows of the mapping table and
depend on AE-2.

K2 class (identity / tenant / special-category personal data; `audit-trail-standard.md` §4.3-§4.4) and fail-closed:

| Komut | K2 | Why | Fail-closed path | Test |
|---|---|---|---|---|
| ConsumeWorkflowDecisionCommand | yes (from DL-4) | An approved decision creates an employee of record (worker identity used by the user-person link and access). | Enlisted in the command's local Mongo transaction; outbox insert fails → transaction aborts → no status change, controlled failure. | Forced outbox failure → employee and draft unchanged; sabotage: remove enlistment → test red. |
| ActivateEmployeeCommand | yes | Creates the employee of record. | Same. | Same, plus no `hcm.employee.activated.v1` outbox event without the audit row (DL-6). |
| TerminateEmployeeCommand | yes | Ends the employment that identity and access depend on. | Same. | Same. |
| RequestEmployeeStatusChangeCommand | yes | Suspension / rehire change the employee of record. | Same. | Same. |
| EnsureEmployeeSmokeFixtureCommand, CleanupEmployeeSmokeFixtureCommand | yes | They create, revive or delete an Employee record of the same type as activation. | Same. | Same (DL-7). |
| All other rows | no | Draft session, business employment data or evidence metadata; no KVKK special-category field is captured (government identifier is rejected, F-3). A later special-category field makes the command K2. | The same enlisted path: GWT-A12 asks every critical draft write to fail closed, K2 or not. | DL-7 / DL-4 audit tests in "Test Expectations". |

Conditions on path `b` that this pack depends on (read from code; not runtime-verified):

- *(Revision 4, AK-29: corrected.)* With `RequireEnlistment = true` - the setting DL-7 registers - a marked command that
  succeeds without enlisting writes **no** central record, only an information log line ("succeeded without writing";
  `CentralAuditRecorder.cs:160-169`, `CentralAuditOptions.cs:38-46`); the forwarder treats it as an idempotent replay
  or no-op. A forgotten enlistment therefore drops the record silently, which is why DL-7 carries a structural guard
  that every HCM repository transaction enlists ("Test Expectations"), not only per-command sabotage. Only a command
  marked `ICentralAuditOutsideTransaction` gets its record written after it succeeds, with the "gap" named in
  `CentralAuditRecorder.CompleteAsync` (a crash in between loses it). No MOD-0251 command may use
  `ICentralAuditOutsideTransaction`; submit and workflow decision enlist in their local state-change transaction.
- `Diten.HcmService` has no Mongo transaction today; DL-7 adds them to the draft and fixture repositories (Mongo
  transactions need a replica set; the HCM test store was not checked).
- *(Revision 4, AK-37.)* The record's tenant must come from the signed token / resolved scope, and Platform files it
  there without re-checking (`ICentralAuditScope.cs:7-10`, `InternalAuditController`). `Diten.HcmService` resolves the
  tenant from the `X-Tenant-Id` header and reads the JWT only to refuse a contradiction (`TenantResolutionMiddleware.cs:34-59`;
  the JWT as tenant source is the separate trust decision BL-324), and its `ITenantContext` carries no user or
  correlation. DL-7 therefore adds an HCM `ICentralAuditScope` adapter that reads tenant and user from the validated
  JWT (user: the `sub` claim, which the default JwtBearer claim mapping exposes as `ClaimTypes.NameIdentifier`; HCM
  `Program.cs:58-71` keeps the default mapping) and correlation from an HCM correlation context filled by a new
  correlation middleware from the gateway's request header (the JWT carries no per-request correlation; HCM has no
  correlation context today, `ITenantContext.cs:3-8`; Inventory uses `ICorrelationContext`,
  `InventoryCentralAuditScope.cs:19,62-68`) (model: `services/Diten.InventoryService/src/Diten.InventoryService.Api/CentralAudit/InventoryCentralAuditScope.cs:7-24,61-75`),
  after open decision BL-324.
- If the caller scope is not resolved, the forwarder files no record and lets the command run; HCM handlers must refuse
  the write without tenant and actor (tenant check exists, `EmployeeDraftHandlerHelpers.TryGetTenantId`; actor check is
  DL-7 work).
- Platform address or key unset: rows accumulate in the outbox, a warning is logged and the health check is Degraded;
  the command is not stopped. Delivery is asynchronous.
- Refused commands (403, 409, validation) leave no central record (AE-2); the record carries no changed-field field
  (AE-3); Platform has no HCM audit category (AE-4).
- The ledger change (HCM `## İzler` row for path `b`, Control Tower acceptance, removal of the eight "Bilinen borç"
  lines) is DL-7 work under `tests/architecture/audit-ledger/`, as done for SupplyChainService
  (`CT-DECISIONS.md`, 2026-10-21).

Never written to the audit record (any row): date of birth; government identifier (raw, token or hash); personal email;
phone; work email; the employee's legal name; nationality; address; bank or tax values; JWT, S2S key (`X-Internal-Api-Key`) or any
other token or secret; raw idempotency key (hash only); draft payload or step payload; provider response bodies; free
text; exported row data. A changed field is written by name; no personal-data value is written (AK-39); whether the employee number may be
written is open decision AE-5 (AK-48). The forwarder sends the actor's display name (`PlatformCentralAuditClient.cs:68`);
the HCM scope adapter sends none (*proposed*: `ActorDisplayName = null`, the actor is named by id only), decided in DL-7. The shared forwarder copies no request
body into the record (`ICentralAuditDeclaration`: "the declaration names the entity, not its data"); a future
extension for AE-3 must keep this list.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi |
|---|---|---|---|
| DL-1 | Scope, evidence and identity reconciliation (documentation) | Revision 2 Review Reconciliation, Corrected Current State, Open Decisions (Owner), Authoritative Baseline After Containment, MOD-0299 Defer / Reconcile Decision, Governance Blocker Scenarios, Implementation Notes, Follow-up Items | E1 |
| DL-2 | P2 correction (reference validation, draft binding, PII guard, UAS-001, atomic idempotency, callback authorization, smoke-fixture boundary) | Known Defects in the Approved P2 Slice (Review 01), P1 Draft / Reference Validation Contract, Validation Contract, Persistence Contract, Fail-Closed Behavior, Protected Action / Endpoint Map, RBAC Fail-Closed Rules, Gateway / API Routing Decision, Layout & Shell Contract, First Approved Draft / Reference-Validation Slice, Acceptance Criteria → Test → Evidence Map | E4 |
| DL-3 | Employee registry and detail read (owner decision F-5 first, then implementation) | P4 Employee Registry Read-Only Scope Contract, Registry Purpose and Boundary, Allowed Records, API Boundary, UI Boundary, P4 Acceptance Criteria, P4 Test Expectations, Detail Read Acceptance (DL-3), Frontend File Contract | E4 |
| DL-4 | Activation and termination (submit, MOD-0023 decision, Active, audit of its own new commands, new employment fields) | Entity Fields, Workflow First-Slice De-scope Decision, MOD-0251 Audit Contract, Audit Fail-Closed Rules, Audited Events, Deferred R1 Sequence Scenarios | E5 |
| DL-5 | User-person link consumption and employment-summary reads | Open Decisions (Owner), Ownership and Boundaries, API / Interface Registry Stubs, Dependencies | E5 |
| DL-6 | Employee events, outbox and incremental sync | API / Interface Registry Stubs, Audit Event List and Safe Payload Policy, Next-Sequence Decision Table | E5 |
| DL-7 | Draft-write central audit (HCM → MOD-0021 via shared forwarder) | Audited Events, MOD-0251 Audit Contract, Audit Event Catalog, Safe Payload Matrix, Audit Fail-Closed Rules, Audit Contract For This Slice, Fail-Closed Behavior, Acceptance Criteria → Test → Evidence Map, Acceptance Criteria, Test Expectations, Open Decisions (Owner) | E5 |

`gerekli kanıt seviyesi` is the minimum evidence level (SOP §23.1) the acceptance record of a work package of that
slice must reach; the acceptance record cites this table row as `required_evidence_source`. Slices are a proposal from
review 01 (F-8) adopted in this draft; this table is not approval by itself
(Revision 5: what an approval row of this version makes dispatchable is stated in the Revision 5 note). Writing slices are at least E4 and cross-module
integration slices E5. Revision 3: DL-2 stays E4 although it now carries the draft-write central audit, because SOP
§23.1 E4 already covers persistence + audit + failure paths, and the HCM side of path `b` (the outbox row committed in
the draft write's own transaction, no write without it) is local, while delivery to Platform is the shared forwarder's
own accepted behaviour (BL-501). Revision 4 (review 03 F-26, owner word `ayri-dilim`): that reasoning is withdrawn.
HCM → MOD-0021 Audit Trail through the shared forwarder is a cross-module seam, which SOP §23.1 puts at E5, and a local
outbox row does not prove delivery (`PlatformCentralAuditClient.cs:49-60`: `Retry` when not configured, `Rejected` for
an unknown category / operation / actor type). The draft-write central audit is therefore its own slice DL-7 at E5;
DL-2 keeps its code fixes, F-14 authorization, the F-6 fixture boundary and the F-13 decision at E4. **P2 closure (the
acceptance of the approved slice) needs both DL-2 and DL-7; DL-2 alone does not close GWT-A12.**

Slice details (prerequisite · in scope · out of scope · exit condition):

- **DL-1** - prerequisite: none. In scope: this pack's text only - current-state corrections, open decisions, the
  acceptance map, the GWT-B07 wording fix. Out of scope: any code, `domain-config.md` (separate change). Exit: review 02
  of this version records no open ENGEL against DL-1 items.
- **DL-2** - prerequisite: approval of this revision; a recorded owner decision on permission breadth (F-13) whether
  the seed is kept or narrowed (Revision 3, F-25). *(Revision 4, F-26: AE-1 to AE-4 are no longer DL-2 prerequisites;
  they are DL-7's.)* In scope: F-1 (read the provider envelope and `referenceable`, match the requested ids; HTTP-200 negative, missing and
  malformed bodies fail closed); F-2 (bind the validation result to the draft's ids and version, invalidate it on any
  reference change); F-3 (field allowlist, recursive PII guard including `personal_email`, `phone` and
  `ClientValidationState`); F-15 (no page skeleton for unauthorized users, UAS-001); F-16 (atomic create idempotency with
  a unique index on `TenantId` + `CreateIdempotencyKeyHash`, duplicate-key returns the original result); F-14
  (`[HasPermission]` or an approved service-actor authorization on `workflow-decisions`); F-6 smoke-fixture boundary
  (access and data limits; audit limits: DL-7; no rewrite of an existing employee number, no revival of soft-deleted
  rows; Gateway narrowing only by `integration-agent`). Out of scope: the draft-write and fixture-write central audit,
  GWT-A12 and the audit parts of GWT-A01, GWT-A02, GWT-A08, GWT-A10, GWT-A11 and GWT-A13, and the audit bullets of
  "Fail-Closed Behavior" (DL-7 since Revision 4, F-26);
  activation and the audit of DL-4's new commands (DL-4); registry/detail (DL-3). Exit: each item has a negative test
  that fails on the old code (sabotage evidence) and an E4 run on the commit carrying this pack version. DL-2's exit
  does not cover GWT-A12 or those audit parts: P2 closure (the approved slice's acceptance) requires both DL-2 and DL-7
  accepted. Order rule (Revision 4, AK-31): DL-2 and DL-7 both change the create/patch write path (F-16 atomic create;
  DL-7 Mongo transactions in `EmployeeDraftSessionRepository`); whichever lands second reruns the first slice's
  acceptance tests on its own commit, and P2 closure rests on one combined DL-2 + DL-7 run on the commit carrying this
  pack version (operating card S-86).
- **DL-3** - prerequisite: owner decision F-5 (retro-approve or close). If closed: remove list/detail API, pages and
  menu entry (code work). If retro-approved: implement "P4 Employee Registry Read-Only Scope Contract" (approved-record /
  fixture separation, masking of `WorkEmail` / `NationalityCode` per MOD-0314 HR Governance & Sensitive Access Controls
  policy or omission, `data_mode: server` per front matter) and "Detail Read Acceptance (DL-3)". Out of scope: export,
  status, evidence, lifecycle actions. Exit: E4 run of REG-GWT-01 to REG-GWT-08 and DET-GWT-01 to DET-GWT-06 (Revision
  3, F-20), or a recorded removal with its own E4 negative run.
- **DL-4** - prerequisite: DL-2 accepted; DL-7 accepted (Revision 4: DL-4's new commands use the forwarder
  registration and Mongo transaction support DL-7 delivers); executable MOD-0023 Workflow Designer contract (source authentication,
  replay/idempotency, tenant validation); K-2, K-5, K-6, K-7 and K-8 decided. In scope: submit → MOD-0023 decision →
  Active, termination (`terminationDate` = last working day, inclusive), central audit of DL-4's own new commands only
  (submit, workflow decision, activation, termination, hire-date change, employment change; same path `b` as DL-7, actor
  and correlation set, fail-closed; the draft-write audit is DL-7's since Revision 4, F-26 (DL-2's in Revision 3); for
  submit and workflow decision the marker is DL-7's and the behaviour and audit row are DL-4's, AK-33), new fields `seniorityDate`, `ftePercent`, `workLocationId`. Out of scope: suspension, rehire and other
  status changes, evidence. Exit: E5 integrated run with MOD-0023 and MOD-0021, including audit-failure (a local outbox write failure, AK-28) and
  workflow-unavailable negatives.
- **DL-5** - prerequisite: DL-4 accepted; `bag-kisi` (decided); K-1, K-3, K-4, K-8, K-9 decided; the module that holds
  the user-person link approved by its owner (XMC-001). In scope: consume the link through a port
  (`ResolvePersonId`, `ResolveUserId`; no `UserId` on `Employee`); `employment-summary` reads with `asOf` per link
  contract v0.3 §2 and §2.1 (`/me`, `/{employeeId}`, `/by-person/{personId}`, `employment-summaries` with
  `updatedSince`); the contract stays a versioned file in the repository with contract tests on both sides, including a
  producer-side contract test in `Diten.HcmService`. Out of scope: writing the link (owned by the other module), display
  name (read from MOD-0288). Exit: E5 run with the consuming module (MOD-0280 Time, Attendance & Leave Management) and
  both contract tests green on the same contract version.
- **DL-6** - prerequisite: DL-4 and DL-5 accepted (Revision 3, F-21: the `updatedSince` read that sync recovery uses
  is delivered in DL-5; Revision 4: DL-7 is reached through DL-4, so the Mongo transaction support DL-7
  introduces exists before the event outbox is built); K-2 and K-4 decided. In scope: outbox in `Diten.HcmService` (at-least-once),
  events `hcm.employee.activated.v1`, `hcm.employee.terminated.v1`, `hcm.employee.hire-date-changed.v1`,
  `hcm.employee.employment-changed.v1` (names open, K-2) with the v0.3 §3 envelope; `updatedSince` incremental sync as the
  mandatory fallback. Out of scope: forbidden fields of v0.3 §2 in any payload. Exit: E5 run proving delivery, consumer
  de-duplication by `eventId`/`version`, and sync recovery after a missed event.
- **DL-7** (Revision 4, review 03 F-26, owner word `ayri-dilim`) - prerequisite: approval of this revision; AE-1 (K4
  lift confirmed for `Diten.HcmService` by the AUD-001 rule owner); AE-2, AE-3, AE-4 and AE-5 decided; BL-324 decided
  (AK-37). The Platform-side and
  Building Blocks-side additions those decisions imply (for example an HCM `AuditCategory`, a changed-field-names field
  on the shared record, recording refused commands) are other-module work: per the operating card §7c (S-90), the
  requesting CT writes them only after the module owner's explicit XMC-001 "(a) write in this work" answer is recorded,
  first as a proposal to that module's pack, then code. Not dependent on DL-3, DL-4, DL-5 or DL-6. On DL-2 (AK-40) only two
  measurements depend: the GWT-A11 read half (rests on the F-3 masking) and the two smoke-fixture commands (conditional
  on the F-6 outcome; if F-6 removes the endpoint there is nothing to measure); both are measured after DL-2 is
  accepted or on the combined AK-31 run. The rejected-category path needs no DL-2 result (test-only declaration). In
  scope: register the shared forwarder `Diten.BuildingBlocks.CentralAudit` in `Diten.HcmService` (`AddCentralAudit<…>`
  with `RequireEnlistment = true`) and its configuration keys `PlatformRegistration:BaseUrl` and
  `PlatformRegistration:InternalApiKey` (`CentralAuditOptions.cs:15`); an HCM `ICentralAuditScope` adapter that reads
  tenant and user (`sub` / `ClaimTypes.NameIdentifier`) from the validated JWT (AK-37; model
  `InventoryCentralAuditScope.cs:7-24,61-75`) and correlation from a new HCM correlation context and middleware fed by
  the gateway request header (AK-41); `MapHealthChecks` for the central-audit health check (HCM maps only `/health`
  today, `Program.cs:123`, on `CreateEmptyBuilder`, `Program.cs:10-15`; AK-47); the fate of the old trail (AK-47,
  *proposed*): the log-only `DraftAuditService` and the unused `GovernedHcmAuditAppendClient` are removed, and the
  ledger trail `hcm-taslak-denetimi` (path `aday`) is replaced by the path `b` trail - no double trail; the
  controlled failure of "Audit Fail-Closed Rules" in place of today's generic 500 (AK-39); Mongo transaction support in the draft and fixture repositories (DL-7 work; replica
  set needed); mark each of the eight existing draft/fixture write commands ("Audited Events"; values from the mapping
  table "Pack event name → central record") and enlist the audit record of the six that write in the command's own
  Mongo transaction, only on a successful write (a 409 path leaves no row, AK-46) - submit and workflow decision get
  only the marker here, their behaviour and audit row are DL-4's (AK-33); until DL-4 the marker serves the ledger and
  the structural guard only, because production never dispatches them (`EmployeeDraftsController.cs:80-99`; AK-49); actor and correlation set; no draft or fixture change when the local outbox row cannot be written (the only
  audit failure that blocks a write, AK-28); the structural guard that every HCM repository transaction enlists (AK-29);
  the refusal records of GWT-A10 / GWT-A11 / GWT-A13 as AE-2 decides; HCM ledger trail for path `b` accepted by
  Control Tower and the eight "Bilinen borç" lines removed. Out of scope: the five code fixes, F-14, F-6 and F-13
  (DL-2); the audit of DL-4's new commands (DL-4, which reuses DL-7's registration). Order with DL-2: the AK-31 rule in
  DL-2 applies. Exit (E5, review 03 F-26): the DL-7 audit tests and the structural guard of "Test Expectations" pass
  with sabotage evidence; on one integrated run of `Diten.HcmService` with Platform (MOD-0021 Audit Trail): a real HCM
  draft write is visible in the Platform central log with the minimum content for the recorded AE-3 outcome
  ("Central-log content measured by DL-7"; tenant, actor, correlation, object; event name and changed-field names only
  if AE-3 adds the field); a delivery outage (Platform address / key not configured, and Platform down) followed by
  recovery is measured - the write commits and is not blocked (AK-28); not configured: the row stays `Pending` with
  `Attempts` unchanged and health Degraded; Platform down: `Pending` with `Attempts` / `LastError` updated (outcome
  `Retry`), health Degraded only past `StaleAfter`; afterwards `Delivered` once (AK-35); the rejected category/operation
  path is measured with a test-only declaration - the command is not stopped and the `Rejected` state is visible
  (AK-40); a forced local outbox-insert failure blocks the write with the controlled failure, not a 500 (AK-39); a
  token without a `tenant_id` claim plus an `X-Tenant-Id` header is refused and neither data nor a record is created
  (AK-42; exact rule after BL-324); the record's `CorrelationId` equals the request's correlation id, not the event id
  (AK-41); with the process stopped between commit and confirmation the row is released after `OrphanAfter` and
  appears as Unconfirmed (AK-46); an HCM cold start keeps
  undelivered outbox rows and delivers them; submit and workflow decision, dispatched through MediatR in the test, leave no row (AK-49); the audit parts of
  GWT-A01, GWT-A02 and GWT-A08 are measured (actor and correlation on the `CreateEmployeeDraftCommand` /
  `PatchEmployeeDraftCommand` records; the reloaded draft's id finds its records by EntityId); an idempotent replay adds
  no second record; GWT-A11 read half: draft review returns no special-category or HR-confidential field, so no read row
  is required (AK-30; on the DL-2 F-3 result); once AE-2 and the forwarder change of "Audited Events" (AK-34) exist, a
  403 on a draft endpoint leaves an `employee.access_denied` record (GWT-A10 and the GWT-A11
  denial half) and a stale-ETag save an `employee.conflict` record (GWT-A13), each read back from the Platform central
  log under its own RequestType with a refused outcome (a reason code only if AE-3 adds the field). Accepted-gap rule: if the AE-2 decision (Control Tower with Building Blocks owner) records the refusal gap as
  accepted, or the AE-3 decision records "entity + operation only" as accepted, the DL-7 acceptance record names that
  gap instead of measuring it. Run on the commit carrying this pack version.
- Later, not numbered now: evidence upload/link, status change beyond termination, export, Data Quality Queue.
