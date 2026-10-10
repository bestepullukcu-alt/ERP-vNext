# NOT VERIFIED — Q77a draft overlay (MOD-0185 producer uptake + F-1)

**DRAFT — not built, not tested, not run.** This lane has no .NET, MongoDB or runtime. Everything below is for Q77b on the local Mac.

| # | Not verified here | Where it is verified |
|---|---|---|
| NV-01 | `dotnet build` of the SupplyChain solution with the overlay (0 errors, warnings compared with the preimage build) | Q77b, isolated env (ISOLATED-ENV.md) |
| NV-02 | Unit tests `LoadRootStorageTests` (no Mongo needed) | Q77b |
| NV-03 | Integration tests `LoadRootQueryTests`, `LoadRootTransitionTests`, changed `LoadContractTests` on the isolated replica set (`MOD0185_TEST_MONGO`, lane port, never 27017) | Q77b |
| NV-04 | Regression: `LoadReplayTests`, `LoadLifecycleTests`, `LoadAtomicityTests`, `LoadConcurrencyTests`, `LoadIsolationTests`, `LoadReferenceTests`; then the full SupplyChain test project with TRX; pre-existing failures listed by name | Q77b |
| NV-05 | Runtime: `runtime_probe.py` (patched) against the built API, then `verify_evidence.py` (repinned) on the real evidence | Q77b (E4) |
| NV-06 | Restart: `restart_probe.py` regression and a list check of the field after restart (restart_probe.py does not assert the field — FN-05) | Q77b (E4) |
| NV-07 | Independent VER of LU-01…LU-14 and F1-01…F1-04 on the frozen source | separate Mac session (LU-15) |

## Check these first on the Mac (compile-sensitive points a syntax parse cannot see)

1. `LoadRootStorageTests` deserializes `LoadPlan` with `BsonSerializer` without the app's serializer registration (same as `ShipmentRootStorageTests`): string GUIDs, string enum `Status`, BSON date `CreatedAt`, record `LoadStop` from a sub-document. If the default serializers reject any of these in isolation, seed the matching BSON types; do not change product code.
2. `new BsonBinaryData(Guid, GuidRepresentation.Standard)` with MongoDB.Driver 2.27.0.
3. `Assert.Equal(ids.Values.Order(), items.Keys.Order())` overload resolution (xUnit 2.9.2).
4. Nil `X-Correlation-Id` accepted at create (`LoadContextMiddleware` applies no non-nil rule to the header; annex line 178) — used by `StoredNilRootIsAPresentRootAndMatchesNilInboundCorrelation`.
5. Shape of the 403 body for a missing read permission (the test only asserts that it holds no `lifecycleCorrelationId` and not the create root).
6. `LoadRepository`: `raw.Select(LoadRootMaterializer.Materialize).ToArray()` converts to `IReadOnlyList<LoadReadResult>`; `read.LifecycleCorrelationId.Value` after the state check.
