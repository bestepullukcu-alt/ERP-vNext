# MVP6 BASE-STACK — v2

| Field | Value |
|---|---|
| Record | BASE-STACK v2 · WP Q157 · CT-QUEUE row `Q157` (line 203: `Q157 · BASE-STACK v2 record (BASE → Q117 → Q121 → Q131) · READY · LANE 2 (integration-agent) · Q155`) |
| Authority | CT-2 in `docs/records/audits/2026-09/mvp6-ct-verdicts-q154-q131-q141-q144-2026-09-27.md` (Q155 record, sha256 `9e1a322db2e6d8b10e22ee4bd23f3a6e45212a6166e183dcb2ad819b8fdf51e5`, line 39): "overlay-q131a (4a4a0860…) is the 3rd fix layer: BASE → Q117 → Q121 → Q131. It is written as BASE-STACK v2 (Q157)." |
| Predecessor | BASE-STACK v1 — `docs/records/audits/2026-09/mvp6-base-stack-v1/BASE-STACK-v1.md` sha256 `80e31c156f08747c934097dcd5a26add8581e357b9f20e9300f4a0fac33534fe` (SHA256SUMS 2/2 OK at Q157). **v1 is not changed.** |
| Written by | LANE 2 (Cowork, Linux VM, repo via bridge), integration-agent + read-only-auditor, 2026-09-27 (+03:00) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `git status --porcelain` only (no `git diff`); no git write; no compose, no build; `~/mvp6-env` not touched |
| Versioning (K4) | This file is never edited. A changed stack is a new file `BASE-STACK-v3.md` in a new folder `mvp6-base-stack-v3/` with its own SHA256SUMS |
| Machine-readable | `LAYERS.tsv` in this folder (same columns as v1; the check column is now `evidence_sums_check_q157`) |

## 0. Status of v1 and v2

- **v1 is superseded by v2 for new Mac WPs.** Every Mac build/test/runtime WP dispatched after the CT verdict on this record cites v2.
- **Running WPs finish on v1.** A WP already dispatched on v1 is not re-based mid-run. Its report states v1, and the composition is the v1 one.
- A module draft built on v1 stays valid as evidence for v1. Moving it to v2 means a new Mac build on v2. Q131 changes only test files and the new test-env script (§2).

## 1. The stack (order is fixed)

```text
BASE a8a236de…  →  Q117 83e6322c…  →  Q121 93bf1c07…  →  Q131 4a4a0860…  →  module draft (per build)
```

| # | Layer | Artifact (full sha256) | Files | Effect → tree | Evidence folder (SHA256SUMS; re-checked by Q157) | Proof / VER | CT verdict record (sha256; lines) |
|---|---|---|---:|---|---|---|---|
| 0 | **BASE** | `mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv` = BASE-HASH `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` | 14,566 | defines the tree (L0 `git archive HEAD` + BC-SOURCE + A12-360 + Auth-22) → 14,566 | `mvp6-q103-accepted-base-01/` (`231563ae…b518`) **14/14 OK** | — | `mvp6-ct-verdicts-q115-q64c-q103-2026-09-26.md` (`f16fe1d0…099e`; lines 37–43) — CT ACCEPTED |
| 1 | **Q117** | `mvp6-q117-test-guard-fixes-01/q117-fixes-overlay.tar.gz` `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` | 11 | 10 modified + 1 added → 14,567 | `mvp6-q117-test-guard-fixes-01/` (`4c11829b…d14d`) **12/12 OK** | `mvp6-q119-ver-q117-01/` (`3e4b9be9…8524`) **15/15 OK** | `mvp6-ct-verdicts-q120-q119-q117-q64e-2026-09-27.md` (`c6dd1ce5…2b04`; lines 11–19) — CT ACCEPTED |
| 2 | **Q121** | `mvp6-q121-mongo-guard-fixes-01/q121-mongo-guard-fixes-overlay.tar.gz` `93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90` | 2 | 2 modified (Platform tests) → 14,567 | `mvp6-q121-mongo-guard-fixes-01/` (`0208a4fc…d937`) **3/3 OK** | `mvp6-q121b-build-test-01/` (`a7832bac…aca1`) **20/20 OK**; `mvp6-q121c-supplychain-rerun-01/` (`2a86c732…57c6`) **18/18 OK** | `mvp6-ct-verdicts-q129-q101b-q121-2026-09-27.md` (`1ebacf02…5d85573`; D-3 line 48, D-5 line 50) — CT ACCEPTED |
| 3 | **Q131** (new) | `mvp6-q131a-port-uri-overlay-01/overlay-q131a.tar` `4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf` | 20 | 15 modified + 5 added → **14,572** | `mvp6-q131a-port-uri-overlay-01/` (`35c9e0fe…85c9`) **5/5 OK** | `mvp6-q154-q131-mac-ver-01/` (`e4f73ece…baa1`) **34/34 OK**; REPORT `b3200e10…ba93`: VER PASS, tree B = 14,572, 0 mismatch (line 65) | `mvp6-ct-verdicts-q154-q131-q141-q144-2026-09-27.md` (`9e1a322d…51e5`; CT-1 line 38, CT-2 line 39) — CT ACCEPTED |
| 4 | **Module draft** | the module's own CT-accepted draft overlay, named with its full sha256 in the build prompt | — | module files only | its own folder | its own VER | its own CT verdict — **not fixed by this record** |

