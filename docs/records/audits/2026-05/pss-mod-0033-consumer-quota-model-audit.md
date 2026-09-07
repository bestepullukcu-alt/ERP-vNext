# MOD-0033 Consumer / Quota Model Audit

Date: 2026-05-12
Branch: `feature/pss/mod-0033-consumer-quota-model`
Status: in-progress

## Scope Implemented
- Added tenant-owned `QuotaUsage` and `QuotaEvent` documents in `Diten.Platform`.
- Added quota repositories with tenant/key/soft-delete filters and atomic conditional consume.
- Added `IQuotaService` runtime enforcement, status, initialization, sync, release, reset, and recalculate flows.
- Added CQRS command/query/handler/validator files for quota operations.
- Added public platform admin status endpoints and internal service-authorized mutation endpoints.
- Wired MongoDB indexes, DI registrations, and subscription assignment/activation quota initialization.
- Integrated `modules.max` consume/release into tenant module entitlement add/enable/disable/remove flows.

## Standards Check
- CQRS requests, handlers, and validators are action-separated.
- API responses use `Response<T>` through `CustomBaseController`.
- Public status endpoints use `PlatformActor` plus `[HasPermission]`.
- Internal mutation endpoints require `X-Internal-Api-Key`, `TenantId`, and `Source`; token/API key values are not logged.
- `quota_usages` uses a tenant + quota key unique active index.
- Limit exceeded uses a single MongoDB conditional update and does not increment usage when the filter fails.
- Gateway `ocelot.json` was not changed because it is protected and gateway routing is integration-agent owned.

## Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` passed.
- `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests/Diten.Platform.Application.Tests.csproj -c Debug --no-restore` could not complete because existing unrelated test files currently do not compile.

## Known Gaps
- Gateway route publication for quota endpoints remains a protected integration-agent task.
- Full quota dashboard and future tenant UI summary remain out of scope for this pack.
- Gateway-level `api.calls.per.month` counting/enforcement remains a later integration task; recalculation returns `QUOTA_RECALCULATION_NOT_SUPPORTED` without that counter.
