# Q202a — Integration write (BASE-STACK v2 → current working tree) · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q202a · `AL-SCM-INTEGRATION-WRITE` (INT) · required E2 — **reached: E1 only.** The write is done and hash-verified; the build and the tests were **not run** (F-Q202a-1). E2 is not claimed. |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent`. **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** Mandatory fields supplied by CT in the prompt. |
| Authority | OD-INTEGRATE-CURRENT (per the prompt); Q201 `ADD-OVERWRITE-CONFLICT.tsv` as the selection |
| Read first | `AGENTS.md` · `devops-agent.md` · `configuration-safety.md` · `git-safety.md` (GIT-002) · BASE-STACK v2 + `LAYERS.tsv` · Q201 `SOP-22.md`, `ADD-OVERWRITE-CONFLICT.tsv`, `Q131-APPLICATION-STATE.tsv` |
| Base stack | BASE-STACK v2 sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` |
| Start / End (Europe/Istanbul) | 2026-10-02 18:01:08 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        WRITE DONE (294 files, 294/294 hash-verified). BUILD AND TESTS NOT RUN. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched, start and end)
Worktree status:      start 32 " M" · 533 "??" · 0 staged = 565
                      end   89 " M" · 576 "??" · 0 staged = 665   (BASELINE-BEFORE-AFTER.md)
Single writer:        no other session running at start (session list: 0 running; no file modified in the prior
                      15 min; no dotnet/msbuild process; no .git/index.lock)
Reversibility:        Q198 backup validated before the first write (below)
Changed files:        the 294 in WRITTEN-FILES.tsv + this record folder. Nothing staged, nothing committed.
Tests:                NOT RUN
Build:                NOT RUN
Blockers:             F-Q202a-1
Out-of-scope changes: none
```

## 1. Preconditions

