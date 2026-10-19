# MVP6 development plan v9.9 — 2026-10-19

Supersedes v9.8 (same day). Every number below is measured on `feature/mvp6-logistics`; the commit or record that
moved it is named. Step-1 numbers are on HEAD `522565d07` (the merged tree) plus, for the SupplyChain re-run only, the
uncommitted ClaimReplay change of step 3.

## 1. What changed since v9.8

- **`origin/main` merged into the branch locally** — `522565d07` (main @ `37530d190`, 77 commits: new CT /
  `.antigravity` rules, `tests/known-failures.tsv` + `scripts/known_failures.py`, SOP r3 / R8). Branch is 0 behind
  main, 114 ahead; **78 commits exist only on this machine** (not pushed).
- **Step 1 (merged-tree preflight) runs on HEAD itself** — no scratch clone needed, HEAD is the merged tree. Lane
  mongod on slot 5 (`127.0.0.1:35994`, replica set `rsct1`, `enableTestCommands=1`), operational 27017 untouched.
- **ClaimReplay: a filter, not a known-failures row** (review 2026-10-19). The test is not broken: it refuses to run
  without `CLAIMS_RESTART_MODE` (`ClaimReplayTests.cs`, "exclude from ordinary suite"). The registry is for real
  defects awaiting a fix; registering a by-design refusal would leave it there for good. Chosen form: the repo's own
  conditional-fact pattern (`MacOsFactAttribute` in Auth `UnixConditionalFacts.cs`, Planning
  `ManualDraftMongoFactAttribute`), not `[Trait]` + `--filter` — the repo has no `Trait("Category")` use and CI runs
  whole projects with no `--filter` on purpose (`service-tests.yml`, CT-CI-MACOS-01 note).
- **Rule 10 kept:** a skipped test is not a proven test. So the child stays skipped only in the ordinary suite, and a
  new ordinary test starts it twice itself (`write`, then `read`, separate `dotnet test` processes — the
  `CapacityRestartTests` pattern), so the restart proof runs in every suite run, CI included.

## 2. Measured state

