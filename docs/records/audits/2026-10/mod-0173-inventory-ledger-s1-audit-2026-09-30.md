# MOD-0173 Inventory Ledger & Valuation — Slice S1 audit (2026-09-30)

| | |
|---|---|
| Work package | WP-0173-S1 · prompt P-0173-S1 v1 · lane AL-SCE-MOD0173-LEDGER |
| Branch | `feature/sce/mod-0173-inventory-ledger-valuation` (local commits, not pushed) |
| Base | `b780fbd9` |
| Service | `services/Diten.InventoryService` · port 5066 · base path `/api/inventory` |
| Authority | module pack `ready-for-dev` > domain-config > AGENTS.md > `.antigravity/` |
| Evidence | [`mod-0173-s1-evidence/`](mod-0173-s1-evidence/README.md) |
| Verdict of this audit | **agent PASS** — not CT ACCEPTED (K13). Acceptance is the CT's independent re-measurement. |

Filed under `2026-10/` as the work package requires; the measurements were taken on 2026-09-30.

## 1. Environment (measured, not assumed)

| # | Measurement | Result |
|---|---|---|
| E1 | MongoDB Windows service `MongoDB` 9.0.2 on 127.0.0.1:27017; config `replication.replSetName: rs0` | `hello` → `setName: rs0`, `isWritablePrimary: true`; transaction start/insert/abort probe OK. **This instance was used**; no temporary replica set was needed; the service configuration was not touched. |
| E2 | Test database naming (DB-010) | one fixed database `diten_inventory_s1_tests`, isolation by a fresh TenantId per test; the repo guard `MongoTestDatabaseGuardTests` is green with the new tests in its scan |
| E3 | .NET | SDK 10.0.401; runtimes 8.0.31 / 10.0.12; the service targets `net8.0` |
| E4 | Python | not installed (only the Store stub) → no Python script written; DCP-002 verifier not run (waiver W-0173-01) |
| E5 | Node / Prism | Node v24.19.0; `@stoplight/prism-cli@4.10.5` resolved from the npx cache and run as one `node` process per mock |

## 2. Phase 0 / 1 — pack gate and analysis

- Pack `MOD-0173-inventory-ledger-valuation.md`: `status: ready-for-dev`, `shell: none`, `golden_reference: none`,
  `form_field_count: 0`, `entity_base: EntityBase`. WP-0173-C1 (`22259f48`) central records present on the branch.
- DCP-002 verifier: **not run** — no Python on the machine. Carried as pack §18 waiver **W-0173-01** (owner Ali;
  compensating control: registry row "DCP-002 gate OK 2026-09-11" + DCP-009 §20; expiry: before the MVP-1 push/PR).
  No PASS is claimed for it.
- Scope used verbatim from pack §1 (S1 row), §4 (fields, dimension, buckets, indexes), §8 (runtime), §12 (matrix and
  field rules), §13 (failure paths), §16 (acceptance). No scope was added.

## 3. Phase 1.5 — architecture checklist

