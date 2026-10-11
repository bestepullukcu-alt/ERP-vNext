# Q152 — Uncontrolled change UC-01 and the `.git/index.lock` source (Q136 scope included) — SOP §37

| Field | Value |
|---|---|
| WP | Q152 · devops-agent (lead) + read-only-auditor + security-agent · SOP v2.4 §17.1, §20, §25, §37 |
| Where | Claude Code on the owner's Mac (Darwin). **Not a new session** — see DV-1 |
| Mode | READ-ONLY (worktree-read-only). Only write: this folder. `git status --porcelain` and `git show HEAD:<file>` only; **no `git diff`** |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; 29 tracked ` M` paths at start and end; no `.git/index.lock` at start and end |
| Time | preflight 2026-09-27 19:14:58 +03:00 → end ~19:55 +03:00 |

## 0. Verification report (SOP §37)

```text
VERIFICATION REPORT

WP ID:                Q152 (Q136 merged)
Verifier:             Mac Claude Code (devops-agent + read-only-auditor + security-agent)
Verification date:    2026-09-27
Branch/HEAD:          feature/mvp6-logistics @ 4a8d4d4b… (start and end)

Agent Verdict:        PASS — UC-01 source identified with HIGH confidence; 4 lock events attributed (1 HIGH-supported, 3 LOW–MEDIUM)
Verification Verdict: PASS (read-only; every DOĞRULA item answered; limits stated in §5)
CT Status:            returning to CT — Agent PASS ≠ CT ACCEPTED

Evidence level achieved:  OBSERVED (file metadata, agent session logs, Claude desktop/Cowork logs, dev mongod log read as a file)
Required evidence level:  read-only process/timestamp evidence

Checks:
- scope:          the 10 known files + 2 UNTRACKED new files (UC-01 is 12 files, not 10)
- build/tests:    not run (forbidden)
- security:       no secret, credential or userinfo URI in any hunk; only loopback mongo-uri literals and port numbers
- 27017:          dev mongod log shows 0 new connections 17:43:00–17:52:30
- repo:           unchanged by this lane (29 ` M`; only this folder added)

Failed criteria:  none
Rework required:  no (for this WP)
Next gate:        CT disposition of UC-01 (§4) and of the uncontrolled Codex lane (§4 R-3)
```

## 1. UC-01 — who

**Source: Codex Desktop (OpenAI; embedded in ChatGPT.app) on the Mac, thread "Agent Lane-4"** (`01a0be2b-7aff-7452-ae7b-b1fb7e7e82f2`, created 2026-09-20), not a Cowork VM lane and not Claude Code. **Confidence: HIGH.**

| Evidence | Finding |
|---|---|
| Codex parent thread `~/.codex/sessions/2026/09/20/rollout-2026-09-20T12-35-15-01a0be2b-….jsonl` | 17:43:00 +03 a user message arrives in the Codex Desktop UI: *"NEREDE: Claude Lane; doğrulama Mac Claude Code. NE İLE: testing-agent + security-agent. Q131 controlling kaydını incele. Platform/Capacity testlerindeki hard-coded Mongo portlarını lane-configurable URI ile değiştirecek dar kapsamı belirle … Shared integration dosyalarına dokunma."* The parent (`/root`) then spawns two sub-agents |
| Sub-agents | `/root/testing_agent` "Chandrasekhar" (`rollout-2026-09-27T17-43-19-01a0e352-091b-…`) and `/root/security_agent` "Mencius" (`…T17-43-24-01a0e352-1b62-…`), `originator: Codex Desktop`, `cwd: /Users/natig/Projects/ERP-vNext-recovery`. Two "guardian" (approval-reviewer) threads (`…0aea…`, `…8e3e…`) at 17:47 |
| Per-second match | Every UC-01 write equals a Codex `apply_patch` time (UTC+3): Schema 17:44:27→mtime 17:44:28; `PlatformMongoTestConnectionTests.cs` 17:44:36→birth 17:44:36; ResidueSweeper 17:45:36→17:45:36; Publish/GSKU/TenantAssignment/Workflow 17:45:56→17:45:57–58; ContainerValidation + RabbitMq ×2 17:46:03→17:46:04; `PlatformMongoTestConnection.cs` 17:48:48→birth 17:48:49; `MongoIntegrationHarness.cs` last patch 17:49:36→17:49:36 (`HUNKS.tsv`, `TIMELINE.tsv`) |
| Concurrency | All three Codex agents patched the same files at once (the sub-agents were apparently meant to only read). The parent misread its own sub-agents' edits as "Q131 already partly started" (17:45:19), created and deleted four temporary helper files (`TestMongoConfiguration.cs`, `EventingMongoTestConnection.cs`, `PlatformLaneMongoTestSettings(+Tests).cs` — all absent now), interrupted both sub-agents at 17:51:31 and closed at 17:51:44 with "Q131 için dar patch hazır" |
| Negative evidence | Mac Claude Code: the only log naming these files is this session (`558e981f`), and it has **0 tool calls 17:40–17:52** (its only "hit" is the Q121b README text). The other Claude Code session today (`4f4f1b32`) never names them. Cowork VM sessions logged commands in the window (LANE 3 / Q147 short commands; LANE 1 burst ended 17:43:14), but the writes match Codex to the second and the files carry no Cowork trace |
| Not the owner's shell | `~/.zsh_history` not needed: the writing process is identified. No evidence of automation; the prompt entered the thread through the Codex Desktop UI |

