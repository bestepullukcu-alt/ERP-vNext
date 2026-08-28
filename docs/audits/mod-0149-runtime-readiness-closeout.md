# MOD-0149 Runtime Readiness Closeout — MOD-0048 Publish + Auth Re-seed Verification

**Date:** 2026-07-14 · **Type:** runtime readiness verification (live Mongo evidence; no operator/UI publish, no fleet re-seed performed) · **Verdict:** PARTIAL

## Preflight — PASS

- MOD-0149 pack `ready-for-dev`, `runtime_code_scope: account-foundation-only` ✓
- Backend Account foundation present (7 command handlers) ✓ · Gateway `/api/crm/accounts*` (2 routes) ✓
- `crm.account.*` seed in DataSeeder (9) ✓ · `crm-account` in `AdminModules` ✓ (source)
- Local Mongo up (27017). Query tool: `mongoexport.exe`. DBs: auth = `diten_auth_v3`; PSS-012 reference = `diten_personalization_dev`.

## A. MOD-0048 Published Set Verification — **BLOCKER (not published)**

Live query of `diten_personalization_dev.business_reference_data_sets`:

| SetCode | Published? | Values | Result |
|---|---|---|---|
| `account-type` | **No — set does not exist** | — | **BLOCKER** |
| `account-status` | **No — set does not exist** | — | **BLOCKER** |

- The collection exists (15 sets) but contains only legacy/test sets (`COUNTRY_CODES` Status:1, `PAYMENT_TERMS`, `PRODUCT_CATEGORY`, `DENEME`, …). No CRM sets.
- **SetCode convention note (open question):** existing live sets use **UPPER_SNAKE** (`COUNTRY_CODES`); the MOD-0149 spec + authoring template use **lowercase kebab-case** (`account-type`). The CRM consumer seam queries `account-type`/`account-status` exactly, so the operator must author with those exact lowercase SetCodes (or EA reconciles the convention). No CRM local seed was created.
- Status enum observed: `0` = Draft, `1` = Published.

**Operator checklist (MOD-0048 / PSS-012 governance UI, `Platform.BusinessReferenceData.*`):**
1. Create set `account-type` (tenant scope) → add values `organization, hospital, pharmacy, clinic, distributor, wholesaler, corporate-group, branch, other`.
2. Create set `account-status` → values `draft, active, inactive, suspended, archived`.
3. `validate → submit → approve → publish` each (Status → Published / `1`).
4. Localize DisplayName (7 languages).
5. Confirm consumer read returns them (see template `docs/audits/mod-0149-crm-reference-data-authoring-template.json`).

## B. Auth Re-seed / Permission Grant Verification — **BLOCKER (not live)**

Live query of `diten_auth_v3.permissions`:

| Permission | Seeded in source? | In live catalog? | Granted to Admin? | Result |
|---|---|---|---|---|
| crm.account.read / create / update / delete / import / export / hierarchy.manage / attribute.manage / overview.read | **Yes** (DataSeeder) | **No — 0 rows** | **No (catalog empty)** | **BLOCKER** |

- Query method validated: `auth.users.*` keys are present (confirms DB/collection/`Key` field), so the 0-result for `^crm.account` is reliable.
- Root cause: `DataSeeder` (seed) + `AdminModules` (grant) changes are in **source only**; they take effect when **AuthService is rebuilt + restarted** (seed runs at startup; `RoleProvisioningService` grants). The fleet has **not** been re-seeded since the change.
- Source builds clean: AuthService.Api 0/0, AuthService.Application.Tests **289/289** (grant change safe).

**Ops checklist:** rebuild + restart `Diten.AuthService` (fleet) → `DataSeeder` seeds the 9 `crm.account.*` permissions; tenant Admin role receives them via the `AdminModules` breadth clause. Then re-run the live catalog query to confirm 9 rows + Admin grant. Existing tenants may need role re-provisioning (`RoleProvisioningService`) if not auto-run.

## C. Backend / Gateway Live Smoke — NOT run (prerequisites open)

- Authenticated CRM Admin create via Gateway **not executed**: no token/authenticated fleet + required reference sets unpublished (create would 400) + `[HasPermission("crm.account.create")]` unresolvable (catalog empty).
- Proven at unit level (13/13): auto-gen `ACC-{YYYY}-{sequence}`, duplicate→409, cross-tenant→404, circular→400, unpublished-set→400, delete-reload→404, external-ref-dup→409, no-Zone.

## Validation Commands

| Command | Result |
|---|---|
| build CrmService.Api | ✅ 0 error |
| test CrmService.Application.Tests | ✅ 13 passed |
| build AuthService.Api | ✅ 0 error |
| test AuthService.Application.Tests | ✅ 289 passed |
| build ApiGateway | ✅ 0 error |
| live: crm.account.* in catalog | ❌ 0 (blocker B) |
| live: account-type/account-status published | ❌ absent (blocker A) |

## Guards — all clean

hardcoded account-type/status fallback in CRM = 0 · ZoneId/MicroZoneId persisted = 0 · crm.account.360.read = 0 · direct 5061 in frontend = 0.

## Changed Files

| File | Change |
|---|---|
| docs/audits/mod-0149-runtime-readiness-closeout.md | Created (this report) |
| execution/registries/module-implementation-status.md | MOD-0149 runtime-readiness note |

No CRM local seed, no MOD-0048 runtime code, no frontend, no Mongo hand-edit.

## Verdict: PARTIAL

Both runtime readiness prerequisites are **open, evidence-based**: (A) MOD-0048 `account-type`/`account-status` **not published** (operator action), (B) `crm.account.*` **not in live catalog** (AuthService re-seed/restart not run). Source builds clean; no boundary violation; no fake readiness. Frontend must not start until A + B are live (otherwise it can't be E2E-proven).
