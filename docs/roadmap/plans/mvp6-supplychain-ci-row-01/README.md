# The SupplyChain CI row — a prepared change CT could not apply

**Why this is a package and not a commit.** Three of the five files the change needs are denied to CT by
`.claude/settings.local.json`:

| file | needed for | CT access |
|---|---|---|
| `scripts/test-env/supplychain-ci-mongo.sh` (new) | the owned replica set | **denied** — `Edit(scripts/**)` |
| `scripts/ci-skip-allowlist.tsv` | the Claims restart skip | **denied** — `Edit(scripts/**)` |
| `tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs` | removing the `NotInThisJob` row | **denied** — `Edit(tests/**)` |
| `.github/workflows/service-tests.yml` | the matrix row and its two steps | allowed |
| `services/Diten.SupplyChainService/tests/.../Claims/` | the skip attribute | allowed |

`scripts/**` and `tests/**` are protected on purpose, so CT did not route around them: inlining the
script into the workflow would both break `CiServiceTestsGuardTests` (it reads the workflow text for
`--replSet`, `--dbpath` and `mongodb://`) and defeat what the deny rule protects. A workflow row
without its script would leave CI broken, so **nothing was committed** — the whole change is here.

The four files' contents are in this directory, ready to copy. Apply them together: the guard asserts
`rows.Intersect(NotInThisJob.Keys)` is empty, so the matrix row and the `NotInThisJob` removal must
land in the same change or the guard is red either way.

## What was decided, and by whom

Two owner decisions in session (2026-10-16), both after CT laid out the alternatives:

1. **Follow the `planning-tests` precedent** — a job-local mongod started from a script — **and record
   the tension** rather than hide it. CT decision D says *"no row starts a job-local mongod; every
   service test owns its mongod"*, and the guard enforces it by reading the **workflow** text. A mongod
   started from a script passes that check while doing what the decision forbids. `planning-tests`,
   added by `origin/main` on 2026-10-16, does exactly this. The alternative — a shared fixture that
   starts its own replica set — would reverse Q131a's deliberate *"there is no fallback port; a missing
   URI fails closed"* across **seven** call sites plus three child-process tests that inherit the
   variable from their parent. That is its own work package. **BL-650** carries the tension to
   decision D's owner.

2. **The Claims restart test skips with an allow-listed code**, the same mechanism
   `manufacturing-tests` uses for `MANUFACTURING_TEST_MONGO`. Today it *throws*
   (`"Explicit write/read restart mode required; exclude from ordinary suite."`), which CI counts as a
   failure, so the row could never be green. A `Skip` with a code expresses the author's own stated
   intent — *exclude from ordinary suite* — in the language the harness understands.

## Why the row needs a replica set and test commands, not a plain mongod

Measured, not assumed:

- `ShipmentSchema.StartAsync` refuses to start without transaction support: *"MOD-0183 requires Mongo
  transaction support (replica set or mongos)"* — it reads `hello` and requires `setName` or `isdbgrid`,
  then opens a transaction before accepting traffic. A standalone mongod is rejected.
- `CapacityTestMongo`'s own doc: the tests *"use transactions, fail points (mongod started with
  enableTestCommands=1) and a second test process"*, and it rejects ports 27017–27021 itself.
- Seven fail-closed variables, from the `GetEnvironmentVariable` call sites in the suite:
  `MOD0183_TEST_MONGO`, `MOD0184_TEST_MONGO`, `MOD0185_TEST_MONGO`, `MVP6_MOD0190_MONGO_URI`,
  `MVP6_MOD0192_MONGO_URI`, `RETURNS_MONGO_URI`, `CLAIMS_TEST_MONGO` — plus
  `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI`, without which `CompositionRootGuardTests` refuses to run
  (Q492, fixed in `82b50e604`; the lane script now prints it too).

## What CT verified locally, and what it could not

**Verified** on the merged tree, with a single-member replica set started by the lane script's own port
rule and `enableTestCommands=1`:

- the full suite: **529 passed, 1 failed, 0 skipped of 530** — the one failure being exactly the Claims
  restart test this change converts to a skip, so with the skip in place the row is **530/530 with one
  allow-listed skip**;
- `CompositionRootGuardTests` 3/3 with the guard variable set, and a loud refusal without it.

**Not verified:** the workflow itself. CT cannot run GitHub Actions, and the three denied files mean the
change has never executed as a unit. Treat the port choice (35994–38994, deliberately clear of
`mod0188-ci-mongo.sh`'s 31994–34994), the `pymongo==4.10.1` venv step and the `$GITHUB_ENV` exports as
**reviewed but unexecuted**. The first CI run is the measurement.