**Intended?** The prompt is a CT-style dispatch for **Q131** addressed to a *Claude* lane ("NEREDE: Claude Lane; doğrulama Mac Claude Code"), but it was entered into Codex. Q131 is still `READY`, "Unassigned (CT to dispatch)" in `CT-QUEUE.tsv:142`, and CT's record says "the owner started no work". So the change was **intended as Q131 work but mis-dispatched** (wrong tool, no WP record, direct repo writes instead of an archive overlay). Who pasted it cannot be read from the logs; CT should ask the owner.

**Is the lane still active?** The Codex app is running (ChatGPT.app Codex helpers present at ~19:50). Thread "Agent Lane-4" has been idle since 17:51:44; its sub-agents were interrupted at 17:51:31. Nothing in the Codex logs after 17:51:44 touches these files. The thread can be resumed at any time and writes natively to the repo.

## 2. UC-01 — what (`HUNKS.tsv`, `diffs/`, `preimage/`, `working-copy/`)

Preimages via `git show HEAD:<file>`; compared with `diff -u` against the working copy (no `git diff`).

| File | +/− | Change |
|---|---|---|
| `Persistence/MongoIntegrationHarness.cs` | +3 −3 | comment; hard-coded mongo-uri constant → `ConnectionStringEnvironmentVariable`; client built from `PlatformMongoTestConnection.RequireConnectionString()` |
| 4 × `BusinessReferenceData*MongoTests.cs` | +2 −1 each | `using` + hard-coded mongo-uri → helper |
| `Schema/PlatformSchemaContractMongoTests.cs` | +1 −2 | constant removed; helper |
| `Workflow/WorkflowTransitionGateMongoRepositoryTests.cs` | +2 −1 | env fallback (`MongoDbSettings__ConnectionString` with a hard-coded default) → helper |
| `BackgroundJobs.Tests/PlatformContainerValidationTests.cs` | +24 −1 | local `RequireLaneMongoConnectionString()` (env required, protected ports refused) |
| 2 × `Eventing.Tests/*RabbitMq*IntegrationTests.cs` | +26 −1 each | local fail-closed guard (Eventing/Mongo env vars; no hard-coded fallback) |
| **NEW, untracked** `Persistence/PlatformMongoTestConnection.cs` | 41 lines | helper: requires env `DITEN_PLATFORM_TEST_MONGO_URI`, explicit host, refuses ports 27017–27021 |
| **NEW, untracked** `Persistence/PlatformMongoTestConnectionTests.cs` | 3 facts | helper tests |

