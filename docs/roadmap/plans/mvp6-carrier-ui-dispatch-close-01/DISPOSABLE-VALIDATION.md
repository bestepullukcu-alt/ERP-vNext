# Disposable validation

- Candidate preparation date: 2026-09-23.
- BC source root: `/private/tmp/mvp6-bc-integration-successor-01-agvc8muo/source`; its 422-row manifest was independently rechecked before candidate preparation.
- `MODULE-REGISTRATION.patch` applied cleanly to a fresh copy of that source. `dotnet build` of `Diten.SupplyChainService.Tests.csproj` with .NET major roll-forward and no restore completed with exit 0, 0 warnings, and 0 errors.
- `GATEWAY.patch` and `NAVIGATION-L10N.patch` applied cleanly to fresh copies of their pinned current-checkout preimages. Resulting `ocelot.json` parsed as JSON and all seven changed RESX files parsed as XML.
- All 18 baseline/target rows in `candidates/BASELINE-PATCH-TARGET.tsv` were hash checked against their respective disposable trees.
- A disposable Gateway test build reached project compilation but could not resolve the already-declared `Microsoft.AspNetCore.TestHost` reference from the copied/offline assets; it also recorded NU1900 because nuget.org was unavailable. No Gateway focused test PASS is claimed. The generated route coverage test remains an independent-VER execution requirement.
- No repository runtime, pack, Gateway, frontend shared resource, or BC source file was modified by these checks.
