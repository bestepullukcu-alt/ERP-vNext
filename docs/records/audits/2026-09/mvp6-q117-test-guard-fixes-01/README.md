# Q117 — test/guard fixes on BASE (N1, N2 ×3, F02) — SOP §22 report

| Field | Value |
|---|---|
| WP / prompt / lane | WP-MVP6-FIX-117 · Q117 v1 · DEV (orchestrator → testing-agent, security-agent, code-quality review) |
| Authority | owner decisions N1 = A, N2 = B, F02 → Q117 (`mvp6-ct-verdicts-q115-q64c-q103-2026-09-26.md` §4) + three owner answers during this lane (§3) |
| Host | Claude Code in the owner's macOS terminal (Darwin), .NET SDK 8.0.417, mongod 8.0.18 |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, READ-ONLY; diff set 19 paths unchanged; no index.lock; no git write |
| BASE | `~/mvp6-env/base/src` = BASE-MANIFEST `a8a236de…` — **14,566/14,566 files verified, 0 extra** before work; base never modified |
| Workspace | `~/mvp6-env/q117/` (`src` = APFS clone of BASE + fixes; `build-base`, `build-A`, `build-B` = writable build clones; no `rm`) |
| Start / end | 2026-09-26T23:57:30+03:00 / 2026-09-27T02:07+03:00 (FINAL-CHECK.txt) |

## 1. Verdict (agent)

**PASS with scope amendments approved by the owner during the lane.** N2 and F02 are fixed as required; N1 is fixed and its real
root cause measured and closed. Architecture 15/18 → **17/18** (the remaining failure is pre-existing guard debt outside scope, §5).
SupplyChain single-process **407/408** (the 1 = the by-design restart test). Loads **33/33**. Secret scans **0 hits**.
Evidence level (SOP §32.0): executed build/test on the Mac; runtime (services) not in scope. **Agent PASS ≠ CT ACCEPTED — returning to CT.**

## 2. Deliverables (D6: archives only)

| File | sha256 |
|---|---|
| `q117-fixes-overlay.tar.gz` — 11 files at repo-relative paths (10 modified, 1 added) | `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` |
| `OVERLAY-MANIFEST.tsv` — path, BASE sha256, overlay sha256, bytes, change | `3675b2c4ac706cb248cb01544edcbc8092d0e905f87044564667de77decda3f4` |
| `FIXES.patch` — unified diff vs BASE, a `# REASON` line per file, 19 hunks | `25eb7c875e551826d76d116851965634324c3e2f5e791813de0f58e67e5462f8` |
| `RAW-LOGS.tar.gz` — build/test logs + TRX (BASE, stage A, stage B), lane scripts, N1 repro source | `5e9cab35b0dd3025868d5a0f430f054436bc9bc5e80f0cad277fa1499bc172ea` |
| `BUILD-SUMMARY.tsv`, `TEST-SUMMARY.tsv`, `TEST-SUMMARY-before-and-stageA.tsv`, `N1-REPRO-OUTPUT.txt`, `SECRET-SCAN-EXACT-F02.txt`, `SECRET-SCAN-PATTERN.txt` | in SHA256SUMS |

`FIXES.patch` fidelity: applied to copies of the BASE files it reproduces all 11 overlay files byte-for-byte (`patch` rc 0). In the
three Loads-probe hunks the **removed** lines carry the exposed F02 value; there it is replaced by a redaction marker, so those hunks
apply only after the marker is swapped back in memory (that is how the fidelity check ran — the value was never written).

## 3. Files changed (11) and why

