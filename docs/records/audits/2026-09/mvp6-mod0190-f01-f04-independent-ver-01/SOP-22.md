# MVP6-MOD0190-F01-F04-INDEPENDENT-VER-01 — SOP §22

**Verdict: PARTIAL.** F01–F03 and the three application correlation rejections in F04 pass independently on native ASP.NET Core 8 and an isolated Mongo replica set. A parser-level 401 was observed, but an **application-generated** 401 could not be reached through the direct service without changing authorization behavior; that subcriterion stays **OPEN**. This is a bounded verification verdict, not CT acceptance.

## Scope and authority

- Branch `feature/mvp6-logistics`; HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Source and status baseline are in the raw archive. No source, pack, contract, guard or shared file was changed.
- Final successor source: `mvp6-mod0190-test-oracle-rework-01/source.tar.gz` SHA-256 `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745`; 379-entry `source-manifest.tsv` SHA-256 `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`. Independent extraction and rehash: 379/379 before and after execution. `Program.cs` SHA-256 `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5`.
- Relative to the earlier HTTP DEV manifest, exactly one of 379 paths differs: `SandopAtomicityTests.cs` (`55d2522b…` → `eba2da6f…`). The other 378 entries, including production code and Program.cs, are identical. Earlier HTTP evidence is therefore content-bound historical support, not a fresh rerun.
- Published SANDOP YAML SHA-256 `9543e3f295dcabb9f7c1ab464fadfacfcbc53ada2f9bec9d32deb8f52cb02ff3`; annex SHA-256 `eb1df1383e637c744179abe4c8faa3b3b7ebe4cccd19738aadf41896a9311bda`. Controlling rules: annex lines 9–15 and 23–28; prior CT [acceptance matrix](../mvp6-mod0190-ct-acceptance-review-01/ACCEPTANCE-MATRIX.tsv).

## Independent execution and provenance

The 379-path source was unpacked into `/private/tmp/mvp6-mod0190-f01-f04-ver-01/source`. Only NuGet restore metadata was copied from a previous disposable verifier workspace; no compiled output was copied. Command: `/Users/natig/.dotnet/dotnet build services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false`, exit 0, zero errors, five offline `NU1900` vulnerability-feed warnings. Runtime: Microsoft.AspNetCore.App and Microsoft.NETCore.App 8.0.23. Fresh DLL SHA-256 `afb5971962b3ce2a65e728937dd728a50ac76eec91deee877e53463dd77d590f` before/after probe. Process 51397 ran that exact DLL on port 57983. Test-owned Mongo replica set `rs190fver` was PRIMARY on port 57883; DB `DitenSupplyChain_Mod0190_Test`, never operational 27017. Source→build→DLL→process→HTTP/DB links are in the raw archive. Ephemeral JWT issuer/audience/key and bearer tokens were deliberately excluded from evidence.

The independently written `probe.py` used the real JWT middleware and sent request bodies with `http.client.endheaders(payload)`. `http.json` records method/path, safe headers, exact sent UTF-8 body, byte length and SHA-256, response status/headers/body. It logged 30 actual requests, including fixture setup; 12 were negative scenarios. Before and after each negative request, separate `mongosh` calls queried **all six** collections scoped by the same tenant and legal entity. All 24 queries exited 0; count and complete scoped documents were byte-equivalent per scenario. `validation.json` reports no missing evidence, request-length mismatch, query failure or state mismatch.

## F01–F04 acceptance matrix

| ID | Actual negative cases | Exact HTTP outcome | Sent bytes each | Scoped six-collection before/after | Verdict |
|---|---|---|---:|---|---|
| F01 | Capture against Approved, Rejected, Archived plans | 409 `SANDOP_PLAN_STATE_CONFLICT` ×3 | 144 | query exits 0/0; no count/document delta in `sandop_plans`, `sandop_snapshots`, `sandop_sign_offs`, `sandop_receipts`, `sandop_audit`, `sandop_outbox` | PASS |
| F02 | Sign-off against Draft, Approved, Rejected, Archived plans | 409 `SANDOP_SIGN_OFF_STATE_CONFLICT` ×4 | 92 | same six collections, exits 0/0 and no delta | PASS |
| F03 | Sign-off using snapshot belonging to a second same-scope plan | 422 `INVALID_SNAPSHOT_REFERENCE` | 92 | same six collections, exits 0/0 and no delta | PASS |
| F04a | Missing, malformed, duplicate `X-Correlation-Id` | 400 `INVALID_CORRELATION_ID` ×3; one valid generated fallback UUID per response, equal in header and error body, distinct from supplied values | 139 | same six collections, exits 0/0 and no delta | PASS |
| F04b | No bearer token on authenticated GET | 401 with `WWW-Authenticate: Bearer`; zero-byte request and response body, no correlation header | 0 | same six collections, exits 0/0 and no delta | Parser-level boundary observed; application-generated 401 **OPEN** |

The fixture setup created plans/snapshots through authenticated HTTP and used a scoped Mongo status update to reach terminal states. These setup writes are recorded separately and precede each negative before-snapshot. For F03 the wrong-plan snapshot is a real persisted snapshot, not an arbitrary unknown ID. `matrix.json`, `http.json`, all `state-*.json` and `mongo-*-query.json` carry the scenario-specific raw data and exit codes.

## Exact open finding

The published annex line 15 requires `X-Correlation-Id` and matching error-body correlation for an **application-generated** 401, but expressly distinguishes a parser-level authentication challenge. The direct-service controller has `[Authorize]` at `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/SandopPlans/SandopPlansController.cs:4`; `Program.cs:95,101` runs authentication and authorization before controller actions. The app 401 branch in `SandopContextMiddleware.cs:13` is consequently not reached by the unauthenticated HTTP probe. Its observed 401 is the parser-level challenge allowed by the annex, and cannot prove the separate application-generated branch. No auth bypass or source change was made. CT should keep that subcriterion open or record a narrowly authorized way to make an application-generated 401 reachable, if required for this bounded acceptance.

## Preservation and cleanup

The initial dirty inventory contained 225 rows and the final inventory 227. The only two additions were other lanes' `mvp6-capacity-duplicate-final-release-prep-01/` and `mvp6-mod0192-hosted-evidence-01/`; neither was touched or counted as this lane's work. `git diff --check` was clean. The extracted 379 sources still match the manifest. Owned API and Mongo processes were stopped; ports 57983 and 57883 have no listeners. Mongo shutdown's client exit 1 reflects the expected server-side connection close, with no remaining process. No commit, push or stash.

**Raw evidence:** [raw-evidence.tar.gz](raw-evidence.tar.gz), SHA-256 `33270f8bedc63e456320e89195de6eb28dd1e55e82dd9a9eaff4072a07523a77` (74 safe files; excludes private JWT configuration, bearer tokens, API logs and database files). This lane ran only F01–F04; no broad suite rerun or waiver is claimed.
