# MOD-0183 VER-04 — SOP §22 independent verification

- WP: `MVP6-MOD0183-DEV-04-R1`
- Lane: independent VER, strict repository read-only; evidence written only under `/private/tmp/mod0183-ver-r4`.
- Branch: `feature/mvp6-logistics`
- HEAD: `bc109afa4c016877dc4ecf203f8a8e91b512e4dd`
- **Verdict: FAIL — evidence inventory/hash recording defect.**
- **Runtime verdict: PASS for the bounded, internal mock-backed DEV-04 slice.** This does not authorize live ingress, MOD-0184, E5/G5, or overall CT acceptance.

## Findings

### Blocking — recorded inventory is neither hash-clean nor complete

`changed-files.json` contains 144 entries (one self-reference), but independent verification found:

- five recorded hash mismatches:
  - `r4/architecture.log`
  - `r4/architecture.trx`
  - `r4/build.log`
  - `r4/service.log`
  - `r4/service.trx`
- seven existing evidence files absent from the manifest, all under `r4/runtime/`:
  - `actual-sent.json`
  - `golden/persisted-state.json`
  - `golden/runtime.json`
  - `golden/service.log`
  - `regression-state.json`
  - `regressions.json`
  - `sent-body-comparison.json`
- the DEV-04 report says 65 .NET tests, while both the current saved TRX and the fresh independent run contain **66** passing tests.

The seven omitted files and five changed artifacts predate VER-04 preflight. VER-04 did not create or mutate them. Because the evidence set changed after its manifest/report was frozen, the claimed complete 144-entry inventory cannot be accepted.

## Independent functional verification

All executable checks ran from `/private/tmp/mod0183-ver-r4/repo`, copied from the preflight worktree with `.git`, build outputs and caches excluded. Required build assets were then copied without changing source content.

| Check | Independent result |
|---|---|
| SupplyChain solution build | PASS — 0 warnings, 0 errors |
| .NET service suite | PASS — 66/66 |
| Python request-capture regression | PASS — 2/2 |
| HTTP/restart/boundary probes | PASS — 22 golden + 11 boundary; 33 transport captures, 0 mismatches |
| Source-state restart probe | PASS — two service OS processes; all eight collections unchanged |
| Protected inputs | PASS — 172/172 hashes match |
| Architecture | 15 PASS / exactly 3 known external FAIL |

The three architecture failures are unchanged and outside MOD-0183: Platform DB-010 per-run database use, HCM/Talent local JWT clock-skew assignment, and HCM/Talent missing shared-skew declaration. `DocsPathGuard` passed; no fourth failure appeared.

## Boundary review

- `WarehouseReadClient` exposes frozen outbound list/detail only. `InventoryReadClient` exposes availability/balance only. Every downstream request is constructed with `HttpMethod.Get`; no Inventory `/movements` use exists.
- No stock collection, balance SoR, foreign database client, or Warehouse/Inventory database sharing exists in the service.
- `WarehouseIntakeCoordinator`, `IWarehouseReadClient`, and `IInventoryReadClient` are not registered in DI and have no controller, hosted worker, poller, subscriber, cron, or public intake route. The only public module controller remains `/api/shipment-bundle/shipments`; the unrelated existing hosted components are schema initialization and Shipment outbox delivery.
- Trusted tenant/legal-entity/actor scope is checked before dependency access. Source intent/link/evidence storage is tenant/legal-entity scoped; source link, Shipment, lifecycle, audit, receipt, outbox, and intent commit occur in one Mongo transaction.
- Fresh tests reproduce durable preparation/recovery, source deduplication, concurrency arbitration, drift detection, replay without writes, cancellation/deletion reservation, rollback, and tenant/legal-entity isolation.
- All 172 protected hashes match, including frozen Warehouse/Inventory contracts and central governance.

## Open compatibility and gate state

GAP-0183-03 is real and honestly reported: the OpenAPI 3.1 Warehouse page schema declares `nextCursor` as `type: string` plus `nullable: true`, while its example supplies `null`. Draft 2020-12 validation returns `None is not of type 'string'`; omitting the optional property passes. No contract was changed.

GAP-0183-01/02/04 remain held. Live Warehouse/Inventory integration, automatic intake, gateway wiring, E5 and G5 remain outside this slice. Overall repository gate remains blocked by the three accepted external architecture failures.

## No-change proof and recommendation

Final branch, HEAD, status, tracked diff, staged diff and stash list exactly match VER-04 preflight; `git diff --check` passes. Test processes and the temporary MongoDB listener were stopped. No commit, push, stash, branch switch or repository write occurred.

Return to a narrow DEV evidence-only rework: regenerate the final report and manifest after all evidence generation is complete, include or remove the unmanifested `r4/runtime/` duplicate set, record the actual 66-test count, and freeze hashes only after writers stop. Then repeat independent inventory verification. Product code rework is not indicated by this VER.
