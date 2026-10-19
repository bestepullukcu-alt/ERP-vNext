---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# BL-650 — decision D's intent diverged from what its guard measures: a decision, with the patch for the recommended option

    item      : BL-650 (v9.6 §5 row 11) · owner: CT (decision D's owner)
    author    : CT (gmg-10)
    measured  : 2026-10-17 on feature/mvp6-logistics at HEAD
    status    : decision owed by CT; this package carries the evidence, a recommendation, and apply-ready
                patches for the two cheap options. The guard is under tests/ (a protected path), so nothing
                is applied here.

## The divergence, measured on this branch

The rule `No_row_starts_a_job_local_mongod_or_hands_the_tests_a_mongo_address` in
`tests/architecture/TenantArchitecture.ArchitectureTests/CiServiceTestsGuardTests.cs:183` reads the
**text of** `.github/workflows/service-tests.yml` and asserts it contains no `--replSet`, `--dbpath`,
`mongo_rs_initiate`, `mongodb://`, nor any returning Mongo-address variable. Its intent comment says
*"no row starts a job-local mongod and no step hands the tests a Mongo address. Every service test owns
its mongod."*

But a row that starts mongod **from a script** passes the text check while doing what the intent forbids.
Confirmed here, not hypothetically:

- `.github/workflows/service-tests.yml:100` has the `planning-tests` row; `:191` runs
  `bash scripts/test-env/mod0188-ci-mongo.sh start`.
- `scripts/test-env/mod0188-ci-mongo.sh` contains `--replSet` and `--dbpath`.
- So the job-local mongod is started, the guard is **green**, and the intent is already breached once.

The SupplyChain CI row (`mvp6-supplychain-ci-row-01`) is built to follow the same pattern with its own
`supplychain-ci-mongo.sh`, so decision D's intent would be breached a **second** time. BL-650 exists so
this is recorded, not hidden.

## The three options (from the backlog), and the recommendation

| | option | cost | what it does | residual |
|---|---|---|---|---|
| **1 ✅** | **Revise the intent**: a job-local mongod may start only from a named `scripts/test-env/` script, each row with a rationale; the guard pins those scripts' existence | low (comment + one assertion) | realigns intent with the pattern the product already uses twice; upgrades the guard from text-absence to a structural check | a script **outside** `scripts/test-env/` that started a mongod would still evade it (narrower than today's gap) |
| 2 | Keep the intent, make the guard read the scripts too | **high — its own work package** | `planning-tests` and the SupplyChain row go red; both redesigned so the tests own their mongod | reverses Q131a's deliberate "no default, missing URI fails closed" across 7 call sites + 3 child-process tests |
| 3 | Leave the behaviour; only fix the comment so it says "the workflow **text** names no job-local mongod" | lowest (comment only) | makes the guard honest about its scope | leaves script-launched mongods entirely unmeasured |

**Recommended: Option 1.** It is the only cheap option that both tells the truth *and* keeps a real
guard. It legitimises the established pattern (planning-tests today, SupplyChain next) instead of
reversing Q131a's fail-closed design, and it gives the guard something structural to check — a row can no
longer reference a `scripts/test-env/` script that is not in the tree. Option 3 is honest but defends
nothing new; Option 2 is correct-in-principle but explicitly its own work package and disproportionate to
a guard-intent fix.

## The patches (alternatives — apply exactly one)

Both target the same file and `git apply --check` clean against HEAD.

- `patches/option1-guard-follows-scripts.patch` (**recommended**, +19/−7): rewrites the intent to the
  revised decision D, renames the test to
  `A_job_local_mongod_starts_only_from_a_named_test_env_script_never_inline`, keeps the three text checks
  (they still bind the workflow itself), and adds: every `scripts/test-env/*.sh` the workflow runs must
  exist in the tree. Uses the file's own `RepoRoot()`; needs no new `using`.
- `patches/option3-relabel-intent-only.patch` (+3/−1): comment-only; the assertions are untouched.

### Verified (logic, against the real tree — not a full build)

`tests/` is protected, so the C# was not compiled in place; the new assertion's logic was mirrored in
Python against the live workflow + tree:

- the workflow runs exactly **one** `scripts/test-env` script — `mod0188-ci-mongo.sh` — and it exists, so
  the Option-1 rule **passes today**;
- the three text checks still pass on the workflow itself (no inline `--replSet`/`mongodb://`);
- a phantom `scripts/test-env/does-not-exist.sh` reference would trip `File.Exists` (negative control).

The owner should still run `TenantArchitecture.ArchitectureTests` in CI after applying, to compile-confirm.

## Files

- `patches/option1-guard-follows-scripts.patch` — recommended.
- `patches/option3-relabel-intent-only.patch` — the honest minimum.
- Option 2 is described above but not patched: it is a separate work package (test redesign across the
  Q131a call sites), not a guard-intent fix.
