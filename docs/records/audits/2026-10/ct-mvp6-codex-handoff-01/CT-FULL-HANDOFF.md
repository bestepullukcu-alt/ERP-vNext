# Control Tower → Codex: full MVP-6 handoff

**Snapshot 2026-10-16 13:34:24 +0300 (Europe/Istanbul, UTC+03:00)**
Repository `/Users/natig/Projects/ERP-vNext-recovery` · branch `feature/mvp6-logistics` ·
HEAD `5e9f2b7249beaca7f3980f4cb17d23cdd83dc7de` · `origin/main` `3912d568b18193f627539a1b27965167ac4077b6`

---

## 1. Scope and currency

### What this package is built from

| source | reach |
|---|---|
| the repository at HEAD | full — tree, history, all committed records |
| `origin/main` | fetched fresh 2026-10-16 |
| the nine module packs | read in full at header depth, plus the authority and gate sections |
| `CT-QUEUE.tsv` | 595 rows; rows Q484–Q499 read in detail, the rest by state scan |
| development plans | v9.5 read in full; v9.0–v9.4 by name and supersession chain |
| `docs/records/decisions/2026-10/` | all 12 files, the merge decision in full |
| worktrees | filesystem and git state of all 26 |
| **this CT conversation** | **partial** — the session was context-compacted before this was written |
| **other lane chats** | **not reachable** — their repository handoffs were used instead |

**This is not a claim of complete history.** It is a measured snapshot. What a lane decided in chat
and never wrote to the repository is not here. See `MISSING-OR-CONFLICTING-INFO.md` §A.

### Historical vs. current, kept apart

Older records are reconciled against newer decisions rather than averaged with them. Three cases
matter and each is resolved explicitly:

- `GATE-18-0.tsv` is the **frozen base**; the four `MOVED.tsv` files are the **current** overlay.
  Base alone under-reports by six cells.
- Pack text below `§28` (MOD-0185), `§31` (MOD-0186/0187) and `§22` (MOD-0190/0192) is **design
  history, superseded** for the approved bounded scope. The packs say so in their own headers.
- Pre-merge evidence is `VALID_PRE_MERGE / INTEGRATION_STALE` by owner decision — diagnostic only.

---

## 2. Module status

Full matrix: **`MODULE-STATUS.tsv`** (9 modules × 21 columns, with writer result, independent VER
result and CT decision in separate columns as required).

### The shape of it

| | modules |
|---|---|
| `ready-for-dev`, bounded scope, backend core CT-accepted | MOD-0183, 0184, 0185, 0186, 0187, 0190, 0192 |
| `draft`, not dispatched, no runtime authority | MOD-0147, 0148 |
| in the GATE-18-0 matrix | **only** 0184, 0185, 0186, 0187 |
| UI DEV released | 0184 (R-4a), 0185 (R-4b) — independent UI VER **open for both** |
| fixture-bound for all of MVP-6 by owner decision | 0190, 0192 |
| **RELEASED** | **none** |

Every `ready-for-dev` is bounded. The recurring sentence across MOD-0186/0187/0190/0192 is:
*"Common-checkout integration, gateway, live producer uptake, UI, E5/G5 and rollout remain open."*
Read `ready-for-dev` as *this exact isolated slice is authorized*, never as *the module is ready*.

### Contract pinning, which has moved and will move again

- MOD-0186/0187 are pinned to **SHIPMENT-BUNDLE 3.1.0 / wire v1** (re-pinned from 3.0.0, §31, Q380).
- MOD-0185 is on **2.0.0 + the Loads annex**; MOD-0192 on **SANDOP-CAPACITY 3.0.0 / wire v1**.
- **3.3.0** (demand history for MOD-0188) is pushed on `contracts/shipment-bundle-3.3-demand-history`
  at `4b4722ece`, **additive only, PR left for the owner, not merged.** Verified node by node:
  nothing removed, 12 paths before and after, 47 → 51 schemas, 319 `$ref`s all resolving. The
  realized fields went on a **response-only** schema, deliberately: `ShipmentLine` is shared with
  `CreateShipmentCommand.lines` and carries `additionalProperties: false`, so adding there would have
  made server-derived, post-dispatch-immutable values writable on the create request.
