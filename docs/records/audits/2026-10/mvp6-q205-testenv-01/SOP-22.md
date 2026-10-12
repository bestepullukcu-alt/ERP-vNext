# Q205 — Isolated test MongoDB for the SupplyChain suite · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q205 · `AL-SCM-TESTENV` (INS) · required E2 — **reached: E2** (full suite run, 139/139 executed, 0 skipped) on the **working tree** (see F-Q205-5) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `devops-agent` (skills mongodb-ops, docker-compose). **G3 satisfied by analogy — §17.4 has no devops-agent row (F-8).** Mandatory fields supplied by CT in the prompt. |
| Read first | `AGENTS.md` (full) · `.antigravity/agents/devops-agent.md` · `.antigravity/rules/dev-runbook.md` · `mongo-indexing.md` (DB-010) · `configuration-safety.md` · BASE-STACK v2 (full) |
| Base stack | BASE-STACK v2 — `docs/records/audits/2026-09/mvp6-base-stack-v2/BASE-STACK-v2.md` sha256 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` (SHA256SUMS 2/2 OK). Not edited (K4). Not composed — this WP ran on the working tree. |
| Start / End (Europe/Istanbul) | 2026-10-02 17:31:07 +03 / 2026-10-02 17:41:57 +03 |

```text
Agent Verdict:        ENVIRONMENT UP, SUITE RE-RUN. Agent PASS ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      git status --porcelain: 32 " M" · 533 "??" · 0 staged = 565 at start and at end
                      (matches G7). No .git/index.lock. No git diff. No git write. No gh.
Changed files:        only docs/records/audits/2026-10/mvp6-q205-testenv-01/** and /private/tmp/q205-testenv-01/**.
                      dotnet test also wrote git-ignored bin/ and obj/ in the test project.
Tests:                before (no variables)   139 total · 46 passed · 93 failed   (= the 2026-10-02 baseline)
                      after  (rsq205s1:31994) 139 total · 138 passed · 1 failed
Persistence evidence: replica set initiated, PRIMARY; transaction commit and abort both proven (PROVISIONING-STEPS §4)
Security/RBAC/Tenant: n/a (no code). Loopback only, no credentials in any URI.
Audit/Evidence:       evidence/before.trx · after.trx · test-before.log · test-after.log · env-all.txt
Migration/Rollback:   stop the lane mongod (PROVISIONING-STEPS §7). Nothing else to roll back.
Decisions:            one, taken by the owner in-session (F-Q205-1)
Blockers:             none
Out-of-scope changes: none
```

## 1. Result

| | Total | Passed | Failed |
|---|---:|---:|---:|
| Baseline, variables unset | 139 | 46 | 93 |
| On the isolated replica set | 139 | 138 | 1 |

92 of the 93 failures were the guard and are gone. Per test: `TEST-RESULTS-BEFORE-AFTER.tsv`.

| Area | Tests needing Mongo | After |
|---|---:|---|
| MOD-0183 (`ShipmentTests`, `SourceIntakeTests`) | 27 | 27 passed |
| MOD-0184 (`Carriers.CarrierContractTests`) | 33 | 33 passed |
| MOD-0185 (`Loads.*`) | 33 | 32 passed · **1 failed** |

## 2. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q205-1** | HIGH (prompt premise) | The CT-accepted script does **not** provision anything. It only prints `export` lines ("starts nothing, connects to nothing and writes nothing", script lines 8–10). BASE-STACK v2 §3 says the same: "the Mac lane starts its own mongod, as Q154 did". The prompt's task 3 ("Run it. Stand up the isolated replica set") cannot be done by the script. Put to the owner; the owner chose to start the mongod by hand per BASE-STACK v2 §4 step 6. No script was written. | `PROVISIONING-STEPS.md` §1 |
| **F-Q205-2** | MEDIUM (genuine defect) | One failure survives provisioning: `Loads.LoadAtomicityTests.UnknownCommitResultRetriesAndCommitsExactlyOnce` — expected `Created`, actual `ServiceUnavailable` (`LoadAtomicityTests.cs:37`). Deterministic here, 4/4. Same test failed at Q154 (O-2, rated LOW env) and passed at Q121c. Two lanes on two mongods now show it, so "environment noise" is less likely than Q154 assumed. Cause not diagnosed. MOD-0185, not MOD-0183. | `GENUINE-DEFECTS.tsv` D-Q205-1 |
| **F-Q205-3** | LOW (record correction) | The baseline note "every failure throws `Isolated Mongo required`" is not exact. The 93 failures carry four messages: `Isolated Mongo required.` ×33 (Carriers), `Set MOD0185_TEST_MONGO to an isolated replica set.` ×33 (Loads), `Isolated Mongo required` ×15 (SourceIntake), `MOD0183_TEST_MONGO must point to an isolated replica set.` ×12 (Shipment). All four are the same fail-closed guard. | `evidence/before.trx` |
| **F-Q205-4** | MEDIUM (env) | The setup does not survive a machine restart: forked process, dbpath under `/private/tmp`, shell-scoped variables. Rebuild takes seconds. The lane mongod (pid 25668, `127.0.0.1:31994`) was **left running** for CT/VER to use. | `PROVISIONING-STEPS.md` §6–§7 |
| **F-Q205-5** | MEDIUM (scope of evidence) | The run is on the working tree, not on a BASE-STACK v2 tree. Q131 is not applied on disk (the prompt says so; Q201 found the same). The E2 result is evidence for the checkout as it stands, not for the stack. | `MOD0183-EVIDENCE-STATEMENT.md` |
| **F-Q205-6** | INFO | The script always prints `DITEN_PLATFORM_TEST_MONGO_URI` and `MVP6_MOD0192_MONGO_URI`, and five more with `--all-supplychain`. The Q205 config surface is three variables, so the output was filtered to the three `MOD018x` lines before `eval`. The test project reads no other variable. | `evidence/env-all.txt` |
| **F-Q205-7** | INFO | `dev-runbook.md` names `mongodb-community@7.0`; the machine runs 8.0 (8.0.18). `devops-agent.md` asks for a 3-node replica set; the lane set is single-member, as BASE-STACK v2 §3 prescribes for tests. Neither file was changed. | — |

## 3. Guard and 27017

- The guard was not weakened. No test, service code, `.csproj`, appsettings or repo script was edited.
- No variable was pointed at 27017. The script itself refuses 27017–27021.
- The homebrew mongod (pid 758) was not contacted, stopped or reconfigured; same pid and listeners before and after.

## 4. Refused / not done

- No provisioning script written (prompt).
- `scripts/evidence-kit/k05_mongo.sh` not used and not read.
- The Q131 layer was not applied to the working tree; the script was run from `/private/tmp` only.
- The surviving failure was not diagnosed in service code and not "fixed".
- Neither MOD-0186 nor MOD-0187 acceptance box was ticked.
- The lane databases were not dropped and the scratch dir was not deleted (no `rm`).

## 5. Files

`SOP-22.md` · `HASH-VERIFICATION.md` · `PROVISIONING-STEPS.md` · `TEST-RESULTS-BEFORE-AFTER.tsv` ·
`GENUINE-DEFECTS.tsv` · `MOD0183-EVIDENCE-STATEMENT.md` · `ARTIFACTS.sha256` · `evidence/` (5 files)

Return to CT; CT decides.