**Mapping:**
- **Q131 scope** (`CT-QUEUE.tsv:142`, "Remove hard-coded test DB ports (Platform 27017, Capacity 57192) → lane-configurable URI"): every hunk is the **Platform half**. The **Capacity (57192) half is not touched**; Codex concluded no Capacity hard-code exists in this checkout, which is true for the working tree but not for the BASE/Q121b trees.
- **Q121 overlay `93bf1c07…`:** **no overlap**. Q121 changes `Audit/PpmAuditRetentionPolicySeedMongoTests.cs` and `Persistence/DisposableStandaloneMongo.cs`; neither is in UC-01.
- **Security:** no secret, password, token, or URI with userinfo in any hunk. Pattern types present: loopback mongo-uri literals (removed), port literals, env-var reads.
- **27017:** the dev mongod log (read as a file) shows **0 `Connection accepted`** from 17:43:00 to 17:52:30. Codex's `dotnet test` runs were helper-only (URI parse). The parent's run failed first on a sandbox named pipe, then on missing restore assets.
- **Side effects outside the 12 files:** 21 `obj/` and 30 `bin/` build files under `services/Diten.Platform/tests` (git-ignored). Four temporary helper files were created and deleted.

## 3. `.git/index.lock` — the four events (Q136 scope)

| # | Time (+03) | Nature | Most likely source | Confidence | Evidence |
|---|---|---|---|---|---|
| 1 | 26 Sep 19:48:13–≤19:48:33 | transient; `.git/index` rewritten at 19:48 by another process | a git index refresh by one of the Cowork VM lanes (LANE 1 `ug6f`, LANE 2 `x5uk`, LANE 3 `bkvd` all running VM bash 19:46–19:47; the VM bridge logs no command text) | LOW–MEDIUM | `MILESTONE-EVENTS.tsv:131`; `main.log`. Codex and Claude Code: 0 git calls in the window |
| 2 | 27 Sep 14:12:10 | transient | LANE 1 (`ug6f`) command at **14:12:09**, 1 s before its own observation; either its own `git status`-type check (lock visible across the VM mount) or a concurrent VM lane (`bkvd` 14:11:41, `x5uk` 14:12:45) | LOW–MEDIUM | Q123 record :31; `main.log` |
| 3 | 27 Sep 14:13:54 | transient | same pattern: LANE 1 command at **14:13:53**; `bkvd` active 14:13:06–14:13:27 | LOW–MEDIUM | same |
| 4 | 27 Sep 17:47:45 | **stale** (0 bytes; removed by the owner ~19:05) | **LANE 3 / Q147 D-1**: a VM `git diff` killed by timeout. Supporting: LANE 3's VM commands at 17:40:50 and 17:42:57 were each killed by the 120 s bridge timeout (17:42:50, 17:44:57); at ~19:05 the lock's only open handle was the macOS Virtualization VM process (pid 1134), i.e. it was created or held through the VM mount. Alternative, less likely: Codex's Mac-native `git status --short` / `git diff` at 17:46:46 and 17:47:12 (no `GIT_OPTIONAL_LOCKS=0`). A Mac git would not leave the VM process as the holder, and no Mac git process existed afterwards | MEDIUM–HIGH | `TIMELINE.tsv`; CT CT-1 |

**Common cause (Q136 answer):** four independent agent runtimes share one working tree — Cowork LANE 1/2/3 in the VM, Codex on the Mac, and Claude Code on the Mac. Any `git status`/`git diff` without `GIT_OPTIONAL_LOCKS=0` refreshes the index and takes `index.lock`. Across the VM file-sharing bridge this is visible to the others, and a timeout kill leaves it behind. The 19:09:10 rewrite of `.git/index`, after the lock removal, is unattributed. Codex state files were updated 19:08–19:11, but CT's "index last written 17:15" is no longer current.

## 4. Conclusion and recommended disposition (for CT; not applied)