| # | Check | Answer |
|---|---|---|
| 1 | All pack fields on the entities? | **Yes.** `InventoryTransaction`: Id/TransactionId, TransactionNumber, TenantId, LegalEntityId, DocumentGroupId, MovementType, MovementReason, ItemId, SkuId, SkuLevel, From/To Warehouse/Location, Quantity, UomId, BaseQuantity, BaseUomId, LotId, SerialIds, From/To StockStatus, SourceModule, SourceType, SourceDocumentId, SourceLineId, SourceSystem, ExternalRef, ReversesTransactionId, TransactionDate, PostingDate, IdempotencyKey, RequestFingerprint, CorrelationId, CreatedBy, CreatedAt, IsDeleted, DeletedAt. `ReversedByTransactionId` is derived at read time (the original is immutable). `InventoryBalance`: full dimension key + OwnerId/ProjectId + OnHand/Reserved/Available/InTransit/Blocked/QualityInspection/BaseUomId/LastMovementId/LastPostingDate/Version. |
| 2 | Global naming? | **Yes** — pack names kept 1:1; C# enum members PascalCase, wire values exactly the contract's SCREAMING_SNAKE (mapped in `WireNames`). |
| 3 | Isolation / soft-delete guarantee? | **Yes** — `Persistence/Repositories/InventoryLedgerRepository.cs`; every query enters through `TransactionScope()` / `BalanceScope()` (TenantId + LegalEntityId + IsDeleted=false) from the JWT scope; tenant-owned model, no GlobalEntity. Ledger is append-only: soft-delete fields exist, no delete path. |
| 4 | Entity base? | **Yes** — `Domain/Entities/EntityBase.cs` (pack `entity_base: EntityBase`), framework-free per pack §10. |
| 5 | CQRS, one type per file? | **Yes** — `Features/Inventory/{Commands,Queries,Handlers/CommandHandlers,Handlers/QueryHandlers,Validators}` + `InventoryModels.cs` (pack-named wire-model file) + `Services/`. |
| 6 | DataTable golden reference? | **N/A** — `shell: none`, form field count 0. |
| 7 | Compact section map? | **N/A** — no UI. |
| 8 | Required-field contract across layers? | **Yes (backend only)** — required: idempotencyKey (+header), movementType, itemId, skuId, skuLevel, quantity, uomId, sourceModule, sourceType, transactionDate, postingDate (= contract `required`); side fields per matrix; optional nullable: documentGroupId, movementReason, lotId, serialIds, sourceDocumentId, sourceLineId, sourceSystem. No ViewModel/Razor (no UI). |
| 9 | Platform lookups? | **None** — MovementReason is an opaque MOD-0048 code in S1 (pack §2, §20); masters are the frozen PRODUCT-MASTER / LOCATION contracts over HTTP. |

No item deviates from the pack → Phase 2/3 proceeded under the CT's conditional pre-approval.

## 4. Phase 5 — tenant audit (`/tenant-audit`), security, code quality

| Risk | Location | Finding | Status |
|:---:|---|---|---|
| — | Persistence | every query carries Tenant + LE + IsDeleted; `inventory_sequences` is keyed `_id = tenant:le:year` | OK |
| — | Indexes | tenant-first on every collection (except the `_id` counter) | OK |
| — | Layers | `MongoDB.Driver` only in Persistence (Domain: no framework reference; Application: no Mongo — architecture-tested) | OK |
| — | DTO / commands | no TenantId / LegalEntityId member; body/query `tenantId`/`legalEntityId` → 400 | OK |
| — | Controller | no business rule; read → send → render | OK |
| 🔵 | Headers | `X-Tenant-Id` is optional here — scope comes from the JWT; a header can only confirm it (mismatch → 403) | by pack §8 |
| 🔵 | `PermissionAuthorizationHandler` | `SuperAdmin` role passes every permission check (template behaviour, copied unchanged) | NOTE for CT |
| 🔵 | Program.cs | no CORS policy (no browser caller); Swagger only in Development / Mock; exceptions answer the Error schema without a stack trace | deliberate |
| 🔵 | Secrets | Mongo + JWT validated at startup (`ValidateRequiredSecrets`); `appsettings.Development.json` carries the repo's local-dev shared JWT secret exactly as ProcurementService does; `appsettings.Mock.json` carries none (environment only) | template parity |
| 🔵 | `serialIds` | no own length cap (bounded by the request size limit); a non-serial item may carry serials | follow-up MOD-0174 |

Code quality: 0 warnings; one type per file except the pack-named `InventoryModels.cs` and the probe console;
comments state the rule each guard enforces and why.

## 5. Acceptance criteria (pack §16 / WP)

