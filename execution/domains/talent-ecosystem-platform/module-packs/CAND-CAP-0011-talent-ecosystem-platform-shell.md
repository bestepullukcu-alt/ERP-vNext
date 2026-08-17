---
id: CAND-CAP-0011
name: Talent Ecosystem Platform Shell
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0011-talent-ecosystem-platform-shell
started: 2026-08-12
target: 2026-09-15
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
candidate_identity: CAND-CAP-0011
legacy_excel_id: MOD-0321
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-authorized-first-slice
runtime_owner_key: tep.shell
permission_namespace: tep.shell.*
---

# CAND-CAP-0011 - Talent Ecosystem Platform Shell

> Status: done. The first TEP backend/API shell metadata contract slice under
> runtime owner/key `tep.shell` and permission namespace `tep.shell.*` has been
> implemented, reviewed, and promoted to done. This pack does not authorize
> frontend, Gateway direct edits, candidate/talent identity, association
> registry, consent engine, reference exchange, reputation, risk, analytics, or
> broad TEP business runtime.
> `CAND-CAP-0011` remains a governance/documentation identity only and must
> never be written into runtime literals. `MOD-0321` remains blocked.

## 1. Module Summary

`CAND-CAP-0011` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform workspace shell and R2 MVP boundary named
`Talent Ecosystem Platform Shell`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`,
where the R2-A Excel/DCP row arrived as `MOD-0321`. `MOD-0321` must not be used
for TEP because it is absent from the Blueprint and has no canonical registry
row. EA/registry owner approval created `CAND-CAP-0011` as a temporary
governance identity pending future canonical MOD allocation.

This pack defines the TEP domain shell boundary, R2 workspace sequencing, and
first backend/API shell metadata contract after HCM R1 foundation completion.
It does not create candidate identity, association registry, consent policy,
reference exchange, reputation, risk, or analytics runtime behavior.

## 2. Ownership and Boundaries

Owned by this ready-for-dev pack:

- Native TEP workspace shell governance and first shell metadata boundary.
- TEP domain ownership boundary against HCM and Platform Shared Services.
- R2 MVP sequencing contract for future TEP module packs.
- Backend/API shell metadata readiness surface for future TEP shell/navigation
  decisions.
- Dependency contract references to completed HCM R1 foundation modules.

Not owned by this pack:

- TEP service scaffold beyond the completed scaffold host.
- Candidate/talent identity, talent profile, association membership, verified HR
  participant/company access, consent policy, review board, trust engine,
  reference exchange, rehire recommendation, dispute management, reputation,
  risk, analytics, or marketplace runtime behavior.
- HCM employee/employment master ownership or employee projection ownership.
- PSS person/org/position directory ownership or external provider ownership.
- Frontend menus, Razor views, JavaScript, DataTables, RESX, gateway routes,
  provider adapters, webhooks, background jobs, broad business database
  collections, or raw/sensitive payload persistence.

## 3. Owned Objects

Governance-only planning objects:

| Object | Type | Purpose |
|---|---|---|
| TepWorkspaceShellContract | Governance contract | Defines the future TEP workspace shell boundary. |
| TepDomainOwnershipBoundary | Governance contract | Defines how TEP consumes HCM/PSS without ownership transfer. |
| TepR2MvpSequencingPlan | Governance plan | Orders first R2 module packs after EA/legal/privacy decisions. |
| TepRuntimeAuthorizationDecisionLog | Governance plan | Records the approved first-slice runtime waiver and remaining exclusions. |
| TepShellPermissionNamespacePlan | Governance plan | Records approved `tep.shell.*` namespace without candidate literals. |

Runtime first-slice objects:

| Object | Type | Purpose |
|---|---|---|
| TepShellMetadata | Runtime metadata contract | Captures shell readiness, dependency state, and shell status without talent/candidate identity. |
| TepShellDependencyState | Runtime metadata value | Records HCM/PSS dependency states as `Available`, `Deferred`, or fail-closed validation outcomes. |

No candidate identity, talent profile, association membership, consent policy,
reference exchange, reputation, risk, analytics, frontend object, Gateway route,
or broad business database collection is authorized by this pack.

## 4. Entity Fields

Approved first-slice shell metadata fields:

- `Code`
- `DisplayName`
- `ShellState`
- `HcmFoundationState`
- `PrivacyLegalState`
- `ConsentBoundaryState`
- `VisibilityBoundaryState`
- `SourceContractVersion`
- `DependencyStates`
- `LastEvaluatedAt`
- `ShellVersion`

Fields must not contain `CAND-CAP-0011` or `MOD-0321` as runtime module
literals. No candidate/talent profile fields, association membership fields,
reference exchange data, consent records, reputation/risk data, raw provider
payloads, credentials, tokens, secrets, bank/tax/payroll/payslip,
biometric/geolocation, national ID, DOB, home address, or PII-heavy fields are
authorized.

## 5. Repo Scope

Authorized runtime scope for this ready-for-dev pack:

- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md`
- `services/Diten.TalentEcosystemService/**`