| # | File | Change | Scope |
|---|---|---|---|
| 1 | `services/Diten.SupplyChainService/tests/…/CapacityPlans/CapacityRestartTests.cs` | lines 32–33: `RegisterSerializer` → `TryRegisterSerializer` (guarded like Persistence) | named (N1) |
| 2 | `services/Diten.SupplyChainService/tests/…/TestAssemblyBsonSetup.cs` (**new**) | `[ModuleInitializer]` runs the product's `AddPersistence` serializer registration before any test | **owner-approved in Q117** (N1 root cause) |
| 3 | `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs` | `ClockSkew = JwtValidationDefaults.ClockSkew`; `iat` check uses the same constant | named (N2) |
| 4 | `services/Diten.MdmService/tests/…/PlatformServiceTokenValidatorTests.cs` | 2 tests: offsets beyond 30 s (nbf +40 s; expired −40 s) | **owner-approved in Q117** |
| 5 | `services/Diten.HumanCapitalService/src/Diten.HumanCapitalService.Api/Program.cs` | `TimeSpan.Zero` → `JwtValidationDefaults.ClockSkew` | named (N2) |
| 6 | `…HumanCapitalService.Api/Diten.HumanCapitalService.Api.csproj` | + ProjectReference `Diten.BuildingBlocks.Security.Secrets` | **owner-approved in Q117** |
| 7 | `services/Diten.TalentEcosystemService/src/Diten.TalentEcosystemService.Api/Program.cs` | as #5 | named (N2) |
| 8 | `…TalentEcosystemService.Api/Diten.TalentEcosystemService.Api.csproj` | as #6 | **owner-approved in Q117** |
| 9 | `services/Diten.SupplyChainService/tests/loads/runtime_probe.py` | static value removed; per-run value (`MOD0185_PROBE_JWT_SECRET` or `secrets.token_urlsafe(48)`); external API without it stops | named (F02) |
| 10 | `…/tests/loads/failure_probe.py` | uses `runtime_probe.SECRET` instead of the same literal | F02 — same exposed value (K4, no half fix) |
| 11 | `…/tests/loads/restart_probe.py` | as #10 | F02 — same exposed value |

Owner answers during the lane (question tool in this Claude Code session; exact times not recorded):

1. MDM skew → **"Shared 30 s everywhere"** (lifetime and `iat`); S2S tolerance widens from the configured ≤10 s to 30 s.
2. HCM/Talent `.csproj` references → **yes** (both services move from `TimeSpan.Zero` to the shared 30 s, as BL-296 standardised).
3. After measuring that MDM's own tests pin a 5 s skew → **"Shared 30 s + adjust the 3 tests"** (only 2 needed a change:
   `Future_iat…` is rejected by the `iat == nbf` rule regardless of skew; `Expired_token_inside…` (−2 s) still holds).
4. After measuring the N1 root cause → **"Guard + test assembly init"**.

Files 10–11 were not asked about: the old value sat verbatim in both (`JwtSettings__Secret` of the API they start). Replacing it in one
file only would leave the exposed value in two (SOP K4). Flagged here for CT.

## 4. N1 — what Q103 measured vs. what the cause is

- Q103: single-process 264/408, 143 × "already a serializer registered for type Guid", attributed to `CapacityRestartTests.cs:32`.
- Measured here: line 32 runs only in the **child** processes (`MOD192_CHILD_MODE` is set only in `StartWorker`); in the parent
  `ChildWorker` returns at once. The exception is thrown by `Persistence/DependencyInjection.cs:22` itself. **Cause:** BSON serializers
  are process-wide; if any test looks up the Guid serializer before the first `AddPersistence` (a typed collection does — the Capacity
  tests never call `AddPersistence`), every later `AddPersistence` throws. Q103 run 3 started with **35 Capacity tests** (TRX start order);
  my runs started with Carriers/Sandop/Loads, so the unmodified BASE passed **407/408** here in both parallel and serial runs — the
  264/408 is **order-dependent** and could not be reproduced by ordering alone.
- Deterministic repro (`N1-REPRO-OUTPUT.txt`, source in RAW-LOGS `lane-tools/repro/`): `persistence-first` OK; **`lookup-first` →
  `BsonSerializationException: There is already a serializer registered for type Guid.`**; `init-then-lookup` OK.
- Fix: file #2 makes the persistence registration happen first in every test process; file #1 keeps the child worker from throwing
  once that registration exists (without it, the child would now fail).

## 5. Before → after

Before-values: BASE runs in this lane where marked "(Q117)"; otherwise Q103 on the same BASE `a8a236de…`.

