# Q281 — Is DEMAND a seam MVP-6 owns? Options for CT

Reading date 2026-10-03, branch `feature/mvp6-logistics` @ `4a8d4d4b3`. Document and code reading only; nothing was
composed, booted or edited. **This is a recommendation. CT rules.**

## 1. The answer to the question

**No. DEMAND is not an MVP-6 seam, and the repository already says so in five places.**

| Source | What it says |
|---|---|
| `docs/analysis/contracts/demand.openapi.yaml:9` | `x-owner: MOD-0188` |
| `docs/analysis/contracts/demand.openapi.yaml:5-6` | MOD-0188 Demand Planning; shared seam consumed by 0172/0176/0189/0192 |
| `docs/analysis/wave0-contracts-product-master-and-inventory.md:237` | "CONTRACT 7 — DEMAND (owner: MOD-0188 — MVP-4 üretir)" |
| `docs/analysis/wave0-contracts-product-master-and-inventory.md:285` | DEMAND · MOD-0188 · produced by MVP-4 |
| `execution/registries/module-id-registry.md:284` | MOD-0188 Demand Planning — `reserved / planned` |

Both packs agree that MOD-0188 owns the data and that they must not edit it (MOD-0190 `:44`, MOD-0192 `:43`). Both list
`demand.openapi.yaml` as a protected, module-owned frozen contract (MOD-0190 `:107`, MOD-0192 `:104`). The packs do not
contradict each other.

What the packs say the seam *is*: a frozen **HTTP** contract, consumed in this slice **only through exact, scoped test
fixtures**, with no live call (MOD-0190 `:42`, `:119`, `:132`; MOD-0192 `:41`, `:117`, `:130`). The published
SANDOP-CAPACITY semantics make that limit part of the frozen contract itself: "exact tenant/LE test fixture for DEMAND
with no producer endpoint … A fixture PASS is not live DEMAND version/checksum … validation"
(`docs/analysis/contracts/sandop-capacity-semantics-v3.0.0.md:56`).

So the modules are not *wrongly* unreachable. They were specified, built and published as fixture-bounded slices
("Shared composition and broader integration remain separately gated", MOD-0190 `:228`). Their unreachability is the
pack's own boundary, not a defect.

## 2. What is actually missing

1. **The producer does not exist.**
   - MOD-0188 has no pack (`find execution -iname '*0188*'` → nothing).
   - It has no code: no `/api/demand` in any `.cs` or `.json` under `services/`, `gateway/` or `frontend/`.
   - It has no gateway route (`grep -i demand gateway/Diten.ApiGateway/ocelot.json` → only the unrelated
     `demand-ideas` and `talent-supply-demand-forecasting`).
   - It has no config key (`grep -i 'demand|sandop|capacity'` over `appsettings*.json` → nothing).
   - MVP-4 produces it; the plan sequences MVP-4 before MVP-6
     (`docs/analysis/inventory-capability-scope-and-dependency-report.md:406-410`, `:439-440`).
2. **The frozen v1 contract cannot answer the question the modules ask.**
   - Both modules match a tuple of plan ID, version, checksum and published state.
   - DEMAND v1 has only `GET /plan?itemId=&period=` and `GET /forecast?itemId=&horizon=`
     (`demand.openapi.yaml:14-55`). It returns `planId`, `status` and quantities, with no version and no checksum.
   - The packs say this themselves: MOD-0190 `:159` "DEMAND v1 has no ID+version endpoint", and MOD-0192 `:237`.
3. **The consumption is undeclared in two places.**
   - `docs/analysis/workpackages/WP-MVP6-logistics.md:8-13`: the MVP-6 CONSUMED list names INVENTORY,
     WAREHOUSE-OUTBOUND, SUPPLIER and Event Bus. **DEMAND is absent**, although line 6 makes 0190 and 0192 MVP-6 SoRs.
   - `demand.openapi.yaml:6` and `wave0…:252`: the DEMAND consumer list is 0172/0176/0189/0192. **0190 is absent.**
