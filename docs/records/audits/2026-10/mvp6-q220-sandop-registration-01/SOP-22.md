# Q220 v2 — S&OP persistence registration: hosted schema · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q220 v2 · `AL-SCM-SANDOP-REGISTRATION` (DEV) · required E2 — **NOT reached for the repo.** The change could not be written. E2 was reached only for the proposed bytes in an isolated scratch copy. |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `backend-architect`. **§17.4's backend-architect row is shaped for new-module work; five of its six fields are n/a for a schema-shape fix. CT filled the one that applies and named the rest.** |
| Read first | `AGENTS.md`, the MOD-0190 pack, `MOD-0190-OWNED.tsv`, `mongo-indexing.md` (read in full earlier in this session; hashes re-checked, unchanged) · `backend-architect.md` · `repository-standard.md` · `code-style.md` (read for this WP) |
| Session note | The prompt asks for its own session and refers to a first dispatch. This run shares a session with Q201–Q216; the first Q220 dispatch was not in this session. |
| Start / End (Europe/Istanbul) | 2026-10-02 22:20:23 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED — repo unchanged. A tested proposal is in this folder. Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 89 " M" · 576 "??" · 0 staged = 665
                      end   89 " M" · 576 "??" · 0 staged = 665   (unchanged — see ARTIFACTS.sha256)
                      No .git/index.lock. No git diff. No git write. No gh.
Build in flight:      none at start (0 dotnet / MSBuild / testhost processes)
Changed files (repo): NONE under services/. Only this record folder.
Build (repo):         NOT RUN — nothing changed.
Tests (repo):         NOT RUN — nothing changed.
Scratch copy:         build 0 errors / 0 warnings; SandopPlans 20/20 (19 existing + 1 new); sabotage proof done.
Decisions:            none taken
Blockers:             1 — the permission rule in §1
Out-of-scope changes: none. Program.cs, ocelot.json, .csproj, permission keys, index definitions,
                      IDemandFixtureReader and other modules' registrations were not touched.
