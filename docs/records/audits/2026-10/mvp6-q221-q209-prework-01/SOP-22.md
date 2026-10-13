# Q221 — Q209 pre-work: `Program.cs` and `ocelot.json` patch proposal · SOP §22 structured report

| Field | Value |
|---|---|
| Work Package / Lane / Evidence | Q221 · `AL-SCM-Q209-PREWORK` (INS) · required E1 — **reached: E1** (static; nothing built, run, started or applied) |
| Placement | Claude app → Code tab → Local, `uname -s` = Darwin. **G2 placement waiver applied — Cowork withdrawn by owner.** |
| Agents | Phase A `read-only-auditor` — mode **strict**, no fixes, no Write. Phase B `documentation-writer` — this folder only |
| Scope (§17.4) | `Program.cs` of the SupplyChain service · the `*PersistenceRegistration.cs` files (six found, all read) · `gateway/Diten.ApiGateway/ocelot.json` · the four VER `REACHABILITY.md` records · `AGENTS.md` §3 |
| Read first, in the prescribed order | `AGENTS.md` (393 lines, in full) · `read-only-auditor.md` · `read-only-audit.md` · `documentation-writer.md` · `routes.md` · `ports.md` · `pipeline-behaviors.md` · Q201 `SHARED-SEAM-PATCH-NEEDS.md` · `REACHABILITY.md` of Q212, Q213, Q210, Q211 |
| Depends on | Q210, Q211, Q212, Q213 (stated as CT ACCEPTED by the prompt; their records were read, not re-judged) |
| Start / End (Europe/Istanbul) | 2026-10-02 21:01:49 +03 / see `ARTIFACTS.sha256` header |

```text
Agent Verdict:        PROPOSAL DELIVERED. Program.cs: exact text for P0 (BASE verbatim) and P1 (+4 provider lines).
                      ocelot.json: exact route set (289) and guard numbers. 12 points left as owner decisions.
                      Agent verdict ≠ CT ACCEPTED.
Branch / HEAD:        feature/mvp6-logistics @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (matched at start and end)
Worktree status:      git status --porcelain: 89 " M" · 576 "??" · 0 staged = 665 at start and at end; diff of the two
                      listings is empty. No .git/index.lock. No git diff, no fetch, no git write, no gh.
Changed files:        this record folder only (6 files). It lies inside the already-untracked 2026-10/ folder.
Build / Tests:        NOT RUN (forbidden).
Applied:              NOTHING. Program.cs, ocelot.json and every protected path were read only.
```

## 1. What was produced

| File | Content |
|---|---|
| `PROGRAM-CS-PROPOSAL.md` | The full proposed file (127 lines), a source and evidence row for every line, and the five traps resolved into exact text |
| `OCELOT-PROPOSAL.md` | The proposed route list (289 routes, one source row each), exact JSON for the 20 supply-chain routes, the CRM port move, the guard numbers |
| `OPEN-DECISIONS.tsv` | 12 rows: each point no record decides, what closes it, who owns it |
| `INTEGRATION-ORDER.md` | Steps, what must land before `Program.cs`, what can follow, order hazards, protected paths and their owners |

Line sources in the proposed `Program.cs`: 71 lines unchanged from the working tree · 47 added by BASE · 5 BASE lines that
reshape a working-tree statement · 4 draft provider lines = 127.

## 2. The five traps — short answers

| Trap | Answer | Where |
|---|---|---|
| 1. Two `IDemandFixtureReader` | Both registrations given, fully qualified. BASE's own text is already unambiguous because Capacity is reached only through aliases. The hazard is a later `using` of a Capacity namespace | `PROGRAM-CS-PROPOSAL.md` §3 Trap 1; lines 75, 78-81 |
| 2. Executor | **Recommend: register** (line 77). The pack names the executor as part of the approved composition, and without it no evaluation ever completes. Two points stay open: the missing 10 s renewal (OD-1) and the unscoped scan it starts (OD-2). The other answer needs a CT ruling that the executor decision is a start-up precondition, then a Capacity DEV change | §3 Trap 2 |
| 3. Shipment branch | Exact expression = BASE line 118 (proposed line 122). **The three S&OP contradictions cannot occur on an S&OP path** — see F-Q221-1. The exclusion is needed for Returns and Claims, and is redundant for S&OP and Capacity. One open point: the service-wide strict UTF-8 rule for `Idempotency-Key` (OD-5) | §3 Trap 3 |
| 4. Persistence and config | Every line listed per module. Two configuration keys are unset: `Returns:ReferenceBaseUrl`, `Claims:ReferenceBaseUrl`; value not decided (OD-4). S&OP index creation is not in `AddSandopPersistence()` (OD-3, Q220) | §3 Trap 4 |
| 5. Gateway | 20 routes on 5061 (6 BASE + 14 fragments), CRM 35 routes → 5065, 289 routes in total. Guard test `CrmAndSupplyChainRoutes_MapOnlyToTheirOwnedServicePorts`: CRM stays **35**, `/api/shipment-bundle/` becomes **10**. The test exists only in the BASE test file | `OCELOT-PROPOSAL.md` §5-§6 |