| AC | Result | Evidence |
|---|---|---|
| AC1 build 0/0, 5 layers, 4 behaviors, CustomBaseController, /health on 5066 | **PASS** | `d1-build.log` (0 Uyarı / 0 Hata, 8 projects incl. the runtime probe) · `SingleSystemOfRecordArchitectureTests.Five_layers_exist` · `PipelineArchitectureTests` · `LedgerImmutabilityArchitectureTests.Every_controller_derives_from_CustomBaseController` · `runtime-probe/r-process.json` (`/health` 200 `{"status":"Healthy","module":"MOD-0173"}` on :5066, both runs) |
| AC2 opening +100 → 201, 1 tx, row 100/100, survives restart | **PASS** | `OpeningAndIssueFlowTests.Opening_balance_100…restart` (new in-process host) · `runtime-probe/r-ac2-opening.json` + `r-restart-read.json` (real process restart, PID 25428 → 32752) |
| AC3 issue 20 → 2nd tx, 80, 1st tx byte-identical | **PASS** | `OpeningAndIssueFlowTests.Issue_20…` (BSON bytes equal) · `r-ac3-ac4-ac5-…json` `ac3ByteIdentity` (SHA-256 before = after) |
| AC4 idempotency replay / key reuse | **PASS** | `IdempotencyTests` (9, incl. 6 parallel duplicates → 1 commit) · runtime counts unchanged after replay and reuse |
| AC5 negative stock 422 + details, no write | **PASS** | `NegativeStockAndConcurrencyTests` · runtime `available 80.000 / requested 1000.000` |
| AC6 parallel 2×60 on 100 → one 201, one 422, 40 | **PASS** | `Two_parallel_issues_of_60…` (5 rounds) + `Many_parallel_issues_never_overdraw` (12×10 on 100) |
| AC7 StockStatus / byStatus | **PASS** | `StockStatusTests` (4) · `r-ac7-ac9-…json` |
| AC8 reversal, ALREADY_REVERSED, INSUFFICIENT_STOCK | **PASS** | `ReversalTests` (8, incl. 4 parallel reversals → 1 commit) |
| AC9 100 BOX (10/1) → 1000 EA | **PASS** | `MasterDataValidationTests.Hundred_BOX…` · runtime 5 BOX → 50 EA on the Prism conversion |
| AC10 isolation, CROSS_LE_FORBIDDEN, tenantId in query → 400, every endpoint | **PASS** | `IsolationTests` (16) · `r-ac10-isolation.json` |
| AC11 401 / 403 on every endpoint | **PASS** | `RbacTests` (10) |
| AC12 audit + Pending outbox, same correlation as header | **PASS** | `AuditCorrelationTests` · runtime: header = transaction = audit = outbox correlation, outbox `Pending` |
| AC13 SourceSystem / ExternalRef persisted, returned where exposed | **PASS** | `AuditCorrelationTests.SourceSystem_and_ExternalRef…` — the frozen `TransactionItem`/`MovementResponse` expose `sourceDocumentId`/`sourceLineId` but not `sourceSystem`, so it is persisted and audited, not returned |
| AC14 single SoR, no Model B / MDM DB | **PASS** | `SingleSystemOfRecordArchitectureTests` (13) |
| AC15 schema-validated responses + Prism smoke with real clients | **PASS** | `ContractValidatingHandler` on every API test client (status allow-list + schema + correlation header) · `ContractSchemaValidatorTests` (13, validator sabotage-checked) · `PrismConsumedContractSmokeTests` (4) |
| AC16 fail-closed 503 PRODUCT_MASTER / LOCATION, no write | **PASS** | `FailClosedTests` (3) · `PrismConsumedContractSmokeTests.Unreachable_masters_fail_closed…` |
| AC17 no update/delete route or repository method | **PASS** | `LedgerImmutabilityArchitectureTests` (7: reflection + source scan of every driver call on the transaction collection) |

## 6. Verification

| # | Command | Result | File |
|---|---|---|---|
| D1 | `dotnet build services/Diten.InventoryService/Diten.InventoryService.sln -c Debug --no-incremental` | 0 Hata / 0 Uyarı | `d1-build.log` |
| D2 | `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests` | 218 / 218 passed, 0 failed, **0 skipped** | `d2-inventory-tests.trx`, `d2-test-summary.log` |
| D3 | `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests` | 18 / 18 passed | `d3-tenant-architecture-tests.log` |
| D4 | sabotage (K3) | see §7 | `d4-*.log` |
| §R | `run-runtime-probe.ps1` | flows 33/33 checks, after-restart 4/4 | `runtime-probe/` |
| D5 | `git log` / `git diff --stat b780fbd9..HEAD` / `git status` | only allowed paths; clean tree | final report |

## 7. Sabotage proof (K3)