All folders are under `docs/records/audits/2026-09/`. Full hashes are in `LAYERS.tsv`.

## 2. Conflict check for layer 3 (Q131) — run by Q157 in this lane

Method (read-only; archives read with Python `tarfile` in memory, nothing extracted into the repo or `~/mvp6-env`):

- For each of the 20 `OVERLAY-MANIFEST.tsv` rows, compare `preimage_sha256` with the version the v1 stack holds at that path:
  - `BASE (…)` rows: the BASE-MANIFEST row; the path must not be in Q117 or Q121.
  - `Q117 overlay 83e6322c` row: the Q117 `OVERLAY-MANIFEST.tsv` `overlay_sha256`, which must equal the Q117 archive member.
  - `NEW` rows: absent from BASE-MANIFEST, Q117 and Q121.
- Separately, each `overlay-q131a.tar` member's sha256 must equal its `postimage_sha256`.

**Summary**

| Check | Result |
|---|---|
| Archive members = OVERLAY-MANIFEST paths | **20 = 20**, same set; 0 AppleDouble, 0 non-file members; modes 0644 (19) and 0755 (`scripts/test-env/mvp6-test-mongo-env.sh`), owner 0/0 |
| Preimage = v1-stack version | **20/20 PASS** (14 × BASE, 1 × Q117, 5 × NEW) |
| Member sha256 = postimage | **20/20 PASS** |
| Q131 ∩ Q121 paths | **0** |
| Q131 ∩ Q117 paths | **1** — `CapacityRestartTests.cs`. Its Q131 preimage is the **Q117 postimage** (`overlay_sha256`), so Q131 must be copied **after** Q117. The fixed order guarantees this. |
| Tree size after Q131 | 14,567 + 5 new = **14,572** (= Q154 tree B, REPORT line 65) |
| BASE sub-layer labels (`L2 A12-360`, `L3 Auth-22`) | not re-derived: BASE-MANIFEST is the composed tree and was the reference. Q131a states them (ENV-VARS.md:55) |

**Per file**

