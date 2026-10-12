# Published Loads binding verification — scoped PASS / architecture BLOCKED

Independent actual published repository snapshot copied byte-identically, tracked+untracked git-visible inputs (baseline.json). No temporary candidate test harness included; no actual repository writes. New restore/rebuild succeeded, NuGetAudit=false for offline focused build; this is not a vulnerability audit.

35 original fixtures PASS. Actual NoCodeFilePointsIntoDocsOutsideTheFiveFolders production entry PASS (1/1), no synthetic opt-in. Full architecture measured 50 PASS / 3 FAIL / 0 skipped, total53. Results and exact failed names in results-summary.json; independent TRX under results/.

Three failures: NoTestCreatesItsOwnDatabasePerRun (Platform PpmAuditRetentionPolicySeedMongoTests.cs and DisposableStandaloneMongo.cs); NoProductionValidatorWritesItsOwnClockSkew and EveryLifetimeValidatingFileDeclaresTheSharedSkew (HumanCapitalService/TalentEcosystemService Program.cs). No new waiver produced.

New real owner decision SHA170ea3c5e47af103aeeceda471606d60fc31e69bff233279cb9ecd4cd6c805e7 and activated authority SHA5d4284eb216a7a4f29f27bb63cacd8d40788abc42da2be207299ebda68adf40d verified before tests. Production guard recomputes exact payload and links decision, verifying approved payload9bc2cb3737d0e8d3330fdc9945d3d268b64ab582ef49dee11cee524811d3846a. All17sealed files/provenance and Carrier annex hash verified. No existing captured source changed during verification (preservation.json); parent owns new publication audit outputs and final repository inventory.

Commands: dotnet test <snapshot>/tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj --filter FullyQualifiedName~DocsPathGuardTests.AuthorityFixture --logger trx;LogFileName=fixtures35.trx --results-directory <results> -p:NuGetAudit=false -p:UseSharedCompilation=false; subsequent --no-build --no-restore normal production guard filter, then no filter full suite. Each invocation logged (fixtures.log,guard.log,architecture.log), VSTest local socket authorized for disposable process.

Verdict scoped published binding/guard PASS E2. Full repository architecture BLOCKED with three pre-existing external failures. No runtime, DB/migration, E5/G5, pack promotion or DEV GO assertion.
