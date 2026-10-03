# Q232 — SupplyChain unbreak: four persistence registrations · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q232 · `AL-SCM-UNBREAK` (DEV) · required E3 (service must start) — **NOT reached.** |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agent | `backend-architect`. **§17.4's backend-architect row is shaped for new-module work; five of its six fields are n/a for a DI registration fix. CT filled the one that applies and named the rest.** |
| Read first | `AGENTS.md`, `backend-architect.md`, `configuration-safety.md`, `code-style.md` (earlier in this session) · `erp-architecture.md`, `pipeline-behaviors.md`, Q217 `STARTUP-VERDICT.md`, `WHAT-Q209-NEEDS.md`, `HTTP-EVIDENCE.md` (for this WP) |
| Start / End (Europe/Istanbul) | 2026-10-02 23:15:11 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        BLOCKED, twice over. Agent verdict ≠ CT ACCEPTED.
                      (1) The edit was refused; the repo is unchanged.
                      (2) The proposed four lines, tested in a scratch copy, do NOT make the
                          service boot in Development.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched)
Worktree status:      start 89 " M" · 576 "??" · 0 staged = 665
                      end   665 lines, byte-identical to the start snapshot
                      No .git/index.lock. No git diff. No git write. No gh.
Build in flight:      none at start (0 dotnet / MSBuild / testhost processes)
Changed files (repo): NONE under services/. Only this record folder.
Build (repo):         NOT RUN — nothing changed.
Scratch copy:         build 0 errors / 0 warnings (three builds)
                      Development start: FAILS, 13 unresolved-service errors at Program.cs:72
                      Suite: 416 / 417 — only the known by-design Claims failure
Evidence reached:     E2 for the proposed bytes in a scratch copy (builds, suite in band).
                      E3 not reached anywhere: nothing boots in Development.
