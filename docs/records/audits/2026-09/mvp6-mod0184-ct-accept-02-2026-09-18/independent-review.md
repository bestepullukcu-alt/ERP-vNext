# MVP6-MOD0184-CT-REVIEW-02 / v1.0 — SOP §22

Date: 2026-09-18. Lane AL-MVP6-CARRIER-INS02. Profile C, strict repository-read-only, HIGH.

## Verdict and exact recommendation

Agent recommendation: bounded Carrier acceptance is supported by content-bound independent E2/E3/E4 evidence. No missing blocking evidence found for the approved three-operation slice. This is a recommendation to central CT, NOT a CT ACCEPTED decision, full-module acceptance, E5/G5 or MOD-0185 DEV GO. Full repository architecture gate remains BLOCKED: last independent measurement 50 PASS / 3 external FAIL.

Repository /Users/natig/Projects/ERP-vNext-recovery; branch feature/mvp6-logistics; HEAD 4a8d4d4b339528a88e6220fb8402e5a2c771136c. Dirty tree is the preserved input, not a clean-commit delivery. baseline.json contains full dirty inventory and 14,433 source hashes; staging empty. No repository writes, fixes, scripts executed in historical evidence directories, git mutation or runtime tests in this review.

## Reviewed inventory and source binding

DEV02 (mod-0184-dev-01) 65 entries + DEV03 16 entries = 80 unique paths. 78 non-self manifest hashes match; two manifest self-references are null by design and their actual hashes are captured in merged-manifest.json. Null self-hashes are not accepted for source/evidence inputs.

The sole overlapping path is services/Diten.SupplyChainService/tests/carriers/restart_probe.py:
- DEV02 historical hash 4de0b3245147f0f2638751729ce9f311cde7f4d2cc9db4ff8a98493b2ef76c4f
- DEV03 effective hash 8ff2674d5cc17c38b7349b0617523239e2502e4c4bb9622a393cac9d1d63f434

DEV03 supersedes this input and the use of defective outage capture evidence; original DEV02 records remain immutable and are not relabeled PASS. VER02's finding is closed by VER03's fresh byte-capture proof.

Against independent VER02 baseline, 7,295 services/contract files have only this expected recorder difference. Against independent VER03 baseline, all 7,296 services/contract files match; no new service file exists. Current Program.cs git diff equals DEV02 program.diff byte-for-byte. See source-binding.json and merged-manifest.json. These content checks justify not repeating unaffected tests. Later DocsPath policy/guard changes have their separate independent E2 verification and CT acceptance; other later changes to MOD0185/0186/0187 packs and ignored docs/.DS_Store do not affect Carrier execution. We do not claim the entire historical VER03 repository snapshot remains unchanged.

## Exact bounded scope / golden flow

Diten.SupplyChainService port5061, five Carrier feature layers. Only GET /api/shipment-bundle/carriers, POST same path, POST /api/shipment-bundle/carriers/{carrierId}/status. Canonical SHIPMENT-BUNDLE1.1.0 and Carrier annex govern wire/errors/replay. Server-resolved tenant+LE and actor, JWT and three operation permissions, exact ordinal scoped code uniqueness including retired/deleted reservations, Active/Suspended/Retired lifecycle, durable scoped idempotency and transactional entity+success receipt+audit. No Carrier lifecycle event/outbox event invented.

Caller → Carrier-only ordered auth/context/header/schema validation → CQRS → scoped atomic Mongo transaction → frozen response. Current response correlation and immutable original audit correlation remain separate. Historical replay can return the original successful receipt despite later status/deletion; fresh reads/status exclude deleted entities.

## Acceptance-evidence matrix

All source paths below are relative to /Users/natig/Projects/ERP-vNext-recovery. T denotes services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Carriers/CarrierContractTests.cs. V2 denotes /private/tmp/mod0184-runtime-ver02; V3 denotes /private/tmp/mod0184-runtime-ver03. PASS means evidence supports the bounded criterion, not a new test run.

