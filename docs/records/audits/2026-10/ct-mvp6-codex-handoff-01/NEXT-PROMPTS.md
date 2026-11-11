# NEXT WORK — the five most critical, in order

Priority order. **1, 4 and 5 are genuinely parallel. 2 depends on 1. 3 depends only on an owner
decision.** Each item gives its exact starting input, the scope it may touch, and what "done" means.

Prompts below are copy-pasteable for Codex. They are written to be read **with** this package, which
Codex should be pointed at: `docs/records/audits/2026-10/ct-mvp6-codex-handoff-01/`.

---

## 1 · Secure the unsecured worktree work — DO THIS FIRST

**Why first:** one worktree holds the only copy of 35 hash-verified audit artifacts whose seals are
already committed, two test files that two MET gate cells depend on, and the Q495 configuration fix.
Its branch is already merged, so the worktree looks disposable. Anything that prunes worktrees or
cleans untracked files destroys it.

| | |
|---|---|
| **exact starting input** | `/Users/natig/mvp6-env/bs-0190-0192-20261011-1411` on branch `feature/sce/mod-0190-0192-bounded-slice` @ `15f9cb9b6`; the committed seals at `docs/records/audits/2026-10/mvp6-stage-r-{row13-01,row13-02-returns-claims,row5-returns-01,row1-loads-config-01}/ARTIFACTS.sha256` and `mvp6-q484-capacity-branch-01/ARTIFACTS.sha256` |
| **permitted scope** | commit only: the 5 `evidence/` directories, `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Common/ReturnsClaimsHttpHost.cs`, the same directory's `ReturnsClaimsObservabilityTests.cs`, and `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.Development.json`. Touch nothing else. Do not edit any `ARTIFACTS.sha256`, any `REPORT.md` or any `MOVED.tsv` (**K4**). |
| **exit criterion** | every path in those five `ARTIFACTS.sha256` resolves to a tracked file whose SHA-256 equals the sealed hash; `ReturnsClaimsObservabilityTests` passes from a clean checkout; the tracked SupplyChain config carries `JwtSettings` and the service boots |
| **blocked by** | nothing |

```text
Read docs/records/audits/2026-10/ct-mvp6-codex-handoff-01/START-HERE.md and SOURCE-ENVIRONMENT.md
first, then findings F1, F2 and F3 in OPEN-FINDINGS-AND-GATES.tsv.

Task: secure the unsecured work in the worktree /Users/natig/mvp6-env/bs-0190-0192-20261011-1411
(branch feature/sce/mod-0190-0192-bounded-slice @ 15f9cb9b6, already merged into
feature/mvp6-logistics). It holds the only copy of three things:

  1. evidence/ directories for five sealed lanes under docs/records/audits/2026-10/ —
     mvp6-stage-r-row13-01, mvp6-stage-r-row13-02-returns-claims, mvp6-stage-r-row5-returns-01,
     mvp6-stage-r-row1-loads-config-01, mvp6-q484-capacity-branch-01.
     Their ARTIFACTS.sha256 seals are ALREADY COMMITTED; 39 of 48 hashed artifacts are not in git.
     As of 2026-10-16, 35 of them were verified present in this worktree with 0 hash mismatches.
  2. services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Common/
     ReturnsClaimsHttpHost.cs and ReturnsClaimsObservabilityTests.cs — the Q489 HTTP test seam that
     moved GATE-18-0 row 13 to MET for MOD-0186 and MOD-0187. Both untracked. Two MET cells
     currently rest on files that are not in the repository.
  3. An uncommitted edit to
     services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.Development.json
     adding JwtSettings (Secret, Issuer diten-auth-service, Audience diten-erp). This is the Q495
     fix. Plan v9.5 section 6.1 reports it as landed; it is NOT in the tree. Program.cs:46
     Required("JwtSettings:Secret") still throws, so SupplyChain cannot boot from a clean clone.

Before committing, re-verify every sealed hash with shasum -a 256 against the committed
ARTIFACTS.sha256 and report any mismatch instead of committing it.

Constraints: commit to feature/sce/mod-0190-0192-bounded-slice or feature/mvp6-logistics — never to
main. Do not edit any ARTIFACTS.sha256, REPORT.md or MOVED.tsv: records are never corrected (K4). Do
not run git clean, git worktree prune or rm anywhere. Put no secret VALUE in any commit message or
record. Do not merge origin/main in this task.

Report: which paths were committed, the hash verification result per lane, and whether
SupplyChain now boots from the tracked configuration.
```

---

## 2 · Merge `origin/main` and run the completion gate

**Why second:** the merge is clean today and the branch is 29 commits behind. But it must not run
while item 1 is unsecured, and it is governed by an owner decision that makes it *"the MVP-6
integrated acceptance gate"*.