| # | Path | Preimage layer (manifest) | Preimage | Expected (v1 stack) | Postimage = member | Result |
|---|---|---|---|---|---|---|
| 1 | `scripts/test-env/mvp6-test-mongo-env.sh` | NEW (absent in BASE-STACK v1) | — | absent in BASE-MANIFEST, Q117, Q121 | `6c936b4a84bb…` | PASS |
| 2 | `Platform/…/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataGskuCatalogLoadMongoTests.cs` | BASE (L3 Auth-22) | `cde981bc574f…` | BASE-MANIFEST `cde981bc574f…` | `28df170a41a2…` | PASS |
| 3 | `Platform/…/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataMongoResidueSweeperTests.cs` | BASE (L3 Auth-22) | `138fb66b09da…` | BASE-MANIFEST `138fb66b09da…` | `bde7a130bae8…` | PASS |
| 4 | `Platform/…/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataPublishOperationMongoTests.cs` | BASE (L3 Auth-22) | `ef4da48e39bf…` | BASE-MANIFEST `ef4da48e39bf…` | `9482272fb72e…` | PASS |
| 5 | `Platform/…/Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTenantAssignmentMongoTests.cs` | BASE (L3 Auth-22) | `22fff095a24c…` | BASE-MANIFEST `22fff095a24c…` | `08d553d83244…` | PASS |
| 6 | `Platform/…/Diten.Platform.Application.Tests/Persistence/MongoIntegrationHarness.cs` | BASE (L3 Auth-22) | `f9921485efbe…` | BASE-MANIFEST `f9921485efbe…` | `b2cc6e0807ed…` | PASS |
| 7 | `Platform/…/Diten.Platform.Application.Tests/Persistence/PlatformMongoTestConnection.cs` | NEW (absent in BASE-STACK v1) | — | absent in BASE-MANIFEST, Q117, Q121 | `6d28ba034d1a…` | PASS |
| 8 | `Platform/…/Diten.Platform.Application.Tests/Persistence/PlatformMongoTestConnectionTests.cs` | NEW (absent in BASE-STACK v1) | — | absent in BASE-MANIFEST, Q117, Q121 | `3d87f8a0e2cd…` | PASS |
| 9 | `Platform/…/Diten.Platform.Application.Tests/Schema/PlatformSchemaContractMongoTests.cs` | BASE (L3 Auth-22) | `09b563878eb6…` | BASE-MANIFEST `09b563878eb6…` | `22ae17b81218…` | PASS |
| 10 | `Platform/…/Diten.Platform.Application.Tests/Workflow/WorkflowTransitionGateMongoRepositoryTests.cs` | BASE (L3 Auth-22) | `a957522d59fd…` | BASE-MANIFEST `a957522d59fd…` | `bc5008509a77…` | PASS |
| 11 | `Platform/…/Diten.Platform.BackgroundJobs.Tests/PlatformContainerValidationTests.cs` | BASE (L3 Auth-22) | `01519158af2c…` | BASE-MANIFEST `01519158af2c…` | `57a0c8124c11…` | PASS |
| 12 | `Platform/…/Diten.Platform.Eventing.Tests/RabbitMqEventingIntegrationTests.cs` | BASE (L3 Auth-22) | `ad7ba3b99065…` | BASE-MANIFEST `ad7ba3b99065…` | `1f16c8755840…` | PASS |
| 13 | `Platform/…/Diten.Platform.Eventing.Tests/TenantLifecycleRabbitMqIntegrationTests.cs` | BASE (L3 Auth-22) | `97debd119595…` | BASE-MANIFEST `97debd119595…` | `104ba9096340…` | PASS |
| 14 | `SupplyChain/…/CapacityPlans/CapacityAtomicityTests.cs` | BASE (L2 A12-360) | `2c133ac1b6b4…` | BASE-MANIFEST `2c133ac1b6b4…` | `426a5765a1b4…` | PASS |
| 15 | `SupplyChain/…/CapacityPlans/CapacityConcurrencyTests.cs` | BASE (L2 A12-360) | `45329002c8e5…` | BASE-MANIFEST `45329002c8e5…` | `34a6bbcb1c7e…` | PASS |
| 16 | `SupplyChain/…/CapacityPlans/CapacityIsolationTests.cs` | BASE (L2 A12-360) | `a6695bfa9822…` | BASE-MANIFEST `a6695bfa9822…` | `da0f9c114cf3…` | PASS |
| 17 | `SupplyChain/…/CapacityPlans/CapacityLeaseTests.cs` | BASE (L2 A12-360) | `ae2f4932e89d…` | BASE-MANIFEST `ae2f4932e89d…` | `132eb146e491…` | PASS |
| 18 | `SupplyChain/…/CapacityPlans/CapacityRestartTests.cs` | Q117 overlay 83e6322c | `aed11cd7390d…` | Q117 overlay_sha256 `aed11cd7390d…` | `8aa66e347fe1…` | PASS |
| 19 | `SupplyChain/…/CapacityPlans/CapacityTestMongo.cs` | NEW (absent in BASE-STACK v1) | — | absent in BASE-MANIFEST, Q117, Q121 | `eeaac903e229…` | PASS |
| 20 | `SupplyChain/…/CapacityPlans/CapacityTestMongoTests.cs` | NEW (absent in BASE-STACK v1) | — | absent in BASE-MANIFEST, Q117, Q121 | `98d5d5092063…` | PASS |

