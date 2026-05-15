---
id: MOD-0021
name: General Audit Trail - All Phases Implementation Plan
domain: platform-shared-services
service: Diten.Platform
shell: platform-admin
status: implementation-plan
source_pack: execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md
updated: 2026-05-14
---

# MOD-0021 All Phases Implementation Plan

This file is the saved phase-based implementation plan for MOD-0021 General Audit Trail. The source of truth for scope and invariants remains:

`execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md`

## Current Repo Discovery Summary

Current implementation status:
- Phase 1 - Domain + Persistence Foundation is implemented.
- Phase 1 review fixes are implemented.
- Platform API build passes.
- Platform test project has unrelated existing compile failures in non-audit tests.

Existing audit-related code before MOD-0021:
- No generic `AuditEvent`, `IAuditService`, `AuditBehavior`, audit API, audit UI, or `audit_outbox` existed.
- Existing `InterfaceRegistry` audit sink is feature-local and not MOD-0021 generic audit SoR.

Tenant/current-user context:
- Platform uses `ITenantContext` from `Diten.Platform.Common`.
- `TenantScopedEntity` has required `TenantId`.
- MOD-0021 now defines `AuditTenantIds.PlatformSystemTenantId`.
- `Guid.Empty` is not valid for audit `TenantId`.

MediatR pipeline:
- Current Platform Application pipeline has validation/logging/exception/performance behaviors.
- No `AuditBehavior` exists yet.
- No `TransactionBehavior`, Mongo session, or unit-of-work exists yet.

Gateway:
- No `/api/platform/audit*` gateway route exists.
- Do not edit `ocelot.json`; gateway route work belongs to integration-agent / integration phase.

## Global Scope Guard

Unless the phase explicitly says otherwise, do not touch:
- `gateway/Diten.ApiGateway/**/ocelot.json`
- `frontend/Diten.Web/**`
- `services/Diten.AuthService/**`
- `services/Diten.MdmService/**`
- `services/Diten.DevEnablementService/**`
- `services/Diten.EnterpriseStrategyService/**`
- `.antigravity/**`
- tenant-side ERP folders

Never add:
- audit event create/edit/delete UI
- `DELETE /api/platform/audit/events`
- `DELETE /api/platform/audit/events/{id}`
- PUT/PATCH audit event mutation
- bulk delete / hard delete for audit events
- raw sensitive data export
- client-payload `TenantId` trust

## Phase 0 - Discovery & Risk Check

Status: completed as planning/discovery.

### Purpose
Confirm repo boundaries, existing audit code, tenant context, MediatR behavior order, gateway state, and transaction/outbox risks before writing code.

### Files / Folders
- `AGENTS.md`
- `execution/domains/platform-shared-services/domain-config.md`
- `execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md`
- `.antigravity/workflows/add-module.md`
- `docs/platform/master-plan.md`
- `services/Diten.Platform/src/**`
- `gateway/Diten.ApiGateway/**/ocelot.json` inspection only

### Work
- Inspect existing audit-related code.
- Inspect tenant/current-user context.
- Inspect MediatR pipeline registration.
- Inspect Mongo persistence patterns.
- Inspect gateway route state without editing.
- Identify transaction/session/unit-of-work blockers.

### Acceptance Criteria
- Existing audit overlap is known.
- Platform tenant convention and current-user context are understood.
- Gateway ownership is documented.
- TransactionBehavior/Mongo session risk is documented.

### Verification
- Read-only repo inspection.
- No code changes.

### Risks
- Hidden existing audit behavior could duplicate MOD-0021 writes.
- Lack of transaction/unit-of-work affects Phase 3 atomicity.

### Commit Suggestion
No commit, discovery only.

## Phase 1 - Domain + Persistence Foundation

Status: implemented.

### Purpose
Create the immutable tenant-aware audit persistence foundation with retention policy and outbox storage, without API/UI/behavior wiring.