| AC | Source / independent evidence | Disposition |
|---|---|---|
| C01 surface | Api/Features/Carriers/CarriersController.cs:12–19 under SupplyChain source; T:108 exact response names; V2/carrier/runtime.json 33 schema-checked requests, canonical SHA bound | PASS. Three operations only; create422 inherited examples N/A, not a missing runtime pass |
| C02 schema | T:199,214,245 ordered validation, nil UUID, schema strings, mode ordering/duplicates, optional null equivalence; V2/tests.log 99/99 | PASS; no invented normalization/limits; transport OWS/encoding limits remain documented |
| C03 scope | T:170 two tenants × two LEs, lists, cross-scope404, deleted reservations/receipt visibility; V2 runtime scoped persisted snapshots | PASS; no cross-scope mutation/leak reproduced |
| C04 RBAC | T:185,191,257,306 duplicate/malformed claims, independent grants, signed duplicate JSON, repository-zero-call validation controls; V2 tests/runtime | PASS; real JwtBearer; invalid token parsing401 distinguished from context403 |
| C05 uniqueness | T:158 concurrent distinct-key creates; T:170 ordinal/reserved codes, scope reuse; persisted counts | PASS; unique-index authoritative |
| C06 lifecycle | T:126–136 all9 pairs with full snapshot equality on rejection; V2 tests/runtime | PASS; legal transitions audited, Retired terminal |
| C07 replay | T:108,231,245,273 original result after later state/deletion, fingerprint conflicts and concurrent first receipt; V2/carrier/runtime.json restart snapshots | PASS; durable receipt, no duplicate audit |
| C08 concurrency | T:290 barrier-controlled distinct-key Suspended/Retired serial-history assertion, T:158 contention; V2 service suite | PASS; no blind overwrite/reopening |
| C09 atomicity | T:139 four rollback phases and T:150 postcommit response-loss recovery; V3/live/commit-uncertainty.json actual failCommand counts2/3, persisted entity/receipt/audit | PASS; unknown commit503 is NOT claimed to prove rollback |
| C10 durability | V2/carrier/runtime.json two processes and persisted collections; T:281 index failure; V2/standalone.log; V3/live/outage.json startup no listener, Q/C/S503 and same-key recovery | PASS; no fallback; isolated fixture databases |
| C11 correlation/audit | T:108 original audit/current response correlation; V2 runtime header/body evidence; V3/live/transport-comparison.json 9/0 mismatches, two empty GETs; V3 regression GREEN→RED→GREEN | PASS after VER03; old outage byte records excluded |
| C12 protection | merged-manifest.json, source-binding.json; pack:255 and386–400; exact Program diff; DEV03 baseline/manifest preservation | PASS for DEV bounded scope. Separate authorized contract publication and DocsPath changes are not attributed to Carrier DEV |

## Composition and owned paths

Pack §23/§29 authorizes new Features/Carriers directories in Api/Application/Domain/Persistence/Infrastructure; Carrier tests and tests/carriers probes; DEV02/DEV03 evidence. Complete exact file inventory is merged-manifest.json, not a broad service write grant.

Only existing production source exception: services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs, three using directives, :42 Carrier persistence registration, :51–52 Carrier-only invalid-model response, :58–59 mutually exclusive Carrier/Shipment middleware branches. Existing Shipment auth/outbox/health remain intact. Shipment33 regression evidence remains source-bound. No unexpected shared production change found.

## Independently executed evidence reused, not rerun

