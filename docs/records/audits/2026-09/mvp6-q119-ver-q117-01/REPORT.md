# Q119 — Independent VER of Q117 (test/guard fixes on BASE) — REPORT

```text
VERIFICATION REPORT (SOP §37)

WP ID:              WP-MVP6-VER-119 · Prompt Q119 v1 · Lane VER (not the Q117 writer window)
Verifier:           read-only-auditor (/read-only-audit) + testing-agent + security-agent; Claude Code on the Mac (Darwin)
Independence:       this chat did NOT write Q117. It wrote Q103 (the BASE composition) and Q118 (records/ledgers); disclosed.
Verification date:  2026-09-27 · start ~12:40 · end 13:16 +03:00 (Europe/Istanbul)
Branch/HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (start and end)
Subject:            docs/records/audits/2026-09/mvp6-q117-test-guard-fixes-01/ · overlay 83e6322c… on BASE a8a236de…

Agent Verdict:        PASS — V1–V9 all PASS; 3 LOW observations (no rework required)
Verification Verdict: PASS (9/9)
CT Status:            returning to CT

Evidence level achieved: EXECUTED (independent apply, build and test on the Mac; forced-order race reproduction with negative control)
Required evidence level: EXECUTED (runtime of services not in scope)

Checks:
- scope:            11 files = the declared set; 0 unlisted (V3)
- build:            10 builds, 0 errors; warnings identical to Q117 (V4)
- tests:            every Q117 figure reproduced exactly (V4); N1 race reproduced on BASE and closed by the fix (V5)
- security:         skew exactly 30 s via JwtValidationDefaults in all 3 places; no other check disabled; risk LOW (V6)
- secrets:          old F02 value 0 hits in 64,266 units; scanner positive control 3/3 on BASE (V7)
- isolation:        repo, BASE and the Q117 folder unchanged; lane processes 0 (V8)
- architecture:     17/18; only MongoTestDatabaseGuard on the 2 named Platform files (V9)

Failed criteria:  none
Rework required:  no
Next gate:        CT disposition of Q117 → integration; Q121 (Mongo test-DB guard, architecture 18/18)
```

## V1–V9

| # | Check | Result | Evidence |
|---|---|---|---|
| V1 | Q117 SHA256SUMS; overlay hash; OVERLAY-MANIFEST ↔ archive | **PASS** — 12/12 OK; archive `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d`; manifest 11 rows = 11 archive files, overlay sha256 11/11, BASE sha256 11/11, bytes 11/11; no unsafe/link/AppleDouble member | `V1.txt` |
| V2 | Independent apply; FIXES.patch reproduces the overlay | **PASS** — `~/mvp6-env/q119/src` = APFS clone of BASE (no rm) + overlay extracted. A strict in-memory unified-diff applier (context must match exactly) applied FIXES.patch to the BASE files: **11/11 byte-for-byte**, 0 context mismatches. The 3 redaction markers (`<REDACTED: Q101-F02 exposed value, never reproduced>`) were swapped back in memory only; nothing with the value was written | `V2.txt`, `VER-TOOLS.tar.gz:v2.py` |
| V3 | Changed files = the declared list | **PASS** — BASE-MANIFEST vs composed tree: 1 added (`TestAssemblyBsonSetup.cs`), 10 changed, 0 removed, **0 unlisted**: 5 named (CapacityRestartTests.cs, MDM validator, HCM Program.cs, Talent Program.cs, runtime_probe.py) + 2 `.csproj` + 1 MDM test file (the "2 MDM tests" are 2 tests in `PlatformServiceTokenValidatorTests.cs`) + the initializer + 2 extra probes | `V3.txt` |
| V4 | Build 0 errors; test figures = Q117 | **PASS** — see table below | `BUILD-SUMMARY.tsv`, `TEST-SUMMARY.tsv`, `mdm-failed-q119.txt`, `RAW-LOGS.tar.gz` |
| V5 | N1 race: Capacity first — fails on BASE, passes with the fix | **PASS** — see §V5 | `ORDER-*.txt`, `TEST-SUMMARY.tsv` rows 1–2 |
| V6 | Security review of the skew changes | **PASS, risk LOW** — see §V6 | this report |
| V7 | Old F02 value: 0 hits; no new secret to disk/logs | **PASS** — see §V7 | `V7-*.txt` |
| V8 | Repo, BASE, Q117 folder unchanged; processes stopped | **PASS** — see §V8 | `V8.txt`, `P3-base-verify.txt` |
| V9 | Architecture: only MongoTestDatabaseGuard on the 2 named Platform files | **PASS** — 17/18; the failure is `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` on exactly `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs` and `…/Persistence/DisposableStandaloneMongo.cs`; both JwtClockSkew tests pass | `RAW-LOGS.tar.gz:test/build-fix--architecture.log` |

