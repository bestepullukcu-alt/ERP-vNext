# MOD-0149 — Correct Tenant Verification + Gateway Create Smoke

**Date:** 2026-07-16 · **Verdict:** **PASS** — golden flow proven end-to-end through the Gateway

## Correction of a previous wrong conclusion

My earlier audit (`mod-0048-crm-set-visibility-debug.md`) concluded a **"tenant mismatch — bestepullukcu is a default-tenant user"**. **That was wrong, and the user was right.**

`bestepullukcu@…` exists in **both** tenants as two distinct users, and `/api/auth/login` selects the tenant from the **`X-Tenant-Id` header**:

| Login | tenant_id claim | user id |
|---|---|---|
| **with** `X-Tenant-Id: 97c5…` | **`97c59330-…-cc93`** ✅ | `c5769c62-…` (**matches the sets' `CreatedBy`**) |
| without the header | `00000000-…-0001` | `db2d465b-…` |

My token-decode probe had logged in **without** the header, got the 0001 user, and I generalised that into a false root cause. There was never a set-visibility bug **and** never a tenant mismatch — only a flawed probe. Withdrawn.

## Published-values Verification (97c5 token) — PASS

| SetCode | Expected | Actual | scope_key | Token tenant | Result |
|---|---|---|---|---|---|
| `account-type` | 9 | **9** — organization, hospital, pharmacy, clinic, distributor, wholesaler, corporate-group, branch, other | `97c5…` | 97c5 ✅ | **200 PASS** (exact match, `IsDeprecated=false`) |
| `account-status` | 5 | **5** — draft, active, inactive, suspended, archived | `97c5…` | 97c5 ✅ | **200 PASS** |

This also **verifies the MOD-0048 ScopeKey fix live** (versions carry `ScopeKey=null`; without the fix these return `scope_not_found`/500).

## CRM persistence fix (required to run the smoke)

The first create returned 500: `GuidSerializer cannot deserialize a Guid when GuidRepresentation is Standard and binary sub type is UuidLegacy`. Cause: the CRM scaffold had **no class maps**, so the driver wrote legacy sub-type-3 Guids while the registered standard serializer reads sub-type 4. MdmService/HcmService avoid this by storing Guids as **strings**; my earlier attempt failed because `MapIdMember` cannot map `Id`/`TenantId`, which are inherited from `EntityBase`.

**Fix:** register the class map on **`EntityBase`** (`MapIdMember(e => e.Id)` + `TenantId` → `GuidSerializer(BsonType.String)`); derived types AutoMap and inherit it; derived Guid members (`ParentAccountId`, `AccountId`) also mapped to string. Serialization-only; no business logic, no Mongo hand-edit.

## Gateway Create Smoke (via Gateway 5000, 97c5 CRM Admin) — **ALL PASS**

| # | Step | Evidence | Result |
|---|---|---|---|
| 1 | Create, `AccountCode` empty | **201**, id `48266952-…`, **`ACC-2026-000001`** (matches `ACC-{YYYY}-{NNNNNN}`) | ✅ |
| 2 | List | 200, total=1, contains the account | ✅ |
| 3 | Details / 360 overview | 200, code `ACC-2026-000001`, `coverage={status:"not-available",source:"MOD-0151"}` (read-only projection, no Zone persisted) | ✅ |
| 4 | Duplicate manual AccountCode | **409** "AccountCode already exists for this tenant." | ✅ |
| 5 | Invalid AccountType | **400** "'not-a-real-type' is not a valid published value of reference set 'account-type'." — **live MOD-0048 validation through the CRM seam (scope_key + parser)** | ✅ |
| 6 | Unknown / cross-tenant id | **404** "Account not found." | ✅ |
| 7 | Soft delete | 200 | ✅ |
| 8 | Reload after delete | **404** (soft-deleted hidden) | ✅ |

## Validation

| Command | Result |
|---|---|
| Auth 5056 / Platform 5057 / Gateway 5000 / CRM 5061 health | ✅ all up |
| login + token decode (97c5 claim) | ✅ (credentials masked) |
| published-values account-type / account-status | ✅ 200 / 9 · 200 / 5 |
| build CrmService.Api | ✅ 0 errors |
| test CrmService | ✅ **19/19** |
| build ApiGateway | ✅ 0 errors |
| Gateway create smoke | ✅ 8/8 steps |
| guards (fallback / local seed / ZoneId / MicroZoneId / 360.read / direct-5061 / plaintext pw) | ✅ all 0 |

## Changed Files

| File | Change | Why |
|---|---|---|
| `…/CrmService.Persistence/DependencyInjection.cs` | `RegisterClassMaps()` — Guid→string via **EntityBase** base class map | Fixes UuidLegacy/Standard deserialization 500; unblocks create |
| `docs/audits/mod-0149-correct-tenant-create-smoke.md` | This report | Evidence + correction |

No frontend · no Mongo hand-edit · no CRM local seed · no hardcoded fallback · no AuthService change · no tenant-isolation weakening · no MOD-0048 runtime change in this task.

## Open items

1. **Frontend** Golden Reference Compact vertical + tenant-shell menu (`crm.account.read` guard) — the only remaining MOD-0149 gap.
2. Platform unit tests for `MatchesScope` (tenant blank-key / company-region strict) — follow-up.
3. `import` / `export` endpoints + MOD-0021 audit HTTP wiring — deferred follow-ups.
4. Dev residue: one orphan `account_code_sequences` doc from the pre-fix attempt (legacy-Guid, unmatched); harmless, no hand-edit performed.

## Verdict: PASS

Correct 97c5 tenant verified; `account-type` (9) and `account-status` (5) published-values return 200 with exact expected codes; **Gateway create smoke proven end-to-end** including auto-generated `ACC-2026-000001`, 409/400/404 failure paths and soft-delete. MOD-0149 Account Foundation backend is **functionally complete and live-proven**.
