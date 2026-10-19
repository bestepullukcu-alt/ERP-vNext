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
| Branch vs main | pushed `db9e79e84` (ls-remote = local) after merging main #150 (`--no-ff`, run by the owner); main then moved 6 more (#151, CT-APR-01 version-bound Module Pack approval + dispatch gate, BL-656) — merge-tree clean, not yet merged | `git ls-remote`, `git rev-list` |
| `sce-mvp6` task source | registered, `owner_confirmation = pending` | `execution/delivery/ct-queue-registry.tsv:2` |

## 3. Finish line (owner-ruled) and the sequence

**MVP-6 done = shipment bundle + S&OP/Capacity + DemandPlanning (UI, fixture-proven), merged to main.**

One numbering, in order. Step 0 gates new dispatch (2, 4, 8) only; it does not block step 1, which pre-checks work
that already exists.

| # | step | owner | state |
|---|---|---|---|
| 0 | Owner confirms the `sce-mvp6` task-source row (R8, SOP r3): `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` @ `6b8a62d5a` is SCE / MVP-6's one registered task source, written only by the MVP-6 Control Tower and shared with no other | owner (Ali) | **open** — explained 2026-10-19, decision pending |
| 1 | Merged-tree preflight: Web, SupplyChain, Auth, Gateway, Platform, Planning — TRX, each checked with `known_failures.py check <trx> --suite <project>` | CT | **done** — no regression from this branch; ClaimReplay closed by step 3; 2 Platform reds come from main (1a) |
| 1a | The 2 main-originated Platform reds (MOD-0022 e-signature, `LoginOnlyEndpointGuardTests`: `InternalESignActionsController.Register`, `ESignatureSamplesController.Image`, `ESignatureSettingsController.ListMeanings`) | MVP-1 lane | **routed, owner-ruled 2026-10-19:** the three endpoints are MOD-0022 (MVP-1) scope and are fixed in the MVP-1 lane; MVP-6 does not touch them. In MVP-6 comparisons they count as **known reds from main**, not new reds. They are not in `tests/known-failures.tsv` (that row would be MVP-1's), so `check --suite Diten.Platform.Application.Tests` still prints them as YENİ — read those two lines as known |
| 1b | The 2 main-originated Web reds (MOD-0191 SafetyStock, PR 155): `NavManifestL10nGuardTests` — `Nav.Module.SAFETYSTOCKOPTIMIZATION` missing in all seven SharedResource languages, and `SafetyStockManifestProvider.cs` yields no pages. Main's resx lacks the key too; SafetyStock and the guard are byte-identical to main (not merge damage) | MOD-0191 owner | **owner-ruled 2026-10-19:** known reds from main, routed to the MOD-0191 owner; MVP-6 does not touch them and does not count them as new reds |
| 2 | Push the branch | owner approves | **done** — `db9e79e84`, ls-remote verified; merge of main #150 by the owner, nav-key check self-test 30/30, 12 known / 0 new; **A-8 after the gate:** `1cded220a` pushed, ls-remote verified, 0 behind main |
| 2a | **A-7 full gate on the merged tree** (`1cded220a`, main PRs 151–157 in; mongod 8.0.18 lanes; `DOTNET_ROOT=$HOME/.dotnet`) | CT | **done — no new red from MVP-6.** SupplyChain 591/0/1 · Auth 2795/0/44 · Gateway 259/0 · CompositionGuard 12/0 · Planning 350/0 · ArchitectureTests 141/3 (3 BİLİNEN) · Platform 8683/2 (MOD-0022, 1a) · Web 2689/2 (MOD-0191, 1b); every suite `known_failures check --suite` |
| 3 | ClaimReplay: `[ClaimsRestartChildFact]` on the child + `Receipt_DurableRecovery_AcrossTwoTestProcesses` runs it as write → read in two `dotnet test` processes | CT | **done, uncommitted** — pair green (PIDs 98792 write / 98796 read, same claim, read `REPLAY=True`); **sabotage**: read on another tenant → child exit 1, parent red; reverted → suite 591/0/1 |
| 4 | CI coverage. Web already runs in `phase1-gates.yml`, Planning in the `planning-tests` row — only SupplyChain was missing. Row `supplychain-tests` + `scripts/test-env/supplychain-ci-mongo.sh` (owned single-member replica set, `enableTestCommands=1`, 8 variables via `$GITHUB_ENV`) + allow-list `CLAIMS_RESTART_CHILD` max 1 + guard (`NotInThisJob` entry out, two `ExpectedIfs` in). From the 2026-10-16 package `mvp6-supplychain-ci-row-01` (owner decision, WP-LANE-MAIN-SYNC-05), applied with `git apply` as `mvp6-supplychain-ci-row-02/supplychain-ci-row.patch` | CT | **applied, uncommitted.** Local runner simulation: script start → 37994 PRIMARY, 8 vars; suite with the CI command 591/0/1; `Skipped = 0` ok for `supplychain-tests` (and red without `--suite`, so the allowance is row-bound); `CiServiceTestsGuardTests` 50/50; sabotage (start step's `if:` removed) → guard red, reverted → green. **Same mongod as CI, measured:** `mongodb-macos-arm64-7.0.28.tgz` (SHA-256 = the workflow's `MONGODB_MACOS_SHA256`, scratchpad only) started by `supplychain-ci-mongo.sh` itself → `7.0.28 rs-supplychain PRIMARY`; suite with the CI command 591/0/1; `Skipped = 0` ok; `known_failures check` clean. **Not measured:** Linux and the `stop` path (it reads `/proc`) — the first CI run is that measurement |
| 4b | Runbook gap (open item, not blocking): `.antigravity/rules/dev-runbook.md:26` and CI say MongoDB **7.0**; this dev machine runs **8.0.18**. **Owner ruling 2026-10-19 (Ali):** not blocking; 8.0 may be used locally; CI is the measure. SupplyChain was also measured on the pinned 7.0.28 (591/0/1). The runbook text vs the machine stays an open item | owner | open item |
| 4a | Docs-path authority pinned shipment-bundle **3.1.0** (`6dc1…`), so `DocsPathGuardTests` failed on the hash. **Owner ruling 2026-10-19 (Ali):** pin main's approved 3.2.0 on main by its own small PR; pin 3.3.0 in the MVP-6 PR. | CT | **done, not pushed.** Main side: branch `feature/sce/mod-0183-docs-path-repin-320` — `9398a9a35` (new decision record `MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-320-REPIN-01`, payload `4bf6fd42…`) + `40f76a646` (registry: 4 AuthorityFixture rows `fixed`, NoCode row new fingerprint `msg:b34871e2e1abbcc1`; `validate` OK, `check --suite` BİLİNEN=3 YENİ=0). MVP-6 side: `40df6861c` (3.3.0 `c59b…`, payload `52f9dd12…`). When main brings in the 3.2.0 PR, the authority's 3 lines conflict → keep 3.3.0 |
| 4c | **Open question — where contracts live.** With the hash fixed, `NoCodeFilePointsIntoDocsOutsideTheFiveFolders` runs its real check for the first time: `docs/analysis/` is not one of the five folders (vendor · records · roadmap · guides · reference), yet the frozen contracts live in `docs/analysis/contracts/`. Main: ~44 references (CT scripts `acceptance_record.py`, `prompt_gate.py`, `status_report_v2.py` and tests, Manufacturing, permission-gate backups, older MVP-6 `ContractErrorCodes.cs` / `ContractErrorCodeParityTests.cs`). This branch adds 3 (`ShipmentBundleContract.cs:9,17`, `supply-constraints.openapi.draft.yaml:160`, `mvp6-sandop-capacity-conformance-01/evidence/compare2.py:2`), so its NoCode fingerprint differs from main's and reads YENİ-AYRINTI until decided. Options: move contracts into one of the five folders (every consumer changes), or name `docs/analysis/contracts/` an allowed target in the rule (`docs-organization`, a rule change via AGENTS.md / `.antigravity` approval). Cross-team (CT tooling, Manufacturing, CRM) | owner | open |
| 5 | Open and merge the PR (`docs/roadmap/plans/mvp6-pr-to-main-01/PR.md`) | owner | after 1–4 |
| 6 | Temporary Admin Shipment grant (Q358, `DefaultRolePermissionTemplate.AdminModules` `shipment-tracking-pod`, `DefaultRolePermissionTemplate.cs:57-68`) | CT prepares, owner applies | **owner ruling 2026-10-19 (Ali): removed at expiry 2026-11-03.** Before that, as its own small work item: measure in a test tenant that Shipments permissions arrive through entitlements; then a revert commit takes `shipment-tracking-pod` out of `AdminModules` |
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
