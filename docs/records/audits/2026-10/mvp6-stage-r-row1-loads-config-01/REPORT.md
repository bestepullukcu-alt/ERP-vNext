# Gate 18.0 row 1 for MOD-0185 — the blocker was already fixed, the note is stale, and nothing was keeping the fix applied

**Lane:** gate 18.0 row 1 (Golden flow) for MOD-0185 Loads · **Date:** 2026-10-13
**Branch:** `feature/sce/mod-0190-0192-bounded-slice`

## Correction: CT already knew this, and did not read its own plan first

Before anything below is read as a discovery, it is not one. `mvp6-development-plan-v9.4.md` §3 — written by
CT on 2026-10-12 from the Q446 date re-check — already lists row 1 for MOD-0185 as **"closed by code 10-06 …
done"**. CT opened this lane from `GATE-18-0.tsv` without re-reading the newer plan, and so re-derived a
conclusion that was already recorded, which is the same mistake as the v9.3 owner queue: acting from an older
artefact when a newer one in the tree had already settled it.

What is new in this lane, and only this:
  * nothing in the repository tested the tracked configuration at all, and
  * the value had already drifted once, 5061 to 5066, correctly but unguarded.
The guard and the three sabotages below are the new work. The cell movement is Q457's and the plan's.

## The row did not need fixing; it needed re-reading

`GATE-18-0.tsv` row 1 for MOD-0185 reads NOT MET with the note: *"the flow passed live only with the lane
override `Loads__ReferenceBaseUrl`; from the tracked config the first create answered 503
DEPENDENCY_UNAVAILABLE (measured). Q414: no tracked config sets `Loads:ReferenceBaseUrl`, so every clone
fails the golden flow."*

Measured today: `appsettings.Development.json` **is tracked** and **does** set it, together with `Returns`,
`Claims` and `LegalEntity`. The queue says why — **Q457**, later than the Q446 lane that wrote the TSV:

> *"OWNER DECISION APPLIED: Loads.ReferenceBaseUrl added where Returns and Claims already have it, in both
> the tracked example and the untracked Development config … This takes Loads' golden-flow row out of NOT
> MET, where Q414 had put it."*

So the row's own fix landed on 2026-10-10 and the TSV was never re-read. This lane claims no credit for that;
it only measured that the note no longer describes the tree.

## What it found on the way, which is the part worth keeping

**The value has already drifted once.** Q457 records it as `http://127.0.0.1:5061/`. The tracked file today
reads `5066`. The reason is correct — owner decision OD-P1 renumbered SupplyChain from 5061 to 5066 and gave
**5061 to CRM** — and the configuration was updated with it. But consider the near miss: had that edit been
forgotten, all three modules would have been reading their shipment and carrier references from **CRM**, and
the only symptom would have been a 503 from whichever module someone next ran live. Q414 is the record of
exactly that symptom costing a golden-flow row.

**Nothing tested the tracked configuration.** Measured: no test in the repository reads
`appsettings.json` or `appsettings.Development.json` — the only matches were build outputs under `bin/` and
`obj/`. The owner's fix was applied and then left unguarded, which is how it drifted into a near miss within
three days.

## The guard

`Common/TrackedConfigurationGuardTests.cs`, 5 cases, all green
(`evidence/clean-run.log`). It does not assert that keys exist — that would pass a file pointing at the wrong
service. It asserts the two relationships that rot silently:

- each of `Loads`, `Returns`, `Claims` has a `ReferenceBaseUrl` that is an absolute http(s) URI **and whose
  host and port are the service's own**, read from `Urls` in `appsettings.json`;
- `LegalEntity:ReferenceBaseUrl` is set and is **not** this service, because the validator must reach MDM;
- the three module roots agree with each other, so a future deliberate move has to move all three.

Q457 flagged that pointing at SupplyChain directly rather than through the gateway is a deliberate shared
choice across all three modules, outside MVP-6 to revisit. That choice is what the guard encodes, and the
class says so, so whoever revisits it knows this is the place that changes with it.

The configuration is read from the **source tree**, not from `bin/`: the copy in `bin` is an output, and a
guard that read it would pass on a stale build.

## Sabotage, three ways

Each applied to the tracked file in place, measured, then restored by copy — digest back to
`d0b1ca4061fe1b45`, `git status` clean.

| sabotage | result | which tests fired |
|---|---|---|
| Loads back to `5061` — the OD-P1 drift, exactly | **2 failed / 3 passed** | the Loads origin case, and the three-roots-agree case |
| `Loads` section deleted — Q414, exactly | **2 failed / 3 passed** | the same two |
| `LegalEntity` moved to `5066` — self-reference | **1 failed / 4 passed** | only the legal-entity case |

The third is the one that shows the guard discriminates rather than going red as a block: pointing the legal
entity at this service leaves all three module cases green, because they are a different claim.

## Cell movement

`GATE-18-0.tsv` sits inside its own lane's `ARTIFACTS.sha256` and was **not edited**. The transition is in
`MOVED.tsv` here.

| module | row | from | to | on whose work |
|---|---|---|---|---|
| MOD-0185 Loads | 1 Golden flow | NOT MET | MET | Q457's owner-applied fix, 2026-10-10; this lane only re-measured it and added the guard |

## Full-suite regression, with all four reds accounted for

`Diten.SupplyChainService.Tests` in full: **520 of 524 passed** (`evidence/full-suite.log`). The four
failures are all explained and none is a regression:

| failure | why |
|---|---|
| `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery` | fails in 1 ms with its own message, *"Explicit write/read restart mode required; exclude from ordinary suite"* — it declares itself out of scope for an ordinary run |
| `CompositionRootGuardTests` ×3 | `SUPPLYCHAIN_COMPOSITION_GUARD_MONGO_URI` is not set, and the lane script does not print it. Set by hand they pass 3/3. Raised as Q492 |

Stating that as "520/524 green" without the second row would be the kind of reporting REP-001 exists to
prevent: three of those four are a guard on the production composition root that nobody's lane arms.
