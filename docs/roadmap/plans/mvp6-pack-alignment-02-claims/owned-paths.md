# MOD-0187 Claims Management — owned paths, protected paths and Program.cs slot

**PROPOSAL — NOT APPROVED. No source-write grant.** This file restates the owner-approved 47-path allowlist and binds each path to the byte identity CT accepted. It does not widen, narrow or reinterpret the allowlist.

- Allowlist: `docs/roadmap/plans/mod-0187-runtime-dispatch-01/owned-paths.txt`, SHA-256 `eebaf0ac75206a664c168c072411224dc22e4f9b295cf70ad79031eb72e99517` (byte-identical to `mod-0187-final-pack-delta-01/prospective-owned-paths.txt`). 47 unique paths; no `ClaimOutboxWorker.cs`; no `Program.cs`.
- Accepted source: `docs/records/audits/2026-09/mvp6-mod0187-normal-baseline-integration-01/combined-source-manifest.tsv`, 341 entries, SHA-256 `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80` (package SHA256SUMS 8/8 OK).
- 47/47 found in that manifest. Present in the common checkout today: **0 of 47**. Source uptake is the integration queue (Q14/Q15), not pack promotion.

## Owned paths (exact, no wildcard)

| # | Path | Accepted SHA-256 |
|---:|---|---|
| 1 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/Claim.cs` | `44011bc61cd7cf9785e3caad54709d6261de2f61d73ab9f7a468355a7aa80150` |
| 2 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimStatus.cs` | `fcb5c62634afb387a76c74d26328b5268329f1c7d99695a1bdefe5cedaee4eb9` |
| 3 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimScope.cs` | `1cbfd707348766dfaeeb8c1ac58d9212f314075982665f1f2834100f3484de49` |
| 4 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimLifecycle.cs` | `59f6efd43a697c248f5035212a9f448c6639c2163773c09378308e7c6a8642ca` |
| 5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimAuditEntry.cs` | `2926601eaf33f2b2767b94ef9d2b740794b52eddd4cc0e2b2ad974b3724bb8a3` |
| 6 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimMutationResult.cs` | `e090934c14cab18e50b9e2e37dfc9dd83134c8214b403b15cda4c5436cd1e6c0` |
| 7 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/IClaimRepository.cs` | `610ca7678a9c29025b355d78c631c4fcc5a5845b0c177cb8f962d2708bade87b` |
| 8 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ExactClaimAmount.cs` | `52e72bea4d19cf578a8ff93818cb9e0f3743da8cec8ddd43323807bbb96c1701` |
| 9 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Domain/Features/Claims/ClaimReferenceSnapshot.cs` | `98ebe4ad25aa6f550dec19f07dd243baccc4fc91393d227d32ecd2a5b3c820d0` |
| 10 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Commands/CreateClaimCommand.cs` | `e921a2551a3c3425f39ae3976858808f8d0e5e180d1120513dbb8ba96501c7c3` |
| 11 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Commands/TransitionClaimCommand.cs` | `c02fc6daf0de997ea4de325b2d738eca992c777861e7468a3755c7e9e08dedcd` |
| 12 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Queries/GetClaimListQuery.cs` | `fcb25ca1fbd670d7dcbd8f0c0efcdb3667ce50c40cf81e0d4504b0b9f06326f0` |
| 13 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Handlers/CommandHandlers/CreateClaimHandler.cs` | `6244605b66e7eac2287a68eed1ba66832973e8b221131022ad0eb67f3053a0e4` |
| 14 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Handlers/CommandHandlers/TransitionClaimHandler.cs` | `4f73eb5b0343c2781afaa472b2cc33c9905f1f9cf39e55686d35627a805df6a7` |
| 15 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Handlers/QueryHandlers/GetClaimListHandler.cs` | `c83f35600934e2ecdf66605d4d9d7d758d848f685f6d75b6b5580e85a6af25bf` |
| 16 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Validators/CreateClaimValidator.cs` | `a0b26fda1a5843bfc87d382fb75e92fa33de9b7fd21e654213e5335555e0f266` |
| 17 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Validators/TransitionClaimValidator.cs` | `f17dacde38169ec66c58d212a8925ffc16b544fe4ddf582c25fa62987ffd3ee0` |
| 18 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/Validators/GetClaimListValidator.cs` | `337d8de737bed571c42abfc1a8b7ca7d31a4e5c2bc5c952f69f34afb51fd3a6d` |
| 19 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/ClaimModels.cs` | `2b8b79d618fcd21e5197bbe36c3089e2888aad36e2b7adc24d28adabb7c9911b` |
| 20 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/ClaimRequestContext.cs` | `f49b1e5a0e987d6d48ad5e7d88c5f7ab9974dcb83d7ad420fe19da6c3b0e259f` |
| 21 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/ClaimRequestFingerprint.cs` | `82b28c195e5dcc3c8b2b009baec9f4af2cf535054a12b534644d8656042e9314` |
| 22 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Application/Features/Claims/IClaimReferenceReader.cs` | `038b1a42edec31273fcdba92fb1628a4d04aa997f3972dffb5274f017a8a50b8` |
| 23 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Claims/ClaimsController.cs` | `b6f819d2e289e835c7a44e0573f66cb732099ec8e5f96315b83a361d68f14553` |
| 24 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Claims/ClaimContextMiddleware.cs` | `f81abe15bfdf7550d20c98f198f651f69c53dbc39557f141e9a16b5109ca7a68` |
| 25 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Claims/ClaimContractError.cs` | `11c12d2e962926f8ced892e24704527b26b26d71eb563aafc9d1582a95cf0373` |
| 26 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/ClaimRepository.cs` | `bc4271597ebc10a2321d664fea76b3f50c1f4eae1497b664c3515cef1a19989e` |
| 27 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/ClaimSchema.cs` | `e1107b5d70c4c458264e5dbd6ce6b8389468c49654e2e5c2c254b53ce95ec0f4` |
| 28 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/ClaimPersistenceRegistration.cs` | `0361088e461cfac00ecf3b34b39ee311a9e58f0b82d51327b82f44ae196752de` |
| 29 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/ClaimOutboxStore.cs` | `7e66de1bcd0fca817cd157a310e96042ab8fc0a43c30fb903d2644c81bb12eea` |
| 30 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/IClaimCommitProbe.cs` | `71cf0ae5d7fc65be462905b79ea5355539568f26485cf3c13f1de57042417bf0` |
| 31 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Persistence/Features/Claims/NoOpClaimCommitProbe.cs` | `4082bb189256e6bffbec4440913f4fd2544ed79ce88252324cd86007d069a222` |
| 32 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimReferenceReader.cs` | `3529fa558ea124681495366765e9671bfb9f8157e78250b3eaeef90df71e7670` |
| 33 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimPermissions.cs` | `03c697514d502b8797b8a7fd5622e3d1547db159213c7a60f2e806e391aff4ce` |
| 34 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimPermissionAttribute.cs` | `ec760a90498dac831af42fc85442931d0f8e7fa0a0fa09129690c85bc5682934` |
| 35 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimContractTests.cs` | `7950b0280038b651f19844c6ae2d2edef22c5b15e88d12ce546b5f7219e46892` |
| 36 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimLifecycleTests.cs` | `56eb72c0e4ece4b4549a3c8e6364582a15aa0860316f9528508a3e0167b07c57` |
| 37 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimIsolationTests.cs` | `d112497ded68afd4226e223948589820393fd2ab7037d478001c1aa8f11b8def` |
| 38 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimReplayTests.cs` | `f1c0916cfddebffe702de0d5490bd08c493eea364b6521e6a640eeef55662420` |
| 39 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimConcurrencyTests.cs` | `69c9fd8b2f19e1046be805701e61186a53be99035c9524530ed0b3e81f2da37d` |
| 40 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimAtomicityTests.cs` | `bc72742fcb7e1b15ee4d3cf129669ba27de5778672c8cd08cbfe5d3f39f0f0de` |
| 41 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimReferenceTests.cs` | `e03360a93509e1a8f9a1610c00aa719b8403136808911062aced7d876a46adf8` |
| 42 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimAmountTests.cs` | `dfd4e72ef6900997f41f7a6e7fcbdbb3bad2155737e73985e5b40dfe9f28d971` |
| 43 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimPermissionTests.cs` | `9ec68b2d2684ab43b1657af0df26a84c037095d5fe6fa065770b08d340ab5edc` |
| 44 | `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimOutboxTests.cs` | `c0930491e3b751dff4373941cc0532d624301841e29c2e623eb0a5025c3c70e9` |
| 45 | `services/Diten.SupplyChainService/tests/claims/runtime_probe.py` | `2f19fd5fe2887f49f231bacaf26e627f6d80e6fdc0a5f55a8d43fcf184597c47` |
| 46 | `services/Diten.SupplyChainService/tests/claims/restart_probe.py` | `575c8fc19fc88c125d56710bc6e145219de6102b5c395af021b7827fda4aea57` |
| 47 | `services/Diten.SupplyChainService/tests/claims/verify_evidence.py` | `78af2a44f5b90650eeaa1c0acb23ab28047a46d713ceaf47b95cc1e43a37d5b5` |

## Protected paths

Pack §6 as promoted, plus the published-contract line the proposal adds, plus the surfaces this alignment must not touch:

- `.antigravity/**`; `AGENTS.md`; DCP/registries/board.
- `docs/analysis/contracts/shipment-bundle.openapi.yaml`, `claims-semantics-v3.0.0.md`, `shipment-root-semantics-v3.0.0.md`, `returns-semantics-v3.0.0.md` and every other frozen contract; the docs-path guard authority.
- `gateway/Diten.ApiGateway/**/ocelot.json` (integration agent only); shared permission catalog/seed; shared DI; archive/frontend shell files (MOD-0187 is `shell: none`).
- MOD-0183/MOD-0184 persistence/features, MOD-0185/0186 and every other SupplyChain feature; finance/payment services and foreign-domain stores.
- `ClaimOutboxWorker.cs` or any publisher/hosted-service registration (not in the allowlist).
- All historical records and the superseded `docs/roadmap/plans/mod-0187-final-pack-delta-01/`.

## Program.cs integration-owner slot

`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is **not** a MOD-0187 owned path. It belongs to the single integration owner (process v1.0 §8; owner authority 2026-09-20: "Program.cs/shared composition consumer writer'larına kapalıdır").

