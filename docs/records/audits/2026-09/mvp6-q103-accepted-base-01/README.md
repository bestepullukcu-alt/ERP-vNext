# Q103 — MVP6 accepted base composition (isolated) — README

WP-MVP6-INT-103 · Prompt Q103 v1 · Lane INT (environment, single) · integration-agent + testing-agent `/test`
Authority: owner decision D1 (`docs/records/decisions/2026-09/mvp6-q101-fix-decisions-owner-decision-01.md`); Q101 F01.
Host: Darwin (macOS), `/Users/natig/.dotnet/dotnet` 8.0.417, mongod 8.0.18. Run 2026-09-26 ~23:05–23:45 +03:00.
Branch/HEAD: `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (start and end).

```text
BASE-HASH  a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661
           = sha256(BASE-MANIFEST.tsv) · 14566 files · composed tree at ~/mvp6-env/base/src (read-only)
```

## 1. Composition (order per D1 and SOURCE-CLOSURE-PLAN)

| Layer | Input | Full sha256 (verified before extract) | Files | Check |
|---|---|---|---:|---|
| L0 | `git archive HEAD` → `head.tar` | `96ae026a23c64bde643fb62147c83b568d511137d449dfaf3671268a4ba8ba2f` (= Q97 L0) | 14251 | — |
| L1 | `mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` | `ebd5d80ca00541235868b94ed9917a53fb63be6e15aa78d0450fa90f33737064` | 422 | SOURCE-MANIFEST (`dec28b6a…`) 422/422 |
| L2 | `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` (prefix `mvp6-shipment-a12-rework-src/` stripped) | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` | 360 | SUCCESSOR-360 (`8ffa6c96…`) 360/360 |
| L3 | `mvp6-carrier-numericdate-exec-01/final-source.tar.gz` | `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` | 3333 | FINAL-22 (`b9713185…`) 22/22 |

- **No UI draft overlay** is in the base (no Claims draft L3/L4 as in Q97).
- AppleDouble (`._*`) metadata members were skipped, not extracted: A12 503, Auth 3333, BC 0 (Q101 already notes AppleDouble in archives). None is in any manifest.
- L3 overlaps BC on 81 `Diten.Building.Blocks` paths; the content is **identical** (0/81 changed). L3 overlaps A12 on 0 paths.
- BC ∩ A12 = 272 paths; A12 differs from BC on 6 (the accepted successors, incl. the 3 PRESERVE_SUCCESSOR rows).
- Cross-check against the Q97 composition (`mvp6-q64b-claims-build-01/compose/SOURCE-MANIFEST.tsv`): equal on all 14566 shared paths
  except the 9 Q97 L4 env-integration files (Claims routes and resx); Q97 has 23 extra Claims-draft files. BC therefore adds no content
  beyond HEAD + A12 + Auth; its 69 files that stay at L1 equal HEAD (PRESERVE_IDENTICAL).
- Post-compose: **360/360** · SupplyChain `Program.cs` = `33027bcd65b7274eda322578da15ef7fa9b9ce6d6d9ef75fe01eb8bc25b29752` ✔.

## 2. Base vs repo working tree (`WORKTREE-DIFF.tsv`)

| Scope | Same | Differs | Only in base |
|---|---:|---:|---:|
| 360 accepted files | 92 | **53** | 215 |
| All 14566 base files | 14263 | 76 | 227 |

The 360 split equals Q101 F01 exactly. Outside the 360, the differences are: 13 L0-HEAD paths where the working tree has uncommitted
edits (the 19-path diff), plus 10 differing and 12 base-only files from Auth 22 (Auth/MDM/Platform). Files that exist only in the working tree are
not listed; the base is defined by the manifest, not by the checkout.

## 3. Build (`BUILD-SUMMARY.tsv`, raw logs in `RAW-LOGS.tar.gz:build/`)

Built in a copy (`~/mvp6-env/base/build-tree`), `--disable-build-servers -p:UseSharedCompilation=false`. **0 errors in all 10 targets.**

| Target | Warnings |
|---|---:|
| SupplyChainService.sln | 0 |
| AuthService.sln | 6 |
| Platform.sln | 122 |
| MdmService.sln | 6 |
| PpmService.sln | 0 |
| BuildingBlocks.Security.Secrets.Tests | 0 |
| Diten.Web.sln | 15 |
| Diten.Web.Tests | 3 |
| Diten.ApiGateway.Tests (+ gateway) | 5 |
| TenantArchitecture.ArchitectureTests | 0 |

Warning codes are nullable (CS86xx), obsolete API (CS0618), async-without-await (CS1998) and similar; there are no new classes of warning.

