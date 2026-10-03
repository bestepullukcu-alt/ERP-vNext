# MOD-0190 S&OP Workflow & Sign-offs — owned paths, protected paths and Program.cs slot

**PROPOSAL — NOT APPROVED. No source-write grant.** This file restates the already owner-approved allowlist and binds each path to the byte identity CT accepted. It does not widen, narrow or reinterpret the allowlist.

Accepted source manifest: `docs/records/audits/2026-09/mvp6-mod0190-test-oracle-rework-01/source-manifest.tsv`, 379 entries, SHA-256 `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`. Allowlist source: `docs/roadmap/plans/mvp6-mod0190-pack-phase15-close-01/OWNED-PATHS.tsv` (SHA-256 `4bfc17f4811073553f1da773748cbc3733cd554671078188d122214ee3947982`); 38 unique paths, 0 overlap with MOD-0192's 43. The same 38 paths are byte-identical in the later 422-entry BC successor manifest `dec28b6a…`. Paths present in the common checkout today: **0 of 38** (the accepted bytes live only in the sealed isolated archive; common-checkout uptake is integration queue item Q14/Q15, not this package).

## Owned paths (exact, no wildcard)

| # | Path | Accepted SHA-256 |
|---:|---|---|
| 1 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/SandopPlans/SandopPlansController.cs` | `e5d439356149b744ec1757b1d5c1fceb0b159d98b94c3ea333d65923bb14a0d5` |
| 2 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/SandopPlans/SandopContextMiddleware.cs` | `2ca4e8ceb6ac3b1fe9c4b35d482980666dd7925587501ef91fb8eb9d7daba65a` |
| 3 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/SandopPlans/SandopContractError.cs` | `16e408e5d2c8a6cf60289cb0a487c16f92fd45ec84c1ba0628606e50319cf296` |
| 4 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Commands/CreateSandopPlanCommand.cs` | `6435c0be18187aeb79cf61f08d26511f2cf29bf0da82414c938d344b97b6d148` |
| 5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Commands/CaptureSandopSnapshotCommand.cs` | `d5c2ac28032ea37fed5d02da0f37202f303c01cce2bfa8390df2ad9995ae6fc5` |
| 6 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Commands/RecordSandopSignOffCommand.cs` | `b019258453a73c60a27bcde951fdb2da501ebb2633a9be6a97fba80e26a025a0` |
| 7 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Queries/GetSandopPlanQuery.cs` | `4577389dd2aaf1110cab5d9e272d33c4b2e3a4291f9175ab1d6ee2cd5c7579f6` |
| 8 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Queries/ListSandopSnapshotsQuery.cs` | `9a659ce176bbcaeb48f76d324c5367ddbbe61aab54437dcc0ba5fa985aec0969` |
| 9 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Queries/ListSandopSignOffsQuery.cs` | `1f0eb5abdf27a26353f410458d9d752c9ea9b82713f3f8b3b470a8117e86b4d7` |
| 10 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/CommandHandlers/CreateSandopPlanHandler.cs` | `4161b36380edcf44e465aabc240e3022a7a16571b2f117ce5c3d5647e869094e` |
| 11 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/CommandHandlers/CaptureSandopSnapshotHandler.cs` | `2885c8452f0c3bdc6c61fd99941057bf81daa164747ac03d8ad28205085bbaa1` |
| 12 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/CommandHandlers/RecordSandopSignOffHandler.cs` | `c05324dbf1601777ba86b5f29ab8ef0f7719ea92d155160d41198c07855580c8` |
| 13 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/QueryHandlers/GetSandopPlanHandler.cs` | `3ef1918bc789c4c65b61bd14b3f371cacaafde2284b66208ff9c3766b5192aee` |
| 14 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/QueryHandlers/ListSandopSnapshotsHandler.cs` | `104684f395cfad31d38e6d34b98cd4a72a83f453ee19a131498fd55bf4676302` |
| 15 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Handlers/QueryHandlers/ListSandopSignOffsHandler.cs` | `faef398b54262bb5788277678f4c6c81272629f76640aed2c419d529bed1dde6` |
| 16 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Validators/CreateSandopPlanValidator.cs` | `6da8f45780a4997be4fff3f52c6c1ae26577998378fd02e84b35ef9595604f31` |
| 17 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Validators/CaptureSandopSnapshotValidator.cs` | `5dea0f43048af13396cd1fc8ca45c758cd06557a254505302449c6393fbd98cb` |
| 18 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/Validators/RecordSandopSignOffValidator.cs` | `fb3cedeb45a8438849d314e921977912b7abeba2b002fe945fb1ac7f1c831150` |
| 19 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/SandopPlanModels.cs` | `60e2a3e3b13da470e2acbb274ff758d27d8bedc89654e42fb21347f405e962c5` |
| 20 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/SandopRequestFingerprint.cs` | `771e9dc51567371ca5a949e9b04d36b127787780ca98cb01d6590a4a581903f6` |
| 21 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/SandopPlans/SandopProjection.cs` | `e18450c4c10d7245e2581f031348c5c631a405767e7894d9d53960300f7ac363` |
| 22 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/SandopPlan.cs` | `7312ac113ef0c92679002dc1bd1b71226a249b66e7dd7a91d511936675a8f99d` |
| 23 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/SandopSnapshot.cs` | `606ab9e7a0cbb19e548707281935d0ee7adf0c8e922fbec7c18f10f8c22a4f86` |
| 24 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/SandopSignOff.cs` | `01a0b736213529a3523be36e7598b9d5460a84286f1a14aaa8009ce3b430788c` |
| 25 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/SandopScope.cs` | `40bbc658ba727f7b5e767c94d3a890e69c1d853d8f3de26a389f56929dd2b70b` |
| 26 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/SandopPlans/ISandopRepository.cs` | `3d355402d0c51fefc3d1daf20550a342ebad7525d22a408cc59276cc50ce5f0c` |
| 27 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/SandopPlans/SandopRepository.cs` | `610c864a149fe756616c27f59ff6748c0ef6f4b38b3a122c0ea533edadc25676` |
| 28 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/SandopPlans/SandopSchema.cs` | `49f87409f29534c835a2aed82e349a22ba383bf5ef9ae4c4a3c17cbc0ae90bad` |
| 29 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs` | `0e74fec0687634d94c61e7c2965f495545b716f446f42fc6c45f1e1bf590961a` |
| 30 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/SandopPlans/SandopPermissionAttribute.cs` | `d9eabf418fdded5d554444310dc27d21cddb14a767c362dcf1773bb66280626f` |
| 31 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/SandopPlans/SandopPermissions.cs` | `47eb6b515e51cc00b3db6689c8f7a9418f218587caf01440a86b96f08c10e0a8` |
| 32 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/SandopPlans/DemandFixtureReader.cs` | `ffb719c08d2373a7ad0751b31e535a9cef978b8e490a6b712011e10c902d8fcc` |
| 33 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopContractTests.cs` | `00cdd63fafaa5502221816209bf6f3a4dc4d872dff2dc100ef46ab4649b71ea2` |
| 34 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopLifecycleTests.cs` | `ea19c6f973257730f001ed0215c0eb8fe5356797dc68e452d619e78a640df041` |
| 35 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopReplayTests.cs` | `76e08631282416a9ceba8f8e67d91b6da0ddfcea4d19e3498f87ad3d0470849a` |
| 36 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopConcurrencyTests.cs` | `b002b30f37249ae64ea3e3cabef2cf2e4ba42688113d3571639c3d495ec9e24e` |
| 37 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopAtomicityTests.cs` | `eba2da6f47a4935b61857a95534b3d1697635910190e3838494d96d956af7f43` |
| 38 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopIsolationTests.cs` | `a3e617663609087e553d8564400cbe9458a52b91cfc5f392b7a3a3e83ff6d47b` |