| Identity | SHA-256 | Meaning |
|---|---|---|
| R14 baseline (normal composition before R14) | `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` | Artifact `mvp6-mod0187-r14-apply-01/artifacts/Program.cs.baseline`; the same value is in that package's `MANIFEST.tsv` and `normal-baseline-integration-01/PATCH-ORDER.md` |
| R14 patch | `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0` | Owner-authorised strict UTF-8 `Idempotency-Key` decoding only (`r14-apply-01/AUTHORITY.md`) |
| Accepted composition | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` | In the accepted 341-entry manifest |
| Common checkout today | `7fdb5ef0d322c3b9c814fab709dcfbaf1c9a2904f3d9a39e6f90bce077f204d8` | Not the accepted composition; uptake is Q14/Q15 |

Record gap (not edited): `r14-apply-01/AUTHORITY.md` prints the baseline as `a72a05a5b7dc235c…ab34a`, which does not match the baseline artifact. Patch and target hashes agree everywhere. The earlier hop `7fdb5ef0…` → `a72a05a5e185…` comes from the combined Returns/Claims composition source (`mvp6-mod0186-r01-rework-01`, `mvp6-mod0187-r21-apply-01`); this lane did not re-trace its authority.

Slot rule: one integration-owner writer, one exact baseline→patch→target decision, applied only after pack promotion and the integration decision. Pack promotion does not grant it.
