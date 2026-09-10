# MOD-0149 Account Foundation Implementation (Diten.CrmService)

**Date:** 2026-07-14 · **Scope:** `account-foundation-only` · **Verdict:** PARTIAL (backend foundation built; frontend/gateway/seed + live golden-flow remaining)

## Preflight (fail-closed) — PASS

- MOD-0149 pack `status: ready-for-dev`, `runtime_code_scope: account-foundation-only` ✓
- `services/Diten.CrmService/**` exists (scaffold) ✓ · port 5061 ✓
- DCP-002 gate OK ✓
- **Controlled dependency:** MOD-0048 `account-type`/`account-status` sets **not published yet** → reference validation is a controlled seam that returns `SetMissing` (no CRM local seed / hardcoded fallback). Create/update with a real reference value is blocked until an operator publishes the sets.
- PSS-012 consumer `published-values`-by-setCode endpoint not confirmed live → validator degrades to `SetMissing` gracefully.

## Implemented (backend, Diten.CrmService) — builds clean (0 error/0 warning, verified)

**Domain:** `EntityBase`, `Account` (no ZoneId/MicroZoneId/TerritoryId/SalesRepId), `AccountExternalReference`, `AccountAttributeValue`, `AccountCodeSequence`; 4 repository interfaces.

**Application (Golden Reference Compact backend convention):**
- `Features/Account/` — `AccountModels.cs`, `AccountMapper`, `AccountReferenceValidation`, `IAccountCodeGenerator` + `AccountCodeGenerator` (ACC-{YYYY}-{sequence}, tenant+year, retry→controlled `AccountCodeGenerationException`), `IAccountAuditPublisher` + `AccountAuditEvents`.
- Commands (7): Create/Update/Delete/BulkDelete/LinkParent/UnlinkParent/UpsertAccountAttribute.
- Queries (4): List/ById/Overview/Hierarchy.
- Handlers: `Handlers/CommandHandlers/` (7) + `Handlers/QueryHandlers/` (4) — `{Verb}AccountHandler`, no suffix.
- Validators: `CreateAccountValidator`, `UpdateAccountValidator`.
- `Common/ReferenceValidation/IReferenceDataValidator` (MOD-0048 seam).

**Persistence:** 4 repositories; class maps (Guid→String); indexes — `ux_accounts_tenant_code` (unique, soft-delete partial), `ix_accounts_tenant_name`, `ix_accounts_tenant_parent`, `ux_account_external_refs_tenant_source_external` (unique partial), `ux_account_attributes_tenant_account_code` (unique), `ux_account_code_sequences_tenant_year` (unique).

**Infrastructure:** `GatewayReferenceDataValidator` (published-values consumer, degrades to SetMissing), `LoggingAccountAuditPublisher` (MOD-0021 seam), DI registrations.

**Api:** `Controllers/CRM/AccountController` — `/api/crm/accounts` (+ `{id}`, `/overview`, `/hierarchy`, `/bulk`, `/{id}/parent`, `/{id}/attributes`), `[Authorize]` + `[HasPermission("crm.account.*")]`, `Response<T>` + `CustomBaseController`.

**Tests:** `AccountFoundationTests` (code format, auto-gen, manual duplicate→409, unpublished set→400, cross-tenant→404, circular→400, no-Zone reflection) + `ScaffoldSmokeTests` (DI resolves core+foundation, tenant guard, envelope).

## Enforced integrity rules

- TenantId server-side (from tenant middleware); DTO/payload never carries it.
- Cross-tenant get/update/delete → **404** (repo keyed by tenant).
- Manual duplicate AccountCode → **409**; AccountExternalReference dup (SourceSystem+ExternalId) → **409**.
- Circular hierarchy → **400**; self-parent → **400**; parent not found / cross-tenant parent → **404**.
- Missing required reference set → **400** (controlled); invalid value → **400**.
- Soft delete via IsDeleted/DeletedAt; deleted accounts excluded from reads.
- **No** ZoneId/MicroZoneId persisted (§3.1); Coverage is a read-only `not-available` projection (MOD-0151 source).
- **No** CRM local reference seed / hardcoded fallback.

## NOT done this turn (remaining — PARTIAL)

| Item | Reason |
|---|---|
| Frontend `Views/CRM/Accounts/**` (compact) + JS + 7 resx + `AccountsController` | Large surface; deferred to keep this turn's deliverable buildable+honest. No fake/no-shell → not stubbed. |
| Menu `<li>` in `_LayoutTenantShell.cshtml` | Depends on frontend + gateway; how-to-add-a-module Adım 9 follow-up. |
| Gateway `/api/crm/accounts*` route (5061 downstream) | integration-agent scope (ocelot.json protected). |
| `crm.account.*` permission seed | MOD-0018/AuthService domain (seed pattern). |
| `import` / `export` endpoints | Secondary; declared in pack, deferred (no fake). |
| MOD-0021 audit HTTP wiring | Currently structured-logging seam; real append-client follow-up. |
| `module-implementation-status.md` MOD-0149 row | registries not in this task's allowed changes; follow-up PR. |
| Live golden-flow proof (authenticated fleet) | Requires Gateway+Web+Auth+CRM+Mongo + published sets + seeded perms + logged-in CRM Admin. |
| Final `dotnet test` re-run | Sandbox command-safety classifier temporarily unavailable at report time; backend build verified clean (0/0) twice; foundation logic tests passed in an interim run; smoke tests refactored to the implemented state (re-run pending). |

## Next

1. Publish MOD-0048 `account-type` + `account-status` sets (operator).
2. integration-agent: gateway route for `/api/crm/accounts*` → 5061.
3. Seed `crm.account.*` permissions (MOD-0018).
4. Frontend compact vertical (`Views/CRM/Accounts/**`) once backend route is reachable.
5. Live golden-flow smoke on the authenticated fleet; then update `module-implementation-status.md`.