## 4. Tests (isolated Mongo only)

Lane mongod: `127.0.0.1:57192`, replica set `rsmod192` (the port and name that CapacityPlans tests hard-code), dbpath under
`~/mvp6-env/base/test-mongo/`. All suite env vars (`MOD0183/0184/0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`, `RETURNS_MONGO_URI`,
`CLAIMS_TEST_MONGO`) point there. Operational 27017 was not used. 57191 (Sandop's deliberate unreachable port) stayed empty.

| Suite | Result |
|---|---|
| SupplyChain — per-module processes (`TEST-SUMMARY-split.tsv`), mongod with `enableTestCommands=1` | **407 / 408 passed** (see below) |
|  · Shipments root (0183) | 74/74 |
|  · Carriers (0184) | 36/36 |
|  · **Loads (0185)** | **33/33** |
|  · Returns (0186) | 78/78 |
|  · Claims (0187) | 128/129 — 1 = `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, by design "exclude from ordinary suite" (needs explicit restart mode) |
|  · S&OP (0190) | 19/19 |
|  · Capacity (0192) | 39/39 |
| SupplyChain — single process, run 1, mongod **without** test commands | 389/408; 18 × `configureFailPoint` not available (environment) + the 1 by-design exclusion |
| SupplyChain — single process, runs 2 and 3, with test commands | 264/408 both times; 143 × `BsonSerializationException: already a serializer registered for type Guid` + 1 exclusion → **finding Q103-N1** |
| Architecture (incl. DocsPathGuard, MongoTestDatabaseGuard) | 15/18. DocsPathGuard **passes**. 3 fail: see Q103-N2 |
| Architecture on a HEAD-only baseline tree | 15/18: the **same 3 tests fail on HEAD** |
| Gateway tests | 87/87 |
| Web tests | 158/158 |
| Secrets building block tests | 5/5 |

TRX and console logs for every run are in `RAW-LOGS.tar.gz` (`test/`, `test/split/`).

## 5. New findings (for CT)

- **Q103-N1 (MEDIUM, test isolation).** `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/CapacityPlans/CapacityRestartTests.cs:32`
  (accepted A12-360 file) calls `BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String))` without the `_configured`
  guard used by `Persistence/DependencyInjection.cs:22`. In one test process, any later `AddPersistence` then throws, and 143 tests fail.
  This is deterministic with fail points enabled (2/2 runs). Per-module processes are all green. This is a test-code defect, not a product regression.
  A full single-process run (K18-style) cannot pass until it is fixed.
- **Q103-N2 (LOW/MEDIUM, guard debt).** Architecture guards on the base: MongoTestDatabaseGuard (2 Platform test files) and
  JwtClockSkewGuard ×2 (HumanCapital + TalentEcosystem `Program.cs`) already fail on **HEAD**. The composition adds one offender:
  `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs`, an **accepted FINAL-22 file**
  that validates lifetime without `JwtValidationDefaults.ClockSkew`.

## 6. Isolation, no-change and hygiene

- Repo: branch/HEAD unchanged, no `index.lock`, the 19 diff paths unchanged (`git diff` sha256 `a705e73f…` before = after), no git writes.
  The only repo write is this folder. During the run, two untracked items from **other writers** appeared (not this lane):
  `mvp6-claims-ui-draft-02/` (23:28) and `mvp6-ct-verdicts-q93-q95-q102-q97-2026-09-26.md` (23:17).
- Q101 F02: the Loads `runtime_probe.py` static secret is **not** in this folder (fixed-string scan of every file = 0 hits).
- No product source is copied here. `scripts/` holds the lane's compose, build and test scripts only.
- Processes: the lane mongod (57192) was shut down cleanly. No dotnet, testhost or MSBuild process is left. The pre-existing system mongod (pid 841)
  was not touched.
- Workspace deviations: two compose attempts stopped fail-closed. Attempt 1 hit an AppleDouble member and attempt 2 hit a headerless manifest.
  Both partial trees are kept in `~/mvp6-env/base/attempts/`. The compose script removes its own temporary extraction dirs
  (`work/x-*`) after each layer.

## Files

`BASE-MANIFEST.tsv` · `BASE-HASH` · `COMPOSE-LOG.txt` · `LAYERS.tsv` (per path: final layer + history) · `WORKTREE-DIFF.tsv` ·
`BUILD-SUMMARY.tsv` · `TEST-SUMMARY-run1.tsv` · `TEST-SUMMARY-split.tsv` · `RAW-LOGS.tar.gz` · `scripts/` · `SHA256SUMS`

Agent PASS ≠ CT ACCEPTED — returning to CT.
