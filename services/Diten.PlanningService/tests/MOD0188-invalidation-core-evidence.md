# MOD-0188 internal invalidation core evidence — 2026-10-07

Scope: WP-MOD0188-INVALIDATION-CORE. This is internal producer behavior, not a public API, delivered event, live authorization/impact proof, MOD-0189 response, or AC-D20–D22 acceptance.

## Boundary and behavior

- The internal invalidation command accepts only Published or Superseded revisions. It requires a separate server-side authority to verify `demand.plans.invalidate`, revision-bound Tenant × LegalEntity × selected-series scope, and material MRP impact behind an evidence reference. The production adapter is unconfigured and fails closed; tests inject explicit fixtures. A forecast deviation alone is rejected.
- A snapshot transaction changes only lifecycle state/version in the Draft and Published manifests. It also changes the current baseline slot to unavailable when applicable and writes a mandatory publication audit record, general audit entry and one outbox intent. Published rows, part identities, count and checksum are unchanged. An identical request key/content replays without another effect; stale or changed requests conflict.
- Invalidating the current Published revision leaves its slot historically pointing to the invalidated revision but unavailable. No older Superseded revision is restored. A later Approved revision must use the ordinary full publication path. Superseded invalidation leaves the actual current slot untouched.
- Normal status/manifest/page reads reject Invalidated. A separate audit-only reader requires its own independent revision-bound authorization and returns a nonempty “planlama için kullanılamaz” warning. Neither reader is exposed by a public API in this slice.

## Verification

| Check | Result |
|---|---|
| Starting full suite, before this WP | 238 passed, 0 skipped (prior verified source state) |
| Focused invalidation/history tests on owned real Mongo | 11 passed, 0 failed, 0 skipped; exit 0 |
| Full MOD-0188 suite on owned real Mongo after restoring sensitivity change | 249 passed, 0 failed, 0 skipped; exit 0 |
| API build | 0 warnings, 0 errors; exit 0 |
| Material-impact sensitivity | Temporarily removing the independently verified material-impact guard made `Invalidate_RequiresIndependentMaterialEvidenceAndPermission` fail: expected `MaterialImpactMissing`, actual `Invalidated`; exit 1. The guard was restored. |

The focused tests cover current and Superseded invalidation, unchanged checksum/content, current-slot removal and no fallback, subsequent normal publish, permissions/scope/source failure, forecast-deviation rejection, audit-only warning, replay/conflicting replay, stale state version, wrong tenant/company, concurrent duplicate requests, racing publish, and audit/outbox failure rollback. They use the explicit `mod0188_tests` database, fixture authority and unique tenant IDs. No live Platform/MDM/MRP impact source is claimed.

Test command: `dotnet test services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj -c Debug -m:1 /nr:false /p:UseSharedCompilation=false /p:DirectoryBuildPropsPath=<existing isolated props> --no-restore` with `MOD0188_TEST_MONGO_URI=mongodb://127.0.0.1:31994/?replicaSet=rs-mod0188`. SDK: user-local .NET 8. API build uses the same flags and props on `services/Diten.PlanningService/src/Diten.PlanningService.Api/Diten.PlanningService.Api.csproj`.

The temporary Mongo was started by this WP on loopback port 31994 with PID 11204 and a unique repository-external `mod0188-invalidation-*` directory. After the tests, only that verified PID was stopped. Final checks showed PID absent, port closed and its temporary directory absent. Operational port 27017 and other Mongo processes were untouched.

## Remaining gates

The authoritative production invalidation permission/data-scope and material-MRP-impact evidence source, public Demand v2 review/freeze, event delivery, MOD-0189 handling, and live integration tests remain open. AC-D20–D22 are not marked complete. K13 and Ali E4 are separate decisions.
