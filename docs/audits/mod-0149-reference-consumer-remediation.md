# MOD-0149 — Reference Consumer Remediation (account-type publish / ScopeKey / CRM parser)

**Date:** 2026-07-16 · **Verdict:** PARTIAL (B + C fixed in code; A SoD-blocked; smoke gated)

## A. `account-type` publish — **BLOCKED (SoD)**

v1 (`d3254861…`) has the 9 values loaded + validate + submit done, but `Status=0` / `PublishedVersionId=null`. Approve requires a **second tenant-97c5 user** (`sod_submitter_cannot_approve`); no such credential is available. Not bypassed, not faked, no Mongo edit.

## B. MOD-0048/PSS-012 tenant-scoped `published-values` — **ROOT-CAUSED + FIXED (code)**

**Root cause (systemic, not data-specific):** `BusinessReferenceDataVersion.ScopeKey` is **never assigned anywhere in the codebase** (verified: the only `ScopeKey =` assignment is on *usage registrations*, a different entity). `BusinessReferenceDataConsumerQueryService.MatchesScope` required `version.ScopeKey == scopeKey`, so for **every** non-global scope it could never match → `KeyNotFoundException("scope_not_found")` → **500**. This broke tenant-scoped consumer reads for all sets, including the correctly-published `account-status` (v1: Status=1, gov=Published, appr=Approved, 5 values, `IsDeprecated=false`, snapshot present).

**Fix** (`BusinessReferenceDataConsumerQueryService.MatchesScope`): a **tenant**-scoped version with a blank `ScopeKey` *is* the tenant's version and matches. Safe because versions are `TenantScopedEntity` and every repository read is already filtered by `TenantContext.TenantId` (`GetPublishedVersionsBySetCodeAsync` filters `x.TenantId == TenantContext.TenantId`) → **no cross-tenant leakage**. `Company`/`Region` keep strict matching (their key is a real discriminator within a tenant). Backward-compatible: already-published versions (e.g. `account-status`) resolve without republish — no Mongo backfill.

`Diten.Platform.Application` builds **0 errors**. The `Diten.Platform.API` build currently fails only with **MSB3021 file-lock** (the running 5057 holds the DLLs) — i.e. the running instance is a **stale binary**; the fix takes effect after a Platform restart.

> Note: `GetBusinessReferenceDataPublishedValuesQuery` *does* implement `IBusinessReferenceDataRequest`, and `BusinessReferenceDataExceptionBehavior` *is* registered (`AddOpenBehavior`, innermost) mapping `KeyNotFoundException → 404`. The observed 500/400-ProblemDetails therefore points to the stale running build; after restart these should surface as controlled `404 scope_not_found` / `404 no_published_version` envelopes. To be re-verified post-restart.

## C. CRM `GatewayReferenceDataValidator` parser — **FIXED + TESTED**

Now parses the canonical `Response<BusinessReferenceDataPublishedValuesModel>` → `{data:{setCode,versionNumber,publishedAt,items:[{valueCode,displayName,isActive,…}]}}`, while still accepting a bare array, `{data:[…]}` and `{items:[…]}`. Deprecated values (`isDeprecated:true` **or** `isActive:false`) are excluded → remain **InvalidValue (400)**. `valueCode`/`value_code`/`code`/`value` all supported. No hardcoded fallback, no local list; `scope_key` still sent from the server-resolved tenant.

**Tests: 19/19 green** — canonical `data.items` envelope parsed; deprecated/inactive → 400; invalid value → 400; missing/unpublished set → SetMissing→400; no tenant → SetMissing + no HTTP call; `scope_key` sent.

## D. Create smoke — NOT run

Gated on A (`account-type` unpublished) and on the Platform restart for B. No fake success.

## Changed Files

| File | Change |
|---|---|
| `…/Platform.Application/…/Services/BusinessReferenceDataConsumerQueryService.cs` | `MatchesScope`: tenant-scope blank-`ScopeKey` match (repo already enforces TenantId) |
| `…/CrmService.Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` | Parse `data.items[]`; skip deprecated/inactive; multi-shape support |
| `…/CrmService.Application.Tests/GatewayReferenceDataValidatorTests.cs` | +2 tests (canonical envelope, deprecated not selectable) |
| `docs/audits/mod-0149-reference-consumer-remediation.md` | This report |

No Mongo hand-edit · no CRM local seed · no hardcoded fallback · no frontend · no AuthService change · no ZoneId/MicroZoneId · no `crm.account.360.read`.

## Remaining

1. **Restart Platform (5057)** so the ScopeKey fix loads; then verify `GET /sets/account-status/published-values?scope_key=<tenant>` → **200 with 5 values**.
2. **Publish `account-type`** (second SoD approver) → verify 9 values.
3. **Platform-side unit tests for `MatchesScope`** (tenant blank-key match / company-region strict / wrong key no-leak) — not added here (needs Platform test host); follow-up.
4. Then run the Gateway create smoke.