| | |
|---|---|
| **exact starting input** | `origin/main` `3912d568b18193f627539a1b27965167ac4077b6`; merge base `6b8a62d5a`; the decision `docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md` |
| **permitted scope** | the merge; bucket-3 semantic resolution in `ocelot.json`; a new audit record under `docs/records/audits/2026-10/`. **No rebase.** |
| **exit criterion** | all 7 gate items recorded with evidence, including the five SupplyChain golden flows re-run and zero known secret material in the result |
| **blocked by** | item 1 |

```text
Read docs/records/audits/2026-10/ct-mvp6-codex-handoff-01/SOURCE-ENVIRONMENT.md section 2 and
docs/records/decisions/2026-10/mvp6-merge-origin-main-owner-decision-01.md in full before starting.

Preconditions: item 1 of NEXT-PROMPTS.md is committed. Confirm this before touching anything.

Task: merge origin/main (3912d568b) into feature/mvp6-logistics and satisfy the owner's 7-item
completion gate. Measured 2026-10-16: 29 behind / 18 ahead, merge base 6b8a62d5a, and
git merge-tree --write-tree origin/main HEAD returns exit 0 with no conflicts.

The owner decision is binding on method: MERGE, NEVER REBASE — rewriting the 18 commits
re-identifies them and costs audit traceability. Upstream arrives as one integration event.

The 7-item gate, verbatim from the decision:
  merge conflicts resolved
  zero known secret material in the resulting tree
  Platform / Auth targeted regression passes
  gateway validation passes
  frontend baseline passes
  five SupplyChain golden flows rerun
  required MVP-6 gates rerun

Note that this gate has NOT been run after the two merges already executed on 2026-10-14 and
2026-10-15 (finding F5), so you are closing that debt too, not only this merge.

Check by hand, do not take ours/theirs wholesale (the decision's conflict bucket 3):
  - gateway/Diten.ApiGateway/ocelot.json and gateway/Diten.ApiGateway.Tests/
    OcelotConfigurationTests.cs — main adds port 5068 for the new Planning service to
    KnownDownstreamPorts. SupplyChain's 5066 is already in that set on both sides, so this should be
    additive; verify duplicate routes, ordering and auth policy, then a gateway runtime smoke.
  - AGENTS.md (4 insertions, 3 deletions on main) — it is the engineering contract, authority above
    .antigravity/. Read the incoming diff rather than accepting it.
  - .claude/, .codex/, .antigravity/ — PR #145's agent-loading pilot changes all three plus
    .antigravity/scripts/sync_adapters.py and adds .github/workflows/agent-adapters.yml.
  - Q363 (IInternalScopeResolutionContext is registered 0 times on origin/main — the defect is live
    there and our 14 lines are new) and Q302 (eager JWT validation in HumanCapital and
    TalentEcosystem Program.cs; main changed 2+/1- lines in each). Both local deltas MUST survive
    and must be RUNTIME-TESTED, not merely present.

Secret gate: the merge brings one new secret-bearing file,
services/Diten.PlanningService/src/Diten.PlanningService.Api/appsettings.json. Classify it A/B/C per
the decision and sanitise if B, recording upstream provenance. Write NO secret value into any record
or commit message. Repository sanitation is not rotation — if anything is live, say so and leave
rotation as a separate owner item.

The incoming 29 commits are almost entirely MOD-0188 Demand Planning (new services/
Diten.PlanningService, PR #146) and touch zero files under services/Diten.SupplyChainService. That
lowers source-conflict risk, not integration risk: the decision is explicit that SupplyChain source
being unchanged is not SupplyChain runtime being unchanged.

Record the result as a new lane under docs/records/audits/2026-10/ with an ARTIFACTS.sha256. Never
commit to main. Claim no integrated PASS until every gate item has evidence (K18).
```

---

## 3 · Q496 — one authorization write, nine gate cells

**Why third:** the highest leverage single action in the programme — nine of thirteen open cells —
but it is **an owner decision, not a task.** Do not open a measurement lane for it.

| | |
|---|---|
| **exact starting input** | `CT-QUEUE.tsv` row Q496; `docs/records/audits/2026-10/mvp6-stage-r-stack-up-01/STACK.tsv`; `docs/roadmap/plans/mvp6-ui-pack-wave-q322-01/LIVE-DENIED-ACCESS.tsv` |
| **permitted scope** | one write into the lane's **own disposable** auth database — nothing in any shared or live environment |
| **exit criterion** | the lane account holds the 25 `supplychain.*` keys; GATE-18-0 rows 2 (×4), 7 (×2), 12 (×3) measured in a browser; results recorded in a new sealed lane with `MOVED.tsv` |
| **blocked by** | **owner decision — this is the one concrete decision needed** |

**The decision to put to the owner, in one sentence:** *may a lane grant its own disposable auth
database's test account the 25 `supplychain.*` permission keys, in order to measure the nine
remaining browser gate cells?*