| Check | Result |
|---|---|
| Q198 record `SHA256SUMS` | 3/3 OK, including `~/Backups/ERP-vNext-recovery-2026-10-02.tar.gz` = `11908716e818bea901ee27d3aa811f0a61572fcd3c4981ab53d501c50fb49542` (1,745,856,694 bytes; re-hashed now) |
| BASE `BASE-MANIFEST.tsv` | `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` OK (folder SHA256SUMS 14/14 OK) |
| Q117 archive | `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` OK |
| Q121 archive | `93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90` OK |
| Q131 archive | `4a4a0860954f437265e21b90a4c028ac282a7444343b5d6d8ea8fa31b7434acf` OK |
| BASE content, L2 A12-360 `successor-source.tar.gz` | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` OK (F-Q202a-3) |
| BASE content, L3 Auth-22 `final-source.tar.gz` | `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` OK (F-Q202a-3) |
| Selection count | **298** = 231 ADD + 62 OVERWRITE + 5 record-decided CONFLICT |
| Working tree unchanged since Q201 | 298/298: each path's current sha256 = Q201 `worktree_sha256` (or absent for ADD) |

## 2. Method

`tools/q202a.py` (the exact program; plan mode first, then `--write`).

- Archives read with Python `tarfile` in memory. AppleDouble `._*` members ignored. Nothing extracted to disk
  except the files written.
- Before each write: member sha256 = Q201 `postimage_sha256`; for BASE rows also = the `BASE-MANIFEST.tsv` row.
- After each write: on-disk sha256 = postimage. The run stops at the first mismatch. There was none.
- Order: BASE (L2, then L3) → Q117 → Q121 → Q131. Each path is written once, with the postimage of the last
  layer that owns it (Q201 gives one row per path).
- Mode: 0755 for `scripts/test-env/mvp6-test-mongo-env.sh`, as in the archive. All other members are 0644.
- No deletion. No git write.

## 3. Result of the write

| | Count |
|---|---:|
| Written and verified (`WRITTEN-FILES.tsv`, result `OK`) | **294** |
| Held — in the 298 but in a forbidden area (F-Q202a-2) | 4 |
| SKIP list | 15 |
| Left for others (MOD-0183 pack) | 1 |

`scripts/test-env/mvp6-test-mongo-env.sh`: present, mode `-rwxr-xr-x`, sha256
`6c936b4a84bb89a27115190c2ea0c87488e690e8ef7dcf1e383ffaeff54d13b1` ✔.

## 4. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q202a-1** | 🔴 Blocker for E2 | **The build and the tests were not run.** After the write, the `dotnet build` command for the SupplyChain solution was denied by the session's permission layer (auto-mode classifier; no reason given). It was not retried and not worked around. So it is **not known** whether the SupplyChain service compiles, whether the suite still stands at 138/1, or whether the Platform test projects build. The working tree is in a written-but-unproven state. | `BUILD-AND-TEST-AFTER.tsv` |
| **F-Q202a-2** | 🟠 High (prompt contradiction) | The 298 contain 4 paths the same prompt forbids: `gateway/Diten.ApiGateway.Tests/CarrierRouteCoverageTests.cs` (ADD), `…/ShipmentRouteCoverageTests.cs` (ADD), `…/OcelotConfigurationTests.cs` (OVERWRITE), `.antigravity/rules/ports.md` (OVERWRITE). "298 or STOP" and "do not touch gateway/** · .antigravity/**" cannot both hold. Put to the owner before any write; owner decision in session: **write 294, hold the 4.** | `SKIPPED-PATHS.tsv` rows `HELD` |
| **F-Q202a-3** | 🟡 Medium (prompt wording) | The prompt names "four source archives", but BASE is a manifest, not an archive. The bytes for the 271 written BASE files came from the two BASE inputs named in the Q103 README §1: L2 `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` and L3 `mvp6-carrier-numericdate-exec-01/final-source.tar.gz`. Both archive hashes match the Q103 README; every member used equals its `BASE-MANIFEST.tsv` row. The composed tree `~/mvp6-env/base/src` was not used. | `tools/q202a.py` `ARCH`; `mvp6-q103-accepted-base-01/README.md:14-17` |
| **F-Q202a-4** | 🟠 High (risk, unverified) | The 193 SupplyChain files (incl. `Diten.SupplyChainService.Api.csproj`) are now at the stack version, while `Program.cs` stays at the working-tree version (`7fdb5ef0…`), not the BASE version (`33027bcd…`). Whether this mix compiles is exactly what the missing build would have shown. | Q201 F-Q201-3; F-Q202a-1 |
| **F-Q202a-5** | 🟡 Medium | The 3 held gateway test files and the untouched `ocelot.json` stay consistent with each other (old tests, old routes). They must move together in the integration-agent lane. | — |
| **F-Q202a-6** | ⚪ Low | The write reaches beyond SupplyChain: 14 commercial-suite module packs, `AGENTS.md`, 10 `scripts/smoke-mod016x-*.ps1`, `watch-diten*.ps1`, 5 CrmService, 8 AuthService, 4 MdmService, 14 Platform, 2 `.csproj` in HCM/TEP (Q117). All are Q201 OVERWRITE/ADD rows with a clean preimage; listed so nobody is surprised by the porcelain. | `WRITTEN-FILES.tsv` |
| **F-Q202a-7** | ⚪ Low | Disk: 3.1 GiB free, volume at 100%. A full build of several solutions may not fit. | `df -h ~` |

## 5. Rollback

Not attempted, per the prompt. Q198 is the rollback path: `mvp6-q198-backup-01/REPORT.md` "How to restore".
For a single file, `WRITTEN-FILES.tsv` gives the pre-write sha256, and the Q198 archive holds the pre-write bytes
of every overwritten file.

## 6. Refused / not done

- Build, SupplyChain test run, Platform build check — not run (F-Q202a-1).
- The 4 held paths — not written (owner decision).
- 15 SKIP paths and the MOD-0183 pack — not written; 16/16 unchanged by hash.
- `Program.cs`, `gateway/**`, `ocelot.json`, `sandop-capacity.openapi.yaml`, module draft overlays — not touched.
- No `git add`, commit, push, stash, checkout, `git diff`, `gh`. No new backup. No hand rollback.

## 7. Files

`SOP-22.md` · `WRITTEN-FILES.tsv` (298 rows: 294 OK + 4 not written) · `SKIPPED-PATHS.tsv` (20 rows) ·
`BUILD-AND-TEST-AFTER.tsv` · `REGRESSIONS.tsv` (header only — **not** "no regressions": tests were not run) ·
`BASELINE-BEFORE-AFTER.md` · `ARTIFACTS.sha256` · `tools/q202a.py` · `tools/exclude.txt`

Return to CT; CT decides.
