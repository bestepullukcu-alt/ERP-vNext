# MOD-0188 internal snapshot read evidence — 2026-10-07

Scope: WP-MOD0188-SNAPSHOT-READ, internal Published manual revision reads only. This is not a public Demand v2 contract, MRP read, live authorization acceptance or AC-D23/D24 closure.

## Technical read and cursor rule

- Status, manifest and each page re-read the full manifest and all selected physical parts in one Mongo transaction with snapshot read concern. The existing canonical checksum, expected part/row counts, unique series/week identities, scope, and reconstructed Draft lifecycle/audit are verified before any result is returned. A failed check returns no page.
- Normal planning reads return content only for `Published`. `Superseded` needs approved bound-MRP-run evidence that does not yet exist; `Invalidated` is closed to normal planning reads. No historical audit read is implemented here.
- Rows are ordered by SKU GUID `D` text (ordinal), Warehouse ID (ordinal), then week number. Each row explicitly carries `Known`/`Missing`/`Unknown` kind and nullable quantity. Current manual publication permits only complete `Known` rows; an actual quantity of zero remains `Known(0)`. Missing rows never become zero.
- The internal cursor is base64url JSON payload plus HMAC-SHA256 signature. It binds Tenant, LegalEntity, revision, checksum, content/state versions, page size, next row index, previous row key and a 30-minute expiry. The signing key is supplied by `DemandPlanning:SnapshotCursorSigningKey` and must be at least 32 UTF-8 bytes. There is no built-in production key. A missing key closes page reads. This format is an internal technical choice and is not the central v2 contract.
- Each read first requires server-side, revision-bound actor permission and Tenant × LegalEntity × selected SKU × Warehouse scope evidence from an authority independent of the requested manifest. Missing Demand read permission is distinct from missing scope. The production adapter is unconfigured and returns unavailable; tests supply explicit revision-bound fixture authority. No client flag, cursor or manifest declaration grants access.

## CT security finding correction

Previously the reader validated the snapshot and state before checking actor scope. A scope-excluded actor could distinguish healthy, Superseded and corrupt records. The reader now resolves actor permission and revision-bound scope **before any Mongo read**. Scope exclusion returns the same empty `NotFound` result on status, manifest and page. Only after this check does it load and validate the complete snapshot. An authorized actor still receives `InvalidSnapshot` for corrupt content and `StateDenied` for a normal read of a Superseded revision. The production authority remains fail-closed because an independent live revision-to-series scope source has not been approved.

## Verification

| Check | Result |
|---|---|
| Focused real-Mongo reader tests | 10 passed, 0 failed, 0 skipped |
| Full MOD-0188 test suite with owned loopback `rs-mod0188` after CT correction | 238 passed, 0 failed, 0 skipped; exit 0 |
| API build | 0 warnings, 0 errors; exit 0 |
| Cursor signature sensitivity | Temporarily bypassing signature comparison made `Read_TamperedAndExpiredCursorFailClosed` fail: expected `InvalidCursor`, actual `Found`, exit 1. The comparison was restored and the full 236-test suite passed. |
| CT scope-guard sensitivity | Temporarily bypassing the revision-scope guard made `Read_OutOfScopeHealthySupersededAndCorrupt_AllHideExistence` fail: expected `NotFound`, actual `Found`, exit 1. The guard was restored; the final full 238-test suite passed with 0 skipped and API build 0 warnings/0 errors. |

Test command: `dotnet test services/Diten.PlanningService/tests/Diten.PlanningService.Cycles.Tests/Diten.PlanningService.Cycles.Tests.csproj -c Debug -m:1 /nr:false /p:UseSharedCompilation=false /p:DirectoryBuildPropsPath=<existing isolated props> --no-restore`. API build uses the same flags on `services/Diten.PlanningService/src/Diten.PlanningService.Api/Diten.PlanningService.Api.csproj`. SDK: user-local .NET 8 at `C:\Users\epenc\AppData\Local\DitenSdk\dotnet8\dotnet.exe`. Mongo URI was explicitly `mongodb://127.0.0.1:31994/?replicaSet=rs-mod0188`; database name `mod0188_tests`, with a unique tenant per fixture. No operational port was used.

Focused cases cover page continuity and ordering, actual zero, manifest/status metadata, cross-scope/revision/page-size cursor rejection, HMAC tamper and expiry, state denial, per-read permission/scope/source checks, duplicate/missing week, missing/unexpected/corrupt part and checksum. The Invalidated test deliberately alters persisted state because the invalidation workflow itself is outside this WP; it proves normal reads do not return content, not end-to-end invalidation.

The two CT correction cases exercise all three read surfaces against the same-tenant out-of-scope actor for healthy, Superseded and corrupt revisions, and separately check an authorized actor, missing Demand read permission and unavailable authority. The fixture authority is supplied with the draft's revision-bound scope independently of the persisted Published manifest.

## Open gates

AC-D23/D24 remain open. Public v2 endpoints and central contract approval, approved MRP Superseded binding evidence, live Platform/MDM authority and end-to-end consumer tests are not part of this slice. Current published content covers manually entered complete weeks only. The temporary Mongo process and folder are removed after verification; cleanup measurements are reported in the task response.
