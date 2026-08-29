# MOD-0149 — Reference Readiness Final Smoke (Platform restart / account-type publish / create smoke)

**Date:** 2026-07-16 · **Verdict:** PARTIAL

## A. Platform restart / rebuild — done, but consumer still failing (new finding)

| Step | Evidence | Result |
|---|---|---|
| Stop Platform 5057 | pid killed | ✅ |
| `dotnet build Diten.Platform.API` | **0 errors** (MSB3021 lock gone once stopped) | ✅ (ScopeKey fix compiled in) |
| Restart Platform 5057 (`ASPNETCORE_ENVIRONMENT=Development`) | `/health` 200 in 2s; log `"Environment":"Development"` | ✅ |
| `published-values?scope_key=97c5…` account-status | **500** | ❌ |
| Platform exception | `KeyNotFoundException: reference_data_set_not_found` in `ResolveVersionAsync` | ❌ |

**New finding (blocks verification of the ScopeKey fix):** the restarted instance **cannot see the two CRM sets at all**.
`GET /api/v1/reference-data/sets` (token: tenant-97c5 steward, `X-Tenant-Id: 97c5…`) returns **7 sets** — `PRODUCT_CATEGORY, BESTE_DENEME, COUNTRY_CODES, PAYMENT_TERMS, BBB, SSSS, DENEME` — **without `account-type` / `account-status`**. The previously-running instance returned **10 sets including both**.

Read-only DB evidence contradicts the API view — both sets exist, correct tenant, not deleted:

| SetCode | TenantId | matches 97c5 | IsDeleted |
|---|---|---|---|
| `account-type` | 97c59330-…-cc93 | ✅ | false |
| `account-status` | 97c59330-…-cc93 | ✅ | false |
| `COUNTRY_CODES` | 97c59330-…-cc93 (and a 0001 twin) | ✅ | false |

So the failure is **not** the ScopeKey fix and **not** tenant ownership: the set lookup itself (`GetSetByCodeAsync`) returns null for these two sets on the freshly-built binary, while the older running binary resolved them. Root cause **not determined** (candidates: set-document deserialization of API-created docs on the new build, a list/lookup filter, or a caching/projection difference). Not investigated further here — reporting rather than guessing. **No Mongo edit, no workaround, no fake.**

> **Environment note:** the previously-running Platform (started outside this session) was stopped and replaced by an instance started here on the same env/DB (`Development` → `diten_personalization_dev`). Ports 5000/5056/5061 untouched.

## B. `account-type` approve + publish — **BLOCKED (SoD)**

No second tenant-97c5 approver credential available (`sod_submitter_cannot_approve`; submitter = `bestepullukcu@…` / ********). Not bypassed, not faked, no Mongo update. `account-type` remains: values 9 loaded, validate+submit done, `Status=0`, `PublishedVersionId=null`.

## C. Create smoke — **NOT run** (gated on A + B). No fake success.

## Published Set Verification

| SetCode | Published? | Expected | Actual | scope_key Used? | Consumer Result | Status |
|---|---|---|---|---|---|---|
| `account-status` | Yes (DB: Status=1, 5 values, IsDeprecated=false) | 5 | — | ✅ | **500 `reference_data_set_not_found`** | **BLOCKER (set not visible to new binary)** |
| `account-type` | No (Status=0) | 9 | — | ✅ | 500 (same set-lookup failure) | **BLOCKER (SoD + above)** |

## Validation

| Command | Result |
|---|---|
| build `Diten.Platform.API` | ✅ 0 errors |
| Platform restart + `/health` | ✅ 200 |
| build `Diten.CrmService.Api` | ✅ 0 errors |
| test CrmService | ✅ **19/19** |
| build Gateway | ✅ 0 errors (unchanged) |
| published-values account-status / account-type | ❌ 500 (`reference_data_set_not_found`) |
| create smoke | ⚠️ not run (gated) |
| guards (fallback / local seed / ZoneId / MicroZoneId / 360.read / direct-5061 / plaintext pw) | ✅ all 0 |

## Changed Files

Only this report + status note. No code changed in this task (the ScopeKey + parser fixes were made in the previous task and remain intact).

## Open items

1. **Investigate why the rebuilt Platform cannot resolve `account-type`/`account-status`** while the DB shows them under tenant 97c5, not deleted (set-lookup/deserialization/caching). This now blocks verifying the ScopeKey fix.
2. **`account-type` publish** — needs a real second tenant-97c5 approver (SoD).
3. Then Gateway create smoke → frontend.
