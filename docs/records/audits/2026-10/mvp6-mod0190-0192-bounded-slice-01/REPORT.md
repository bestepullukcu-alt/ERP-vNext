# MOD-0190 / MOD-0192 — bounded slice E4 evidence

- date: 2026-10-11
- by: Control Tower
- basis: `origin/main` `a814bad0f`, clean worktree; lane mongods 31994 (`rsg5s1`) and 32994 (`rsg5s2`),
  one per module. The operational band 27017–27021 was not contacted.
- authority: §22 composition uptake gate (CT-QUEUE Q292) read by the owner on 2026-10-11 as binding the
  **production** host only. No production composition changed; `Program.cs:82-92` is untouched.
- ledger: Q292 (unchanged), this record

## What was measured

| suite | result | duration |
|---|---|---|
| `Diten.SupplyChainService.Tests.SandopPlans` + bounded-slice HTTP | **25 / 25 pass, twice in a row** | ~20 s |
| `Diten.SupplyChainService.Tests.CapacityPlans` | **not deterministic** — 48/48, 41/48, 46/48, 48/48 | ~2 m |

The CapacityPlans figure is the important one, and it is not a pass. The first 48/48 was taken on a mongod that had been running
for hours; on a cold lane replica set four runs gave 48/48, 41/48, 46/48 and 48/48 — half red, a
**different** set each time. Every failure sits in a fail-point driven path — the timing problem Q214 already
measured, whose recommended fix (Q214 R1-R3) is still an open work package. The red sets covered almost every
test MOD-0192's `:197` and `:198` cited, including the one holding the `capacity_audit` count, so both ticks
were retracted the same day they were made. SandopPlans, by contrast, was 25/25 on two consecutive cold runs.

Both suites run against the test-only `DemandFixtureReader` / `ConstraintFixtureReader` seam and the
lane replica sets. Nothing in them reaches a live DEMAND producer, a Workflow endpoint or an Event Bus.

## Decisions linked to immutable fixture provenance

The acceptance claim this record answers is that **bounded decisions cite provenance and never override a
source SoR**. Measured, per module:

- **MOD-0190.** A captured snapshot stores `demandPlanId`, `demandPlanVersion`, `sourceChecksum` and
  `sourceCapturedAt` and nothing else from DEMAND. A capture whose checksum disagrees with the fixture is
  refused `422` with no capture effect. Sign-off binds to a snapshot id, so every decision points at an
  immutable provenance record rather than at a mutable plan field.
- **MOD-0192.** A plan whose `SourceChecksum` disagrees with the fixture is refused
  `INVALID_DEMAND_REFERENCE` and writes no receipt and no outbox record. Scenarios store only their own
  adjustments plus opaque `ConstraintRefs(name, "SUPPLY-CONSTRAINTS", version)`.

## The negative half, which had never been asserted

Both packs forbid a second source of truth for DEMAND. Neither suite checked it completely:

- MOD-0190 asserted **nothing**. A scan for `series|forecast|quantity|confidence` across `SandopPlans`
  returned zero matches. `SandopLifecycleTests.Capture_persists_exact_fixture_provenance_and_stores_no_demand_series`
  now reads the raw `sandop_snapshots` document and requires none of those tokens anywhere in it. The scan
  covers the whole document rather than its field names, because `Provenance` and `SupplyInputRefs` are
  persisted as JSON strings and a series smuggled inside one would hide from a field-name check.
- MOD-0192 asserted `DemandQuantity` and `ForecastSeries` absent but **not confidence**, although its
  acceptance line names all three. The check is now a loop over six tokens.

## What this evidence is not

Per the acceptance line itself, live producer uptake and E5/G5 are separate gates. This record proves the
bounded slice only:

- No HTTP surface was exercised. Both suites drive the repository and, in one case, the context middleware
  directly; `WebApplicationFactory` and `HttpClient` appear **zero** times in either suite.
- The modules remain outside the composed production host. The controllers exist
  (`/api/supply-chain/sandop-plans`, `/api/supply-chain/capacity-plans`) but `ComposedFeatureControllerFilter`
  drops them while `IDemandFixtureReader` has no registration.
- No gateway route, no UI, no live DEMAND, no Event Bus delivery. Those stay open in both packs.

## Correction recorded in passing

Both packs declared the service on **port 5061** (`MOD-0190:127`, `:189`; `MOD-0192:125`, `:188`). 5061 is
CRM's route; `gateway/Diten.ApiGateway/ocelot.json` sends SupplyChain to **5066**. A gateway work package
reading these packs would have routed S&OP and Capacity traffic into CRM. The four lines were corrected.
