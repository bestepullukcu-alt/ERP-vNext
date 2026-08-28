# MOD-0149 Operator/Ops Blocker Closeout — MOD-0048 Publish + Auth Live Grant

**Date:** 2026-07-14 · **Type:** ops re-seed executed + live Mongo verification (no operator UI publish, no Mongo hand-edit, no code fake) · **Verdict:** PARTIAL (blocker B CLOSED; blocker A OPEN)

## Preflight — PASS

- MOD-0149 `ready-for-dev`, `account-foundation-only` ✓ · backend/gateway/seed/grant in source ✓
- Local Mongo up (27017); AuthService port 5056 free (fleet down). Query tool: `mongoexport.exe`.

## B. AuthService Re-seed / Live Grant — **CLOSED** ✅

**Action taken (legitimate ops re-seed, not a code fake):** built + ran `Diten.AuthService` (Development) → its `DataSeeder.SeedAsync` ran to "Seeding completed successfully" (idempotent: `if (!exists) InsertOneAsync`) → stopped the service.

**Live verification (`diten_auth_v3`):**

| Permission | In Source Seed? | In Live Catalog? | Granted to a Role? | Module | Scope | Result |
|---|---|---|---|---|---|---|
| crm.account.read/create/update/delete/import/export | ✅ | ✅ (9/9) | ✅ (rolePermissions by PermissionId) | `crm-account` | 0=Tenant | **PASS** |
| crm.account.hierarchy.manage / attribute.manage / overview.read | ✅ | ✅ | ✅ | `crm-account` | 0=Tenant | **PASS** |

- `permissions` query `{Key:/^crm.account/}` → **9 rows**, `Module=crm-account`, `Scope=0` (Tenant).
- `rolePermissions` keys by **PermissionId** (GUID), not PermissionKey. Matching the 9 crm.account PermissionIds against 597 rolePermissions rows → **9/9 granted** (2–3 grants each = SuperAdmin full catalog + tenant Admin breadth via `AdminModules += crm-account`).
- Runtime match: keys equal the `[HasPermission("crm.account.*")]` attributes exactly; `crm.account.360.read` absent.

## A. MOD-0048 / PSS-012 Publish — **BLOCKER (open)**

Live query `diten_personalization_dev.business_reference_data_sets`:

| SetCode | Published? | Values | DisplayName/L10n | Evidence | Result |
|---|---|---|---|---|---|
| `account-type` | **No — absent** | — | — | not in 15 existing sets | **BLOCKER** |
| `account-status` | **No — absent** | — | — | not in 15 existing sets | **BLOCKER** |

- Not performed here: publishing requires the PSS-012 governance flow (create→validate→submit→approve→publish) via the governance UI/API with a platform-admin token + running Platform service. **Directly writing the sets into Mongo would bypass validate→publish governance = fake readiness → refused.** No CRM local seed/fallback created.
- **SetCode convention note (open question):** existing live sets are UPPER_SNAKE (`COUNTRY_CODES`=published, `PAYMENT_TERMS`, `PRODUCT_CATEGORY`). MOD-0149 requires **lowercase kebab** `account-type`/`account-status` (the CRM consumer seam queries these exact codes). Operator must author with lowercase codes; EA may reconcile the convention.

**Operator checklist (MOD-0048/PSS-012 governance, `Platform.BusinessReferenceData.*`):**
1. Create set `account-type` (tenant) → values: organization, hospital, pharmacy, clinic, distributor, wholesaler, corporate-group, branch, other.
2. Create set `account-status` → values: draft, active, inactive, suspended, archived.
3. `validate → submit → approve → publish` each (Status → 1/Published); `IsDeprecated=false`.
4. 7-language DisplayName.
5. Confirm consumer read returns them (template: `docs/audits/mod-0149-crm-reference-data-authoring-template.json`).

## C. Backend / Gateway Create Smoke — NOT run

Authenticated create via Gateway not executed: blocker A open (reference sets unpublished → create would 400 on `account-type`/`account-status` validation). Auth/permission path now provable (grant live) but reference validation blocks a successful create. Unit level: 13/13 (auto-gen, 409/404/400/delete-reload/external-ref/no-Zone).

## Validation Commands

| Command | Result |
|---|---|
| build AuthService.Api | ✅ 0 error |
| test AuthService.Application.Tests | ✅ 289 passed |
| build CrmService.Api | ✅ 0 error |
| test CrmService.Application.Tests | ✅ 13 passed |
| build ApiGateway | ✅ 0 error |
| live: crm.account.* catalog | ✅ **9/9** (blocker B closed) |
| live: crm.account.* role grant | ✅ **9/9 by PermissionId** |
| live: account-type/account-status published | ❌ absent (blocker A) |

## Guards — clean

hardcoded account-type/status fallback in CRM = 0 · ZoneId/MicroZoneId = 0 · crm.account.360.read = 0 · direct 5061 frontend = 0.

## Changed Files

| File | Change |
|---|---|
| docs/audits/mod-0149-operator-ops-blocker-closeout.md | Created |
| execution/registries/module-implementation-status.md | Blocker B closed; A open |

Runtime action: ran AuthService once to execute its own idempotent seeder (no source change, no Mongo hand-edit). No CRM/frontend/MOD-0048 code touched.

## Verdict: PARTIAL

Blocker B (`crm.account.*` catalog + role grant) **CLOSED and live-verified**. Blocker A (MOD-0048 `account-type`/`account-status` publish) **OPEN** — operator governance-UI action, not faked. Frontend + live create smoke gated on A.
