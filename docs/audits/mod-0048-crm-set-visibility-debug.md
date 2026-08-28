# MOD-0048 — CRM Set "Visibility" Debug — Root Cause: Tenant Mismatch (NOT a Platform bug)

**Date:** 2026-07-16 · **Verdict:** PARTIAL (root cause found; no Platform fix warranted; live verify needs a tenant-97c5 credential)

## Conclusion up front

**There is no set-lookup/visibility regression.** Tenant isolation is working exactly as designed. The `reference_data_set_not_found` / "only 7 sets" symptom was caused by a **tenant mismatch in the credential used to query**, not by the rebuilt binary, the ScopeKey fix, deserialization, projection, collation or caching. My previous hypothesis (in `mod-0149-reference-readiness-final-smoke.md`) is **withdrawn**.

## Root Cause Analysis

| Hypothesis | Evidence | Result |
|---|---|---|
| Wrong DB / connection | Platform log `"Environment":"Development"` → `diten_personalization_dev`; API returns `COUNTRY_CODES` which exists in dev | ❌ rejected |
| Wrong collection name | `business_reference_data_sets` reads fine (7 rows returned) | ❌ rejected |
| Deserialization/projection silently skipping API-created docs | The "missing" sets are a **clean tenant partition**, not a random subset | ❌ rejected |
| Cache / index / collation on lowercase-kebab SetCode | `qms-document-class`, `qms-document-retention` (lowercase-kebab) exist fine under 97c5 | ❌ rejected |
| Status/ScopeType/IsDeleted filter | Both sets `IsDeleted=false`, `Status=1` | ❌ rejected |
| **Tenant mismatch (credential ≠ data owner)** | **CONFIRMED — see below** | ✅ **ROOT CAUSE** |

**Decisive evidence:**
- `bestepullukcu@…` (********) JWT claim: **`tenant_id = 00000000-0000-0000-0000-000000000001`** → a **default-tenant** user (`actor_type=tenant_user`).
- Platform resolves the tenant context from the **JWT claim** (the `X-Tenant-Id` header is not authoritative for `tenant_user`), and `BusinessReferenceDataStewardshipRepository` filters `x.TenantId == TenantContext.TenantId`.
- DB partition (dev, 17 sets total):
  - **default tenant `…0001` → exactly 7 sets**: `BBB, BESTE_DENEME, COUNTRY_CODES, DENEME, PAYMENT_TERMS, PRODUCT_CATEGORY, SSSS`
  - **tenant `97c5…` → 10 sets** incl. `account-type`, `account-status`, `qms-document-*`
- The API returned **exactly** the 7 default-tenant sets → the repository is correct; the token simply belongs to the other tenant.

So: **the CRM reference sets live in tenant `97c5…`, but the only available credential authenticates into tenant `…0001`.** Querying them with this token *must* return not-found — and making it succeed would **break tenant isolation** (explicitly forbidden).

## Visible vs Invisible — field-level diff (definitive)

Read-only comparison of two **visible** sets vs the two **invisible** CRM sets:

| Field | Visible (`DENEME`, `PRODUCT_CATEGORY`) | Invisible (`account-type`, `account-status`) | Difference | Impact |
|---|---|---|---|---|
| **TenantId** | `00000000-…-0001` | **`97c59330-…-cc93`** | ✅ **THE differentiator** | **Repository filters `TenantId == TenantContext.TenantId` → excluded for a 0001 token** |
| Document shape | 21 keys | 21 keys | none (**identical**) | rules out deserialization/projection skip |
| `_t` discriminator | none | none | none | rules out polymorphic mapping |
| `BusinessReferenceDataSetId` | present | present | none | domain id fine |
| `IsDeleted` | false | false | none | not a delete filter |
| `SetCode` casing | UPPER_SNAKE | lowercase-kebab | cosmetic | **not** the cause (`qms-document-*` lowercase-kebab sets are visible under 97c5) |
| ScopeType | Global | Tenant | expected | affects scope matching only, not visibility |
| Status | 0 (Draft) | **1 (Published)** | expected | — |
| PublishedVersionId | None | **`d3254861…` / `2b2c5811…`** | expected | — |

**Conclusion: `TenantId` is the only difference.** Document shapes are byte-for-byte structurally identical (21 keys, no discriminator, no missing/extra fields), so deserialization, projection, collation, SetCode normalization and caching are all excluded.

## Publish state (new, good news)

| SetCode | Status | PublishedVersionId | Note |
|---|---|---|---|
| `account-status` | **1 (Published)** | `2b2c5811-…` | consistent ✅ |
| `account-type` | **1 (Published)** | **`d3254861-…`** | consistent ✅ (the earlier `null` was transient — the operator has since completed the publish; **anomaly withdrawn**) |

The operator has now published **both** sets. The SoD approve/publish blocker is therefore **closed at the data level** — no publish-flow bug and no governance repair needed.

## Fix Summary

**No Platform code change made in this task.** The reported symptom is not a defect; "fixing" the lookup to satisfy a foreign-tenant token would violate tenant isolation.

The **ScopeKey fix** from the previous task (`BusinessReferenceDataConsumerQueryService.MatchesScope`: a *tenant*-scoped version with a blank `ScopeKey` matches, since the repository already filters `TenantId`) **remains in place and compiles (0 errors)**, but is **still unverified live** — verification requires a tenant-97c5 token. It is still expected to be necessary: `BusinessReferenceDataVersion.ScopeKey` is never assigned anywhere in the codebase.

## Live Verification

| Step | Evidence | Result |
|---|---|---|
| Build `Diten.Platform.API` | 0 errors | ✅ |
| Platform 5057 restart + `/health` | 200 | ✅ |
| `GET /sets` (bp token) | 7 sets = exactly the default-tenant partition | ✅ correct behaviour |
| `published-values` account-status/account-type (bp token) | 500 `reference_data_set_not_found` | ⚠️ **expected** — wrong tenant, not a bug |
| Verify with a tenant-97c5 token | **not possible — no 97c5 credential** | ⚠️ blocked |
| Build CrmService.Api / tests | 0 errors / **19/19** | ✅ |

## Open Items / Blockers

| Item | Severity | Owner | Blocks Frontend? | Blocks Release? |
|---|---|---|---|---|
| **Tenant-97c5 credential** (or confirmation of which tenant CRM actually runs in) — needed to verify published-values + run create smoke | **High** | user/ops | **Yes** | **Yes** |
| **Tenant decision:** sets are in `97c5`; the available CRM Admin credential is `…0001`. Either publish the sets in the tenant CRM uses, or supply a 97c5 CRM Admin. | **High** | EA/ops | **Yes** | **Yes** |
| `account-type`: `Status=1` but `PublishedVersionId=null` (inconsistent publish) | Medium | MOD-0048 owner | Maybe | Maybe |
| ScopeKey fix live verification (needs 97c5 token) | Medium | Platform | — | — |

## Guards

No Mongo hand-edit · no CRM local seed · no hardcoded fallback · no fake published-values · no SoD bypass · no frontend · no tenant-isolation weakening · credentials masked, tokens never persisted.

## Verdict: PARTIAL

Root cause identified and proven (**tenant mismatch, not a Platform defect**); the previously-suspected regression is withdrawn; no code fix warranted. Live verification and the create smoke remain blocked on a **tenant-97c5 credential** (or a decision to publish the reference sets into the tenant the CRM Admin actually belongs to).