- **MOD-0188 on main already consumes the frozen 3.2.0** (`7fa528eb9` "align shipment needs with
  frozen 3.2.0"). 3.3.0 was built *for* MOD-0188 and is not merged. Worth one line to the owner.

### Shared surfaces — Auth, Platform, MDM, Gateway, CI, environment

Shown separately because none of it belongs to a module and all of it can break every module.

| surface | state |
|---|---|
| **Auth** | works for the lane. Sign-in measured live, leaking nothing (loopback helper, `openssl` credential, mode-600 file). **The blocker is here:** the lane account is role `Viewer` with 0 of 25 `supplychain.*` keys → **F6 / Q496**. |
| **Platform** | `SavedViewsController.cs:13` serves row 2's saved-view state; the chain is measured end to end. Platform tests **hard-code 27017** — do not repoint them. Untracked `CompositionRootGuardTests.cs` sits in `services/Diten.Platform/tests/Diten.Platform.Application.Tests/` in the primary tree. |
| **MDM** | `GET /api/legal-entities/lookup` is real and **server-side tenant-filtered**, verified in the repository layer (`LegalEntityRepository.cs:30-37`, `TenantFilter` from the injected `ITenantContext`), not inferred from the handler. `LegalEntitiesController.cs:48` already exposes `lookup-validation`, which shrank R-2: the lane calls it fail-closed instead of writing a new one. |
| **Gateway** | catch-all serves the shipment-bundle family and is **transparent** — identical answers direct at 5066 and through the gateway on all five paths (Q497). Explicit routes are **decided** (2026-10-05) and **unassigned** since 2026-10-10 (**F11**). `gateway/**` is Protected Paths in every pack. Main adds **port 5068** to `KnownDownstreamPorts`. |
| **CI** | **`Diten.SupplyChainService` has no CI job** — named nowhere in `.github/`, on either side of the merge. 13 services are covered on main; the service carrying all seven logistics modules is not one of them (**F4**). Only two workflows exist; no Phase 5/6 workflow was found (**G4**). |
| **environment** | the data volume filled **twice** (10-12, 10-14); the second time it cost a session every shell command. A lane mongod dies **silently** when it fills and takes a suite red (Q490). 797 tracked build-output files (**F7**). |

---

## 3. Decisions and authority

Full ledger: **`DECISIONS-AND-AUTHORITY.tsv`** — 16 rows with
APPROVED / PROPOSED / OPEN / SUPERSEDED, authorized target, file scope, hash, limits and supersession.

**Three things to be strict about:**

1. **Exactly one 2026-10 record carries verbatim owner text** — "Approve both scopes, build"
   (`mvp6-carrier-loads-ui-scope-owner-decision-01.md`, 2026-10-04). Every other record is
   attributed and paraphrased. The merge decision itself says *"in the owner's words"* and then
   summarises. Raw owner authority for MOD-0186/0187 is archived in two `authority-source.md` files
   under `docs/records/audits/2026-09/` with published SHA-256s; neither was opened here.
2. **This handoff creates no authority.** Not one row is new. A CT recommendation is not a decision —
   Q484 is the model case: CT **refused** the re-pin the release included and returned it to the
   owner *with* a recommendation. The recommendation is not the decision.
3. **A technical PASS is not an approval.** MOD-0184 §25 Phase 1.5 reads **HELD — "no user approval
   claimed"**, and is recorded that way here.

**The decision that governs the branch right now** is `MVP6-MERGE-ORIGIN-MAIN-01` (2026-10-05):
merge, never rebase; and a 7-item completion gate that makes the merge *"the MVP-6 integrated
acceptance gate"*. Two merges have run under it and the gate has not. See §5 and `SOURCE-ENVIRONMENT.md §2`.

---

## 4. Lanes and work queue

Full ledger: **`LANE-WP-STATUS.tsv`** — 16 queue rows Q484–Q499, 8 sealed lanes, 1 proposal package,
1 worktree entry, with SOP states, writer/verifier role, owned files, last handoff and conflicts.

**`RUNNING` appears nowhere in this package.** Nothing was running when it was written, and a state
that cannot be checked live is not claimed.

Queue scale: 595 rows. A reliable aggregate state count **cannot** be produced — `CT-QUEUE.tsv` has
embedded tabs and multi-thousand-character items, so column 3 holds prose on many rows. **Read it by
row id.** (`MISSING-OR-CONFLICTING-INFO.md` §C-5.)

### Shared-writer conflict — the one real risk

`/Users/natig/mvp6-env/bs-0190-0192-20261011-1411`, branch `feature/sce/mod-0190-0192-bounded-slice`
@ `15f9cb9b6` — **already merged into the primary branch** — still holds:

- `M` the Q495 `JwtSettings` fix (the only copy)
- `??` evidence directories for **5 sealed lanes** (35 files, the only copies)
- `??` `ReturnsClaimsHttpHost.cs` + `ReturnsClaimsObservabilityTests.cs` (the only copies)

Because the branch is merged, this worktree looks disposable and is not. Anything that prunes
worktrees or cleans untracked files destroys 35 hash-verified audit artifacts and two test files that
two MET gate cells depend on. **Secure it before anything else.**

### Stale and contradicting ledger rows

| row | issue |
|---|---|
| Q495 | ledgered DONE and reported "Fixed" in v9.5 §6.1; **not in the tree** (**F2**) |
| rows 10, MOD-0186/0187 | `GATE-18-0.tsv` NOT MET vs. a 2026-10-05 waiver — TSV stale **deliberately** (**F10**) |
| 6 rows | `VALID_PRE_MERGE / INTEGRATION_STALE` — authority withdrawn, records kept |
| v9.5 §8 | "1 commit, nothing since" and "10 sealed lanes" — both stale/unreconciled (`§C-2`, `§C-3`) |
| Q485 | tombstone file left untracked by owner decision; CT may not delete it (no-`rm` rule) |

---

## 5. Source and execution

Full detail: **`SOURCE-ENVIRONMENT.md`**. The essentials:

**Main synchronisation — the thing you asked to check first.**

| | |
|---|---|
| divergence | **29 behind, 18 ahead** of `origin/main` |
| merge base | `6b8a62d5a` (PR #144) |
| conflict test | `git merge-tree --write-tree origin/main HEAD` → **exit 0, no conflicts** |
| incoming | ~all MOD-0188 Demand Planning (new `Diten.PlanningService`, PR #146) + the İ5-A agent-loading pilot (PR #145) |
| incoming changes to `services/Diten.SupplyChainService` | **0** |
| needs hand-checking after merge | `ocelot.json` + `OcelotConfigurationTests.cs` (adds port 5068), `AGENTS.md` (4+/3−), `.claude/` `.codex/` `.antigravity/` standards surfaces |
| **gate** | the 7-item completion gate from the owner decision — **not run** after the 10-14/10-15 merges (**F5**) |

The merge is clean and is *not* therefore free: the owner decision is explicit that *"SupplyChain
source being unchanged is not SupplyChain runtime being unchanged"*, and that the five golden flows
are **mandatory** after the merge.

**Secret gate.** 48 tracked `appsettings*.json` carry a `JwtSettings.Secret` key: **24 empty**
(base configs) and **24 non-empty** (14 `appsettings.Development.json` under the owner-decided dev
pattern `c74ea4839`, plus 10 build-output copies). The next merge brings **one new** secret-bearing
file, `services/Diten.PlanningService/src/Diten.PlanningService.Api/appsettings.json`, which needs
class A/B/C classification before the merge completes. **No secret value appears anywhere in this
package** — only counts and paths. Repository sanitation is not rotation.

**Build/run commands are documented in `SOURCE-ENVIRONMENT.md` §4–§6 for reference and carry no
authority to execute.** Toolchain as measured: dotnet `8.0.417`, mongod at `/opt/homebrew/bin/mongod`,
node `v25.5.0`, `dotnet-counters 10.0.745401` (not on the default PATH).

---

## 6. Quality and acceptance evidence

Full matrix: **`OPEN-FINDINGS-AND-GATES.tsv`** (12 findings F1–F12, 5 gates G1–G5).
Freshness classes and the full method: **`METRICS-AND-METHOD.md` §2**.

**GATE-18-0: 43 of 56 MET** — 9 NOT MEASURED (browser, behind one authorization), 2 NOT MET with the
integration owner, 2 NOT MET in a stale TSV only. Reconstructed independently on 2026-10-16 from the
sealed base (37/9/10) plus four `MOVED.tsv` overlays, and it reconciles to v9.5 exactly.

**Nothing failing is hidden.** Carried forward verbatim: MOD-0184 §25 Phase 1.5 **HELD**; the Claims
test that **refuses the operational Mongo by design** (Shipments 80 of 81, that refusal being the
one failure); the `ContractErrorCodes` parity test **deliberately not written** because Q215 was
ungranted and CT had already recorded writing test code without it as self-violation V-2; and Q488,
where the Returns transition race has one winner in 13 of 13 races and **CT did not establish why**.

**Security.** Findings F7 (797 tracked build outputs, 24 of them carrying a dev secret) and F8 (the
incoming Planning config) are open. BL-534's 2026-10-14 assessment — repo public, live default
password refused, interim rule recorded — is in `docs/roadmap/backlog/product-backlog.md`.
No secret values are reproduced here.

**Where records conflict, the controlling one is named with a reason** —
`MISSING-OR-CONFLICTING-INFO.md` §C. The sharpest case: on Q495 the **tree controls over plan v9.5**,
because the plan reports a working-tree state that was lost before it was committed.

---

## 7. Measurement

Full method, denominators, formulas and reserve overlaps: **`METRICS-AND-METHOD.md`**.

| | |
|---|---|
| last CT-accepted readiness measure | GATE-18-0, **43/56 = 76.8%** of gate cells MET, denominator 56, as of 2026-10-13 (plan v9.5) |
| newer but not CT-accepted | the 2026-10-16 measurements in this package — the git geometry, secret classification, CI gap, untracked-artifact audit, checkbox counts. **Measured, not accepted.** |
| frozen baseline | GATE-18-0 as sealed, 37/9/10 |
| current forecast | **none exists.** No record projects a completion date. None is invented here. |
| second denominator | 85 open acceptance checkboxes of 161 across nine packs (61 of 129 across the seven logistics packs) |
| unestimated | MOD-0147 + MOD-0148 entirely — both `draft`, blocked on DC-01 and DC-02 |

**Two rules obeyed:** estimated effort is never shown as elapsed work, and **no completion
percentage is derived from prompt or message counts** — message volume measures conversation, not
delivery. The two denominators (56 cells, 161 checkboxes) are different instruments and are never
averaged.

---

## 8. Process and owner preferences

**Authority order** (`CLAUDE.md`): Module Pack > Domain Config > `AGENTS.md` > `.antigravity/` > Archive.
`AGENTS.md` is the contract; `CLAUDE.md` is only the doorway and must stay short. **`AGENTS.md`,
`.antigravity/` (44 rules, 20 agents, 18 workflows), `.claude/` and `.codex/` are not auto-loaded —
they must be read explicitly.** Main is about to change all four.

**Standing rules that are not negotiable**

| | |
|---|---|
| tenant isolation | every query scoped by `TenantId`; a breach is **silent** and leaks data |
| languages | platform modules 2 (en, tr) · tenant modules **7** (en, tr, fr, es, zh, ar, ru). A missing language means the work is not done. |
| git | **never commit to `main`**; one branch per module |
| screens | UAS-001 — no page skeleton and no redirect for an unauthorized user |
| tests | a rule is measured against **production** code, never a copy of itself. **A guard with no sabotage proof is not a guard.** |
| records | **K4 — a written record is never corrected.** A wrong audit is superseded by a new one; the old one stays. |
| docs placement | the five questions in `.antigravity/rules/docs-organization.md`; nothing in `docs/` root but `README.md` |
| other modules | **XMC-001** — a fix needed outside scope is *asked about first*, not made |
| record codes | **RCS-001** — a new record type's visible code comes from the shared service (`MG-ORG-000042`) |
| deletion | **no `rm`.** A reset is a new timestamped folder plus `cp`. |
| CT prompts | an **AGENT CONTRACT** is required — naming an agent is not enough: §17.4 fields plus an explicit `Read` of `.antigravity`, or G3 fails |

**Owner working preferences, as observed:** report first, parallel continuation prompts after;
one writer per file with independent VER by a different actor; protected paths respected without
asking; and git authority bounded — push and merge are the owner's, not a lane's.

### Failed approaches not to repeat

1. **Acting from an older artifact while a newer one in the tree has already settled it.** This has
   now happened **twice** — the v9.3 mistake, then CT opening a lane on MOD-0185 row 1 from
   `GATE-18-0.tsv` without re-reading v9.4, which already recorded it closed. v9.5 opens by recording
   its own failure to read v9.4. **Read the newest plan before measuring anything.**
2. **Counting with the wrong instrument.** A localization "defect" in MOD-0184 evaporated on
   re-measurement: `grep -c` counts *lines*, and a `find | head -1` had picked CRM's unrelated
   `ClaimsIndex` set. All four modules carry seven languages with equal counts.
3. **Blaming the nearest plausible component.** Row 13 was attributed to context middlewares; the
   replay signal was in the **controllers** all along. Q486 and Q484 carry the same self-correction.
   MOD-0184's `:376` reservation withholds runtime *write*, not observation, and never blocked row 13.
4. **Leaving the stack running across a long read-only excursion.** Twice this filled the volume; the
   second time it cost every shell command in the session. Cap logs, measure free space each turn.
5. **Writing test code without the authorization for it.** Recorded as self-violation V-2; the second
   occurrence was refused on purpose even though it cost the work package its real value.
6. **Editing a sealed matrix.** `GATE-18-0.tsv` is never edited; movements go in `MOVED.tsv`.

---

## 9. Continuation plan

Full prompts, scopes and exit criteria: **`NEXT-PROMPTS.md`**.

| # | work | parallel? | blocked by |
|---|---|---|---|
| 1 | **Secure the unsecured worktree work** — 35 evidence files, 2 test files, 1 config fix | independent, **do first** | nothing |
| 2 | **Merge `origin/main` (29 commits, clean) and run the 7-item completion gate** | **depends on 1** | needs 1 done |
| 3 | **Q496** — one auth write granting the lane 25 `supplychain.*` keys → **9 gate cells** | independent of 1/2 | **owner decision** |
| 4 | **Q484** — Capacity middleware re-pin, CT recommends the test-host option | independent | **owner decision** |
| 5 | **Give `Diten.SupplyChainService` a CI job** | independent | nothing |

**1, 4 and 5 are genuinely parallel. 2 depends on 1. 3 depends only on the owner.**

**One concrete decision is needed to unblock the largest single block of work:** Q496 — a single
write into the lane's own disposable auth database, granting the lane account the 25 `supplychain.*`
permission keys. It closes **nine of the thirteen** open gate cells. CT could not run it; the harness
refused with reason `[Permission Grant]`, and refused a second command that merely *counted* the
keys, so the refusal is on the subject matter rather than the command.

**No new scanning loop is proposed for anything still BLOCKED.** Rows 3 (integration owner), the
gateway release (decided, unassigned), MOD-0147/0148 (DC-01/DC-02), Q15, Q322, Q493, Q498 and the
MOD-0184 runtime-write release are unchanged since they were last measured. They need owners and
decisions, not another measurement pass.