- V2/build.log: successful build, zero warnings/errors.
- V2/tests.log: 99/99 service tests, 66 Shipment +33 Carrier.
- V2/carrier/runtime.json:33 PASS, two-process persistence evidence; capture.log zero byte mismatches.
- V2/shipment/sent-body-comparison.json:33 requests,0 mismatches.
- V3/regression-results.json:green0,old-defect red1,restored green0; three actual-recorder regressions.
- V3/live/transport-comparison.json:9 requests,0 mismatches,2 bodyless requests.
- V3/live/outage.json and commit-uncertainty.json: live recovery, actual unknown commit retries/exhaustion, one entity/receipt/audit.
- V2/results.json and runchecks.py retain original commands/exits; V3 report and live.log retain affected rerun context. This review ran hash/JSON/log/source inspections only.
- evidence-hashes.json records available independent artifacts as inspected now; not a retroactive signed provenance claim.

## DocsPath closure, external failures and deferral

DocsPath CT record docs/records/audits/2026-09/mvp6-docs-path-ct-accept-01.md:3–18 closes only the approved policy WP. Current policy/guard/authority hashes equal independent DocsPath VER inputs. All17 sealed and2 canonical hashes, and three independent TRX hashes match (docs-closure-proof.json). Architecture history14/4 is superseded for current gate assessment by independent DocsPath50/3; 35 added fixtures are included in53, not additional tests.

Three exact residual failures:
1. MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun — services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs:79 and Persistence/DisposableStandaloneMongo.cs:25.
2. JwtClockSkewGuardTests.NoProductionValidatorWritesItsOwnClockSkew — services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Program.cs:32 and services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Program.cs:31.
3. JwtClockSkewGuardTests.EveryLifetimeValidatingFileDeclaresTheSharedSkew — same files:27/:26.

Existing user deferral is recorded in docs/roadmap/plans/mvp6-development-plan-v2.0.md:165–168, reiterated by v4 and DocsPath CT record:16–17. It is not a time-unlimited waiver and does not turn the full gate green. This review grants no new deferral, expiry or owner decision. HumanCapitalService is the offending service, not Diten.HcmService.

## Risks, exclusions and decisions

No bounded Carrier blocker remains in the reviewed evidence. Remaining gate/risk: three external architecture failures; temporary independent evidence durability (CT should preserve its evidence inventory); no production performance/pagination SLO or transport-wide Unicode-header guarantee inferred. Machine hashes establish content binding, not independent human authority authentication.

Excluded: gateway routing/shared permission catalog provisioning, UI, live Shipment/Warehouse ingress and MOD0183 GAP01/02/03/04, Inventory/stock writes, Supplier master, pricing/tendering/settlement, Loads/returns/claims implementation, external integrated consumers and E5/G5. Static publication/fixture uptake is not runtime consumer integration. MOD0185 dependency design may cite this bounded seam only after CT's separate decision; no downstream dispatch here.

Persistence/security/audit evidence is E4 within isolated Carrier scope. Observability is Carrier-local scoped outcome/current correlation without token/payload disclosure. No migration/rollback performed: documented rollback stops Carrier exposure and reverts only authorized composition, preserving Carrier data/receipts/audit. No destructive cleanup authorized. ASSUMPTION: unchanged content-bound inputs allow reuse of independently measured evidence, as explicitly requested; historical draft sections are superseded by pack§29, not implicitly promoted here.

## Records for CT to update separately

1. New central MOD0184 bounded acceptance decision, citing this recommendation and exact effective manifest/evidence.
2. MOD0184 pack effective acceptance checklist/status note with C01–C12 mapping; retain historical sections, do not imply full module completion.
3. Current MVP6 development plan/continuation status and owned delivery board/backlog/module-registry/DCP-009 execution status where applicable; CT ownership only, no contract/ID change.
4. VER-184-01 closure cross-reference to VER03 and DocsPath closure reference; preserve immutable old FAIL reports.
5. External failure owner/review follow-up under existing deferral; no invented waiver.
6. Separate MOD0185 dependency/DoR/contract/approval decision before any DEV GO; Carrier recommendation alone does not open that gate.

## No-change

See no-change.json for final source hashes and Git comparison. Changed repository files: none. Out-of-scope changes by this WP: none. All new artifacts are under /private/tmp/mvp6-carrier-ct-review-i2osd393. No commit/push/stash/branch/staging operation.