```

## 1. The blocker

Three writes were attempted and all three were refused by the session's permission settings:

| Attempt | Target | Result |
|---|---|---|
| Edit | `services/…/Persistence/Features/SandopPlans/SandopSchema.cs` | "File is in a directory that is denied by your permission settings." |
| Edit | `services/…/Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs` | same |
| Write | `services/…/tests/Diten.SupplyChainService.Tests/SandopPlans/SandopRegistrationTests.cs` | same |

The rule is in `.claude/settings.local.json`, `permissions.deny`:
`Edit(services/**)`, `Write(services/**)` (and the same with the absolute path).

- This is a deliberate guardrail, so it was **not** worked around. No shell redirect, `sed`, `cp` or script wrote
  into `services/`.
- The three repo files are untouched. Their sha256 values at the end equal the "before" values in `BEFORE-AFTER.md`.
- The WP's Allowed Paths and that deny rule contradict each other. Resolving it is the owner's call: lift the rule
  for these three paths, or apply the proposed files by hand.

## 2. What was done instead

To hand CT a tested change rather than only a refusal, the proposal was verified outside the repo:

- A copy of `services/Diten.SupplyChainService` and `services/Diten.Building.Blocks` (no `bin`/`obj`) was made in
  the session scratchpad. The three proposed files were applied to the copy only.
- The copy was built and the SandopPlans tests were run against the existing lane replica set `rsq208s1`
  (`127.0.0.1:31994`), with `MVP6_MOD0190_MONGO_URI` set.
- Results (`BUILD-AND-TEST.tsv`):

| Step | Result |
|---|---|
| Build | 0 errors, 0 warnings |
| SandopPlans tests | **20 / 20** — the 19 existing tests plus the new one |
| Sabotage: remove `AddHostedService<SandopSchema>()`, rebuild, run the new test | **fails** — `Assert.Single() Failure: The collection was empty` |
| Sabotage: the other 19 tests | 19 / 19 — they do not notice the missing hosted service |
| Restore, rebuild, run twice | 20 / 20, 20 / 20 |

This is evidence for the proposed bytes. It is **not** evidence about the repo, which was not built or tested.

## 3. The proposed change (`BEFORE-AFTER.md`, `proposed/`)

1. `SandopSchema` becomes a `sealed class … : IHostedService` with a primary constructor taking `IMongoDatabase`,
   matching `CapacitySchema.cs:5` and `ReturnSchema.cs:5`. `StartAsync` calls the existing static `EnsureAsync`;
   `StopAsync` returns a completed task.
2. The static `EnsureAsync` stays, with the same signature (CT decision 2). `SandopContractTests.cs:6` is untouched.
3. The six index definitions are byte-for-byte identical: the seven lines holding them hash to `d0978366…` before
   and after.
4. `AddSandopPersistence()` gains exactly one call, `services.AddHostedService<SandopSchema>()`.
5. One new test proves the indexes come from the registration path and fails without the hosted service.
6. No `.csproj` change is needed.

## 4. Shape comparison, line by line (`PEER-SHAPE-COMPARISON.tsv`)

| Element (CT decision 3) | Load | Carrier | Return | Capacity | Claim | S&OP today | S&OP proposed |
|---|---|---|---|---|---|---|---|
| Request context, if the module has one | yes | yes | yes | yes | yes | none exists | none to register |
| Repository | yes | yes | yes | yes | yes | yes | yes |
| `AddHostedService<…Schema>()` | yes | yes | yes | yes | yes | **missing** | **added** |
| Module-specific extras (probe, outbox store, lease store) | 2 | 1 | 2 | 1 | 2 | 0 | 0 |

- **Today: not shape-equal.** The hosted schema is missing.
- **With the proposal: shape-equal** on the three elements CT defined. S&OP has no request-context class — its
  contexts are records built per request in the controller — so there is nothing to register for that element.
- `IDemandFixtureReader` is not registered, per CT decision 1.

## 5. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q220-1** | 🔴 Blocker | The WP cannot be executed from this session: `Edit(services/**)` and `Write(services/**)` are denied in `.claude/settings.local.json`, and all three Allowed source/test paths are under `services/`. Repo unchanged. | §1 |
| **F-Q220-2** | 🟢 Result | The proposed change builds with 0 errors and 0 warnings and gives SandopPlans 20/20 in an isolated copy; the 19 existing tests are unaffected. | `BUILD-AND-TEST.tsv` |
| **F-Q220-3** | 🟢 Result | The new test is a real guard: it fails when the hosted-service line is removed, while the other 19 tests stay green. That second half is F-Q212-4 shown directly — today's tests cannot see the defect. | `BUILD-AND-TEST.tsv` rows t2, t3 |
| **F-Q220-4** | 🟡 Medium | The five peers' `StartAsync` first checks that Mongo is a replica set and throws if not (for example `CapacitySchema.cs:9-11`). The proposed `SandopSchema.StartAsync` does not: the WP says to change only when and by whom the indexes are created. So S&OP would be the one module that does not fail closed at start-up on a standalone Mongo. In a composed service the peers' checks already stop start-up, so the practical gap is small. CT to decide whether to add the check. | `proposed/SandopSchema.cs.txt:5` |
| **F-Q220-5** | ⚪ Info | The prompt says "You WILL change this" about the porcelain baseline. Even when applied, it would not: both S&OP folders are already untracked (`??` directory lines), so edits and a new file inside them do not change `git status --porcelain`. | preflight |
| **F-Q220-6** | ⚪ Info | The new test file would be a 39th S&OP path. It is inside the WP's Allowed Paths but outside the 38 pinned by `MOD-0190-OWNED.tsv`. Q212's scope check would report it as present-but-outside-the-38 unless the owned-path record is extended. | `MOD-0190-OWNED.tsv` |
| **F-Q220-7** | ⚪ Info | The scratch test runs created one new database on the lane mongod: `DitenSupplyChain_Mod0190_Registration_Test` (six empty collections with their indexes). Left in place; nothing was dropped. | lane mongod `127.0.0.1:31994` |

## 6. To finish the WP

Either of these, then a build and the SandopPlans run **in the repo**:

1. The owner allows `Edit`/`Write` for the three paths (or for this WP), and the WP is re-dispatched; or
2. someone applies the three files from `proposed/` by hand (drop the `.txt` suffix; hashes in `BEFORE-AFTER.md`).

Until one of them happens, F-Q212-4 stands and Q209 should still not wire S&OP into `Program.cs`.

## 7. Refused / not done

- No write into `services/` by any route. No repo build, no repo test.
- No `.csproj`, `Program.cs`, `ocelot.json`, permission key, index definition, `IDemandFixtureReader` registration,
  other module's registration, or second new test.
- No git write, no `git diff`, no `gh`. No ledger row changed.

## 8. Files

`SOP-22.md` · `BEFORE-AFTER.md` · `PEER-SHAPE-COMPARISON.tsv` · `BUILD-AND-TEST.tsv` · `ARTIFACTS.sha256` ·
`before/` (2 byte copies) · `proposed/` (3 files, `.txt`) · `evidence/` (scratch build and test logs,
`trx-sha256.txt`)

Return to CT; CT decides.