## 3. Test environment that goes with v2

Source: `mvp6-q131a-port-uri-overlay-01/ENV-VARS.md` (in its SHA256SUMS; §1–§2, lines 9–37). Q154 used it and proved it (REPORT lines 74, 80, 84–85).

| Variable | Read by | Rule |
|---|---|---|
| `DITEN_PLATFORM_TEST_MONGO_URI` | every Platform test that used 27017 (MongoIntegrationHarness + classes on it, 4 × BusinessReferenceData*MongoTests, PlatformSchemaContractMongoTests, WorkflowTransitionGateMongoRepositoryTests, PlatformContainerValidationTests, the 2 RabbitMq integration tests' Mongo side) | no default; unset or invalid → the test fails closed; the URI is never echoed |
| `MVP6_MOD0192_MONGO_URI` | the 5 CapacityPlans Mongo test files (Atomicity/Fault, Concurrency, Isolation, Lease, Restart incl. its child worker) | no default; also requires `replicaSet=`; fails closed |

- **Slot rule** (`scripts/test-env/mvp6-test-mongo-env.sh`, added by Q131):
  - Mongo port = `30994 + 1000 × slot`, slots 1–9 → 31994 … 39994.
  - `--port` may override. The script refuses 27017–27021 and ports ≥ 49152, so the old 57192 is refused.
  - It prints only `export` lines (`mongodb://127.0.0.1:<port>/?replicaSet=<rs>&serverSelectionTimeoutMS=5000`, no credentials). On any error it prints nothing and exits non-zero.
  - Usage: `eval "$(scripts/test-env/mvp6-test-mongo-env.sh --slot <n> --rs <rs>)"`. Add `--all-supplychain` to also set `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `RETURNS_MONGO_URI` and `CLAIMS_TEST_MONGO` to the same lane mongod.
- **No fallback to 27017 or 57192.** A v2 tree has no hard-coded test connection to either port. With the variables unset the tests fail; they never fall back. Q154 evidence:
  - negative control and sabotage rejected 27017, an omitted port, fake credentials, a non-loopback host and `mongodb+srv` (line 80);
  - 0 new 27017 connections (line 84);
  - slots 6 and 7 ran in parallel without collision (line 85).
- **The lane mongod**:
  - a single-member replica set on loopback, on the slot port;
  - started with `--setParameter enableTestCommands=1`, because the CapacityPlans fault tests use `configureFailPoint` (ENV-VARS.md:35);
  - the kit does not yet do this (Q116 scope v2, CT-5 in the Q155 record line 43), so the Mac lane starts its own mongod, as Q154 did.

## 4. Compose recipe (same as v1 — cp only, fail-closed, never deletes — plus one Q131 step)

Run on the Mac in a new, empty folder `~/mvp6-env/<lane>/`. Never in the repo, and never inside an existing tree.

1. **Verify before use.** For each layer:
   - run `sha256sum -c SHA256SUMS` in its evidence folder (§1), then check the archive's sha256 against §1;
   - BASE: sha256(`BASE-MANIFEST.tsv`) = `a8a236de…`, and the source tree matches the manifest 14,566/14,566 with 0 extra;
   - any mismatch → **stop**.
2. **BASE.** `cp -Rc` (APFS clone), or `cp -Rp`, of the verified BASE tree into `<lane>/src`. Never modify the BASE tree itself.
3. **Each overlay, in the order Q117 → Q121 → Q131 → module draft.** For each:
   - extract the archive into a new empty staging folder `<lane>/stage/<layer>/`;
   - check that the member list equals the layer's declared list (Q117 `OVERLAY-MANIFEST.tsv`; Q121 README; **Q131 `OVERLAY-MANIFEST.tsv`, 20 rows**) and that every member's sha256 matches;
   - skip AppleDouble `._*` members and report them;
   - copy with `cp -p` from stage into `src/`, overwriting or adding;
   - never `rm`, never `--delete` or `rsync --delete`, never `git apply` in the tree;
   - any unexpected member, missing member or hash mismatch → **stop** and keep everything as it is.
4. **Q131 step (new in v2).** Before copying Q131, check every Q131 `preimage_sha256` against the file now in `src/`:
   - the 14 `BASE (…)` rows must still have their BASE-MANIFEST sha256;
   - `CapacityRestartTests.cs` must have the Q117 postimage;
   - the 5 `NEW` paths must be absent.
   - Any mismatch → **stop** (a layer was skipped or the order changed).
   - After copying, keep mode 0755 on `scripts/test-env/mvp6-test-mongo-env.sh` (`cp -p`).
5. **Verify after.**
   - Total file count: BASE + Q117 + Q121 + Q131 = **14,572**, plus the module draft's added files.
   - Every overlay path in `src/` has the sha256 of the **last** layer that writes it. For `CapacityRestartTests.cs` that is the Q131 postimage.
   - Every other path still has its BASE-MANIFEST sha256.
   - Record the result in the lane's evidence folder.
6. **Environment.** Start the lane mongod on the slot port with `enableTestCommands=1`, then set the variables with `mvp6-test-mongo-env.sh` (§3). Do not use 27017 or 57192.
7. **Clean-up.** Staging folders and failed attempts are kept, with no deletion. Build in a clone of `src/`, never in `src/` itself.

**Known exposure** (carried over from v1): BASE still holds the old F02 value in the 3 Loads probe `.py` files. Q117 replaces them, so a tree without Q117 is exposed. Q131 does not touch these files.

## 5. UC-01 working-tree files — still in no layer

- The 10 UC-01 tracked files (` M`) and the 2 untracked UC-01 files (`PlatformMongoTestConnection.cs`, `PlatformMongoTestConnectionTests.cs`) are **not part of any layer**. Q131 is defined only by `overlay-q131a.tar` and its OVERLAY-MANIFEST (OD-UC01 in `mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md`, sha256 `9e279dec…4f15f18`, line 47).
- Q157 measured the working-tree copies by sha256 only; the files were not opened for content and not changed:
  - 7 of the 12 are byte-equal to their Q131 postimages;
  - 5 differ: `PlatformContainerValidationTests.cs`, `RabbitMqEventingIntegrationTests.cs`, `TenantLifecycleRabbitMqIntegrationTests.cs`, and the 2 untracked files.
  - Equality does not make them a source.
- **Never copy them from the working tree into a stacked tree.** The authorized writer restores the working tree to HEAD at integration (Q15, OD-UC01).
- The same holds for every other working-tree edit: the stack is defined by the manifests and archives above, not by the checkout.

## 6. How to cite this stack

```text
Base stack: BASE-STACK v2 — docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md sha256 <sha256 of this file, from SHA256SUMS>
            (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → Q131 4a4a0860 → <module draft name + full sha256>)
```

- The lane checks `SHA256SUMS` in this folder and the quoted hash before composing.
- A build that deviates from the stack writes the deviation in its report.
- New Mac WPs cite v2 once CT has accepted this record. Running WPs keep v1 (§0).