Everything in front of it is already proven: the stack comes up (6 services + loopback credential
helper + 1 mongod, all measured LISTENING), sign-in works and leaked nothing, routing/shell/denied
state are confirmed live on all four modules, and row 2's saved-view chain is measured end to end.
Rows 7 and 12 already have their server halves in the suites. CT could not perform the write — the
harness refused with reason `[Permission Grant]` and refused even a command that *counted* the keys.

---

## 4 · Q484 — Capacity middleware re-pin (owner decision)

| | |
|---|---|
| **exact starting input** | `CT-QUEUE.tsv` row Q484; `docs/records/audits/2026-10/mvp6-q484-capacity-branch-01/REPORT.md`; Q292 (owner-approved 2026-10-03) and Q298 |
| **permitted scope** | none until decided — this is a decision, not work |
| **exit criterion** | an owner decision record under `docs/records/decisions/2026-10/` |
| **blocked by** | **owner decision** |

CT **refused** the re-pin the release included, because Q292 keeps that hash and Q298 executed it
*"without altering its hash"*. **CT recommends the test-host option**, on the ground that
`SandopCapacityHttpHost`'s own header records the owner reading Q292 on 2026-10-11 as **not binding a
test host**. Measured alongside: the production container is intact with the branch in place and
`CompositionRootGuardTests` is 3/3. **The recommendation is not the decision.**

---

## 5 · Give `Diten.SupplyChainService` a CI job

| | |
|---|---|
| **exact starting input** | `.github/workflows/service-tests.yml`; `scripts/test-env/mod0188-ci-mongo.sh` on `origin/main` as the pattern for a Mongo-backed service job |
| **permitted scope** | `.github/workflows/service-tests.yml`, a new `scripts/test-env/` helper if needed |
| **exit criterion** | SupplyChain suites run in CI with per-module Mongo URIs; the Claims test that refuses the operational Mongo by design is handled explicitly rather than silently excluded |
| **blocked by** | nothing — but do it **after** item 2, or the workflow file will conflict with the incoming merge |

```text
Read docs/records/audits/2026-10/ct-mvp6-codex-handoff-01/OPEN-FINDINGS-AND-GATES.tsv finding F4 and
SOURCE-ENVIRONMENT.md section 6 first.

Task: add a CI job for Diten.SupplyChainService in .github/workflows/service-tests.yml.

Measured 2026-10-16: the string "SupplyChain" appears NOWHERE in .github/ — not on
feature/mvp6-logistics and not on origin/main. Thirteen services have jobs on main
(ApiGateway, AuthService, Building, BuildingBlocks, DevEnablementService, InventoryService,
ManagementGovernanceService, ManufacturingService, MdmService, PlanningService, Platform,
ProcurementService, PvgService). The service that carries all seven MVP-6 logistics modules is not
one of them.

Use origin/main's scripts/test-env/mod0188-ci-mongo.sh and its "recognize Planning Mongo CI steps"
commit (9bd834404) as the pattern for a Mongo-backed service job.

Requirements that will otherwise bite:
  - One Mongo URI variable PER MODULE; Capacity is MVP6_MOD0192_MONGO_URI. The Q266 recipe on port
    57061 yields 417/418.
  - Do NOT repoint Platform tests: they hard-code 27017.
  - Mongo:DatabaseName has never been tracked and every lane supplies its own; Loads:ReferenceBaseUrl
    and its siblings ARE tracked.
  - SupplyChain is port 5066, NOT 5061 — owner decision OD-P1 renumbered it and gave 5061 to CRM.
    TrackedConfigurationGuardTests already asserts this; keep it passing.
  - One Claims test REFUSES the operational Mongo by design. Handle it explicitly and say so in the
    job; do not silently exclude it and do not report a green run that hid it.
  - This job cannot pass until item 1 commits the JwtSettings fix (finding F2) — a clean checkout
    cannot boot the service without it.

Do this after the origin/main merge (item 2) or the workflow file will conflict.
```

---

## Not proposed, deliberately

These are **BLOCKED and unchanged** since they were last measured. They need owners and decisions,
not another scanning pass: GATE-18-0 rows 3 (integration owner), the gateway explicit-routes release
(decided 2026-10-05, unassigned since 2026-10-10), MOD-0147/0148 (DC-01 and DC-02 both open), Q15
(one registered checkout, the Stage 2 gate), Q322 (four UI pack revisions, needs owner approval per
pack), Q493 (personalization key conventions, does not block row 2), Q498 (contract under-declaration,
owner contract edit) and the MOD-0184 runtime-write release at `:376`.

One more that is **work, not a decision**, and ranked sixth rather than dropped: MOD-0188 on main
already consumes the **frozen 3.2.0** contract while **3.3.0** — built for its demand history — sits
pushed but unmerged at `4b4722ece` with the PR left for the owner. Worth one line to the owner when
item 2 lands.
