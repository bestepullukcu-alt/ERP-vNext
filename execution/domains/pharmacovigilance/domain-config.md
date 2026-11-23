# Pharmacovigilance (PVG) - Domain Config

> Bu dosya domain'in **sinirlarini ve kararlarini** tanimlar. Engineering NASIL kurallari
> [.antigravity/rules/](../../../.antigravity/rules/)'dadir; capability-level sozlesme
> [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md)'tedir.

## Current MOD-0230 revision 22 CI-closure exception (2026-11-23; formal/dispatch gated)

MOD-0230 revision 22 is `ready-for-dev`, build/test gate open and human `approval_status: approved` for the exact
27-file correction below. Direct user **“onaylıyorum”**, scope/XMC/content/presentation/single-writer consent and
mechanical pack/domain promotion authority are recorded in
`docs/records/decisions/2026-11/pvg-mod0230-rev22-owner-approval-01.md`, SHA-256
`0fa566c24df9a3d0910e1ebd1b4febc68711d92ca22e64dbc754535fe217e0d7`. That consent is NOT final-byte formal review/HIGH,
ordinary prompt approval or an actual dispatch gate pass. Fresh author-separate final-version review/HIGH records
for the exact pack/domain bytes, then each worker's reviewed ordinary scoped prompt and real dispatch gate 0 remain
mandatory before product work. No worker GO or successful CI is claimed. Operational runtime and publication remain
closed; new-SHA push/main merge are not authorized by this record.

This section is the sole narrow revision 22 exception to the frontend/SharedResource Protected Paths and old exact
DL-12 allowances below. The exhaustive 27 paths/key/statement boundaries live in the approved scope of
`execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` revision 22 and the durable artifacts:
- `docs/records/reviews/MOD-0230/2026-11-23-rev22-scope-01/scope.json`, canonical LF SHA-256
  `c1238043f7bced900b9b59224ada56e5cc5c9776a2be9c3a5c46077038d477ed`;
- `docs/records/reviews/MOD-0230/2026-11-23-rev22-scope-01/localized-content.json`, canonical LF SHA-256
  `875b6a025a64bac11cb923092015858d5e6b7615b2d684a7172ffb2327b405e4`.
The owner record binds the original reviewed CRLF raw scope hash
`a386b1bfe016bfdde6f129fef9dcc54efbc90595be87c5c7bb80141ae99105a9` and content hash
`5157d2ec9e40bd835330f9117cbf6da08d13434391cb2ebb0680ee2c665c1d2a`. The same durable directory's
`SOURCE-MANIFEST.json` preserves lossless raw originals/base64 and both representations' hashes with semantic
equality. LF adoption changes no approved path/key/value/presentation. Frozen draft labels inside those artifacts
remain original proposal provenance; the direct owner record adopts their exact scope/content, not a dispatch status.

**Exact key/statement limits, seven languages `en,tr,fr,es,zh,ar,ru`:**