## 3. Findings

Severity scale of `read-only-auditor.md`. Findings are reports, not fixes.

| ID | Sev | Finding | Evidence |
|---|---|---|---|
| **F-Q221-1** | 🟠 High — record correction for CT | Trap 3's premise (F-Q212-5) is not supported by the code. `ShipmentContextMiddleware` returns at its line 9 for every path outside `/api/shipment-bundle`; S&OP is at `/api/supply-chain/sandop-plans`. The Shipment correlation, scope and idempotency rules never run for an S&OP request, with or without the exclusion term. Q213 states the same for Capacity. The exclusion **is** required for Returns and Claims | `…/Api/Middleware/ShipmentContextMiddleware.cs:9`; `…/Api/Features/SandopPlans/SandopPlansController.cs:4`; Q212 `REACHABILITY.md:36-49`; Q213 `REACHABILITY.md:26-27` |
| **F-Q221-2** | 🟠 High — for CT | Registering the executor (BASE line 77) starts a worker that departs from its own decision record in two ways: no 10 s lease renewal (`DECISION.md:21`), and a discovery scan with no tenant filter (`DECISION.md:27`). Harmless with the instant fixture oracle; not harmless as a pattern. Recommendation and conditions in Trap 2 | `CapacityEvaluationExecutor.cs:15-32`; `CapacityLeaseStore.cs:23-29`, `:52-64`; `docs/roadmap/plans/mod-0192-executor-exact-decisions-01/DECISION.md:21`, `:27` |
| **F-Q221-3** | 🟠 High | The four draft provider classes are absent from the tree (`grep` = 0 each). The "four draft provider lines on top of BASE" cannot compile today. They are a later, per-module step, not part of the first `Program.cs` patch | `PROGRAM-CS-PROPOSAL.md` lines 90-93; `Api/ModuleRegistration/` holds 5 files, 2 providers |
| **F-Q221-4** | 🟠 High | Wiring S&OP (line 73) while `AddSandopPersistence()` creates no indexes makes the three F-Q212-4 invariants unprotected on a **reachable** endpoint. Order matters: Q220 before the `Program.cs` patch | `SandopPersistenceRegistration.cs:4-5` (sha256 `0e74fec0…` at start and end); `INTEGRATION-ORDER.md` |
| **F-Q221-5** | 🟡 Medium | The gateway guard that everybody cites is not in the tree. The working-tree `OcelotConfigurationTests.cs` has no supply-chain count and its `KnownDownstreamPorts` (line 16) lacks 5063, 5064 and 5065, although the working-tree `ocelot.json` has 108 routes on 5063/5064. By reading, `EveryRoute_DownstreamPortIsInKnownServiceSet` cannot pass on today's tree — **not run** | `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs:16` (`2e64c273…`); BASE member `efcc548e…` lines 17, 125-138 |
| **F-Q221-6** | 🟡 Medium | The Claims draft's guard patch (6 → 8) is wrong once the Returns routes are present: the number is 10. The 10 `/api/supply-chain/` routes are counted by no guard (OD-7) | `OCELOT-PROPOSAL.md` §6; Q201 `SHARED-SEAM-PATCH-NEEDS.md:86-89` (F-Q201-8) |
| **F-Q221-7** | 🟡 Medium | `Returns:ReferenceBaseUrl` and `Claims:ReferenceBaseUrl` are set nowhere and no record gives their value. The same holds for the already-wired `Loads:ReferenceBaseUrl` | `ReturnReferenceReader.cs:71`; `ClaimReferenceReader.cs:88-89`; `LoadReferenceReader.cs:44`; `Api/appsettings.json:1-14` |
| **F-Q221-8** | 🟡 Medium — queue accuracy | CT-QUEUE row Q220 says S&OP "has NO SandopPersistenceRegistration at all". The file exists (created 2026-10-02 19:05:48 +03, byte-equal to the BASE member) and BASE calls it. What is missing is index creation inside it. Q220's goal stands; its stated cause does not | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:316`; `…/Persistence/Features/SandopPlans/SandopPersistenceRegistration.cs:1-5` |
| F-Q221-9 | ⚪ Low | The prompt's scope says "the five `*PersistenceRegistration.cs` files". Six exist: CapacityPlans, Carriers, Claims, Loads, Returns, SandopPlans. All six were read | `find … -name '*PersistenceRegistration.cs'` |
| F-Q221-10 | ⚪ Low | Four BASE Shipment routes have no `OPTIONS`, against NET-001 (OD-8). Route 10's `GET` on the S&OP collection has no action behind it (OD-9) | `OCELOT-PROPOSAL.md` §7 |
| F-Q221-11 | ⚪ Low | `.antigravity/rules/ports.md` is stale against `AGENTS.md` §3: band "5011-5064" and no row for 5061, 5062 or 5065. `AGENTS.md` wins (`AGENTS.md:16`). Not edited | `.antigravity/rules/ports.md:14`, `:18-28`; `AGENTS.md:86-87`, `:90` |
| F-Q221-12 | ⚪ Info | The packs name other "accepted composition" hashes for `Program.cs` (Capacity `50c48a2b…`, pack :273; Claims `11c586e0…` per Q211 `REACHABILITY.md:71-72`). They are isolated compositions. The base version is decided as BASE `33027bcd…` and was not re-opened | MOD-0192 pack :273 |
| F-Q221-13 | ⚪ Info (positive) | Q201's "BASE is a superset" holds line by line: 71 of the 73 working-tree lines appear unchanged in BASE and the other 2 are reshaped (lines 22 and 68). Every type BASE names exists in the tree. `appsettings.json` and the Api `.csproj` already equal their BASE members | `PROGRAM-CS-PROPOSAL.md` §2, §4 |