4. **Capacity consumes a second undeclared seam.**
   - Supply constraints ("SUPPLY-CONSTRAINTS"), TEST FIXTURE ONLY (MOD-0192 `:119`).
   - No contract exists for it in `docs/analysis/contracts/`.
   - The pack says a live adapter "requires separate producer-owned contract and owner scope" (MOD-0192 `:244`).
5. **Capacity's evaluation engine is itself a fixture.**
   - `CapacityEvaluationExecutor` is a literal oracle (`OracleId = "CAPACITY-EVAL-FIXTURE-192-01@1"`, `:7`, `:27-30`)
     that completes only for one hard-coded tenant.
   - It is not registered anywhere in `src/`.
   - A production demand reader would not make Capacity real.

## 3. Options

### A — write a production DEMAND reader

- **What it would call:** `GET /api/demand/plan?itemId=&period=` (the only plan operation in DEMAND v1).
- **Does it exist?** **No.** Not in this repository, and not as a running producer anywhere the repository knows of.
- **Can it be written against the declared contract?** **No.**
  - The S&OP interface needs `MatchesAsync(scope, planId, version, checksum)`
    (`Domain/Features/SandopPlans/ISandopRepository.cs:3-4`).
  - The Capacity interface needs `IsExact(scope, plan)` over ID, version and checksum
    (`Domain/Features/CapacityPlans/CapacityScope.cs:10-13`).
  - DEMAND v1 has no key by plan ID and returns neither version nor checksum. A reader written today would have to
    invent the endpoint, or invent the version and checksum. That is the fabricated reference reader Q272 refused.
- **Cost if pursued properly:**
  1. A DEMAND **v1.1 additive** amendment by its owner — a plan-version lookup returning `status`, `version` and
     `checksum`. K16 allows additive changes; a versioned spec task is needed.
  2. MOD-0188 built (MVP-4, not started).
  3. Two consumer readers, one per module (§4).
  4. For Capacity additionally: a supply-constraints contract and producer, a real evaluation engine replacing the
     literal oracle, and registration of the executor.
- **Unblocks:** live S&OP once 1–3 land; Capacity only after 4 as well.
- **Forecloses:** nothing. It is the end state the packs already describe as "still open".

### B — declare DEMAND external and amend the packs to take 0190/0192 out of MVP-6

- **DEMAND is already external** (§1), so the declaration is true today. Only the two omissions in §2.3 need fixing.
- **What moving the modules out would cost:**
  - `WP-MVP6-logistics.md:1` and `:6` name S&OP and Capacity as the "Integrated" half of MVP-6; MVP-6 would become
    logistics only.
  - SANDOP-CAPACITY 3.0.0 (`docs/analysis/contracts/sandop-capacity.openapi.yaml`) is a published contract whose owners
    would leave the MVP that published it.
  - 67 tests (S&OP 19, Capacity 48 — Q266 `PER-MODULE.tsv`) and both packs' accepted evidence would be parked.
  - Both packs would need amending. They are owner-promoted (MOD-0190 `:10`), so that is an owner action, not CT's
    alone.
- **What it would not cost:** MVP-6's G5 acceptance names only the logistics flow — "shipment→carrier→POD→return/claim"
  (`WP-MVP6-logistics.md:25`). Neither S&OP nor Capacity is in the acceptance line. Removing them does not move G5.
- **Unblocks:** a clean MVP-6 close on logistics.
- **Forecloses:** an MVP-6 S&OP/Capacity delivery. The work moves to whenever MVP-4 lands, and is re-homed.

### C — not considered in the dispatch

**C1 — leave the packs and the modules as they are; fix only the declarations; gate composition on the producer.**
- Amend `WP-MVP6-logistics.md` CONSUMED to add DEMAND (producer MOD-0188 / MVP-4).
- Add 0190 to the DEMAND consumer list in `demand.openapi.yaml:6` and `wave0…:252`.
- Record in the ledger that composing 0190/0192 is gated on MOD-0188 + DEMAND v1.1, and on SUPPLY-CONSTRAINTS for 0192.
- Cost: documentation and contract-annotation edits only, done by a spec writer. `demand.openapi.yaml` is a protected
  path for both module lanes.
