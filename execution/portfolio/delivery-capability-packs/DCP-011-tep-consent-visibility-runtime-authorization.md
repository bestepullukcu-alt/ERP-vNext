---
id: DCP-011
slug: tep-consent-visibility-runtime-authorization
name: TEP Consent/Visibility Runtime Authorization Decision Pack
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: talent-ecosystem-platform
owner: enterprise-architect / tep-domain-owner / platform-team / security-owner / legal-owner
branch: feature/governance/tep-consent-visibility-runtime-authorization
created: 2026-08-13
source_dcp: execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md
production_implementation_authorized: false
runtime_authorization_authorized: true
service_scaffold_authorized: false
---

# DCP-011 - TEP Consent/Visibility Runtime Authorization Decision Pack

> **Artifact type:** This is a Delivery Capability Pack. It is not a runtime
> entity, not a product module, not a service scaffold, and not a substitute for
> the member module packs required by `module-pack-standard.md`.

> **Execution authorization:** approved for CAND-CAP-0013 runtime authorization
> governance only. This pack authorizes a later ready-for-dev module-pack
> promotion for the first TEP consent/visibility metadata/policy decision
> contract slice. It does not authorize production implementation by itself,
> service scaffold, frontend work, Gateway route changes, broad consent engine,
> RBAC/ABAC engine copy, legal document execution, preference center,
> association registry runtime, candidate/talent identity, or reference exchange
> behavior.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-011` |
| Name | TEP Consent/Visibility Runtime Authorization Decision Pack |
| Type | Delivery Capability Pack |
| Standard | `CAP-001` |
| Status | `approved` |
| Owner domain | `talent-ecosystem-platform` |
| Candidate runtime service | `Diten.TalentEcosystemService` |
| Production implementation | Not authorized |
| Runtime authorization | Authorized for CAND-CAP-0013 ready-for-dev promotion only |
| Service scaffold | Not authorized by this DCP; already exists from DCP-010 |
| Source roadmap | `DCP-008` |
| Related policy module pack | `CAND-CAP-0013` |
| Association blocker target | `CAND-CAP-0012` |

## 2. Business outcome

Define the governance decisions required before the TEP consent/visibility/access
policy boundary can move from governance-only planning to a first backend/API
metadata/policy decision contract slice.

The intended outcome is to let `CAND-CAP-0013` close the policy boundary needed
by `CAND-CAP-0012` Association runtime without starting a broad consent engine,
copying RBAC/ABAC authorization, exposing frontend/Gateway routes, or writing
candidate identities into runtime surfaces.

## 3. Problem statement

`CAND-CAP-0013` is governance-only `approved`. Its module pack explicitly stated
that runtime owner/key, permission namespace, legal/privacy approval,
association consumption contract, data-scope/visibility model, MOD-0018
integration pattern, and audit/evidence/retention expectations were open. This
DCP records the governance decisions that close those runtime authorization
blockers for a narrow metadata/policy decision contract slice.

DCP-010 cannot simply be reused because it authorizes runtime only for the first
`CAND-CAP-0011` TEP Shell backend/API metadata slice. DCP-010 keeps consent
engine, association registry, reference exchange, and broad TEP runtime outside
its scope.

## 4. Capability boundary

In boundary:

- Governance authorization decision for `CAND-CAP-0013`.
- Candidate-runtime waiver decision.
- Runtime owner/key and permission namespace decision.
- First backend/API metadata/policy decision contract boundary.
- Legal/privacy metadata-only waiver decision.
- Association consumption contract for `CAND-CAP-0012`.
- Data-scope/visibility classification behavior.
- HCM `CAND-CAP-0008` sensitive access dependency/precondition behavior.
- `MOD-0018` substrate integration pattern without engine copy.
- Local/deferred audit/evidence/retention metadata decision.
- Frontend/Gateway remain-closed decision.

Out of boundary:

- Production code or runtime implementation.
- Creating controllers, entities, repositories, Mongo indexes, DTOs, validators,
  commands, handlers, or database collections.
- Service scaffold creation.
- Frontend/UI/Razor/JavaScript/DataTable/RESX/menu work.
- Gateway `ocelot.json` direct edits.
- Broad consent engine, RBAC/ABAC engine copy, legal document execution,
  preference center, signature workflow, association registry runtime,
  candidate/talent identity, reference exchange, reputation, risk, analytics,
  dispute, or review board runtime.

## 5. Member modules and follow-ups

| ID | Name | Type | Current status | Role in this DCP |
|---|---|---|---|---|
| `CAND-CAP-0013` | Consent, Visibility & Access Policy | TEP module pack | approved / governance-only | Policy boundary to authorize for first metadata/policy decision slice. |
| `CAND-CAP-0012` | Association Membership & Member Company Registry | TEP module pack | approved / governance-only | Runtime blocker target consuming CAND-CAP-0013 decisions. |
| `CAND-CAP-0011` | Talent Ecosystem Platform Shell | TEP module pack | done | Completed shell dependency and runtime host context. |
| `Diten.TalentEcosystemService` | TEP service scaffold | Scaffold-only host | completed | Runtime host, but not authorized for this policy slice until DCP-011 approval and module-pack promotion. |
| `CAND-CAP-0008` | HR Governance & Sensitive Access Controls | HCM module pack | done | Sensitive access dependency/precondition context. |
| `MOD-0018` | RBAC / ABAC Authorization | PSS substrate | substrate dependency | Permission substrate only; no engine copy or identity reuse. |
| `MOD-0021` / `MOD-0028` / `MOD-0030` / `MOD-0031` | Audit/evidence/retention substrates | PSS substrates | dependency / future integration | Local/deferred metadata in first slice; real integration later. |

## 6. Ownership map

| Area | Owner | Decision |
|---|---|---|
| TEP policy boundary | TEP domain owner | Native TEP ownership; no HCM/PSS ownership transfer. |
| Candidate identity | EA / registry owner | `CAND-CAP-0013` remains governance/documentation identity. |
| Runtime service | Platform / TEP owner | Candidate host `Diten.TalentEcosystemService`; not authorized by this draft. |
| Legal/privacy waiver | Legal owner | Required before ready-for-dev. |
| Security policy/data-scope | Security owner | Required before ready-for-dev. |
| HCM sensitive access dependency | HCM / security owner | Dependency/precondition only. |
| MOD-0018 substrate | Platform owner | Contract/substrate only; no engine copy. |
| Gateway | integration-agent | Future follow-up only if exposure is needed. |
| UI/frontend | TEP / frontend owner | Closed for first slice unless separately approved. |

## 7. Dependency graph

```text
DCP-002 candidate reservation
  -> CAND-CAP-0011 TEP Shell done
  -> CAND-CAP-0012 Association governance approved
      -> blocked until consent/visibility policy boundary is explicit
  -> CAND-CAP-0013 Consent/Visibility governance approved
      -> DCP-011 runtime authorization decisions
          -> future CAND-CAP-0013 ready-for-dev promotion
              -> future first backend/API metadata/policy decision slice