| Suite | Before | After (stage B = all fixes) |
|---|---|---|
| Build (0 errors required) | MDM 6 W, HCM 1 W, Talent 1 W (Q117) · SupplyChain 0 W (Q103) | **0 errors** in 8 targets; MDM 6 W, HCM 1 W, Talent 1 W, SupplyChain 0 W, Secrets.Tests 0 W, Web+Web.Tests 18 W (= 15 + 3), Gateway.Tests 5 W, Architecture 0 W — no new warning |
| SupplyChain single process (parallel) | 264/408 (Q103, order-dependent) · 407/408 (Q117, this machine) | **407/408** |
| SupplyChain single process (collections serial) | 407/408 (Q117) | **407/408** |
| Stage A (line-32 guard only) | — | 407/408 (does not address the race — §4) |
| SupplyChain per module | 74 · 36 · **33** · 78 · 128/129 · 19 · 39 (Q103) | 74 · 36 · **33 (Loads)** · 78 · 128/129 · 19 · 39 — the 1 = `ClaimReplayTests…DurableRecovery` (by design, explicit restart mode) |
| Architecture | 15/18 (Q103) | **17/18** — JwtClockSkew ×2 now pass; `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` remains |
| Gateway | 87/87 (Q103) | 87/87 |
| Web | 158/158 (Q103) | 158/158 |
| Secrets building block | 5/5 (Q103) | 5/5 |
| MDM Application.Tests | 490/496; validator 33/33 (Q117) | 490/496; validator **33/33** — same 6 unrelated failures (product item/SKU master) before and after |
| F02 probes | static literal in 3 files | per-run value; compile OK; tokens verify with the per-run value; two imports differ; lane env value honoured; old value in 0 files |

**MongoTestDatabaseGuard — cause, not fixed (outside scope):** `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs:79`
and `…/Persistence/DisposableStandaloneMongo.cs:25` name a database with a fresh `Guid` per run (each starts its own disposable mongod).
Both exist on HEAD and come from the Auth 22 layer. A fix is either a guard licence (`KnownPerRunDatabase` + reason — a guard-owner
decision) or a Platform test rewrite — neither is "test-config-only inside this scope".

## 6. Security evidence

- **F02:** exact-value scan (value held in memory, raw/base64/base64url/URL-encoded) over this folder, every archive member, `~/mvp6-env/q117/src`
  and `build-B` → **29,208 units, 0 hits** (`SECRET-SCAN-EXACT-F02.txt`). K08 pattern scan of this folder → **0 hits / 77 units**.
  The old value was never printed, logged or written by this lane. It remains in BASE (`~/mvp6-env/base/src`, read-only) and in the
  repo working tree (`services/Diten.SupplyChainService/tests/loads/{runtime,failure,restart}_probe.py`) until this overlay is
  integrated — **treat it as exposed**. Measured use: only as the JWT signing value of API instances started by these three probes; before the fix it occurred in no other file of the BASE tree (exact scan of `~/mvp6-env/q117/src` found it only in the three probes).
- **N2 behaviour change (owner-approved):** HCM and TalentEcosystem accept tokens up to 30 s after expiry (was 0 s); the MDM
  Platform→MDM service-token validator allows 30 s skew (was the configured 0–10 s) for lifetime and `iat`. Tokens there still live
  5–60 s. `ClockSkewSeconds` now only feeds the option range check.
- No new permission, endpoint or product route. No product code touched outside files 3, 5, 7 (and the two `.csproj`).

## 7. Isolation, processes, no-change

- Tests used a lane mongod `127.0.0.1:57192/rsmod192` (`enableTestCommands=1`, dbpath `~/mvp6-env/q117/test-mongo/`), shut down at the end.
  First start failed (relative pid path under `--fork`); restarted with absolute paths. Operational 27017 (pid 841) untouched.
- `~/mvp6-env/base` unchanged (mode and content; the copy's 10 edited files were made writable in the copy only).
- No dotnet/testhost/MSBuild/mongod process of this lane remains. `~/mvp6-env/q117` kept (no `rm`).

## 8. ASSUMPTIONs

- **A1** "Guard like the persistence setup" (N1) is met with `TryRegisterSerializer`, the driver's idempotent registration (MongoDB.Bson 2.27.0);
  the persistence `lock`/`_configured` flag is private and could not be shared from test code.
- **A2** The module initializer calls the product's `AddPersistence` with a dummy connection string; the service provider is never built,
  so no client connects (MongoTestDatabaseGuard and JwtClockSkew guards unaffected: 17/18).
- **A3** Before-values for gateway/web/secrets/architecture and the per-module split are Q103's (same BASE hash); SupplyChain single-process
  and MDM were re-measured here.

## 9. To-do (for CT)

1. Accept or reject the four owner-approved scope amendments (§3) and the two extra F02 files.
2. MongoTestDatabaseGuard: guard-owner decision (licence with reason vs. Platform test rewrite).
3. Q119 independent VER (different chat): re-apply the overlay on BASE, verify hashes, re-run §5; try to reproduce 264/408 on BASE by
   ordering Capacity first (e.g. `--filter` runs in one process) and confirm the initializer closes it.
4. Integration: the exposed value is still in the repo working tree and BASE until this overlay is integrated.

Agent PASS ≠ CT ACCEPTED — returning to CT.