- Unblocks: nothing runtime. It closes the "consumed but undeclared" gap and stops the question coming back.
- Forecloses: nothing. It is compatible with A later and with B later.

**C2 — front-load a consumer-facing DEMAND slice, exactly as was done for WAREHOUSE-OUTBOUND and SUPPLIER.**
- The precedent is `docs/analysis/contracts/README.md:20-29` (commit `c8badff96`). CT centrally froze the "minimal
  surface" MVP-6 consumes from a producer that did not exist yet; the producer must later implement a superset.
- Applied here: a frozen DEMAND v1.1 consumer slice with a plan-version lookup returning `status`, `version` and
  `checksum`.
- Real HTTP readers could then be written against a declared contract. With no producer running they would fail
  **loudly** with a contract 503, not silently with `false`.
- Cost:
  1. A versioned contract task with MOD-0188 owner consent.
  2. Two readers.
  3. A base-URL config key.
  4. For Capacity, the same again for SUPPLY-CONSTRAINTS, plus the executor and evaluation questions in §2.5.
- Unblocks: S&OP composition with honest failure semantics.
- Forecloses: nothing in the contract — additive only. It does commit the future MOD-0188 to that slice.
- **Caveat:** a composed module that answers every write with 503 is still a module that refuses everything. It is
  honest where the empty-fixture host would be silent, but it is not "reachable" in a useful sense until MVP-4 ships.
  CT should decide whether loud refusal clears K4.

## 4. A correction to the dispatch's framing

There is not one seam called `IDemandFixtureReader`. There are **two interfaces with the same simple name**, one per
module, each with its own implementation. That is consistent with both packs' rule of no shared internal types
(MOD-0190 `:123`).

| Module | Interface | Shape | Implementation | What the implementation is |
|---|---|---|---|---|
| S&OP | `Domain.Features.SandopPlans.IDemandFixtureReader` (`ISandopRepository.cs:3`) | `Task<bool> MatchesAsync(scope, planId, version, checksum, ct)` | `Infrastructure/Features/SandopPlans/DemandFixtureReader.cs:5` | takes `IEnumerable<DemandFixture>`; empty in a composed host, so always `false` |
| Capacity | `Domain.Features.CapacityPlans.IDemandFixtureReader` (`CapacityScope.cs:10`) | `bool IsExact(scope, plan)` | `Infrastructure/Features/CapacityPlans/DemandFixtureReader.cs:3` | parameterless; hard-codes tenant `19200000-…-0001`, LE `…-0002`, plan `dp-2027` v`3`, checksum `sha256:ee56d4f9a3c8` |

The consequences differ by module:
- **Capacity's fixture reader is resolvable today** (no constructor arguments). If registered, it would not refuse
  everything. It would refuse every real tenant and **accept writes for one synthetic tenant**.
- Its constraint reader (`ConstraintFixtureReader.cs:3`) and executor (`CapacityEvaluationExecutor.cs:27`) share that
  synthetic scope.
- That is a worse K4 than the S&OP case: a composed Capacity module would look alive and work, but only for a tenant
  that does not exist.

## 5. Recommendation (CT rules)

1. **Take C1 now.** It is documentation-only and makes the repository say what is already true. Do not compose 0190 or
   0192.
2. **Reject A as framed.** No reader can be written honestly against DEMAND v1. Any attempt is invention.
3. **Hold B and C2 for an owner decision**, keyed on one fact CT does not have: when MVP-4 / MOD-0188 is expected to
   start.
   - If it is far off, B is cheaper than it looks, because G5 does not include S&OP/Capacity.
   - If it is near, C2 gives S&OP an honest contract to build against.
   - Capacity needs C2 twice (DEMAND and SUPPLY-CONSTRAINTS) plus a real evaluation engine. Treat it as the later of
     the two in either case.
4. **Never register the existing fixture readers in a composed host**, either module's. The S&OP one refuses everything
   silently; the Capacity one serves a synthetic tenant. Neither is a stepping stone.
