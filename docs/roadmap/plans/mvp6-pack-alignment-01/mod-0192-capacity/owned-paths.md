# MOD-0192 Capacity Planning — owned paths, protected paths and Program.cs slot

**PROPOSAL — NOT APPROVED. No source-write grant.** This file restates the already owner-approved allowlist and binds each path to the byte identity CT accepted. It does not widen, narrow or reinterpret the allowlist.

Accepted source manifest: `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/SOURCE-MANIFEST.tsv`, 422 entries, SHA-256 `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634` (BC successor, CT BOUNDED ACCEPTED). Allowlist source: `docs/roadmap/plans/mvp6-mod0190-0192-dispatch-preflight-01/MOD-0192-OWNED.tsv` (SHA-256 `88f2327d81bf2e456cfdb1109145f9b1eeac81b49413ecf5b6244466b6c247a7`); 43 unique paths, 0 overlap with MOD-0190's 38. Paths present in the common checkout today: **0 of 43** (the accepted bytes live only in the sealed isolated archive; common-checkout uptake is integration queue item Q14/Q15, not this package).

## Owned paths (exact, no wildcard)

| # | Path | Accepted SHA-256 |
|---:|---|---|
| 1 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/CapacityPlans/CapacityPlansController.cs` | `c710d3610d3b5af4f43d3b2e303be73520e3c98e735d616f1178252600d04b7b` |
| 2 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/CapacityPlans/CapacityContextMiddleware.cs` | `3c7bb5e3fc8301f987695ed0defea60793d4b486da623244f1b7c0719e0bbe8e` |
| 3 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/CapacityPlans/CapacityContractError.cs` | `c55ad1616576bf73c062e16a8e745bd15e652c9ff4cf1ef575fc59b123c9cd7e` |
| 4 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Commands/CreateCapacityPlanCommand.cs` | `3394bf7cec6746cb83411c7749347920819eb4938e3bdaad0c7b7ee9345ef31d` |
| 5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Commands/CreateCapacityScenarioCommand.cs` | `247252e6d43f96c4ce86a1faa4c60d3421c0a5661141f7964e2ca1967a772816` |
| 6 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Commands/EvaluateCapacityScenarioCommand.cs` | `1f9cd9a115f0b3d6ff4b3b7766de02a5b140d763d3f373d826232c4862612018` |
| 7 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Queries/GetCapacityPlanQuery.cs` | `40ad6030105249068d5f3ff8a98538b36098cdc921ca90d010ed3608e9e41dc4` |
| 8 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Queries/GetCapacityScenarioQuery.cs` | `04c32861cdeb1c2b65610bd12067b28ef8a99e4d870ecf168ada71fc49f8e92d` |
| 9 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Queries/GetCapacityEvaluationQuery.cs` | `a32d4fc3a6cb919df2dc4a6feff7d9a421e655f1b6ee7559a8866c0dadbf3794` |
| 10 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/CommandHandlers/CreateCapacityPlanHandler.cs` | `0bda2641dad54d874453387572daf527f558408ac5f4a2d5e91c51c201e2432a` |
| 11 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/CommandHandlers/CreateCapacityScenarioHandler.cs` | `381465d79d0884c7a3df77aa983adf0813065d48aabdcb17c1c6d1f68272b600` |
| 12 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/CommandHandlers/EvaluateCapacityScenarioHandler.cs` | `059bbf40a85f0f40f586fd9d290c37bb530d7781be377cd4b4ea089e0ccbc369` |
| 13 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/QueryHandlers/GetCapacityPlanHandler.cs` | `4e64d826124e86621efcd0773e967197eea6d283e6c705088f4c4c5d9cfef699` |
| 14 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/QueryHandlers/GetCapacityScenarioHandler.cs` | `f51f66afd22fa76005b56860193f2b6b8dd3c7b1e87eb3e923e8d45158586de4` |
| 15 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Handlers/QueryHandlers/GetCapacityEvaluationHandler.cs` | `867a5f3aafcc7d4d42d2517c3226c022c75d9394f40c9c79edc59618cfe39bc3` |
| 16 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Validators/CreateCapacityPlanValidator.cs` | `c2cdc9852b490705439a35941c6e9ad22455110cc2ee325bb955ae6114378fd6` |
| 17 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Validators/CreateCapacityScenarioValidator.cs` | `1b88af4911180465e479ec045f6225dc041557b030f9f4f33a24574fecf26bb8` |
| 18 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/Validators/EvaluateCapacityScenarioValidator.cs` | `3f6f6b7fa24cfe3b3b327de7d7e512d3e4d92bbfbebfeb5e71c0df04c00732b3` |
| 19 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/CapacityPlanModels.cs` | `e66712cc613f2f6b32c2085530ad1b6407fd70af71bb2df83ce6b74c734c0984` |
| 20 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/CapacityRequestFingerprint.cs` | `2c722f4d9bd6f56df196281de62a2fd44165c61479b3b1d4aa35917bdd7c818f` |
| 21 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/CapacityPlans/CapacityProjection.cs` | `e689658fb75e1fafd8c8b063476128664ef6c7988a7daaad94b67b3afe0af7ba` |
| 22 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/CapacityPlans/CapacityPlan.cs` | `6a1808eafa4538f7c911045eaf2ece6b607df02ff4d1455e51ee6e32e5608c92` |
| 23 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/CapacityPlans/CapacityScenario.cs` | `a2b5acea18c965abcbba1be6c0d38f62eaeedab04cdf989ad4a40460acc56ae5` |
| 24 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/CapacityPlans/CapacityEvaluation.cs` | `4020898d74e66ad7b9ab4bec77e1189528595cb0983c0cf2389a1e0161270c85` |
| 25 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/CapacityPlans/CapacityScope.cs` | `5d511cfaeb38d82b10c8e3de71e8c46eb58a401c63962c130d082d0eea4c0033` |
| 26 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/CapacityPlans/ICapacityRepository.cs` | `c0362b0f92fa967887381e58abce0cfbba37a173892225b01baac3ab50d34b50` |
| 27 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityRepository.cs` | `6bf026d3bfaeddf37ff4e461efedfa977a07765a413d5f363fcbdc80edfe0364` |
| 28 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacitySchema.cs` | `ac704148cff99a8015ba3e8337ad7c05daf319d4f39eb18e571b09a7c0fc0ed7` |
| 29 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityPersistenceRegistration.cs` | `4df27804b709b32dea9a7f0717bd997d645015df2064d999db11939aa4d771d6` |
| 30 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/CapacityPlans/CapacityLeaseStore.cs` | `d6f932b5fcfcbadbb44b65ccac0cbccc1f1fb0e8c576b333c186a735a524902a` |
| 31 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/CapacityPermissionAttribute.cs` | `2cab8049bb96e231ac6aa7a41a8f54665652e570ffbe522e9a8eb3ce116f4881` |
| 32 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/CapacityPermissions.cs` | `7dd343d01865b40999444c7ceb83283a77963e7fe7db6c5f788018f1510a1c3b` |
| 33 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs` | `d49b359e03606026eea001fa1a40ce185c6934b1c0bee2c4cd10d02c6d938c22` |
| 34 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/ConstraintFixtureReader.cs` | `02be87604a2be8c9b425df000adb0ddde03bffd7377083d87d642a3c09d1802f` |
| 35 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/CapacityPlans/CapacityEvaluationExecutor.cs` | `c5bcc0490669b3c692980b106a6b036d606966a1b5dabd6c96635e425421d095` |
| 36 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityContractTests.cs` | `4d57bf51d9d299b02037db0494e3f339e3c1558c2edd59b7a0f04cd578fa52cf` |
| 37 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityLifecycleTests.cs` | `f63c37279fc1011ff60ac86bfb2080619c01f1ca68ff178e32abec74f1d8bfcc` |
| 38 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityReplayTests.cs` | `89f5fc5a0555de770f639f0d743badbfdfae9058fb258236adc2ca75f94d8cbd` |
| 39 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityConcurrencyTests.cs` | `45329002c8e54ae225081152104116e204e878e1df77267ba6be9c1e1422d514` |
| 40 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityAtomicityTests.cs` | `2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43` |
| 41 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityIsolationTests.cs` | `a6695bfa982286265ade5b7cdcba0d2797286d56d9aa2f665a46c3c14ec3f4bc` |
| 42 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityLeaseTests.cs` | `ae2f4932e89d3c38be369509dbe27838813c33176437316b5c9313dca1257071` |
| 43 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityRestartTests.cs` | `81f58dd4482c35d15a672d5f30ef7b85dfc565522ca78c07ae636c6d5bda8228` |

## Protected paths

Unchanged from pack §6 (the proposal adds only the v2/v3 annex line), plus the surfaces this alignment must not touch:

- `.antigravity/**`; `AGENTS.md`; DCP/registries/board.
- `docs/analysis/contracts/sandop-capacity.openapi.yaml`, `sandop-capacity-semantics-v2.0.0.md`, `sandop-capacity-semantics-v3.0.0.md`, `demand.openapi.yaml` and every other frozen contract.
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration agent only); shared permission catalog/seed; shared DI.
- Frontend Archive paths and `frontend/Diten.Web/Views/Shared/_Layout.cshtml`; MOD-0192 has `shell: none`, so no UI path is owned.
- Every MOD-0183..0190, MOD-0191 and MOD-0147/0148 path; domain-external services.
- All historical records, including every file cited in `SOP-22-PACK-DELTA.md`.

## Program.cs integration-owner slot

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is **not** a MOD-0192 owned path. It belongs to the single integration owner (process v1.0 §8).

| Identity | SHA-256 | Meaning |
|---|---|---|
| Baseline (accepted MOD-0190 composition) | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` | Preimage |
| Integration-owner patch | `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e` | Owner-authorised composition (hosted consolidate record) |
| Accepted composition | `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` | Preserved unchanged by the BC successor decision |
| Common checkout today | `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8` | Not the accepted composition; uptake is Q14/Q15 |

Slot rule: one integration-owner writer, one exact baseline→patch→target decision, applied only after pack promotion and the integration decision. Pack promotion does not grant it.