### V4 — build and tests (tree `build-fix` = BASE + overlay; lane mongod 127.0.0.1:57192 `rsmod192`, `enableTestCommands=1`)

| Suite | Q117 | Q119 | = |
|---|---|---|---|
| Build, 8 targets (SupplyChain.sln, MDM.sln, HCM Api, Talent Api, Secrets.Tests, Web.Tests(+Web), Gateway.Tests, Architecture) | 0 errors; W: 0 · 6 · 1 · 1 · 0 · 18 · 5 · 0 | 0 errors; W: 0 · 6 · 1 · 1 · 0 · 18 · 5 · 0 | ✓ |
| SupplyChain single process (default) | 407/408 | 407/408 | ✓ |
| SupplyChain single process, `xUnit.ParallelizeTestCollections=false` | 407/408 | 407/408 | ✓ |
| Per module: Shipments root · Carriers · **Loads** · Returns · Claims · S&OP · Capacity | 74 · 36 · **33** · 78 · 128/129 · 19 · 39 | 74 · 36 · **33** · 78 · 128/129 · 19 · 39 | ✓ |
| Architecture | 17/18 | 17/18 | ✓ |
| Gateway · Web · Secrets | 87 · 158 · 5 | 87/87 · 158/158 · 5/5 | ✓ |
| MDM Application.Tests | 490/496 | 490/496 — the **same 6** failing tests as Q117 BASE and B (LSKU register ×3, product item/SKU manifest ×3); validator tests 33/33 | ✓ |

In every SupplyChain run, the one failure is `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`. It is excluded by design ("explicit write/read restart mode required"). The WP's "407/407" is the same measurement counted over runnable tests, as CT records in `mvp6-ct-verdicts-q118-q64d-q117-2026-09-27.md` A3.

### V5 — N1 race proof

