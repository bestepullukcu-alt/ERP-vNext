# MOD-0033-FU01 Tenant Quota Governance UI - Post-Fix Smoke

## Summary

- **Date:** 2026-08-07
- **Branch:** `codex/pss/mod-0023-transition-gate-sort-fix`
- **Permission fix:** `d1ecd9c5 fix: align tenant quota proxy permission key`
- **Result:** PASS-with-gaps

The local fix aligns the Web same-origin quota proxy with the Platform quota API on canonical permission key `platform.tenants.quotas.read`. Legacy `platform.tenants.quotas.view` remains Platform-side alias compatibility only.

## Validation

- `frontend/Diten.Web` build passed with existing warnings only.
- Focused Platform permission/alias/quota tests passed: `35/35`.
- `git diff --check` passed.

## Smoke Evidence

- Tenant used: `00000000-0000-0000-0000-000000000001`.
- Quota data state: no quota rows; list endpoints returned `200` with `data: []`.
- Gateway quota list: `GET :5000/api/platform/tenants/{tenantId}/quotas` returned `200`, `isSuccessful: true`, `dataLength: 0`.
- Direct Platform quota list: `GET :5057/api/platform/tenants/{tenantId}/quotas` returned `200`, matching Gateway behavior.
- Optional `users.max` read returned expected `404 QUOTA_USAGE_NOT_FOUND` through both Gateway and direct Platform.
- UI same-origin proxy proof is static/code proof: browser JavaScript fetches `/Platform/Tenants/{tenantId}/QuotaStatus`; no browser-to-service-port call is used.
- Unauthenticated Gateway and direct Platform requests returned `403 Forbidden Actor`.

## Remaining Gaps

- Platform health was degraded because RabbitMQ was unavailable; Mongo and Hangfire were healthy.
- Authenticated Web-cookie proof was blocked: Web login returned `200` JSON but no reusable curl auth cookie, so `/Platform/Tenants/{tenantId}/QuotaStatus` redirected to `/platform/login`.
- Positive quota-row render proof remains pending because the safe tenant had no quota rows.
- Restricted non-bypass actor proof was skipped because no safe existing restricted token/session was available.

## Status

MOD-0033-FU01 remains `review / pending-web-smoke`, not `done`.

Quota override/admin mutation UI remains outside MOD-0033-FU01 and requires separate approved scope.