Runtime scope is limited to the first backend/API shell metadata contract slice
inside the already scaffolded `Diten.TalentEcosystemService`. Frontend and
Gateway remain closed.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- `services/Diten.AuthService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `AGENTS.md`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

Completed HCM foundation dependencies:

- `CAND-CAP-0006` - HR Capability Block Shell, governance boundary.
- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, completed
  first backend/API slice.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, completed first
  backend/API sensitive access slice.
- `CAND-CAP-0009` - Position & Organization Assignment, completed first
  backend/API assignment overlay slice.
- `CAND-CAP-0010` - Offboarding & Exit Management, completed first backend/API
  offboarding case slice with local/deferred TEP handoff metadata only.

PSS substrate dependencies referenced only as dependencies:

- `MOD-0018` - RBAC / ABAC Authorization substrate.
- `MOD-0021` - Audit Trail Service substrate.
- `MOD-0023` - Workflow substrate for future review/dispute flows.
- `MOD-0028` - Documentation and evidence storage substrate.
- `MOD-0030` - Records / Retention / Legal Hold substrate.
- `MOD-0031` - Evidence Linking substrate.
- `MOD-0048` - Reference Data Management.
- `MOD-0057` - Taxonomy / Tagging Service.
- `MOD-0251` - HRIS External SoR.
- `MOD-0279` - Payroll Engine External SoR.
- `MOD-0280` - Time & Attendance External Provider.
- `MOD-0281` - Payroll Integration & Governance.
- `MOD-0288-FU02` - Person Reference Directory Projection.

R2 extension dependencies to reconcile before user-facing exchange modules:

- `MOD-0027` - Notification.
- `MOD-0263` - Notification Provider / Delivery.
- `MOD-0029` - Controlled Documents.
- `MOD-0262` - External Docs Repository.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first TEP shell backend/API
  metadata contract slice while status is `ready-for-dev`.
- TEP service scaffold is complete and remains limited to the existing
  `Diten.TalentEcosystemService` host.
- Runtime owner/key is `tep.shell`.
- Runtime permission namespace is `tep.shell.*`.
- `CAND-CAP-0011` is a governance identity only and must never be written into
  runtime literals, permission seeds, Hangfire owner names, route metadata,
  database records, telemetry owner fields, config keys, or test fixtures.
- `MOD-0321` must not be used for this TEP capability.
- API/controller/command/validator/repository/index/database work is authorized
  only for the TEP shell metadata contract. Frontend menu, Razor view,
  JavaScript, DataTable, RESX, gateway route, webhook, background job, provider
  adapter, and candidate/talent identity behavior remain unauthorized.
- TEP must not build candidate/talent identity directly from raw HRIS data or
  PSS person projection alone. HCM foundation and consent/visibility contracts
  remain required prerequisites.
- HCM/PSS dependency contracts must be consumed if available; if unavailable,
  the shell must use explicit `Deferred` state or fail closed.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

Future TEP workspace shell UI must be authorized by a separate implementation
pack after TEP runtime authorization, service/shell decisions, legal/privacy
approval, and gateway ownership decisions. This pack only defines that TEP
workspace shell ownership must remain in the TEP domain and must not collapse
into HCM or PSS.

## 10. Backend File Convention

Backend implementation is authorized only under
`services/Diten.TalentEcosystemService/**` for the TEP shell metadata contract.

The implementation must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns where applicable, `Response<T>` envelope, tenant
server-side resolution, soft delete rules where persisted metadata is used,
MongoDB repository/index standards where persistence is used, authorization
conventions, and fail-closed/deferred privacy/legal gates defined by DCP-010.

## 11. Frontend File Contract

Frontend implementation is N/A for this governance-approved pack.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0011` and
  `Talent Ecosystem Platform Shell` must pass before this pack is promoted.
- Any future runtime pack must fail closed if it attempts to use `MOD-0321` as
  the TEP identity.
- Any future runtime pack must fail closed if it writes `CAND-CAP-0011` into
  runtime literals.
- Future TEP module packs must declare whether they depend on HCM employee
  projection, sensitive access, assignment overlay, offboarding handoff
  metadata, PSS reference data, notification, controlled documents, evidence,
  audit, workflow, retention, or consent/visibility contracts.
- Candidate/talent identity work cannot be inferred from this shell pack.
- Cross-company reference exchange, reputation, restricted integrity, risk, and
  dispute features require explicit legal/privacy governance before runtime.

## 13. Failure Path to Verify

Before promoting this pack or preparing a runtime follow-up, verify these
failure paths remain blocked:

- Reusing `MOD-0321` for TEP.
- Treating `CAND-CAP-0011` as a runtime module ID.
- Creating `services/Diten.TalentEcosystemService/**` without explicit scaffold
  authorization.
- Creating API controllers, entities, repositories, database collections, UI, or
  gateway routes from this governance-approved pack.
- Starting TEP candidate/talent identity runtime before consent/visibility and
  legal/privacy decisions.
- Building TEP identity directly from PSS person references or raw HRIS payloads
  without HCM foundation contracts.
- Moving HCM employee projection or PSS substrate ownership into TEP.
- Starting reference exchange, rehire recommendation, dispute, reputation, or
  risk runtime from this shell pack.

## 14. Authorization Convention

Runtime permission namespace authorized for the first shell metadata slice:

- `tep.shell.*`
- `tep.shell.read`
- `tep.shell.manage`

This namespace must not use `CAND-CAP-0011` or `MOD-0321` in permission seeds,
route metadata, policy attributes, database records, telemetry owner fields,
config keys, or tests.

## 15. Gateway / API Routing Decision

Gateway routing is N/A for this first backend/API slice.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after this
backend/API slice is reviewed.

## 16. Acceptance Criteria

1. AC-01: DCP-002 candidate gate passes for `CAND-CAP-0011` and
   `Talent Ecosystem Platform Shell`.
2. AC-02: Pack status is `done` after the first TEP shell backend/API metadata
   contract slice implementation passed done-promotion readiness review.
3. AC-03: `MOD-0321` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0011` is documented as governance-only and not a runtime
   literal.
5. AC-05: Runtime owner/key is `tep.shell`; candidate ID and legacy ID are not
   runtime literals.
6. AC-06: API/controller/entity/repository/DB collection work is limited to shell
   metadata only; frontend/gateway remain closed.
7. AC-07: TEP ownership is not moved into HCM or PSS, and HCM/PSS ownership is
   not moved into TEP.
8. AC-08: Completed HCM foundation dependencies are listed:
   `CAND-CAP-0007`, `CAND-CAP-0008`, `CAND-CAP-0009`, and `CAND-CAP-0010`.
9. AC-09: `CAND-CAP-0010` offboarding handoff metadata is treated as
   local/deferred prerequisite metadata only, not as TEP runtime.
10. AC-10: PSS substrate dependencies are listed as dependencies only.
11. AC-11: TEP candidate/talent identity is explicitly out of scope for this
    shell pack.
12. AC-12: Consent, visibility, access policy, legal/privacy, and dispute
    decisions are future prerequisites before user-facing exchange modules.
13. AC-13: R2 MVP sequencing starts with TEP shell governance before association,
    consent, candidate identity, reference exchange, and R4 intelligence modules.
14. AC-14: Gateway exposure is integration-agent follow-up only if later needed.
15. AC-15: Runtime literal scan for `CAND-CAP-0011|MOD-0321` across runtime,
    frontend, gateway, and tests returns no matches.
16. AC-16: UI/DataTable/l10n verifier is N/A.
17. AC-17: DCP-010 runtime authorization is reconciled and approved for this
    first shell metadata slice.
18. AC-18: Service scaffold completion is recorded.
19. AC-19: Runtime-ready checklist is closed for the first shell metadata slice.
20. AC-20: Future EA canonical MOD assignment remains a follow-up and does not
    authorize runtime.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0011 --name "Talent Ecosystem Platform Shell"`
- `rg -n "CAND-CAP-0011|MOD-0321" services frontend gateway tests`

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP shell targeted tests covering tenant isolation, permission checks,
  runtime literal prohibition, metadata-only scope, dependency `Deferred` /
  fail-closed behavior, and forbidden candidate/talent/exchange fields.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0011`.
- [x] DCP-002 candidate gate PASS recorded before pack authoring.
- [x] `MOD-0321` remains blocked and must not be used.
- [x] `CAND-CAP-0011` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] HCM R1 foundation completion dependencies are listed.
- [x] PSS substrate dependencies are listed as dependencies only.
- [x] TEP runtime authorization DCP reconciliation is recorded.
- [x] Service scaffold completion is recorded.
- [x] Runtime implementation is authorized only for first shell metadata slice.
- [x] UI/frontend/gateway remains closed.
- [x] Candidate/talent identity remains outside this shell pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance approval: none if candidate gate remains PASS.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved.
- [x] Runtime owner/key approved: `tep.shell`.
- [x] Runtime permission namespace approved: `tep.shell.*`.
- [x] `Diten.TalentEcosystemService` scaffold completed.
- [x] TEP service port `5060` approved.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] API/controller/entity/repository/persistence/test scope approved only for
  shell metadata contract.
- [x] Minimal TEP shell metadata field/object list approved.
- [x] Privacy/legal waiver approved only for shell metadata.
- [x] Consent and visibility policy remain future blockers for user-facing
  exchange modules.
- [x] HCM dependency mechanism approved: consume contracts where available;
  otherwise explicit `Deferred` state or fail-closed.
- [x] PSS substrate mechanism approved as dependency-only; no ownership copy.
- [x] Gateway direct edit closed; integration-agent follow-up only if needed.
- [x] UI/frontend shell closed for first slice.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0011|MOD-0321`.

Open blockers: none for the completed first TEP shell backend/API metadata
contract slice.

## 19. Implementation Notes

- DCP-008 lists Talent Ecosystem Platform Shell in R2-A with legacy Excel ID
  `MOD-0321` and output role "TEP workspace shell".
- DCP-002 blocks `MOD-0321` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA approved `CAND-CAP-0011` as a temporary candidate identity for governance
  documentation only.
- `CAND-CAP-0011` is not an EA canonical MOD allocation and remains a
  governance/documentation identity.
- HCM R1 foundation is available as prerequisite contract context, but this
  pack does not consume HCM runtime APIs or create TEP runtime behavior.
- DCP-010 authorizes the first `Diten.TalentEcosystemService` shell metadata
  runtime slice while keeping production implementation outside this governance
  edit.
- Review-ready reconciliation: read-only implementation review PASS. Previous
  High auth wiring blocker is closed; JWT authentication wiring is aligned to
  repo pattern; middleware order is `UseAuthentication` before
  `UseAuthorization`; `[Authorize]` plus `tep.shell.read` / `tep.shell.manage`
  permission-gated API surface is verified; auth wiring focused tests PASS;
  candidate gate PASS; TEP API build PASS; TEP shell targeted tests PASS
  (15/15); runtime literal scan PASS for `CAND-CAP-0011|MOD-0321`; business
  scope scan PASS; frontend/gateway/HCM/PSS scope remains closed.
- Done promotion reconciliation: done-promotion readiness review PASS. Pack
  status `review`, `Open blockers: none`, AC-02 review alignment, auth wiring
  blocker completion note, candidate gate, TEP API build, TEP shell targeted
  tests, runtime literal scan, runtime owner/key, permission namespace, and
  frontend/gateway/HCM/PSS closed scope were verified before promotion to
  `done`.

## 20. Follow-up Items

- Decide future EA canonical MOD assignment for Talent Ecosystem Platform Shell.
- Prepare R2 follow-up module identity decisions for Association Membership &
  Member Company Registry, Verified HR Participant & Company Access, Consent,
  Visibility & Access Policy, and Industry Candidate Identity & Talent Profile.
- Route any future Gateway exposure through integration-agent.