```

## 8. Ordered delivery sequence

1. Keep DCP-010 shell-only and do not broaden it.
2. Draft DCP-011 for CAND-CAP-0013 runtime authorization decisions.
3. Review EA/security/legal/platform decision table.
4. If approved, reconcile DCP-011 and CAND-CAP-0013 module pack.
5. Promote CAND-CAP-0013 to `ready-for-dev` only after all runtime blockers close.
6. Implement only the first metadata/policy decision contract slice through
   `@orchestrator`.
7. Reconcile `CAND-CAP-0012` Association runtime blockers against the implemented
   CAND-CAP-0013 contract.

## 9. Prerequisites

- `CAND-CAP-0013` candidate gate remains PASS.
- `CAND-CAP-0013` module pack remains governance-only `approved`.
- `CAND-CAP-0012` remains governance-only and runtime-blocked until this policy
  contract is explicit.
- `CAND-CAP-0011` TEP Shell remains `done`.
- `CAND-CAP-0013` and `MOD-0325` remain absent from runtime literals.
- Legal, security, platform, and EA decisions are recorded before ready-for-dev.

## 10. Architecture decisions

| Decision | Approved value | Status | Owner |
|---|---|---|---|
| DCP structure | Use DCP-011; keep DCP-010 shell-only | Approved | EA / PMO |
| Candidate-runtime waiver | `CAND-CAP-0013` remains documentation identity; no runtime literal | Approved | EA |
| Runtime owner/key | `tep.consent-visibility-policies` | Approved | Platform / TEP owner |
| Permission namespace | `tep.consent-visibility-policies.read`, `tep.consent-visibility-policies.manage`, `tep.consent-visibility-policies.evaluate`, `tep.consent-visibility-policies.audit.read` | Approved | Security / Platform |
| First slice type | Metadata/policy decision contract only | Approved | EA / Legal / Security |
| Legal/privacy waiver | Metadata-only waiver for first slice | Approved | Legal |
| Association activation | Fail-closed when required policy approval is missing; no blind activation | Approved | EA / Security / Legal |
| Policy unavailable behavior | Evaluation metadata may be `Deferred`; activation remains fail-closed | Approved | Security |
| HCM sensitive access | Dependency/precondition; unavailable is fail-closed or explicit `Deferred` | Approved | Security / HCM |
| MOD-0018 | Substrate contract only; no engine copy | Approved | Platform |
| Audit/evidence/retention | Local/deferred metadata first; real integration future follow-up | Approved | Platform / Legal |
| Frontend/Gateway | Closed; Gateway via integration-agent only if needed later | Approved | Platform |

## 11. Scope

This approved DCP authorizes CAND-CAP-0013 module-pack promotion for a first
backend/API slice with this narrow scope:

- Policy definition metadata.
- Consent requirement state metadata.
- Visibility scope/classification metadata.
- Data-scope state metadata.
- Policy evaluation outcome metadata.
- Association consumption/precondition metadata.
- Local/deferred audit/evidence/retention reference metadata.
- Tenant-scoped, permission-gated API behavior.

This DCP does not authorize creating that implementation. Production work
requires the CAND-CAP-0013 module pack to be promoted to `ready-for-dev` and an
explicit user implementation prompt.

## 12. Explicit exclusions

- `CAND-CAP-0013` and `MOD-0325` runtime literals.
- `MOD-0018` as TEP-native identity.
- Broad RBAC/ABAC engine copy.
- Broad consent engine.
- Consent capture UX, preference center, signature/legal document execution.
- Association membership/member company runtime implementation.
- Candidate/talent identity.
- Reference exchange, reputation, risk, analytics, dispute, review board.
- Raw provider payload, credential/token/secret, PII-heavy, payroll/bank/tax,
  payslip, biometric/geolocation, national ID, DOB, or home address persistence.
- Frontend/Gateway direct work.

## 13. Governance drift risks

- Treating DCP-010 shell authorization as broad TEP runtime authorization.
- Writing `CAND-CAP-0013` or `MOD-0325` into runtime literals.
- Reusing `MOD-0018` as the TEP consent/visibility identity.
- Implementing a broad consent engine instead of metadata/policy decision
  contract.
- Allowing Association activation without consent/visibility/data-scope policy.
- Treating `Deferred` policy metadata as approval for active Association use.
- Adding UI/Gateway exposure before legal/privacy and integration-agent gates.

## 14. Review questions

| Question | Owner | Current answer |
|---|---|---|
| Is candidate-runtime waiver approved for CAND-CAP-0013? | EA | Approved |
| Is `tep.consent-visibility-policies` approved as runtime owner/key? | Platform / TEP owner | Approved |
| Is the proposed permission namespace approved? | Security / Platform | Approved: read / manage / evaluate / audit.read |
| Is metadata-only legal/privacy waiver acceptable? | Legal | Approved |
| Which Association states require policy approval? | EA / Legal / Security | Consent-required membership/company states and visibility-approved activation states |
| Is activation fail-closed while evaluation can be deferred? | Security | Approved |
| How does CAND-CAP-0008 gate sensitive access? | Security / HCM | Dependency/precondition; unavailable is fail-closed or explicit `Deferred` |
| How is MOD-0018 consumed without engine copy? | Platform | Substrate contract only; no engine copy |
| Are audit/evidence/retention local/deferred metadata acceptable first? | Platform / Legal | Approved |
| Do frontend and Gateway remain closed? | Platform | Approved |

## 15. Gate criteria

- DCP-002 candidate gate for `CAND-CAP-0013` PASS.
- Runtime literal scan for `CAND-CAP-0013|MOD-0325` no matches.
- DCP-011 approval recorded before CAND-CAP-0013 ready-for-dev promotion.
- Candidate-runtime waiver recorded.
- Runtime owner/key and permission namespace approved.
- Legal/privacy metadata-only waiver approved.
- Association consumption contract approved.
- MOD-0018 no-engine-copy pattern approved.
- Frontend/Gateway remain closed or explicit separate authorization exists.

## 16. Acceptance criteria

1. AC-01: DCP-011 status is `approved`.
2. AC-02: `production_implementation_authorized` is `false`.
3. AC-03: `runtime_authorization_authorized` is `true` only for CAND-CAP-0013
   ready-for-dev promotion governance.
4. AC-04: DCP-010 remains shell-only and is not broadened by this DCP.
5. AC-05: CAND-CAP-0013 is the target module pack.
6. AC-06: CAND-CAP-0012 Association blocker relationship is recorded.
7. AC-07: Candidate-runtime waiver is approved.
8. AC-08: Runtime owner/key is `tep.consent-visibility-policies`.
9. AC-09: Permission namespace is approved for read, manage, evaluate, and
   audit.read.
10. AC-10: First slice is metadata/policy decision contract only.
11. AC-11: Legal/privacy metadata-only waiver is approved.
12. AC-12: Association activation fail-closed behavior is approved.
13. AC-13: Policy unavailable `Deferred` metadata is limited to evaluation, not
    activation approval.
14. AC-14: HCM `CAND-CAP-0008` remains dependency/precondition.
15. AC-15: `MOD-0018` remains substrate only; no engine copy.
16. AC-16: Audit/evidence/retention real integrations are future follow-ups.
17. AC-17: Frontend/Gateway remain closed.
18. AC-18: Runtime literal scan remains required.
19. AC-19: CAND-CAP-0013 ready-for-dev promotion conditions are clear.
20. AC-20: Production implementation remains blocked until module pack promotion
    and explicit user implementation prompt.

## 17. Downstream business-module impacts

- `CAND-CAP-0012` Association cannot become runtime-ready until this policy
  boundary is approved and its consumption contract is explicit.
- Verified HR Participant & Company Access must consume the same policy boundary
  or define a compatible sibling contract.
- Candidate identity, reference exchange, reputation, risk, analytics, dispute,
  and review board remain blocked until consent/visibility/legal gates mature.

## 18. Closed decisions

- EA candidate-runtime waiver for `CAND-CAP-0013`: approved.
- Runtime owner/key approval: `tep.consent-visibility-policies`.
- Permission namespace approval:
  - `tep.consent-visibility-policies.read`
  - `tep.consent-visibility-policies.manage`
  - `tep.consent-visibility-policies.evaluate`
  - `tep.consent-visibility-policies.audit.read`
- Legal/privacy metadata-only waiver: approved for the first metadata/policy
  decision contract slice only.
- Minimal metadata field/object list: policy definition, consent requirement,
  visibility scope/classification, data-scope state, policy evaluation outcome,
  Association consumption/precondition, and local/deferred audit/evidence/
  retention reference metadata.
- Association consumption contract: consent-required states, visibility approval
  requirement, activation fail-closed, policy unavailable evaluation `Deferred`,
  and no blind Association activation.
- Data-scope/visibility classification model: metadata-only first slice with
  fail-closed activation and `Deferred` evaluation allowed when unavailable.
- HCM sensitive access precondition behavior: `CAND-CAP-0008` dependency/
  precondition approved.
- MOD-0018 substrate integration/no engine copy pattern: approved.
- Audit/evidence/retention local/deferred metadata waiver: approved; real
  integration remains future follow-up.
- Frontend/Gateway remain-closed decision: approved.

## 19. Future follow-ups

- Reconcile CAND-CAP-0013 to ready-for-dev under this DCP.
- After CAND-CAP-0013 first slice is implemented and reviewed, reconcile
  CAND-CAP-0012 runtime blockers.
- Prepare Verified HR Participant & Company Access identity decision.
- Route Gateway exposure through integration-agent only if explicitly needed.
- Real audit/evidence/retention integration remains a future pack.

## 20. Audit and reconciliation notes

- DCP-011 is created as a separate authorization pack because DCP-010 is
  shell-only.
- 2026-08-13 approval reconciliation: DCP-011 approved for CAND-CAP-0013
  runtime authorization governance only. `production_implementation_authorized`
  remains `false`; `service_scaffold_authorized` remains `false`;
  `runtime_authorization_authorized` is `true` for CAND-CAP-0013 ready-for-dev
  promotion governance.
- No production code, runtime/API files, service scaffold, frontend, Gateway, or
  database files are authorized by this DCP alone.
- `CAND-CAP-0013` and `MOD-0325` must remain absent from runtime literals.
- `MOD-0018` remains PSS RBAC/ABAC substrate only.
- `CAND-CAP-0012` Association runtime remains blocked until CAND-CAP-0013 is
  promoted, implemented, and reviewed through its own module-pack gate.
