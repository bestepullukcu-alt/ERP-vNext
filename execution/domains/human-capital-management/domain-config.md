# Human Capital Management - Domain Config

This file records HCM-specific ownership and boundary decisions. Engineering implementation standards live in `.antigravity/rules/` and are referenced, not repeated here.

## Purpose

Human Capital Management owns native HCM Foundation domain capabilities, starting with the internal employee and employment master. HCM modules consume platform services for authorization, audit, workflow, evidence, records retention, reference data, taxonomy, and gateway routing.

## In-Scope Modules

- `MOD-0251 Core HR / Employee Master`
  - Blueprint: Human Capital Management Foundation / Core HR Master / W-1.
  - Status: P2 draft/reference-validation slice implemented and browser-validated.
  - HCM service scaffold exists at `services/Diten.HcmService`.
  - Approved runtime boundary remains draft/reference-validation only: create draft, save/update with ETag, reload, validate person/organization-unit/position/legal-entity references, and non-submit review.
  - Registry/detail scope decision: Employee Registry and Employee Detail are not P2 support surfaces and must move through later approved read-only sequences before runtime smoke closure can include them.
  - Registry read-only governance contract exists for later `MOD0251-P4-REGISTRY-READ-M1`; runtime implementation remains unapproved until that prompt explicitly authorizes backend/frontend/gateway/test changes.
  - Full Employee Master lifecycle remains blocked until a later approved scope closes submit, approval/rejection, activation, MOD-0023 workflow, `employee.created`, evidence, export/status/Data Quality Queue, and government identifier/tokenization contracts.

## Out-of-Scope

- Platform shared services such as RBAC, audit, workflow, documents, evidence linking, reference data, taxonomy, gateway, observability, and secrets.
- Organization, person, and position reference directories owned by `MOD-0288`.
- Assignment workflow/orchestration owned by `MOD-0299`.
- Payroll, time, attendance, leave, compensation, benefits, performance, employee relations, and Talent Ecosystem modules unless separate module packs approve them.

## Domain-Level Repo Scope

Planning/governance scope for this scaffold:

- `execution/domains/human-capital-management/**`
- HCM module specifications under `docs/specs/**` for read-only reference
- Enterprise Blueprint projections under `execution/portfolio/**` for read-only reference

Runtime business implementation is approved only for the completed P2 draft/reference-validation slice. The broader MOD-0251 employee lifecycle, registry/detail pages, submit/approval/activation behavior, evidence integration, export/status/Data Quality Queue, government identifier capture/tokenization, and any additional gateway/backend/frontend/test work remain blocked until separately approved.

## Protected Paths

- `.antigravity/**`
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml`
- `frontend/Diten.Web/Controllers/Archive/**`
- `frontend/Diten.Web/Views/Archive/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` unless an approved integration-agent task exists
- Other domains' `execution/domains/**` paths
- Other domains' `services/**` paths
- Runtime frontend/backend/gateway/test/migration files outside the approved MOD-0251 P2 draft/reference-validation slice, except when a later approved or ready-for-dev module pack/prompt authorizes them

## Ownership Boundaries

- `MOD-0251` owns Employee, Employment Record, and Job Assignment / Employment Assignment Snapshot.
- `MOD-0299` owns assignment workflow/orchestration.
- `MOD-0288` owns organization, person, and position reference directories.
- `MOD-0023` owns approval workflow state and decisions; HCM modules may only delegate to or consume decisions from MOD-0023.
- `MOD-0314` owns HR-sensitive access controls and masking policy evaluation.

## Runtime Decisions

- Backend service owner: `Diten.HcmService` at `services/Diten.HcmService`, local downstream port `5060`. Do not use obsolete `Products`, `SampleModule`, inactive service assumptions, or hardcoded `5050`.
- Frontend shell for MOD-0251: `tenant`; HCM operational HR users work in the tenant shell. Approved MOD-0251 tenant pages use `Layout = "_LayoutTenantShell";`.
- Gateway: all browser/runtime traffic must go through Gateway `5000`; browser JS must not call service ports directly.
- Persistence: tenant-scoped employee data must use server-side `TenantId`, soft delete, optimistic concurrency, and fail-closed tenant isolation per global rules.
- Sensitive data: raw government identifier storage is prohibited in R1. Capture remains blocked until tokenization/security service ownership and contract are confirmed.
- Repo packaging/commit remains unsafe until the no-valid-`HEAD` / all-untracked workspace condition is resolved.
