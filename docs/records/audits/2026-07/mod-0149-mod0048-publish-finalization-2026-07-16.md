# MOD-0149 — MOD-0048 Publish Finalization (Second Approver + scope_key Fix)

**Date:** 2026-07-16 · **Verdict:** PARTIAL (scope_key fix DONE; publish still SoD-blocked — no real second approver credential)

## A. Approve + Publish — NOT executed (no real second approver)

The task supplied the second approver as literal placeholders — `<SECOND_APPROVER_EMAIL>` / `<SECOND_APPROVER_PASSWORD>` — i.e. **no actual credential**. Per the SoD/security rules I did **not** guess a password, enumerate the identity store to find another account, crack a hash, hand-edit Mongo, or bypass SoD.

Live read-only state (unchanged, still awaiting a second-actor approval):

| SetCode | ScopeType | Status | ActiveDraftVersionId | PublishedVersionId |
|---|---|---|---|---|
| `account-type` | Tenant | 0 (Draft/Submitted) | `d3254861-3387-4d80-a10b-0fde1be446d1` | **null** |
| `account-status` | Tenant | 0 (Draft/Submitted) | `2b2c5811-9fed-4f25-8114-514e115ab260` | **null** |

Governance state (prior run): values loaded (9 / 5), validated, **submitted** by `bestepullukcu@…` (********). Remaining: a **second tenant-97c5 user** with `platform.businessreferencedata.version.approve` must `approve` → `publish` (X-Tenant-Id: 97c5). SoD (`sod_submitter_cannot_approve`) is a real 2-person control; override does not bypass it; a platform_admin token is rejected by the tenant-scoped endpoints, and the default-tenant admin cannot act cross-tenant on 97c5 versions.

## B. CRM consumer `scope_key` fix — **DONE** ✅

PSS-012 tenant-scoped sets require `?scope_key=<tenantId>` on the consumer read (otherwise `scope_key_required`). The CRM seam now sends it from the **server-resolved tenant** — no hardcoded/default tenant, no local list.

| File | Change |
|---|---|
| `…/Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` | Inject `ITenantContext`; append `?scope_key={tenantId}` to `…/sets/{setCode}/published-values`; when no tenant context → controlled `SetMissing` + warning, **no call attempted**, no fallback |
| `…/tests/…/GatewayReferenceDataValidatorTests.cs` | New: scope_key is sent for the tenant; no-tenant → SetMissing & no HTTP call; unknown value → InvalidValue; unpublished set (404) → SetMissing |

Build 0 errors; **tests 17/17 green** (13 existing + 4 new).

## C. Backend / Gateway create smoke — NOT run

Gated on A: sets are Submitted, not Published → `account-type`/`account-status` validation would still return a controlled 400. No fake success produced. Unit level remains green (auto-gen `ACC-{YYYY}-{seq}`, 409 dup, 404 cross-tenant, 400 circular/invalid-ref, soft-delete reload).

## Guards / security

hardcoded fallback & local seed = 0 · ZoneId/MicroZoneId = 0 · crm.account.360.read = 0 · direct-5061 = 0 · plaintext password in changed files = **0** · tokens never persisted · no Mongo hand-edit · **no SoD bypass**.

## Remaining (single blocker)

Provide a **real second tenant-97c5 approver** (≠ `bestepullukcu`) holding `version.approve`, then for each version:
`POST /api/v1/reference-data/versions/{versionId}/approve {"decision":"approve"}` → `POST …/publish {"publish_mode":"Immediate"}` (X-Tenant-Id: 97c5) → verify `GET /sets/{setCode}/published-values?scope_key=97c5…` returns 9 / 5 values, `IsDeprecated=false`, DB `Status=1` + `PublishedVersionId` set. Then run the CRM create smoke.
