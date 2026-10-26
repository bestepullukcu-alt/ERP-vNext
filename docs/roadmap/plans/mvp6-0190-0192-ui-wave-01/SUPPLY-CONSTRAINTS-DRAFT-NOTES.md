---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# SUPPLY-CONSTRAINTS contract — draft notes (G2 of the 0190/0192 UI wave)

    authored  : 2026-10-18 by CT (gmg-10) · the draft: `supply-constraints.openapi.draft.yaml` (v0.1.0-draft.1)
    status    : DRAFT, NOT FROZEN, not in `docs/analysis/contracts/`. It closes the "no contract exists"
                finding (Q281 §52) with a concrete shape the owner can freeze — it does not itself authorise
                a live adapter (MOD-0192 pack §244 reserves that to a named producer owner + owner scope).

## Why this contract exists

MOD-0192 Capacity consumes a **second undeclared seam** besides DEMAND: supply/capacity constraints, today
TEST-FIXTURE-ONLY (`ConstraintFixtureReader`, pack §119). Q281 confirmed **no contract exists** for it. The
0190/0192 full-working-UI override (G2 in `PLAN.md`) needs it authored and frozen before Capacity can be
composed against anything real.

## Fit to the consumer (measured, not assumed)

The draft is shaped to the consumer's EXISTING types, so no re-model is forced:

| consumer (domain) | draft field |
|---|---|
| `CapacityConstraintReference(ConstraintId, Source, SourceVersion)` | path `{constraintId}` + `source` const "SUPPLY-CONSTRAINTS" + path `{version}` |
| `CapacityAdjustment/CapacityBottleneck(ResourceRef, Period, …, UomId)` | `ConstraintAvailability{resourceRef, period, availableCapacity, uomId}` |
| fixture tuple `("line-4-hours","SUPPLY-CONSTRAINTS","8")` → `("line-4","2027-W03","80.000","HOUR")` | `resolveConstraintRevision(constraintId="line-4-hours", version="8")` → availability `[{line-4, 2027-W03, "80.000", HOUR}]` |
| pack §159 "unknown/stale/cross-scope/mismatch → 422 `INVALID_CONSTRAINT_REFERENCE`" | the single 422 code, fail-closed, no existence disclosure |
| pack §79/§196 "opaque reference; no constraint value copied" | consumer keeps the ref; the availability payload is for evaluation only |

Decimal values are **strings with 3 fractional digits** (`"80.000"`), matching the domain's decimal-string
discipline (no float). Provenance (`IntegrityProof.digest`, uppercase-hex SHA-256) mirrors demand-v2 so both
producer seams verify the same way.

## Deliberately NOT like DEMAND v2

DEMAND v2 forced the consumer to re-model identity (opaque planId → revisionId + planningPeriodKey +
contentVersion). This contract is keyed by the consumer's **existing** `(constraintId, version)` string
identity, so MOD-0192 needs **no identity re-model** — only an HTTP adapter replacing the fixture reader.
That makes G2 lighter than G3 (the DEMAND v2 long pole).

## Open items the owner must close before FREEZE

1. **Producer owner (x-owner: TBD).** Who produces resource availability — a resource/capacity master, or a
   manufacturing-resource module? Not MOD-0192. This is the gating decision; the contract can't be frozen
   ownerless (DEMAND is MOD-0188-owned for the same reason).
2. **Canonical checksum spec.** `SUPPLYCONSTRAINTS-CANONICAL-1` is named but its exact field ordering is
   undefined; define it + publish an independent consumer test vector before freeze (same open item as
   demand-v2's known-gap).
3. **Scope header vs claim.** The draft takes `X-Legal-Entity-Id`; confirm that matches the SupplyChain
   gateway/middleware convention (the five modules put legalEntityId on the wire) rather than a JWT claim.
4. **Permission `supply.constraints.read`** — a new grant (shape like `demand.plans.consume`); feeds G7.

## Where it goes on freeze

On owner freeze: move to `docs/analysis/contracts/supply-constraints.openapi.yaml`, bump to `1.0.0`, set
`x-owner` to the named producer, record a freeze decision, and pin it in the MOD-0192 pack (as DEMAND is
pinned at `:104`). Then G4 can build the live adapter replacing `ConstraintFixtureReader`.

## Files

- `supply-constraints.openapi.draft.yaml` — the draft contract (YAML validated).
- `SUPPLY-CONSTRAINTS-DRAFT-NOTES.md` — this.