### Files / Folders
- `services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Repositories/IAudit*.cs`
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/**`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/**`

### Work
- Add audit enums.
- Add `AuditTenantIds.PlatformSystemTenantId`.
- Add `AuditEvent : TenantScopedEntity`.
- Add `AuditEventRetentionPolicy : GlobalEntity`.
- Add `TenantAuditPreference : TenantScopedEntity`.
- Add infrastructure-only `AuditOutboxMessage`.
- Add infrastructure-only `AuditOutboxStatus`.
- Add custom `AuditEventRepository` without generic update/delete surface.
- Add retention/preference repositories.
- Add `IAuditOutboxWriter` and register outbox writer through interface.
- Add Mongo index definitions.
- Add default retention policy seed.
- Add Phase 1 negative tests.

### Acceptance Criteria
- Audit events are append-only and immutable by repository contract.
- `Guid.Empty` audit tenant id is rejected.
- Platform-global events use `AuditTenantIds.PlatformSystemTenantId`.
- Tenant isolation reads are current-scope by default.
- Explicit cross-tenant repository reads require platform context.
- Retention seed is idempotent and does not overwrite existing policies.
- Upserts do not revive soft-deleted retention/preference records.
- `audit_outbox` has unique `IdempotencyKey` index.

### Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet test services/Diten.Platform` when unrelated existing test compile failures are fixed.

### Risks
- Test project currently has unrelated compile failures in non-audit tests.
- No transaction/session exists yet for atomic command + outbox write.

### Commit Suggestion
`feat(platform): add audit trail persistence foundation`

## Phase 2 - Application Core

Status: next recommended phase.

### Purpose
Build audit application services and helpers on top of Phase 1 persistence without wiring MediatR pipeline auditing yet.

### Files / Folders
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/**`

### Work
- Add `IAuditService`.
- Add `AuditService`.
- Add `ISensitiveFieldRedactionRegistry`.
- Add default sensitive-field redaction registry.
- Add `SensitiveFieldRedactor`.
- Add `IAuditRetentionPolicyResolver`.
- Add retention resolver with category + plan-tier fallback to `Default`.
- Add `IAuditRecursionGuard`.
- Add deterministic audit idempotency key builder.
- Use `IAuditOutboxWriter` to enqueue audit payloads.
- Prepare meta-audit-safe service boundaries.
- Add unit tests for redaction, retention resolution, recursion guard, and outbox enqueue request creation.

### Acceptance Criteria
- `IAuditService` can prepare redacted audit payloads and enqueue through `IAuditOutboxWriter`.
- Sensitive field values never enter outbox payload raw.
- Password, token, secret, API key, and connection string style fields are masked.
- Tenant writes use server-resolved tenant context.
- Platform-global writes use `AuditTenantIds.PlatformSystemTenantId`.
- Phase 2 does not add API/controllers/UI/gateway/AuditBehavior.

### Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- Run audit-related unit tests if test project compile state allows.
- If `dotnet test services/Diten.Platform` fails due to unrelated existing tests, report separately.

### Risks
- Redaction registry must be conservative to avoid raw PII/sensitive leaks.
- Application service must not become a backdoor for arbitrary client `TenantId`.
- Meta-audit recursion guard must be simple enough for Phase 3 behavior wiring.

### Commit Suggestion
`feat(platform): add audit application core services`

## Phase 3 - AuditBehavior

Status: planned.

### Purpose
Wire opt-in command auditing through MediatR without duplicate, recursive, noisy, or infrastructure/system spam.

### Files / Folders
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/**`
- `services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs`
- selected command request files only when explicitly opted in
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/**`

### Work
- Add opt-in marker interface or attribute for auditable requests.
- Add exclusion marker for system/internal/noise requests.
- Add `AuditBehavior<TRequest,TResponse>`.
- Register behavior in a clear pipeline order.
- Prevent duplicate audit enqueue.
- Prevent recursive audit of audit commands/meta-audit/internal outbox operations.
- Implement outcome matrix:
  - validation failure: no audit
  - authorization denied: safe denied audit where request reaches behavior
  - handler exception: failed audit
  - success: succeeded audit
- Decide transaction/outbox atomicity strategy.

### Acceptance Criteria
- AuditBehavior is not global blanket auditing.
- Query audit is not default.
- System/internal commands are excluded.
- Export/redaction/meta-audit recursion does not loop.
- Business command latency is not dominated by sync audit writes.
- Audit failure does not unnecessarily break business commands.

### Verification
- Backend build.
- Unit tests for opt-in/exclusion/duplicate prevention/recursion guard/outcome matrix.
- Integration-style handler tests where feasible.

### Risks
- No current `TransactionBehavior`, Mongo session, or unit-of-work.
- Atomic business write + audit outbox enqueue may need explicit design before broad rollout.
- Authorization behavior does not currently appear in Platform pipeline; denied audit may require API-layer or later permission integration design.

### Commit Suggestion
`feat(platform): add opt-in audit pipeline behavior`

## Phase 4 - API Endpoints

Status: planned.

### Purpose
Expose authorized platform audit query/detail/export/retention/redaction endpoints without mutation or delete surfaces for audit events.

### Files / Folders
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/Platform/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/Queries/**`
- `services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/Commands/**`
- `services/Diten.Platform/tests/**`

### Work
- Add `GET /api/platform/audit/events`.
- Add `GET /api/platform/audit/events/{id}`.
- Add `GET /api/platform/audit/export`.
- Add `PUT /api/platform/audit/retention`.
- Add `POST /api/platform/audit/redact-actor`.
- Enforce permissions:
  - `Platform.Audit.Read`
  - `Platform.Audit.Export`
  - `Platform.Audit.Retention.Update`
  - `Platform.Audit.RedactActor`
- Enforce `PlatformActor`.
- Enforce tenant isolation.
- Add meta-audit for read/export/retention/redaction.
- Add 405/absence tests for forbidden operations.

### Acceptance Criteria
- Unauthorized read/export/redaction is blocked.
- Platform admin can query/export authorized data.
- Tenant A cannot see Tenant B audit events.
- Redaction masks PII and does not delete events.
- Export is audited and redacted.
- No audit event update/delete endpoint exists.

### Verification
- Backend build.
- Integration tests:
  - query
  - detail
  - export
  - redact actor
  - unauthorized
  - forbidden operation tests
  - tenant isolation tests

### Risks
- Gateway route may be missing; do not edit `ocelot.json` directly unless integration-agent owns it.
- Export volume limits must avoid memory/time abuse.
- Redaction must be careful not to create fake audit timelines.

### Commit Suggestion
`feat(platform): expose audit platform api`

## Phase 5 - Frontend Platform UI

Status: planned.

### Purpose
Add platform-admin audit screens using gateway-backed MVC proxy patterns and DataTable v2 conventions.

### Files / Folders
- `frontend/Diten.Web/Controllers/Platform/**`
- `frontend/Diten.Web/Views/Platform/AuditLog/**`
- `frontend/Diten.Web/Views/Platform/AuditRetention/**`
- `frontend/Diten.Web/wwwroot/assets/js/Platform/Audit/**`
- `frontend/Diten.Web/Resources/**`

### Work
- Add `/Platform/AuditLog`.
- Add DataTable v2 audit event list.
- Add advanced filters:
  - date range
  - actor
  - tenant
  - category
  - entity type
  - operation
- Add detail modal with before/after JSON diff.
- Add export button for CSV/JSON.
- Add `/Platform/AuditRetention`.
- Add retention policy management form.
- Add permission denied states.
- Add en/tr resources, and any required existing localization standard resources.
- Use gateway, not direct service ports.

### Acceptance Criteria
- DataTable standard is followed.
- Audit event has no create/edit/delete UI.
- Detail modal displays safe redacted JSON diff.
- Export button calls authorized gateway-backed route.
- Retention page enforces platform-admin permission state.

### Verification
- Frontend build.
- RESX checker.
- DataTable verifier if applicable.
- Browser smoke:
  - AuditLog page
  - filters
  - detail modal
  - export button
  - AuditRetention page
  - permission denied states

### Risks
- DataTable complexity may require custom/non-CRUD pattern while still satisfying v2 contract.
- UI must not imply audit events are editable.
- Frontend must not call service port 5057 directly.

### Commit Suggestion
`feat(web): add platform audit log ui`

## Phase 6 - Testing & Quality Gate

Status: planned.

### Purpose
Bring the full MOD-0021 slice through build, tests, smoke, verifier, and negative/security checks.

### Files / Folders
- test projects under `services/Diten.Platform/tests/**`
- frontend verification scripts/resources as needed
- docs/audit notes if needed

### Work
- Run backend build.
- Run frontend build.
- Run gateway build.
- Run RESX checker.
- Run DataTable verifier.
- Run unit tests:
  - redaction
  - retention validation
  - tenant isolation
  - meta-audit recursion guard
  - outbox enqueue
- Run integration tests:
  - audit event query
  - detail
  - export
  - redact actor
  - unauthorized
  - forbidden operations
- Run browser smoke:
  - AuditLog page
  - filters
  - detail modal
  - export button
  - AuditRetention page
  - permission denied states

### Acceptance Criteria
- Audit events are immutable.
- Commands are audited only when opted in.
- Sensitive fields are redacted.
- Tenant isolation works.
- Platform admin authorized query/export works.
- Meta-audit works and does not recurse.
- GDPR redaction masks PII and does not delete events.
- Retention policy floor/ceiling is enforced.
- Export CSV/JSON works.
- Unauthorized read/export/redaction is blocked.
- Negative tests are explicit.

### Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug`
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug`
- `dotnet build gateway/Diten.ApiGateway/Diten.ApiGateway.csproj -c Debug`
- `dotnet test services/Diten.Platform`
- DataTable verifier.
- RESX checker.
- Browser smoke.

### Risks
- Existing unrelated Platform tests currently fail to compile and must be fixed or explicitly excluded before green full test gate.
- Gateway route ownership may require integration-agent.
- Export volume and redaction edge cases need careful negative testing.

### Commit Suggestion
`test(platform): complete audit trail quality gate`

## Prompt - Start Phase 2

```text
MOD-0021 General Audit Trail icin yalnizca Phase 2 - Application Core implementasyonunu yap.

Kaynak module pack:
execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md

All-phases plan:
execution/domains/platform-shared-services/module-packs/MOD-0021-all-phases-implementation-plan.md

Handoff:
execution/domains/platform-shared-services/module-packs/MOD-0021-phase-2-handoff-plan.md

Zorunlu okuma sirasi:
1) AGENTS.md
2) execution/domains/platform-shared-services/domain-config.md
3) execution/domains/platform-shared-services/module-packs/MOD-0021-general-audit-trail.md
4) execution/domains/platform-shared-services/module-packs/MOD-0021-all-phases-implementation-plan.md
5) execution/domains/platform-shared-services/module-packs/MOD-0021-phase-2-handoff-plan.md
6) .antigravity/workflows/add-module.md
7) docs/platform/master-plan.md

Scope:
- Sadece Phase 2 Application Core.
- API controller yazma.
- Frontend yazma.
- Gateway/ocelot degistirme.
- AuditBehavior yazma.
- Hosted worker loop yazma.
- Export/GDPR endpoint yazma.
- Existing business modules'a audit marker ekleme.
- .antigravity/** dokunma.

Uygula:
- IAuditService
- AuditService
- SensitiveFieldRedactor
- ISensitiveFieldRedactionRegistry ve default registry
- IAuditRetentionPolicyResolver
- IAuditRecursionGuard
- deterministic audit idempotency key builder
- IAuditOutboxWriter uzerinden outbox enqueue orchestration
- meta-audit recursion guard hazirligi
- Phase 2 unit tests

Dogrulama:
- dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug
- mumkunse ilgili Phase 2 unit testlerini calistir
- dotnet test services/Diten.Platform unrelated compile failure verirse ayrica raporla
```
