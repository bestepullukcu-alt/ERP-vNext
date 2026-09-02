---
id: CAND-CAP-0016
name: Trust Level & Multi-Signature Engine
domain: talent-ecosystem-platform
service: Diten.TalentEcosystemService
shell: none
golden_reference: none
entity_base: BaseEntity
status: done
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/tep/cand-cap-0016-trust-level-multi-signature-engine
started: 2026-08-24
target: 2026-10-21
form_field_count: 0
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
reservation_sources:
  - execution/registries/module-id-registry.md
  - execution/portfolio/blueprint-master-plan-reconciliation.md
shell_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md
association_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md
consent_visibility_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md
verified_access_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0014-verified-hr-participant-company-access.md
review_board_dependency: execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0015-talent-ecosystem-governance-review-board.md
candidate_identity: CAND-CAP-0016
legacy_excel_id: MOD-0330
canonicalization_status: candidate / pending-EA
runtime_service_candidate: Diten.TalentEcosystemService
bootstrap_type: runtime-ready-first-slice
runtime_owner_key: tep.trust-levels
permission_namespace: tep.trust-levels.*
runtime_repo_scope: services/Diten.TalentEcosystemService/**
---

# CAND-CAP-0016 - Trust Level & Multi-Signature Engine

> Status: done. The first metadata-only TEP backend/API trust level and
> multi-signature policy validation contract slice under
> `services/Diten.TalentEcosystemService/**` has been implemented and
> implementation review is PASS, and done-promotion readiness review is PASS.
> This pack does not authorize signing workflow runtime, signature execution,
> cryptographic service ownership, key management, external signature provider
> integration, frontend, Gateway, HCM/PSS implementation changes, or production
> e-signature behavior. `CAND-CAP-0016` remains a governance/documentation
> identity only and must never be written into runtime literals. `MOD-0330`
> remains blocked for TEP use.

## 1. Module Summary

`CAND-CAP-0016` reserves the temporary DCP-002 candidate identity for the native
Talent Ecosystem Platform R2-B capability named `Trust Level & Multi-Signature
Engine`.

The source roadmap is
`execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`.
DCP-008 places this row in R2-B with legacy Excel ID `MOD-0330` and output role
"Trust level validation". DCP-002 blocks `MOD-0330` because it is absent from
the Blueprint and has no canonical registry row. EA/registry owner reservation
recorded `CAND-CAP-0016` as a temporary governance identity pending future
canonical MOD allocation.

This ready-for-dev pack authorizes only a metadata-only trust level and
multi-signature policy validation contract slice. It does not create signing
workflow runtime, cryptographic services, e-signature envelopes, external
signature provider integration, dispute workflow, or user-facing
trust/reputation features.

## 2. Ownership and Boundaries

Owned by this ready-for-dev first slice:

- TEP-native metadata-only trust level policy boundary.
- Multi-signature policy and validation metadata boundary.
- Trust level dependency model over completed TEP foundation modules.
- Fail-closed/deferred trust validation contract.
- Contract for consuming `MOD-0022` e-Signature Service as PSS
  substrate only.
- Local/deferred audit/evidence/retention metadata only.

Not owned by this pack:

- `MOD-0022` e-Signature Service ownership, envelope lifecycle, signer
  participant model, signature artifact storage, or provider-backed signature
  execution.
- Cryptographic service, key management, signature verification engine, or
  signing workflow runtime.
- Trust scoring, reputation/risk analytics, reference exchange, candidate/talent
  identity, dispute workflow, external review-board integration, or marketplace
  behavior.
- Association membership registry, consent/visibility policy, verified access,
  or review-board implementation.
- HCM or PSS ownership transfer.
- Frontend, Gateway, raw/sensitive payload persistence, broad workflow engine,
  broad audit/evidence/retention engine, or RBAC/ABAC engine copy.

Authorized runtime owner/key:

- `tep.trust-levels`

Authorized permission namespace:

- `tep.trust-levels.read`
- `tep.trust-levels.manage`
- `tep.trust-levels.evaluate`
- `tep.trust-levels.audit.read`

Authorized runtime repo scope:

- `services/Diten.TalentEcosystemService/**`

## 3. Owned Objects

First-slice metadata objects:

| Object | Type | Purpose |
|---|---|---|
| TepTrustLevelPolicyMetadata | Runtime metadata contract | Defines trust level states and validation prerequisites. |
| TepMultiSignaturePolicyMetadata | Runtime metadata contract | Defines when multiple approvals/signatures would be required without executing signatures. |
| TepTrustValidationSnapshot | Runtime metadata contract | Records dependency readiness for Association, Consent/Visibility, Verified Access, and Review Board. |
| TepSignatureSubstrateReference | Runtime metadata contract | Records how `MOD-0022` may be referenced as PSS substrate without ownership transfer. |
| TepTrustAuditMetadata | Runtime metadata contract | Records local/deferred audit/evidence/retention expectations. |

No signing workflow, signature execution, cryptographic service, or key
management runtime is authorized by this pack.

## 4. Entity Fields

Authorized minimal metadata fields:

- `Code`
- `DisplayName`
- `TrustLevelState`
- `MultiSignaturePolicyState`
- `AssociationMembershipRegistryId`
- `ConsentVisibilityPolicyId`
- `VerifiedParticipantAccessId`
- `ReviewBoardCaseId`
- `SignatureSubstrateState`
- `TrustValidationState`
- `PolicyEvaluationState`
- `LegalSecurityState`
- `AuditEvidenceState`
- `RetentionState`
- `DependencyStates`
- `SourceContractVersion`
- `LastEvaluatedAt`
- `TrustLevelVersion`
- `DeferredReason`

Forbidden for this pack:

- `CAND-CAP-0016` or `MOD-0330` as runtime module literals.
- Signing workflow runtime, cryptographic service ownership, key management,
  signature execution, external signature provider integration, or `MOD-0022`
  ownership copy fields.
- Candidate/talent identity, reference exchange, reputation, risk analytics,
  dispute workflow, marketplace, or external review-board fields.
- Raw HRIS/provider payloads, credentials, tokens, secrets, bank/tax/payroll,
  payslip, biometric/geolocation, national ID, DOB, home address, or PII-heavy
  fields.

## 5. Repo Scope

Authorized runtime scope for this ready-for-dev pack:

- `services/Diten.TalentEcosystemService/**`

Governance reconciliation may update only this pack. No changes are authorized
under `frontend/**`, `gateway/**`, HCM, or PSS implementation paths.

## 6. Protected Paths

This ready-for-dev pack must not change:

- `services/Diten.HumanCapitalService/**`
- `services/Diten.Platform/**`
- any service path outside `services/Diten.TalentEcosystemService/**`
- `frontend/**`
- `gateway/**`
- `execution/domains/platform-shared-services/**`
- `execution/domains/human-capital-management/**`
- `execution/domains/master-data-management/**`
- `execution/domains/developer-enablement/**`
- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0011-talent-ecosystem-platform-shell.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0012-association-membership-member-company-registry.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0013-consent-visibility-access-policy.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0014-verified-hr-participant-company-access.md`
- `execution/domains/talent-ecosystem-platform/module-packs/CAND-CAP-0015-talent-ecosystem-governance-review-board.md`
- `execution/registries/module-id-registry.md`, except a separate EA-approved
  identity reservation or canonicalization update.
- `.antigravity/**`

## 7. Dependencies

Governance dependencies:

- `execution/domains/talent-ecosystem-platform/domain-config.md`
- `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md`
- `execution/portfolio/delivery-capability-packs/DCP-010-tep-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-011-tep-consent-visibility-runtime-authorization.md`
- `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

Completed TEP foundation dependencies:

- `CAND-CAP-0011` - Talent Ecosystem Platform Shell, `done`.
- `CAND-CAP-0012` - Association Membership & Member Company Registry, `done`.
- `CAND-CAP-0013` - Consent, Visibility & Access Policy, `done`.
- `CAND-CAP-0014` - Verified HR Participant & Company Access, `done`.
- `CAND-CAP-0015` - Talent Ecosystem Governance & Review Board, `done`.

HCM foundation dependency context:

- `CAND-CAP-0007` - Employee Profile & Employment Record Projection, `done`.
- `CAND-CAP-0008` - HR Governance & Sensitive Access Controls, `done`.
- `CAND-CAP-0009` - Position & Organization Assignment, `done`.
- `CAND-CAP-0010` - Offboarding & Exit Management, `done`.

PSS substrate dependencies referenced only as dependencies:

- `MOD-0022` - e-Signature Service substrate only.
- `MOD-0018` - RBAC / ABAC Authorization substrate.
- `MOD-0021` - Audit Trail Service substrate.
- `MOD-0028` - Documentation and evidence storage substrate.
- `MOD-0030` - Records / Retention / Legal Hold substrate.
- `MOD-0031` - Evidence Linking substrate.

## 8. Runtime Constraints

- Runtime implementation is authorized only for the first metadata-only trust
  level and multi-signature policy validation contract slice under
  `services/Diten.TalentEcosystemService/**`.
- `CAND-CAP-0016` is a governance identity only and must never be written into
  runtime literals, permission seeds, route metadata, database records,
  telemetry owner fields, config keys, background job owner names, or tests.
- `MOD-0330` must not be used as the TEP identity or remapped to TEP.
- `MOD-0022` remains the PSS e-Signature Service substrate and must not be
  reused as this TEP-native identity.
- `MOD-0022` may be referenced only through a substrate/dependency contract; no
  e-signature engine, envelope lifecycle, signature execution, provider, key, or
  cryptographic ownership is copied into TEP.
- No signing workflow runtime, signature execution, signature envelope
  ownership, cryptographic service, key management, or external signature
  provider integration is authorized.
- No trust/reputation/risk analytics behavior may be inferred from this pack.
- CAND-CAP-0011/0012/0013/0014/0015 completion does not authorize trust or
  multi-signature runtime by itself.
- Runtime must fail closed or produce explicit `Deferred` metadata if
  Association, Consent/Visibility, Verified Access, Review Board, legal/security,
  or signature substrate dependencies are unavailable.
- Activating/trust-elevation decisions fail closed when multi-signature policy
  or legal/security trust prerequisites are unavailable.
- Non-activating evaluation metadata may be recorded as explicit `Deferred`.
- Runtime owner/key is `tep.trust-levels`.
- Runtime permissions are limited to `tep.trust-levels.read`,
  `tep.trust-levels.manage`, `tep.trust-levels.evaluate`, and
  `tep.trust-levels.audit.read`.

## 9. Layout & Shell Contract

Current shell decision:

- `shell: none`
- `golden_reference: none`
- UI/frontend: N/A

No TEP trust-level UI, navigation, Razor view, JavaScript, DataTable, RESX, or
menu entry is authorized. Future UI requires a separate module pack.

## 10. Backend File Convention

Backend/API implementation is authorized only for the first metadata-only slice
under `services/Diten.TalentEcosystemService/**`.

Backend/API work must follow the repo's 5-layer .NET service convention,
CQRS/MediatR patterns where applicable,
`Response<T>` envelope, tenant server-side resolution, soft delete rules where
metadata is persisted, MongoDB repository/index standards, JWT/RBAC
authorization conventions, and fail-closed/deferred privacy/legal gates.

## 11. Frontend File Contract

Frontend implementation is N/A.

No Razor view, JavaScript file, DataTable contract, menu item, localization
resource, layout binding, shell route, or frontend navigation entry may be
added from this pack.

## 12. Validation Rules

Planning validation rules:

- DCP-002 candidate gate for `CAND-CAP-0016` and `Trust Level &
  Multi-Signature Engine` must pass before governance approval if the verifier
  is available.
- Candidate gate note: `.antigravity/scripts/verify_module_id.py` was not
  present in this checkout during pack preparation; candidate gate is not
  executable in this checkout; reservation is recorded in the registry and
  reconciliation ledger.
- Runtime literal scan for `CAND-CAP-0016|MOD-0330` across
  `services/Diten.TalentEcosystemService`, `frontend`, `gateway`, and `tests`
  must return no matches.
- `MOD-0330` remains blocked and must not be reused for TEP.
- `CAND-CAP-0016` must remain governance/documentation identity only.
- `MOD-0022` remains PSS substrate only and must not become the TEP-native trust
  or multi-signature identity.
- `CAND-CAP-0011`, `CAND-CAP-0012`, `CAND-CAP-0013`, `CAND-CAP-0014`, and
  `CAND-CAP-0015` must remain completed dependencies.
- Trust, multi-signature, legal/security, signature substrate, and
  audit/evidence boundaries are approved only for the metadata-only first
  slice.

## 13. Failure Path to Verify

Before implementation or review promotion, verify these failure paths remain
blocked:

- Reusing `MOD-0330` for TEP.
- Treating `CAND-CAP-0016` as a runtime module ID.
- Treating `MOD-0022` as the TEP trust/multi-signature identity.
- Starting runtime/API/controller/entity/repository/database work outside the
  approved metadata-only slice and `services/Diten.TalentEcosystemService/**`
  scope.
- Creating frontend or Gateway routes from this pack.
- Creating signing workflow runtime, cryptographic service ownership, or
  external signature provider integration.
- Creating trust/reputation/risk analytics behavior.
- Creating trust validation without CAND-CAP-0012 Association, CAND-CAP-0013
  Consent/Visibility, CAND-CAP-0014 Verified Access, and CAND-CAP-0015 Review
  Board dependency boundaries.
- Persisting raw provider payloads, credentials, tokens, secrets, or PII-heavy
  data.

## 14. Authorization Convention

Runtime owner/key:

- `tep.trust-levels`

Approved permission namespace:

- `tep.trust-levels.read`
- `tep.trust-levels.manage`
- `tep.trust-levels.evaluate`
- `tep.trust-levels.audit.read`

Only these permission strings may be used for the first metadata-only trust
level and multi-signature slice.

## 15. Gateway / API Routing Decision

Gateway routing is N/A.

No Ocelot route may be created. If future external API exposure is needed, the
Gateway route must be handled by the integration-agent workflow after a
separate ready-for-dev runtime module pack is approved and reviewed.

## 16. Acceptance Criteria

1. AC-01: EA/registry owner reservation exists for `CAND-CAP-0016`.
2. AC-02: Pack status is `done` after first metadata-only trust level and
   multi-signature policy validation contract slice implementation, read-only
   implementation review PASS, and done-promotion readiness review PASS.
3. AC-03: `MOD-0330` is not used as the TEP identity.
4. AC-04: `CAND-CAP-0016` is documented as governance/documentation identity
   only and not a runtime literal.
5. AC-05: Candidate gate status is recorded; if the script is unavailable, the
   pack records "candidate gate not executable in this checkout; reservation
   recorded".
6. AC-06: Runtime literal scan for `CAND-CAP-0016|MOD-0330` across runtime,
   frontend, gateway, and tests returns no matches.
7. AC-07: `CAND-CAP-0011` TEP Shell dependency is listed as `done`.
8. AC-08: `CAND-CAP-0012` Association dependency is listed as `done`.
9. AC-09: `CAND-CAP-0013` Consent/Visibility dependency is listed as `done`.
10. AC-10: `CAND-CAP-0014` Verified Access dependency is listed as `done`.
11. AC-11: `CAND-CAP-0015` Review Board dependency is listed as `done`.
12. AC-12: `MOD-0022` is listed only as PSS e-signature substrate/dependency.
13. AC-13: Trust-level boundary is scoped as metadata-only policy validation
    contract.
14. AC-14: Multi-signature boundary is scoped as metadata-only policy
    validation contract.
15. AC-15: Signing workflow runtime, cryptographic service ownership, and
    external signature integration are explicitly out of scope.
16. AC-16: Candidate/talent identity, reference exchange, reputation/risk/
    analytics, dispute workflow, and marketplace behavior are explicitly out of
    scope.
17. AC-17: Frontend/Gateway remain closed.
18. AC-18: Runtime/API/controller/entity/repository/database work is authorized
    only under `services/Diten.TalentEcosystemService/**`.
19. AC-19: Runtime owner/key, permission namespace, minimal fields,
    preconditions, and `MOD-0022` substrate behavior are testable.
20. AC-20: Open blockers are `none` for the approved first backend/API slice.

## 17. Test Expectations

Governance validation commands:

- `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0016 --name "Trust Level & Multi-Signature Engine"`
- `rg -n "CAND-CAP-0016|MOD-0330" services/Diten.TalentEcosystemService frontend gateway tests`

Candidate gate note:

- Candidate gate was not executable during pack preparation because
  `.antigravity/scripts/verify_module_id.py` was not present in this checkout.
  Reservation is recorded in `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.

Runtime validation commands:

- `dotnet build services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj -c Debug --no-restore /nr:false -m:1 --tl:off`
- TEP trust-level targeted tests must pass.
- Full TEP Application tests must pass.
- Production in-memory repository scan must return no matches under
  `services/Diten.TalentEcosystemService/src`.

Frontend/DataTable/l10n verifier: N/A because `shell: none` and
`golden_reference: none`.

## 18. Governance Approval and Runtime-Ready Checklists

### Governance Approval Checklist

- [x] EA/registry owner reservation exists for `CAND-CAP-0016`.
- [x] DCP-002 candidate gate PASS recorded, or verifier unavailability accepted
  as governance note.
- [x] `MOD-0330` remains blocked and must not be used.
- [x] `CAND-CAP-0016` remains governance/documentation identity only.
- [x] TEP domain config exists.
- [x] `MOD-0022` remains PSS substrate/dependency only.
- [x] `CAND-CAP-0011` TEP Shell dependency is `done`.
- [x] `CAND-CAP-0012` Association dependency is `done`.
- [x] `CAND-CAP-0013` Consent/Visibility dependency is `done`.
- [x] `CAND-CAP-0014` Verified Access dependency is `done`.
- [x] `CAND-CAP-0015` Review Board dependency is `done`.
- [x] HCM foundation completion dependencies are listed as context.
- [x] UI/frontend/gateway remains closed.
- [x] Signing workflow runtime remains outside this pack.
- [x] Cryptographic service ownership remains outside this pack.
- [x] Candidate/talent identity, reference exchange, reputation/risk analytics,
  dispute workflow, marketplace behavior, and external signature integration
  remain outside this pack.
- [x] TEP ownership remains separate from HCM and PSS.

Open blockers for governance continuation: none.

Governance note: DCP-002 candidate gate script is not present in this checkout;
EA/registry owner reservation is recorded. The gate must be rerun if/when the
verifier is restored before any later runtime-ready decision.

Governance-only approval reconciliation: EA approved candidate-based governance
continuation for `CAND-CAP-0016`. This remains the reservation baseline; the
runtime-ready reconciliation below authorizes only the first metadata-only
backend/API slice.

### Runtime-Ready Checklist

- [x] EA candidate-runtime waiver approved for `CAND-CAP-0016`.
- [x] Runtime owner/key approved: `tep.trust-levels`.
- [x] Runtime permission namespace approved:
  `tep.trust-levels.read`, `tep.trust-levels.manage`,
  `tep.trust-levels.evaluate`, `tep.trust-levels.audit.read`.
- [x] Runtime repo scope approved: `services/Diten.TalentEcosystemService/**`.
- [x] Minimal trust-level metadata field list approved.
- [x] Multi-signature policy/validation state model approved.
- [x] `MOD-0022` e-Signature consumption contract approved as substrate only.
- [x] Association dependency consumption contract approved.
- [x] Consent/visibility policy precondition contract approved for this module.
- [x] Verified Access precondition contract approved for this module.
- [x] Review Board precondition contract approved for this module.
- [x] HCM foundation validation/consumption boundary approved as dependency
  context only.
- [x] Legal/security trust model approved for metadata-only local/deferred first
  slice.
- [x] Signing workflow runtime explicitly deferred and out of scope.
- [x] Cryptographic service ownership explicitly deferred and out of scope.
- [x] Audit/evidence/retention metadata behavior approved as local/deferred
  metadata; real integration is future follow-up.
- [x] Gateway direct edit remains closed; integration-agent follow-up only if
  needed.
- [x] UI/frontend remains closed.
- [x] Runtime literal scan requirement retained for `CAND-CAP-0016|MOD-0330`.

Open blockers: none for the approved first backend/API trust-level metadata
slice.

Runtime-ready reconciliation: EA candidate-runtime policy waiver approved.
`CAND-CAP-0016` remains a governance/documentation identity and `MOD-0330`
remains blocked; neither may appear in runtime literals. Runtime owner/key is
`tep.trust-levels`, permission namespace is limited to the four permissions
listed above, and scope is limited to `services/Diten.TalentEcosystemService/**`.

Done promotion reconciliation: done-promotion readiness review PASS. First
metadata-only backend/API trust level and multi-signature policy validation
contract slice is implemented under `services/Diten.TalentEcosystemService/**`;
runtime owner/key is `tep.trust-levels`; permission surface is limited to
`tep.trust-levels.read`, `tep.trust-levels.manage`,
`tep.trust-levels.evaluate`, and `tep.trust-levels.audit.read`. Build PASS with
0 warnings and 0 errors; TrustLevel targeted tests PASS 16/16; full TEP
Application tests PASS 101/101; runtime literal scan PASS for
`CAND-CAP-0016|MOD-0330`; production in-memory repository scan PASS;
frontend/Gateway/HCM/PSS scope remains closed. `MOD-0022` remains
substrate/dependency reference only; signing workflow, signature execution,
cryptographic service ownership/key management, external signature provider
integration, candidate/talent identity, reference exchange, reputation/risk
analytics, and raw/sensitive payload persistence remain out of scope.

## 19. Implementation Notes

- DCP-008 lists Trust Level & Multi-Signature Engine in R2-B with legacy Excel
  ID `MOD-0330` and output role "Trust level validation".
- DCP-002 blocks `MOD-0330` for TEP use because it is not Blueprint-backed and
  has no canonical registry row.
- EA/registry owner reservation is recorded for `CAND-CAP-0016` in
  `execution/registries/module-id-registry.md` and
  `execution/portfolio/blueprint-master-plan-reconciliation.md`.
- `CAND-CAP-0016` remains a governance/documentation identity only and
  `MOD-0330` remains blocked; neither may appear in runtime literals.
- `MOD-0022` e-Signature Service remains PSS substrate/dependency only and must
  not be reused as this TEP-native identity.
- `CAND-CAP-0011`, `CAND-CAP-0012`, `CAND-CAP-0013`, `CAND-CAP-0014`, and
  `CAND-CAP-0015` are completed TEP dependencies.
- HCM foundation is available as prerequisite context, but this pack does not
  consume HCM runtime APIs or create HCM ownership.
- Runtime implementation is open only for the first metadata-only backend/API
  trust level and multi-signature policy validation contract slice under
  `services/Diten.TalentEcosystemService/**`.

## 20. Follow-up Items

- Start implementation only through `@orchestrator` and only within
  `services/Diten.TalentEcosystemService/**`.
- Rerun DCP-002 candidate gate when `.antigravity/scripts/verify_module_id.py`
  is available before implementation review or future canonicalization.
- Decide future EA canonical MOD assignment for Trust Level & Multi-Signature
  Engine.
- Decide whether CAND-CAP-0016 receives candidate-runtime waiver or waits for a
  canonical MOD.
- Decide runtime owner/key and permission namespace.
- Decide `MOD-0022` substrate consumption contract without copying PSS
  e-signature ownership.
- Decide whether signing workflow runtime remains deferred or requires a
  separate module identity.
- Keep cryptographic service ownership and external signature integration as
  future follow-ups.
- Keep real audit/evidence/retention integration as a future follow-up.
- Route any future Gateway exposure through integration-agent.