Out-of-scope changes: none
```

## 1. Blocker one — the edit was refused

One Edit was attempted on `services/…/Diten.SupplyChainService.Api/Program.cs` and refused:
"File is in a directory that is denied by your permission settings."

- Rule: `.claude/settings.local.json`, `permissions.deny` → `Edit(services/**)` (and the absolute-path form).
- It was not worked around. No shell, script or copy wrote into `services/`. Repo `Program.cs` is
  `7fdb5ef0…` at start and end.
- **The one settings change that unblocks it:** remove `Edit(services/**)` and its absolute-path twin from
  `permissions.deny`, or add an `allow` for exactly
  `Edit(services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs)` if the owner's settings
  give allow precedence. In Claude Code a deny rule normally wins over an allow, so narrowing the deny is the
  reliable route. Not verified here — the settings file is not mine to change.
- Per the WP, the work then moved to PROPOSAL MODE: a copy of `services/Diten.SupplyChainService` and
  `services/Diten.Building.Blocks` (no `bin`/`obj`) in the session scratchpad, with the eight lines applied there.

## 2. Blocker two — the four lines are not enough

Answer to task 9, plainly: **No. With the four lines the service still does not boot in Development, so
MOD-0183, Carriers and Loads are not restored there.**

| | Lines removed (today's repo) | Lines present (proposal) |
|---|---|---|
| Build | 0 errors / 0 warnings | 0 errors / 0 warnings |
| Development start | exit 134 at `Program.cs:64` | exit 134 at `Program.cs:72` |
| Unresolved-service errors | 18 — the 4 repository interfaces | 13 — 4 reader interfaces that were hidden behind them |
| Production start (supplementary) | boots (Q217) | boots; HTTP answers identical to Q217 |
| Suite | 415–416 / 417 (Q216) | 416 / 417 |

The 13 remaining errors (`STARTUP-AND-HTTP.md`):

- `SandopPlans.IDemandFixtureReader` — 3 handlers
- `Returns.IReturnReferenceReader` — 1 handler
- `Claims.IClaimReferenceReader` — 2 handlers
- `CapacityPlans.IDemandFixtureReader` — `CapacityRepository` and the 6 handlers behind it;
  `IConstraintFixtureReader` is next in the same constructor

Implementations of all of them exist under `Infrastructure/Features/`, but none is registered, and registering
them takes HttpClient wiring, configuration keys (`Claims:ReferenceBaseUrl` per Q211) and a decision on fixture
data. That is exactly the list task 3 reserves for Q209, so nothing was added.

## 3. Findings

| ID | Severity | Finding | Evidence |
|---|---|---|---|
| **F-Q232-1** | 🔴 Blocker | The WP cannot be executed from this session: `Edit(services/**)` is denied and the only allowed source path is under `services/`. Repo unchanged. | §1 |
| **F-Q232-2** | 🔴 Blocker | The WP's premise does not hold. The four registration calls remove all 18 repository errors but expose 13 more; Development startup still fails at `builder.Build()`. A DI-only "unbreak" needs at least five further registrations (four reader interfaces plus `IConstraintFixtureReader`), and the list is still a lower bound. | `SABOTAGE-PROOF.md`, `STARTUP-AND-HTTP.md` |
| **F-Q232-3** | 🟡 Medium | The K3 sabotage proof cannot be given as specified: removed → fails (18), present → fails (13). The lines are shown to be necessary, not sufficient. | `SABOTAGE-PROOF.md` |
| **F-Q232-4** | 🟡 Medium | Applying the four lines alone is a half-fix of the kind SOP §32 K4 warns about: in Production the service boots with Returns, Claims and Capacity persistence and their hosted schemas live, while their write handlers still cannot be constructed and their routes are still answered by the Shipment middleware. CT should decide whether the four lines land alone or together with the reader registrations. | `STARTUP-AND-HTTP.md` |
| **F-Q232-5** | 🟡 Known, accepted by CT | S&OP boots with no schema: after the Production run the database had 32 collections and zero `sandop_*` — no unique indexes (F-Q221-4; Q220 blocked). Reported, not fixed; no hosted service added. | `STARTUP-AND-HTTP.md`, last section |
| **F-Q232-6** | 🟢 Result | No regression from the proposed bytes: suite 416 / 417; the one failure is `Claims.ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` (10/10 by design in Q216); the Loads flip test passed this run; no new failing test name. | `TEST-RESULT.tsv` |
| **F-Q232-7** | 🟢 Result | Unauthenticated HTTP behaviour in Production is unchanged versus Q217 on all ten probes, including the Shipment contract body on the Claims path. Task 7 could not be done in Development. | `STARTUP-AND-HTTP.md` |
| **F-Q232-8** | ⚪ Info | The supplementary Production process ran ~6.5 min instead of ~30 s because SIGINT from the script was ignored; stopped with SIGTERM, graceful shutdown, port 5061 free afterwards. It created database `diten_q232_unbreak` on the lane mongod (32 collections), left in place. | `evidence/service-prod.log` |

## 4. Options for CT (not taken)

1. **Widen Q232** to the full container fix: the four calls plus the reader registrations, with CT naming where
   fixture data and `…:ReferenceBaseUrl` come from. This is most of Q209's DI share.
2. **Q217 option B**: stop `Application/DependencyInjection.cs:11` from scanning the held modules' handlers. One
   change to shared Application code restores Development boot for MOD-0183/Carriers/Loads without wiring anything
   half-way. Not measured here.
3. Land the four lines as they are, accepting that Development stays down until Q209.

My recommendation is 2 if the goal is "unbreak now", 1 if Q209 is close. Either way the deny rule must be
resolved first.

## 5. Refused / not done

- No write into `services/` by any route. No repo build, no repo test, no repo process start.
- No registration beyond the four calls, no hosted service, middleware branch, gateway route, permission key or
  config key. No appsettings, `.csproj` or `ocelot.json` edit. Nothing pointed at 27017.
- No token invented; no authenticated call.
- No git write, no `git diff`, no `gh`.

## 6. Files

`SOP-22.md` · `BEFORE-AFTER.md` · `SABOTAGE-PROOF.md` · `STARTUP-AND-HTTP.md` · `TEST-RESULT.tsv` ·
`ARTIFACTS.sha256` · `before/Program.cs.txt` · `proposed/Program.cs.txt` · `evidence/` (3 build logs, 4 service
logs, HTTP captures, test log, `run.sh`). The 2 MB `.trx` stays in the scratchpad; its hash is in `TEST-RESULT.tsv`.

Return to CT; CT decides.