| Existing path family / exact file | Only permitted revision 22 change | Count / owner-consented writer |
|---|---|---|
| `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx` | For CASEINTAKETRIAGE, CASEINTAKETRIAGECREATE, CASEINTAKETRIAGEEDIT and CASEINTAKETRIAGEDETAILS: only Help.{CODE}.Purpose / Audience / Step1Title / Step1 | 7 files / `pvg_r22_shared_writer` |
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` | Only four NEW Nav.Page keys for those normalized codes; additionally only SharedResource.fr.resx existing Nav.Domain.PHARMACOVIGILANCE value Pharmacovigilance → Pharmacovigilance (surveillance des médicaments) | 7 files / `pvg_r22_shared_writer` |
| `frontend/Diten.Web/Resources/Views/Governance/RoleAssignments/RoleAssignmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` | Only ActionVerb_Triage / ActionVerb_Route | 7 files / `pvg_r22_shared_writer` |
| `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` | Only triage/route entries in the existing ActionVerbs map to those two Localizer keys | 1 file / `pvg_r22_shared_writer` |
| `frontend/Diten.Web/wwwroot/assets/js/Governance/RoleAssignments/perm-label.js` | Only triage word added to the existing success / bx-check-circle (decide) row, route word to dark / bx-user-check (assign/delegate); presentation only | 1 file / `pvg_r22_shared_writer` |
| `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml` | Only ViewData["PageCode"] = "CASEINTAKETRIAGE" in the existing setup block | 1 file / `pvg_r22_views_writer` |
| `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml` | Only ViewData["PageCode"] = "CASEINTAKETRIAGECREATE" in the existing setup block | 1 file / `pvg_r22_views_writer` |
| `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml` | Only ViewData["PageCode"] = "CASEINTAKETRIAGEEDIT" in the existing setup block | 1 file / `pvg_r22_views_writer` |
| `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml` | Only ViewData["PageCode"] = "CASEINTAKETRIAGEDETAILS" in the existing setup block | 1 file / `pvg_r22_views_writer` |

Total 27 product files: **23 shared files with one writer**, **four module views with the other**, 154 new localized
values plus exactly one existing French value replacement. The listed named workers are consented plans, not
started/dispatched jobs. No overlapping author is allowed. They may run in separate clean managed worktrees in
parallel only after their individual formal/ordinary prompt/dispatch gates pass. CT writes no product code.

The nine RoleAssignments resource/bridge/script paths remain owned by **MOD-0018-FU9 / platform-team**, with the
frontend-ui-ux implementation role identified by that pack's §3/§4. The direct user record grants ONLY this precise
PVG label addition XMC exception, including the existing presentation rows; it does not transfer ownership, edit
FU9's pack or authorize its other atomic groups. Its separate-step conditions remain. The direct user also covers
this work's exact 14 common Help/SharedResource paths and coordination/single writer without giving PVG canonical
ownership or whole-file permission. No authority is inferred from DCP-001/DCP-004 and neither DCP scope nor
operational/API/auth boundary is expanded. The existing module identity and twelve slices remain; DL-7 help,
DL-6 label obligation and DL-11 French correction are bounded by the pack's AC-46...AC-50 while AC-1...AC-45/history
remain unchanged. Existing E4/E5 evidence minima and live-acceptance deferrals are not lowered to E2.

No permission/grant/RBAC/checkbox/UAS behavior, schema/catalog/manifest/menu/routes/API/controller/store/audit, layout,
CI/test/guard/ledger or operational behavior may change. No new module/FU/delivery slice, tour, related-page catalog
or new product feature is authorized. Unlisted writes stop for a new bounded scope decision; already adopted routine
choices do not require repeating owner approval. Final values are exact hash-bound content, not worker discretion.
The previous six-file FE producer/prompt cannot certify these 27 paths. Use new ordinary scope-bound prompts and
source/diff evidence, author-separate E2 verification, CT no-ff integration and independent combined verification.
Keep PR #460. Every new final H invalidates old exact-source CI/AC-45 Unix binding; final required CI and existing
pvg-api/ubuntu-22.04 target/full same-job TRX/source proof must be renewed without CI/guard/ledger changes. No merge/
DONE while final required results are pending/red/unbound; operational runtime remains closed.

> **Historical revision 21 gate note below:** the dated 2026-11-20 block (including its old “Current” heading and
> embedded source-baseline hashes) is preserved verbatim as history. It is not revision 22 approval/dispatch evidence.
> For the new correction only, this revision 22 section and the exact pack/owner record govern; other modules'
> authorizations and all earlier version-bound records remain unchanged.

## Current MOD-0230 build/test gate (2026-11-20)

MOD-0230 revision 21 is ready-for-dev with build_gate open and direct human scope/policy/shared-file/single-writer
and bounded build/test consent originally recorded in pvg-mod0230-rev20-owner-approval-01.md, with revision 21's
one ACL method and separate five-row metadata addition in docs/records/decisions/2026-11/pvg-mod0230-extra-scope-approval-01.md.
That consent does not imply new-version formal review/HIGH or prompt gates passed. The approved source baseline is
pack `186ad9675e2f5219d240080f93012c54f266561f6ce70451f96aaeddb242aa01` /
domain `914c0c1dab0979d3ec48e3d4f6e08646ad0bf4494f9b73a97fa27bca4c5ead9c`;
revision 20 records remain bound to their bytes, not these amended revision 21 bytes.
Formal final-version independent review/HIGH ledger records and the passed prompt/dispatch gate remain required
before any product-class worker starts, including each preparation WP. Bounded preparation uses independent
WP-0230-DL12-PREP-WIN and WP-0230-DL12-PREP-WEB; each has its own ordinary prompt/HIGH review, valid formal pack
approval and dispatch gate. Their feasibility evidence is output, not a prerequisite for that same dispatch, and
neither depends on the parent feature WP. No successful feasibility is claimed. Operational runtime stays closed; actual WP state comes from CT-QUEUE/gate, not pack dl12_state.
Other modules' authorizations and historical version-bound reviews are unchanged.

## Purpose

Pharmacovigilance domain'i, regulated life-sciences safety operations icin Safety Case intake/triage,
signal-minimum case processing, MedDRA coding, and Signal Management contract boundaries sahiplenir.
Ilk kapsam yalnizca DCP-004 tarafindan tanimlanan urgent W-3 first-stage governance block'tur.

## In-Scope Modules

> Canonical identity and names come from the Blueprint and DCP-002 preflight. Wave/status details remain in
> portfolio-level governance; burada sadece PVG sahiplik listesi tutulur.

**First-stage DCP-004 members:**

- MOD-0230 - Case Intake & Triage
- MOD-0231 - Case Processing; urgent W-3 delivery slice: Signal Minimum Scope
- MOD-0232 - MedDRA Coding
- MOD-0234 - Signal Management; Signal MVP contract/object model/workflow/interface gates only

## Out-of-Scope

- W-3A0 foundation remediation development; not waived, still a production blocker.
- Operational runtime implementation, production deployment, supplier qualification, validation, collections
  (except the MOD-0230 build/test intake-draft store, DCP-004 §10), seed data, jobs, migrations (that store's startup
  index bootstrap excepted; no data migration), and any runtime surface outside the
  MOD-0230/MOD-0231/MOD-0232/MOD-0234 build/test gates.
- MOD-0234 runtime shell, placeholder dashboard, placeholder endpoint, menu entry, fake data, fake signal, fake
  metric, or fake cohort.
- Full W-4/W-5 PV scope outside DCP-004, including MOD-0233, MOD-0235, MOD-0236, MOD-0237, MOD-0238, and MOD-0239.
- AI summarization, extraction, recommendation, or routing implementation until governed-AI prerequisites are approved.

## Domain-Level Repo Scope

**Authorized now (updated 2026-08-12 - final all-four PVG governance package; DCP-004 `approved`,
MOD-0230/MOD-0231/MOD-0232/MOD-0234 `ready-for-dev` for build/test):**

- `execution/domains/pharmacovigilance/**`
- `services/Diten.PvgService/**` - dedicated PVG service boundary, port **5011** (OD-7). MOD-0230 slice 1,
  MOD-0231 Signal Minimum Scope, MOD-0232 MedDRA Coding, and MOD-0234 Signal Management class-library
  contracts/tests only; since 2026-10-29 MOD-0230 slice 1 also has its build/test Mongo store (DEPO-01, pack slice
  DL-10). MOD-0231, MOD-0232, and MOD-0234 must stay non-operational: no API host, controllers,
  appsettings, persistence, Mongo, repositories, Gateway route, frontend, seeds, jobs, dictionary import,
  dictionary redistribution, static MedDRA data, cache/search index, data-product stub, fake signal, fake metric,
  fake cohort, partner integration, AI, archive/void/export/delete/bulk-delete, or runtime endpoint.
- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/**` plus matching JS / l10n / resource files. Since
  2026-10-31, once revision 15 is approved on main: exactly two `Nav.*` keys in the seven
  `frontend/Diten.Web/Resources/SharedResource.*.resx` files (pack slice DL-11).
- `gateway/Diten.ApiGateway/**/ocelot.json` - one MOD-0230 route family, integration-agent-owned.
  Upstream `/api/pv-case-intake-triage`, downstream `/api/v1/pv-case-intake-triage` (NET-001).
- `tests/**` for the above.

**All of the above is authorized for the build/test gate only: local, dev, and CI. Production deployment,
supplier qualification, and validation approval remain unauthorized. The MOD-0230 runtime authorization packet is
recorded as draft / not approved and does not open operational runtime.**

**Still blocked:**

- MOD-0231 operational runtime; its build/test gate is open only for non-operational class-library contracts/tests.
- MOD-0232 operational runtime - build/test gate is open only for non-operational class-library contracts/tests.
- MOD-0234 operational runtime - build/test gate is open only for no-shell non-operational class-library
  contracts/tests.
- Archive, void, export, seed data, and background jobs for MOD-0230 (out of slice 1).
- Any AI behaviour.

## Protected Paths

- `.antigravity/**` (global engineering system). `rules/ports.md` needs explicit approval before port 5011 is registered there.
- `services/**` **except `services/Diten.PvgService/**`**
- `frontend/**` **except the MOD-0230 view root and its JS / resource siblings**, and the two `Nav.*` keys of pack slice DL-11
- `gateway/**` except the single MOD-0230 route family
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (FROZEN)
- `frontend/Diten.Web/Controllers/Archive/**`, `frontend/Diten.Web/Views/Archive/**`
- PvgServiceApiHostTests.cs is protected to PREP-WIN outside the sole ACL method BODY below; no using/helper/
  ScanFixture/scanner/other DL-3 change. tests/known-failures.tsv is protected except the separate five-row metadata
  correction below; it is not a PREP/feature writer path. service-tests.yml is read-only evidence, no CI-edit exception.
- Other domain governance folders except by explicit user request

## Revision 21 owner-consented alignment - record/dispatch gated

The direct 2026-11-20 owner records approve the retained revision 20 scope plus only the explicit revision 21 additions,
pre-read field-policy order, exact
shared-file paths and single-writer arrangement, bounded build/test preparation and delegated development after
required gates. Ready-for-dev metadata expresses human consent; it is not a completed formal review/HIGH ledger.
Final-version independent review/HIGH records and a passed prompt/dispatch gate are still required before a DL-12
product worker starts. The path exceptions below follow that consent and those gates; the ACL exception is only one existing test body
and the five-row ledger job is separately scoped administrative metadata, not PVG product/PREP code.
Publication and operational use remain unauthorized.

- `frontend/Diten.Web/Controllers/PharmacovigilanceCaseIntakeTriageController.cs`: update-only lossless form input and
  payload conversion plus minimum update-local helpers; no route, auth/header, generic error or Create changes.
- `frontend/Diten.Web.Tests/Controllers/PvgCaseIntakeTriageWebTests.cs`: only this module's real MVC/connected API/store
  tests, alias-qualified PVG host, embedded node -e jsdom program and bounded stdin/stdout RPC; no hidden install,
  runtime skip, new runner or other module tests.
- `frontend/Diten.Web.Tests/Diten.Web.Tests.csproj`: exactly one ProjectReference to
  `../../services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj` with alias `pvgapi`; no other
  references/packages/framework/solution changes. Human consent for this exact shared-file/single-writer scope is recorded. Its alias build and embedded connected RPC remain unmeasured readiness conditions; additional
  needs require a new explicit proposal, not silent expansion.

- `frontend/Diten.Web/Views/Pharmacovigilance/CaseIntakeTriage/_Form.cshtml`: only Edit-mode required attributes and
  matching required indicators for SourceReference and PatientSubjectCode, both service-optional. Create and all
  other markup/validation stay unchanged; the historical view-root exception does not widen this exact DL-12 scope.
- `.github/workflows/phase1-gates.yml`: only two prerequisites before the unchanged Run Phase 1 Gates step:
  actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020, Node 22/npm cache/frontend lock path, followed by
  npm ci --no-audit --no-fund in frontend/Diten.Web. Human CI-path/single-writer consent is recorded; no new workflow,
  package/framework, triggers, permissions, gate commands or other jobs are proposed. All .github paths stay
  protected; only these two owner-consented steps in this exact file follow the bounded preparation/dispatch gates above.

Within the already scoped PVG service, the exact owner-consented / dispatch-gated DL-12 test scope is
`services/Diten.PvgService/tests/Diten.PvgService.RegPvBase.Tests/PvgIntakeDraftApplicationServiceTests.cs`:
only the Update row of Cross_tenant_mutations_do_not_leak_existence_through_evidence_link_denial may align its
permission-only/no-field expectations to the owner-adopted pre-read update field-policy calls.
Not-found/safe/no-record/no-workflow/no-evidence assertions, Triage/Route rows and all other tests/helpers remain
unchanged. The same human consent and final-version formal record/dispatch gates above apply.

Revision 21 adds this exact PREP-WIN exception, retaining DL-3 ownership of the rest of the file:
`services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgServiceApiHostTests.cs`, ONLY BODY of
Configuration_scan_fails_closed_on_inaccessible_directory. Use existing .NET8/current-SID ACL on the own private
ScanFixture/locked only: independent original DACL copy, no inheritance propagation or external/global ACL, deny
ListDirectory only without removing restore rights, exact UnauthorizedAccessException for enumeration AND scanner,
real before/after scanner positives, finally original DACL/access-inheritance restore and verified private cleanup.
Keep Unix chmod/security branch source-preserved; no using/helper/fixture/scanner/host/store/config/dependency edits,
skip, early return or weaker exception assertion. New exact-version formal/HIGH records and normal HIGH prompt/gate
remain required. No real ACL/restore success is claimed by this amendment.

Separate owner-approved administrative metadata scope: `tests/known-failures.tsv`, ONLY one literal ,*.xml removal
from rerun_trigger in the five fixed DocsPathGuardTests rows identified individually in the pack Repo Scope (suite
TenantArchitecture.ArchitectureTests). Every other trigger token/order/cell/status/fingerprint and all OTHER rows stay unchanged.
The existing CT-registered WP-0230-DL12-REGISTRY-01 (extra-scope approval record) handles this file under its own
normal reviewed prompt/gate; no PVG PREP/feature worker may edit it. No test, guard, runtime rule or product scope
is relaxed; no new module/slice/AC identity is created and no additional WP identity is invented here. Its recorded
acceptance uses the existing production known_failures validator/check on preserved normal Web TRX (Diten.Web.Tests),
plus an in-memory old-XML-token refusal; no guard-source edit or premature Web VERIFIED/INTEGRATED claim.

The direct owner record adopts the field-policy/pre-read order and record-existence visibility contract; no user
policy or exact shared-path consent remains pending for this unchanged scope. Embedded Node/RPC, aliased reference,
clean-local execution and Windows Mongo preparation remain unimplemented/unmeasured outputs of separately gated
product-class preparation WPs. PREP-WIN writes the existing two Mongo harness/guard-test paths PLUS only the new ACL
method BODY above; its three-file candidate is measured together from baseline58920519 through final,
PREP-WEB only the existing module C# Web test, single-alias csproj and exact two Phase 1 prerequisite steps. PREP-WEB
measures transport/current behavior and negative failure controls; final no-loss acceptance is feature work and its
known current RED stays visible. No production service/DTO/controller/form.js/_Form/l10n change belongs to PREP-WEB.
Each PREP WP requires its own normal prompt/HIGH review, formal pack approval and passed dispatch gate; --prepare is
not authority. Feasibility is its output, not a circular entry condition. Main feature dispatch requires BOTH E2
outputs independently verified and integrated plus its own normal prompt/HIGH review/formal approval/passed gate.
Clean LOCAL same-prerequisite/same-dotnet-test evidence measures feasibility; real GitHub Linux Phase 1 is required
at closure of the single final PR, not before a not-yet-opened PR. A separate job's JS setup proves no transport.
Bounded work stays under the same consent/single-writer scope; no implementation or successful measurement is claimed.

The stopped WIN tree stays dirty/unchanged/recoverable, with no checkpoint or RED-test commit exception. The pack
binds its frozen two raw disk hashes, patch SHA and capture metadata. After the amended formal pack/domain records,
a clean separately authorized replacement worker measures ExpectedHEAD/source chain and passes its renewed normal
HIGH prompt gate 0 BEFORE exact saved-patch application; both canonical LF contents/hashes must match the snapshot,
with raw CRLF disk vs Git LF distinguished. Stop on mismatch, no silent repair. Then fix only the ACL method.
Windows target/full API 0 failed/0 skipped, unchanged counts, original guards/two targets/35 Mongo tests and private
cleanup accompany SIX separate compiling scratch RED/original GREEN/source-hash proofs: unconditional Windows flag,
dropped Unix flag, missing/relative binary guard drop, 27017 guard drop, Windows deny removal, scanner fail-open with
real denial precondition. No skipped old four cases or committed mutations. Independent E2 covers all three files;
only the final verified commit may integrate after it. These are outputs, not same-PREP dispatch prerequisites.

Unix runtime stays open until the final PR's existing service-tests pvg-api/ubuntu-22.04 target/full TRX is 0 failed/
0 skipped and source-bound to final H. Same run/attempt event H/base B and actual checkout C are measured:
require H ∈ parents(M), meaning H is a direct parent of synthetic M, with real commit/base relation and
tree(M)==tree(H) Git objects, plus checkout log and same-
job TRX; API head_sha/comments are insufficient. Only if that normal PR equality cannot be proved may EXISTING
unfiltered workflow_dispatch on final CT ref prove C==H and its same-job TRX. No CI YAML/input/job/step/permission
edit or unnecessary second matrix. Changed H or unequal source invalidates old results. Pending/red/unbound Unix
proof prevents final PR/main merge and WP DONE, not prior PREP integration or feature dispatch. Phase1 PVG build/
Web tests do not replace the service API job. No Unix/runtime/build/test result is claimed by these governance edits.

The test-only --nounixsocket platform-selection scope for the pack's exact Mongo harness and guard test files is
within this service boundary under the same consent/gates. In the existing named safety test, only synthetic
missing/positive binary fixture paths may become platform-fully-qualified while every safety assertion stays intact:
Path.IsPathFullyQualified/existence checks, null/empty/relative/missing rejection, 27017 prohibition, isolation and
cleanup are never weakened. This is portability within the existing guard-test file, not a new path or permission.
No store/production host change, weaker binary/port/
replica-set safety or permissive adapter is authorized. Protection remains outside the exact listed exceptions.
Other slices, MOD-0231/0232/0234, other domain ownership and all operational gates are unchanged. No product/test/CI
file is changed by this administrative transition, and no code/test/feasibility success is claimed.

## Ownership Boundaries

- **MOD-0230 Case Intake & Triage:** owns Safety Case intake records, intake artifacts, triage state, and routing decision boundary for the first PVG sequence.
- **MOD-0231 Case Processing:** canonical module name remains Case Processing. In DCP-004 first stage, only the Signal Minimum Scope delivery slice may be planned.
- **MOD-0232 MedDRA Coding:** owns coded-term assignment boundaries and MedDRA dictionary-version binding contracts for Safety Case terms. It cannot hardcode dictionary data as local UI/static data.
- **MOD-0234 Signal Management:** owns signal hypothesis, evaluation, review decision, linked evidence boundaries for Signal MVP planning. Runtime shell implementation is explicitly blocked.
- **W-3A0 foundation dependencies:** REG-PV-BASE, CASE-LIFECYCLE, CODESET, and REG-SIGNAL-BASE dependencies remain external prerequisites and production blockers where applicable.
- **Evidence, workflow, audit, masking, data product, semantic metric, and governed-AI controls:** PVG consumes approved contracts from their owning modules; this domain does not reimplement those foundations inside member modules.

## Runtime Decisions

> MOD-0230, MOD-0231, MOD-0232, and MOD-0234 build/test preparation is governed by their ready-for-dev module packs and
> DCP-004. This domain config does not authorize operational runtime, production use, supplier qualification, or
> validation.

- **Identity and naming:** Blueprint canonical IDs/names are mandatory. Ref: [DCP-002](../../portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md).
- **Orchestration boundary:** DCP-004 stays the first-stage capability contract until user approval changes it. Ref: [DCP-004](../../portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md).
- **Gateway and ports:** MOD-0230 records `Diten.PvgService` on port 5011 and one route family for the build/test
  gate only. MOD-0231, MOD-0232, and MOD-0234 record no additional port and no Gateway route.
  `.antigravity/rules/ports.md` remains protected and has not been updated here. No other PVG route or port is
  reserved. Ref:
  [.antigravity/rules/ports.md](../../../.antigravity/rules/ports.md), [.antigravity/rules/routes.md](../../../.antigravity/rules/routes.md).
- **Security and regulated data:** PHI/PII masking, row/field security, RBAC/ABAC, audit, correlation, evidence-link, and regulated error-model gates must remain fail-closed. Full owner approvals still gate operational runtime. Ref: [.antigravity/rules/security-jwt.md](../../../.antigravity/rules/security-jwt.md), [.antigravity/rules/multi-tenancy.md](../../../.antigravity/rules/multi-tenancy.md).
- **Localization and UI:** MOD-0230 tenant UI is limited to the build/test gate if executed by its pack. Other PVG UI remains unauthorized. Approved UI must follow repo localization/layout rules. Ref: [.antigravity/rules/localization-standard.md](../../../.antigravity/rules/localization-standard.md), [.antigravity/rules/views-organization.md](../../../.antigravity/rules/views-organization.md).
- **Data and persistence:** no operational collection/schema/index is authorized here. Exception (2026-10-29, DEPO-01): MOD-0230 may use one Mongo store under its build/test gate only (local / dev / CI) - collection, tenant-first indexes, startup schema bootstrap - as scoped by the MOD-0230 pack (revision 14, slice DL-10) and DCP-004 §10; MOD-0231, MOD-0232, and MOD-0234 gain nothing from it. Any other persistence decision belongs in an approved member module pack and remains subject to the operational runtime gate. Ref: [.antigravity/rules/entity-base-template.md](../../../.antigravity/rules/entity-base-template.md), [.antigravity/rules/mongo-indexing.md](../../../.antigravity/rules/mongo-indexing.md).

## Domain Bootstrap Notes

- This PVG governance scaffold exists and contains DCP-004 member module packs for:
  - `MOD-0230 Case Intake & Triage` - `ready-for-dev` for the build/test gate only
  - `MOD-0231 Case Processing` - `ready-for-dev` for the build/test gate only; Signal Minimum Scope delivery slice
  - `MOD-0232 MedDRA Coding` - `ready-for-dev` for the build/test gate only; non-operational class-library contracts/tests
  - `MOD-0234 Signal Management` - `ready-for-dev` for the build/test gate only; no-shell non-operational class-library contracts/tests
- **2026-08-09:** DCP-004 is `approved`; MOD-0230 is `ready-for-dev` for the build/test gate.
- **2026-08-10:** MOD-0231 is `ready-for-dev` for the build/test gate only.
- **2026-08-11:** MOD-0232 and MOD-0234 are `ready-for-dev` for the build/test gate only. MOD-0234 keeps
  `shell: none` and authorizes no operational runtime, shell, placeholder dashboard, fake signal, fake metric, or
  fake cohort.
- **2026-08-12:** Final all-four PVG governance package reconciliation recorded. MOD-0230 runtime authorization
  packet remains draft / not approved. Operational runtime remains **NO-GO**; MOD-0231, MOD-0232, and MOD-0234
  operational runtime remain blocked.
- **2026-10-29:** MOD-0230 build/test Mongo store exception (DEPO-01, DEPO-03; DCP-004 §10; pack revision 14, slice
  DL-10). Operational runtime stays **closed**; MOD-0231, MOD-0232, and MOD-0234 are unchanged.
- **2026-10-31:** MOD-0230 build/test closed catalog registration exception (`docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md`,
  KK-1 ... KK-6, corrected by `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-02.md`, re-confirmed by `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-03.md`, sequenced by `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-04.md`; DCP-004 §10; pack revision 15, slice DL-11): non-assignable, hidden from the menu, pushed once at start
  by `Diten.PvgService` when the key and the base URL are both set, the URL is a valid absolute http/https address, and the host is not production-like. In force only once revision 15 is approved on main. Operational
  runtime stays **closed**; MOD-0231, MOD-0232, and MOD-0234 are unchanged.
- MOD-0230 consumes MOD-0019, MOD-0023, and MOD-0031 through fail-closed PVG-owned ports
  (`IPvgFieldSecurityPolicy`, `IPvgWorkflowTransitionGate`, `IPvgEvidenceLinkPort`) because those modules have no
  runtime. Ports are interface + deny default only; they must never store policy, host a workflow engine, or
  persist evidence. Detailed port contract material remains a pending support package and is not normative until
  committed.
- Operational runtime authorization for every PVG member remains **closed**.
- Seed data, background jobs, menu entries, and module-catalog work remain blocked. One exception (2026-10-31,
  DCP-004 §10; pack revision 15, slice DL-11): the closed catalog registration of MOD-0230 under its build/test gate,
  once revision 15 is approved on main. Its one startup push is not a background job, and the catalog→Auth sync it triggers
  is the OD-PVG-1 path, not a seed. Nothing else opens: seed data, grant code, menu entries, and every other catalog entry
  stay blocked, for MOD-0231, MOD-0232, and MOD-0234 too.