| | value | source |
|---|---|---|
| Web | 2621/2621 · `check --suite` clean | TRX, HEAD |
| SupplyChain (HEAD) | 590/591 · `check --suite` YENİ=1 = ClaimReplay `msg:f55034968f3321e8` — by design, not a defect | TRX, HEAD |
| SupplyChain (HEAD + step 3) | **591 passed / 0 failed / 1 skipped (the child)** of 592 · `check --suite` clean | TRX, working tree |
| Auth Application (whole project) | 2709 / 0 / 44 skipped of 2753 · clean (v9.8's 848/0/7 was the Roles subset only) | TRX, HEAD |
| Gateway | 259/259 · clean | TRX, HEAD |
| Platform CompositionGuard | 12/12 · clean | TRX, HEAD |
| Planning Cycles (0188) | 350/350 · clean — on a lane set named `rs-mod0188` (the tests refuse any other set name; a first run on `rsct1` failed all 145 Mongo tests in 1 s on that guard) | TRX, HEAD |
| **Platform Application** | **8646 / 2 failed / 2 skipped** · `check --suite` YENİ=2 — `LoginOnlyEndpointGuardTests` (2 tests) name MOD-0022 e-signature controllers (`InternalESignActionsController.Register`, `ESignatureSamplesController.Image`, `ESignatureSettingsController.ListMeanings`). **From main, not this branch:** the branch differs from main in Platform by 4 files only (correlation + one test); the controllers and the guard are byte-identical to main | TRX, HEAD; `git diff --stat origin/main HEAD -- services/Diten.Platform*` |
| Why these suites | the branch changes `gateway/` (6 files), `Platform.Common` (`SignedCorrelation`, 3), `Platform` (1 test); Planning has no change, but 0188 had no independent run record | `git diff --stat origin/main...HEAD` |
| Independent VERs | 6-UI PASS · G8 PASS (+ delta PASS) | v9.8 |
| Pack §16 (shipment bundle) | unchanged from v9.8 | v9.8 |
| Branch vs main | 114 ahead / 0 behind; 78 commits not pushed | `git rev-list` |
| `sce-mvp6` task source | registered, `owner_confirmation = pending` | `execution/delivery/ct-queue-registry.tsv:2` |

## 3. Finish line (owner-ruled) and the sequence

**MVP-6 done = shipment bundle + S&OP/Capacity + DemandPlanning (UI, fixture-proven), merged to main.**

One numbering, in order. Step 0 gates new dispatch (2, 4, 8) only; it does not block step 1, which pre-checks work
that already exists.

| # | step | owner | state |
|---|---|---|---|
| 0 | Owner confirms the `sce-mvp6` task-source row (R8, SOP r3) | owner | **open** — `pending` |
| 1 | Merged-tree preflight: Web, SupplyChain, Auth, Gateway, Platform, Planning — TRX, each checked with `known_failures.py check <trx> --suite <project>` | CT | **done** — no regression from this branch; ClaimReplay closed by step 3; 2 Platform reds come from main (1a) |
| 1a | Route the 2 main-originated Platform reds (MOD-0022 e-signature, `LoginOnlyEndpointGuardTests`) to the PSS / MOD-0022 owner — gate the 3 actions or add `[LoginOnly]` / the internal allow-list. Not ours to fix (XMC-001); not registered in `known-failures.tsv`, so main's own `check` is red too | owner → PSS | open |
| 2 | Push the branch (78 local-only commits + steps 1/3) right after step 1's record — not waiting for the PR | owner approves | open |
| 3 | ClaimReplay: `[ClaimsRestartChildFact]` on the child + `Receipt_DurableRecovery_AcrossTwoTestProcesses` runs it as write → read in two `dotnet test` processes | CT | **done, uncommitted** — pair green (PIDs 98792 write / 98796 read, same claim, read `REPLAY=True`); **sabotage**: read on another tenant → child exit 1, parent red; reverted → suite 591/0/1 |
| 4 | SupplyChain + Web (+ Planning) suites into `service-tests.yml` — today every green number is local | CT / CI owner | open |
| 5 | Open and merge the PR (`docs/roadmap/plans/mvp6-pr-to-main-01/PR.md`) | owner | after 1–4 |
| 6 | Temporary Admin widenings (Q358, BL-652, G7): expiry guard + re-approve or replace by entitlements | owner | **deadline 2026-11-03** |
| 7 | Apply BL-652 v2 (Admin baseline for Carrier/Load/Return/Claim) | owner | patch ready (`…-v2-after-g7.patch`) |
| 8 | Gateway `Observability:Correlation:SigningKey` in dev/live config | owner | open |

**Critical path:** 2 (push) → 4 (CI) → 5 (PR), with 6 before 2026-11-03 and 0 before 4 is dispatched. 1a runs in
parallel; it does not block our PR but main's Platform suite stays red until PSS acts.

**Disk:** 1.7 GiB free after this run (two lane mongods, TRX, builds); MongoDB needs ≥ 500 MB.

**Out of this PR:** CRM `.tmp-*` build folders tracked on main (740 files, `0f71a237c`) — CRM owner's own small PR
(XMC-001); we do not touch another module.

**Dates:** temp grants expire **2026-11-03**. R8's 30-day transition counts from SOP r3's merge to main — main got r3
in #147 (`913d725a1`, 2026-10-19), so it ends about **2026-11-18** (inference from the record
`ct-r8-transition-period-owner-decision-01`; the owner confirms the start day).

## 4. After the PR (hardening, not in the finish line)

H1 dev JWT secret fail-fast, out of 23 appsettings · H2 test hosts mint AuthService-shaped tokens + a legal-entity
failing path per gate · H3 BL-615 audit ledger (joint with CRM) · H4 one adapter base.

## 5. Post-MVP-6 (named, not in the finish line)

DemandPlanning production authorities (S1, AC-D01…D29); DEMAND v2 (G3) to replace the S&OP/Capacity fixture seed;
SUPPLY-CONSTRAINTS producer MOD-0193 (G2); BL-652 Option 2 (SupplyChain → MDM service identity); the parked process
audit (`memory: mvp6-process-audit-2026-10-19`).
