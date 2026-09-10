# MOD-0149 — MOD-0048 Publish Verification + Create Smoke

**Date:** 2026-07-16 · **Type:** read-only verification (no code, no Mongo hand-edit) · **Verdict:** PARTIAL

## Preflight

MOD-0149 `ready-for-dev` / `account-foundation-only`. Fleet fully up: Gateway 5000, Auth 5056, Platform 5057, CRM 5061. CRM `scope_key` fix present (validator sends `?scope_key=<server-resolved tenant>`; tests 17/17).

## A. Published Set Verification — **FAILED (2 distinct issues)**

| SetCode | Published? | Expected | Actual (via consumer) | IsDeprecated | scope_key Used? | Result |
|---|---|---|---|---|---|---|
| `account-type` | **No** — version v1 `Status=0`, set `Status=0`, `PublishedVersionId=null` | 9 | **400 `no_published_version`** | n/a | ✅ | **BLOCKER — not published** |
| `account-status` | **Yes (data-level)** — v1 `Status=1`, gov=Published, appr=Approved, `PublishedAt`/`PublishedBy` set, **5 values** (draft/active/inactive/suspended/archived, kebab, `IsDeprecated=false`), snapshot present | 5 | **500 Server Error** | false ✓ | ✅ | **BLOCKER — consumer read broken** |

**Findings (read-only evidence):**
1. **`account-type` was not published.** The manual governance-UI publish covered only `account-status`. Its v1 (`d3254861…`) is still `Status=0` (Draft/Submitted).
2. **`account-status` is genuinely published at the data level but is unreadable through the consumer contract.** The published version's **`ScopeKey` is `null`** while the set is `ScopeType=Tenant` and the consumer requires `scope_key` (empty → `scope_key_required`; tenant GUID → **500** inside `BusinessReferenceDataConsumerQueryService.ResolveVersionAsync`). So the value that CRM must read cannot be resolved. This is a **MOD-0048/PSS-012 (Platform) concern** — not fixable from CRM and out of this task's scope.
3. SetCodes are correct lowercase-kebab; **no UPPER_SNAKE alias**; **no CRM local seed/fallback** (guards = 0).

> **Extra seam mismatch (follow-up, CRM side):** the consumer returns a *model*, not an array — `Response<BusinessReferenceDataPublishedValuesModel>` → `{data:{setCode, versionNumber, publishedAt, items:[{valueCode,…}]}}`. The CRM `GatewayReferenceDataValidator.ExtractValueCodes` currently accepts a bare array or `{data:[…]}`, so it would **not** find `data.items` even on a 200. Needs a small parser fix once the endpoint returns 200 (not changed here — outside this task's allowed scope).

## B. Backend / Gateway Create Smoke — **NOT run**

Gated on A: `account-type` is unpublished → `crm.account` create would return a controlled **400** on reference validation. No fake success produced. Unit level remains green (17/17: auto-gen `ACC-{YYYY}-{seq}`, 409 duplicate, 404 cross-tenant, 400 circular/invalid-ref, soft-delete reload, scope_key sent).

## Validation

| Command | Result |
|---|---|
| Fleet health (5000/5056/5057/5061) | ✅ all up |
| consumer `published-values?scope_key=…` account-type | ❌ 400 `no_published_version` |
| consumer `published-values?scope_key=…` account-status | ❌ 500 (ScopeKey=null vs scope_key) |
| live DB read-only (sets + versions by domain id) | ✅ evidence above |
| build CrmService.Api / Gateway | ✅ 0 errors |
| test CrmService | ✅ 17/17 |
| guards (fallback / local seed / ZoneId / MicroZoneId / 360.read / direct-5061) | ✅ all 0 |

No Mongo hand-edit, no MOD-0048 code change, no CRM local seed, no frontend.

## Remaining blockers

1. **Publish `account-type`** via the governance UI (author is already done: v1 has the 9 values loaded; it needs approve→publish by a second approver per SoD).
2. **`ScopeKey=null` on Tenant-scoped published versions** → consumer `published-values` 500. Platform/MOD-0048 owner must resolve (publish should stamp `ScopeKey`, or the consumer must resolve tenant scope without it).
3. **CRM validator parser** must read `data.items[].valueCode` (model shape), not a bare array — small seam fix once (2) is resolved.

Only after 1+2 can the Gateway create smoke prove the golden flow.
