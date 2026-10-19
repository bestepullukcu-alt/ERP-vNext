# MOD-0188 internal authoritative revision status — 2026-10-07

Scope: WP-MOD0188-AUTHORITATIVE-STATUS. This is an internal status-only producer read, not a public Demand v2 API, content-read permission, live MOD-0189 integration or AC-D23/D27 acceptance.

## Security and consistency

- The reader checks an independently resolved `demand.plans.consume` permission and revision-bound Tenant, LegalEntity and full selected-series scope **before any Mongo read**. The production authority is unconfigured and fails closed. Tests inject a revision-bound fixture; no client cursor, manifest or flag supplies authority.
- A single Mongo snapshot read compares Published and manual manifests, review and publication/invalidation audit sequence and fingerprints, and the current baseline slot. State versions must increase without gaps. Disagreement returns `Inconsistent` with no status; Mongo outage returns `StoreUnavailable` and authority outage returns `AuthorityUnavailable`. Outbox retention is not specified, so an outbox intent is not used as lasting status authority.
- Authorized `Published`, `Superseded` and `Invalidated` return lifecycle metadata only. A physically corrupted week part does not hide an already durable, verified `Invalidated` state. This never grants access to its content; the normal snapshot status/manifest/page reader retains its original full-integrity and state restrictions.
- An out-of-scope actor receives the same empty `NotFound` result for healthy, Invalidated and inconsistent records. Missing consume permission is a separate denial. Status contains no forecast rows, quantities or checksum.

## Verification

| Check | Result |
|---|---|
| Previous full suite before this WP | 249 passed, 0 skipped (prior verified run) |
| Focused real-Mongo authoritative-status tests | 8 passed, 0 failed, 0 skipped; exit 0 |
| Final full MOD-0188 suite on owned real Mongo | 257 passed, 0 failed, 0 skipped; exit 0 |
| API build | 0 warnings, 0 errors; exit 0 |
| Scope sensitivity | Temporarily removing the revision-bound scope guard made `AuthoritativeStatus_OutOfScopeRecordsHideAllStatesAndCorruption` fail: expected `NotFound`, actual `Found`; exit 1. The guard was restored before the full green run. |

The focused cases cover all three states and their increasing versions, Invalidated with corrupt content, missing/mismatched audit and state version, status after outbox removal, same-tenant scope denial that hides corruption, permission and authority outage, wrong tenant/company, and concurrent invalidation observation. They use the explicit `mod0188_tests` database and unique Tenant IDs. No live scope or MRP binding source is claimed.

Command: `dotnet test services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj -c Debug -m:1 /nr:false /p:UseSharedCompilation=false /p:DirectoryBuildPropsPath=<existing isolated props> --no-restore` with explicit `MOD0188_TEST_MONGO_URI=mongodb://127.0.0.1:31994/?replicaSet=rs-mod0188`. The API build used the same props and the user-local .NET 8 SDK.

This WP used repository-external, loopback-only temporary Mongo replica sets on port 31994. The first verification used PID 17968. After the outbox-retention correction, the final full run used PID 35272. Each process and its unique directory were separately cleaned. Final checks found both owned PIDs absent, port closed and both temporary directories absent. Port 27017 and other Mongo processes were untouched.

## Open gates

The authoritative live permission/revision-scope source, central Demand v2 review/freeze, public API, MOD-0189 integration and live acceptance remain open. AC-D23/D27 are not marked complete; CT K13 and Ali E4 remain separate.