| | |
|---|---|
| Source | Codex Desktop thread "Agent Lane-4" (parent + `testing_agent` + `security_agent`), Mac, 17:43:00–17:51:44 +03 |
| Intended | yes as Q131 content; **not** as a controlled lane (no WP, no record, no archive overlay, wrong tool, repo written directly) |
| Still active | Codex app running; thread idle since 17:51:44; can be resumed |
| Risk | **MEDIUM**. The content is good and in scope (fail-closed; operational port refused; no secrets; no 27017 contact). But: (a) the working tree now differs from every accepted base and overlay chain (Q103/Q117/Q121) in 12 files, 2 of them untracked (easy to miss with a tracked-only baseline); (b) **no CI or script sets `DITEN_PLATFORM_TEST_MONGO_URI`**, so integrated as-is, every Platform Mongo/eventing test using it fails closed until lanes/CI provide the variable; (c) unbuilt and untested — Codex's only test attempt did not compile; (d) three agents edited concurrently, and temporary files came and went |

**Recommended disposition (R-1 … R-4):**

- **R-1: keep as a Q131 *candidate* overlay.** A CT-authorized writer archives exactly these 12 files (the 10 tracked working copies + 2 untracked new files) as `q131-platform-candidate` (tar + manifest). This folder already holds byte copies and hashes.
- **R-2: restore the working tree at integration, not before.** After R-1, a single authorized writer restores the 10 files to HEAD and removes the 2 untracked ones, so the tree matches the accepted chain again. Until then, lanes baseline **29 ` M` + these 2 `??`**.
- **R-3: treat Codex "Agent Lane-4" as an unregistered writer.** Stop or register it, and give Codex the same rules as the lanes: archive-only, `GIT_OPTIONAL_LOCKS=0`, no `git diff` under timeout. Check any Codex thread before future baselines.
- **R-4: complete Q131 properly.**
  - Take the Capacity (57192) half into scope.
  - Add `DITEN_PLATFORM_TEST_MONGO_URI` to CI and the kit.
  - Run a Mac build and Platform tests before/after with a lane URI.
  - Run architecture 18/18: `MongoTestDatabaseGuard` must still pass with the new test file.
  - Independent VER.

## 5. Limits and deviations

- **DV-1:** The WP asked for a new session. This is the continuing Mac Claude Code session (`558e981f`), which also ran Q121b/Q121c/Q129 and removed nothing itself. It is one of the sources examined, and it made 0 tool calls 17:40–17:52.
- **DV-2:** Sources were extended beyond the WP's list: `~/.codex` (Codex Desktop session logs, read-only). They were decisive. `~/.zsh_history` was not needed once the writer was identified.
- **DV-3:** The Cowork bridge log (`main.log`) records command length and session, not command text, so the attribution of lock events 1–3 stays LOW–MEDIUM. Today's Cowork transcripts are not stored on the Mac. VM-side evidence would need CT inside the VM.
- **DV-4:** `working-copy/` copies were made with `cp -p` (mtime kept), so they carry the original times. This lane wrote 3 small scratch lists in the per-user `$TMPDIR` (`q152-files.txt`, `q152-rb.txt`, `q152-rb2.txt`); nothing was deleted.
- **DV-5:** This lane's previous turns (before the Q147 rule) ran `git diff` at ~19:0x; in Q152 itself, only `git status --porcelain` and `git show` were used.
- **Values policy:** connection-string values are not reproduced in this report or in `HUNKS.tsv`. They exist only inside the required `preimage/` and `working-copy/` copies, and they are credential-free loopback URIs.

## 6. Files

`REPORT.md` · `TIMELINE.tsv` · `HUNKS.tsv` (22 rows) · `preimage/` (10 × `git show HEAD:`) · `working-copy/` (12, incl. the 2 untracked) · `diffs/` (10 × `diff -u`) · `SECRET-SCAN.txt` · `SHA256SUMS`

## 7. To-do (CT)

1. Confirm with the owner who entered the Q131 prompt into Codex at 17:43:00 (DV/§1).
2. Decide R-1…R-4; until then, the baseline is 29 ` M` + 2 `??` (`PlatformMongoTestConnection*.cs`).
3. Stop or register Codex "Agent Lane-4"; extend the lane rules to Codex.
4. Q131: add the Capacity half, the CI variable and independent VER.
5. Optional VM-side check of lock events 1–3 (VM shell history of LANE 1/2/3).

Agent PASS ≠ CT ACCEPTED — returning to CT.