## 4. Refused / not done

- **No answer was picked where no record decides it:** 12 rows in `OPEN-DECISIONS.tsv`. The one recommendation the prompt
  asked for (Trap 2) is given with its conditions and with what would reverse it.
- `Program.cs`, `ocelot.json`, `appsettings.json` and the gateway test were not edited, reformatted, built or run.
- The assembled `ocelot.json` text was computed in memory to get its sha256 and to check for duplicates. **No such file
  was written.** The 234 routes that do not change are listed by index, template, methods and port, not reprinted as JSON
  (the file is 121 kB); they are identical in the working tree and BASE (list equality checked). This is a deviation from
  "full proposed file content" for `ocelot.json`, stated here so CT can ask for the full text if wanted.
- Whether the proposed `Program.cs` compiles, and whether the service starts in `Development` (OD-11): not checked.
- Run-time claims in the VER records (header pass-through by Ocelot, 405 on the S&OP collection GET, behaviour of the
  strict UTF-8 selector) are marked "by reading" or "insufficient evidence".
- Q212's accepted record was not corrected; F-Q221-1 goes to CT.
- The `read-only-auditor` baseline block lists `git diff --name-only` and `git diff --check`
  (`read-only-auditor.md:50`, `:58-60`). This WP forbids `git diff`, so the baseline and the final check use
  `git status --porcelain` only, plus sha256 of the scope files.

## 5. No-change verification

| Check | Start (21:01:49 +03) | End |
|---|---|---|
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b…` | same |
| `git status --porcelain` | 89 ` M` · 576 `??` · 0 staged = 665 | 665; `diff` of the two listings: empty |
| `.git/index.lock` | absent | absent |
| `…/Api/Program.cs` | `7fdb5ef0d322c3b9…` | `7fdb5ef0d322c3b9…` |
| `gateway/Diten.ApiGateway/ocelot.json` | `b0121d2f5b7f809d…` | `b0121d2f5b7f809d…` |
| `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` | `2e64c27331039126…` | `2e64c27331039126…` |
| `…/SandopPlans/SandopPersistenceRegistration.cs` (Q220 may change it) | `0e74fec0687634d9…` | `0e74fec0687634d9…` — unchanged during this WP |
| `…/Api/appsettings.json` | `55ef125b8f6b2529…` | `55ef125b8f6b2529…` |

Sealed sources read in memory, never extracted to disk: `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz`
(`7b6a0d1a…`), and the four UI-draft archives (`716e7c4c…`, `5c0a3b61…`, `50724097…`, `2b34741a…`).
A generator script was used to build the two proposal files from those sources; it lives in the session scratch folder
outside the repository and wrote only into this folder.

## 6. Files

`SOP-22.md` · `PROGRAM-CS-PROPOSAL.md` · `OCELOT-PROPOSAL.md` · `OPEN-DECISIONS.tsv` · `INTEGRATION-ORDER.md` · `ARTIFACTS.sha256`

Agent verdict ≠ CT ACCEPTED. Returning to CT.
