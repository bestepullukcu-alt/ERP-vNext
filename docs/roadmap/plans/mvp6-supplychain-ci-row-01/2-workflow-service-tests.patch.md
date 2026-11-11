# `.github/workflows/service-tests.yml` — two edits

## A. the matrix row

Add after the `planning-tests` row and before the `# Main sync (CI follow-up 3 review)` gateway comment:

```yaml
          # WP-LANE-MAIN-SYNC-05 (owner decision 2026-10-16): SCE / MVP-6's own suite, which left the guard's
          # NotInThisJob in the same change. It needs a single-member REPLICA SET with enableTestCommands=1 — not a
          # plain mongod: ShipmentSchema refuses to start without transaction support, and the CapacityPlans /
          # SandopPlans fail-point tests configure fail points. Its eight variables come from the step below, because
          # the suite is fail-closed by design (Q131a: "there is no fallback port"). The one skip is allow-listed.
          - suite: supplychain-tests
            project: services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj
```

## B. the two steps

Add the start step next to the Planning one (before `- name: Test`):

```yaml
      - name: Start owned Mongo (SupplyChain)
        if: matrix.suite == 'supplychain-tests'
        run: bash scripts/test-env/supplychain-ci-mongo.sh start
```

and the stop step next to Planning's (after `- name: Skipped = 0`):

```yaml
      - name: Stop owned Mongo (SupplyChain)
        if: always() && matrix.suite == 'supplychain-tests'
        run: bash scripts/test-env/supplychain-ci-mongo.sh stop
```

**Nothing else changes.** No `mongodb://`, no `--replSet`, no `--dbpath` enters the workflow, so
`No_row_starts_a_job_local_mongod_or_hands_the_tests_a_mongo_address` stays green — the same way
`planning-tests` does. The job keeps the file's existing `timeout-minutes: 90`, the shared
`--blame-hang --blame-hang-timeout 20m`, the TRX logger and the `Skipped = 0` step; none of them is
per-row, so the guard's checks on those are unaffected.