## Protected paths

Unchanged from pack §6, plus the surfaces this alignment must not touch:

- `.antigravity/**`; `AGENTS.md`; DCP/registries/board.
- `docs/analysis/contracts/sandop-capacity.openapi.yaml`, `sandop-capacity-semantics-v2.0.0.md`, `sandop-capacity-semantics-v3.0.0.md`, `demand.openapi.yaml` and every other frozen contract.
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration agent only); shared permission catalog/seed; shared DI.
- Frontend Archive paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`; MOD-0190 has `shell: none`, so no UI path is owned.
- Every MOD-0183..0187, MOD-0192 and MOD-0147/0148 path; domain-external services.
- All historical records, including every file cited in `SOP-22-PACK-DELTA.md`.

## Program.cs integration-owner slot

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is **not** a MOD-0190 owned path. It belongs to the single integration owner (process v1.0 §8: Auth, Gateway, navigation, localization and `Program.cs` changes go through one integration owner, in sequence).

| Identity | SHA-256 | Meaning |
|---|---|---|
| Accepted MOD-0190 composition | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` | The composition CT accepted with the 379-entry source |
| Later accepted MOD-0190+0192 composition | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` | Integration-owner patch `78cc0fa6…` on top of `a2a216be…`; part of the Capacity BC successor |
| Common checkout today | `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8` | Neither accepted composition; uptake is Q14/Q15 |

Slot rule: one integration-owner writer, one exact baseline→patch→target decision, applied only after this pack promotion and the integration decision. Pack promotion does not grant it.