The test assembly already runs collections serially (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`,
`Loads/LoadContractTests.cs:29`). The order therefore comes from xUnit's default collection orderer, and that order varies between build trees.
This fits Q103's tree running Capacity first while Q117's did not; it is an inference, not a measurement. To remove the dependence, a **VER-only harness**
(`Q119VerCapacityFirstOrderer.cs`, in `VER-TOOLS.tar.gz`; never to be integrated) was added **identically** to both trees. It is an
assembly `TestCollectionOrderer` that runs `CapacityMongo` first, then the other CapacityPlans collections, then the rest in their default
order, and it logs the order it applied.

| Tree | Forced order (main run, 39 collections) | Result |
|---|---|---|
| `build-base-ordered` = plain BASE + harness (**negative control**) | positions 1–4: CapacityMongo, CapacityContract, CapacityLifecycle, CapacityReplay | **264/408 FAIL** — 143 × `BsonSerializationException: There is already a serializer registered for type Guid.` + the 1 by-design exclusion: **exactly Q103's figure and signature** |
| `build-fix-ordered` = BASE + Q117 overlay + harness | identical to the above (first 39 lines of both logs equal) | **407/408 PASS** (only the by-design exclusion) |

The extra single-collection lines in the order logs come from the child test processes that `CapacityRestartTests` starts. Each
child runs only `CapacityMongo` (BASE 3, fix 2). They do not change the main order.
The negative-control tree leaves out the 3 Loads probe `.py` files, the only files that hold the old F02 value. This keeps the value out of `~/mvp6-env/q119` (V7).
No .NET test references them (grep: 0).

### V6 — security review (security-agent)

- **Exactly 30 s via the shared constant.**
  - `JwtValidationDefaults.ClockSkew = TimeSpan.FromSeconds(30)` (`Diten.BuildingBlocks.Security.Secrets/JwtValidationDefaults.cs:50`).
  - HCM `Program.cs:33` and Talent `Program.cs:32`: `ClockSkew = JwtValidationDefaults.ClockSkew`; before, both were `TimeSpan.Zero`.
  - MDM `PlatformServiceTokenValidator.cs`: `ClockSkew = JwtValidationDefaults.ClockSkew` for lifetime, and `(long)JwtValidationDefaults.ClockSkew.TotalSeconds` (= 30) in the `iat` check.
  - No literal remains; JwtClockSkew guards 2/2 pass.
- **Nothing else disabled.** The diff touches only those skew expressions (plus a `using` line and the two `.csproj` references).
  - HCM and Talent still set `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime` and `ValidateIssuerSigningKey = true`.
  - MDM still has, unchanged: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `RequireExpirationTime` and `ValidateIssuerSigningKey`; `alg == HS256`; `kid`; exactly one audience; `sub`; `scope`; tenant/actor/legal-entity; non-empty `jti`; `iat == nbf`; `exp > iat`; lifetime ≤ `MaximumTokenLifetimeSeconds` (5–60); and the ≥ 32-byte secret check.
  - Executed: validator 33/33 (wrong signing key, missing and extra audience, excessive lifetime, expiry and nbf outside the skew, duplicate or missing claims, and more).
- **Risk: LOW.** The change is owner-approved and aligns the three validators with the BL-296 standard. What it widens:
  - HCM and Talent accept a token up to 30 s after `exp` (before: 0 s).
  - An MDM service token is accepted up to 30 s after `exp` and with `iat` up to 30 s in the future (before: the configured 0–10 s).
  - The worst-case lifetime of an MDM token is 60 s + 30 s.
  - The replay window for a captured token grows by the same amount; there is no `jti` replay cache, which is pre-existing.
- **Observations (LOW, no rework):**
  - **O1** `PlatformServiceIdentityOptions.ClockSkewSeconds` is now inert: it is range-checked (0–10) but no longer used. An operator who sets it gets 30 s. The MDM tests still pass `clockSkewSeconds: 5`, which is misleading. Remove or document the option.
  - **O2** The MDM validator tests have no negative case for a wrong **issuer**, `kid` or `alg`. Those checks are unchanged code, so this is a pre-existing coverage gap.
  - **O3** The probes still hand the signing value to the child API through its process environment. This mechanism is unchanged and not written to disk.

### V7 — F02

- **Old value.** The value was held in memory only and never printed. The scanner checked 9 forms: raw, URL-encoded ×2, and base64 and base64url at 3 alignments.
  - It covered every file, and every member of each `.tar.gz`/`.gz`, in:
    - the Q117 folder (overlay and RAW-LOGS included);
    - the whole `~/mvp6-env/q119` tree (src, 3 build trees with bin/obj, all logs and TRX);
    - this evidence folder (re-scanned after it was written).
  - Result: **64,266 units, 0 hits**.
  - **Positive control:** the same scanner on the BASE `tests/loads` folder finds it in exactly the 3 probes (expected).
- **New code.**
  - `SECRET` = `MOD0185_PROBE_JWT_SECRET` or `secrets.token_urlsafe(48)`. It is used only for HMAC token signing and as `JwtSettings__Secret` in the child API's environment.
  - No `print`, `write_text` or `json.dump` touches it. Evidence records only `authorizationPresent` (a boolean) and non-secret headers.
  - Q117 changed no output sink: 0 changed lines touch evidence, print or capture code.
  - Without the env value, an external API now stops with a message that does not echo any value.

### V8 — no change, processes

- Repo:
  - `feature/mvp6-logistics` @ `4a8d4d4b…`, no `index.lock`, 19 diff paths, `git diff` sha256 `a705e73f…` = preflight.
  - The only new untracked items are this folder and 2 items from **other writers**: `mvp6-claims-ui-draft-03/` (13:12) and `mvp6-ct-verdicts-q118-q64d-q117-2026-09-27.md` (12:44).
- BASE:
  - `BASE-MANIFEST.tsv` = `a8a236de…`.
  - `src` verified 14,566/14,566 before and after this lane, with 0 writable files.
- The Q117 folder is still 12/12.
- Processes:
  - The lane mongod (57192, pid 76249) was shut down; 0 listeners remain.
  - 0 processes carry this lane's environment (`MOD0183_TEST_MONGO`/`Q119_ORDER_LOG`/`mvp6-env/q119`).
  - **7 MSBuild node processes** (started 13:01:56–58, parent launchd, `/nodeReuse:true`) carry **none** of this lane's variables. Every process this lane started inherits them, and `ps` shows environments on this host. They were attributed to a parallel lane and **left untouched**.
- No `rm` was used.
  - The workspace `~/mvp6-env/q119/` (src read-only, build-fix, build-fix-ordered, build-base-ordered, logs, test-mongo) is kept.
  - The only in-place changes were to Q119's own clones: the overlay extraction and the harness copies.

## Files

`REPORT.md`
`V1.txt` `V2.txt` `V3.txt` `V7-positive-control-base-loads.txt` `V7-scan-q119-tree-and-q117-folder.txt` `V8.txt` `P3-base-verify.txt`
`BUILD-SUMMARY.tsv` `TEST-SUMMARY.tsv` `ORDER-build-base-ordered.txt` `ORDER-build-fix-ordered.txt` `mdm-failed-q119.txt`
`VER-TOOLS.tar.gz` — the harness `.cs`, `build.sh`, `test.sh`, `v2.py`, `v7scan.py` (D6: no open `.cs` under docs)
`RAW-LOGS.tar.gz` — every build/test log and TRX
`SHA256SUMS`

No fixes were made. Q117 and BASE are unmodified.

Agent PASS ≠ CT ACCEPTED — returning to CT.