| Guard | Sabotage (temporary, never committed) | Red | Restored |
|---|---|---|---|
| Idempotency — replay/reuse decision | `PostMovementHandler.Replay`: fingerprint comparison disabled | `Same_key_with_a_different_payload…` → 200 instead of 400 (1 of 9 red) | 9/9 green, diff empty |
| (layer 2) idempotency pre-check | handler lookup disabled, unique index left | idempotency tests stay green — the unique index still yields exactly one commit; `An_idempotent_replay_does_not_need_the_upstream_masters` goes red (503) | green, diff empty |
| Negative stock | `InventoryLedgerRepository.DecreaseAsync`: coverage condition removed | 5 of 18 red (over-issue 201, 60+60 → −20, 12×10 overdraw, reversal of consumed stock, uncovered transfer) | 18/18 green, diff empty |
| Tenant/LE isolation — data | `BalanceScope`: Tenant + LE predicate removed | 3 of 16 red (tenant B sees A's rows, other LE sees rows, rows written without scope) | 16/16 green |
| Tenant/LE isolation — header | middleware `X-Legal-Entity-Id` check disabled | 4 of 16 red (no CROSS_LE_FORBIDDEN on all 4 endpoints) | 16/16 green, full suite 218/218, `git diff` empty |

## 8. Defects found and fixed during the slice (measured)

1. `UomConverter` multiplied a pre-divided factor: `3 PACK` at `1/3` gave `0.999…`. Now numerators and denominators
   are multiplied separately and divided once (`03946aaf`).
2. The PRODUCT-MASTER client accepted a `validate` answer of the wrong shape (a LOCATION answer); result entries must
   now name an item/sku and a boolean `exists`, else fail closed (`03946aaf`).
3. Test harness: Prism started via `cmd → npx → node` left the grandchild alive, which held the test host's stdout
   pipe open — the run hung after all tests had finished. Prism now runs as one `node` process.
4. Test harness: JsonSchema.Net evaluations running concurrently let one invalid body pass in a full run (never in
   isolation). Schema evaluation is now serialised.

## 9. Assumptions (ASSUMPTION-S1-xx)

| ID | Assumption |
|---|---|
| S1-01 | "Fixed/shared DB name forbidden" is read together with DB-010: never the dev/mock/prod database and never a per-run GUID database; DB-010's own pattern (one fixed test database, isolation by TenantId) is used. |
| S1-02 | l10n N/A (`shell: none`, no resx). API messages are English; the machine-readable part is `error.code`. |
| S1-03 | 401, permission 403, tenant-header 403 and missing-claim 403 carry the `Error` body **without** `error.code` — the contract declares none and codes outside ASSUMPTION-0173-04 are not invented. |
| S1-04 | GET /availability → 404 `UNKNOWN_ITEM` when the item/sku has no ledger row in the caller's Tenant + LE; reads make no upstream call (a 503 would be an undeclared status on this operation). |
| S1-05 | GET operations also answer 400 (malformed parameters, pack §12), 401 and 403 (pack §13.4-5 "every endpoint") although the contract lists only 200 (+400/404 for availability). These come from the pack, not from this slice. |
| S1-06 | Null members are omitted from response bodies (OpenAPI 3.1 has no `nullable`; `$ref: StockStatus` members cannot be null). |
| S1-07 | Reversal refusals: bad envelope, original not found in scope, field mismatch, reversal of a reversal → 400 `INVALID_REQUEST`; already reversed → 422 `ALREADY_REVERSED`. A REVERSAL request must omit sides, lot and serials. |
| S1-08 | "from*"/"to*" in the matrix include the side's stock status; WAREHOUSE/LOCATION transfers are single-step with the same status (pack §21); STATUS_TRANSFER requires `to` warehouse/location equal to `from`. |
| S1-09 | `INV-{yyyy}-…` uses the UTC year of `postingDate`; counters live in `inventory_sequences` (no quantity). |
| S1-10 | Idempotency fingerprint compares canonical typed values (quantity scale, defaulted status, UTC instant). |
| S1-11 | UoM: only declared directions, chains ≤ 4 hops, effective dates checked against `transactionDate`; a result needing > 6 fraction digits or a `getSkuUom` 404 → 422 `UOM_CONVERSION_MISSING`. |
| S1-12 | A location of another legal entity is `UNKNOWN_LOCATION` for the caller. The check may be off only in the `Mock` environment; the host refuses to start with it off anywhere else (tested). |
| S1-13 | Outbox is a service-local `inventory_outbox` collection written in the posting transaction; status vocabulary mirrors `Diten.BuildingBlocks.Eventing.EventOutboxDeliveryStatus`. The building-block signed-envelope outbox is not used (it needs transport metadata; transport is integration-owned). |
| S1-14 | The index initializer blocks startup on failure (template: best-effort) because the unique indexes carry correctness. |
| S1-15 | Scope middleware is `InventoryScopeResolutionMiddleware`, not a `TenantResolutionMiddleware` family member (403 semantics, JWT only, no dev bypass). See NOTE-3. |
| S1-16 | Index creation method is `InventoryIndexConfiguration.ApplyAsync` (only this service's collections); tests never call it — each test host creates the indexes at startup. |
| S1-17 | Module self-registration (`ModuleManifestProvider`) not added: `shell: none`, no view routes to mirror. Belongs with the UI follow-up pack. |
| S1-18 | PRODUCT-MASTER calls: `validateRefs`, `getProduct`, `getSku`, `getSkuUom` (`resolveSkuProduct` not needed — `getSku` carries the D-SKU-LINK). LOCATION: `validateLocations`, `getLocation`, `getLocationPath`. The caller's bearer and the correlation id are forwarded. |
| S1-19 | The service `.sln` also lists `Diten.BuildingBlocks.Security.Secrets` (added by `dotnet sln add` as a referenced project) — reference only, not modified. |
| S1-20 | The runtime probe writes to `diten_inventory_mock` with fresh tenants per run and does not delete them. |

## 10. N/A phases, blockers and notes

| Item | State |
|---|---|
| Phase 3.5 gateway | **BLOCKER/NOT:** route `/api/inventory/**` → 5066 does not exist; `ocelot.json` untouched → **INT-1** (integration-agent). |
| Permission seed | **NOT:** `supplychain.inventory.read` / `.movements.post` are checked, not seeded → **INT-1**. |
| Phase 4 / 4a UI, 4.5 UI smoke | N/A (`shell: none`); replaced by the API runtime probe (§R). G1's "UI + API + persisted + audit" E4 bar cannot close from S1 (pack ASSUMPTION-0173-00). |
| DCP-002 | **Waiver W-0173-01** — verifier not run (no Python); must run exit 0 before the MVP-1 push/PR. |
| NOTE-1 | Root `*.sln`, `run_all.sh`, `run-diten.sh` registration not done (shared; separate WP). |
| NOTE-2 | `docs/records/audits/README.md` index not updated (shared file outside the allowed paths). |
| NOTE-3 | `tests/architecture/…/TenantContradictionSiteGuardTests` counts files named `TenantResolutionMiddleware.cs` (9 sites). The new scope middleware is deliberately not one of them and is not classified there; whether the guard should learn about differently named scope resolvers is a CT / EA decision (file outside the allowed paths). |
| NOTE-4 | Pack §18 "Code precondition" checkbox and §19 notes were not edited. |
| NOTE-5 | `SuperAdmin` bypass in the permission handler (template parity) — confirm for a stock SoR. |

## 11. Proposed row for `execution/registries/module-implementation-status.md` (not written — CT records it)

```
| MOD-0173 Inventory Ledger & Valuation | Backend (S1) | 35 | **S1 Ledger core** in `services/Diten.InventoryService` (5-layer @5066; append-only `inventory_transactions` + `inventory_balances` projection; atomic posting in one Mongo transaction with coverage-guarded legs, Pending `InventoryMovementPosted` outbox + audit; idempotency replay/reuse; REVERSAL; StockStatus buckets; base-UoM conversion; JWT-only Tenant+LE scope, `CROSS_LE_FORBIDDEN`; fail-closed PRODUCT-MASTER/LOCATION HTTP clients; POST /movements, GET /availability, /balance, /transactions per INVENTORY-BUNDLE v1). 218 tests green (real Mongo, contract-schema-validated, Prism smoke); sabotage proof for idempotency / negative stock / isolation; runtime E4 probe incl. process restart. Build 0/0. | **INT-1** gateway route `/api/inventory/**`→5066 + permission seed; **S2** reservations; **S3** valuation (needs additive contract v1.1); UI follow-up (tenant shell, 7 languages) for G1 E4; DCP-002 verifier re-run (waiver W-0173-01); progressive integration with real MOD-0290 / Location master. |
```
