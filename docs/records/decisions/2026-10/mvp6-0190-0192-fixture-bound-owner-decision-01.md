# Owner decision — MOD-0190 and MOD-0192 stay fixture-bound in MVP-6

- date: 2026-10-05
- owner: ny@gmgroup.ch
- status: ACCEPTED
- ledger: Q441, Q442

## Decision

**MOD-0190 S&OP and MOD-0192 Capacity remain bound to test-only DEMAND fixtures for the whole
of MVP-6.** They are not composed, their routes answer 404, and they do not run on real demand
data in this programme.

This is the status quo. **It has been in force since the packs were written and nobody had
recorded it as a decision** — which is why it kept being re-discovered as a blocker.

In parallel, **a DEMAND v2 request goes to the MOD-0188 owner** (below). The two do not
conflict: one makes today honest, the other starts the way out.

## Why not the other two options

**Narrowing what the modules ask was rejected.** Both packs are built on an immutable-provenance
guarantee — a plan is bound to an exact `DemandPlanId` + `DemandPlanVersion` + `SourceChecksum`.
MOD-0190's sign-off chain and MOD-0192's scenario evaluation both rest on it. Dropping the
checksum, or resolving by `itemId + period` and comparing, would make the modules cheaper and
erode the thing they exist to provide.

**DEMAND v2 alone was not enough**, because it belongs to another team and another MVP. Waiting
for it without recording the present state leaves two modules looking unfinished rather than
deliberately bounded.

## What this decision does NOT say

- It does not close MOD-0190 or MOD-0192. Their §18.0 position is unchanged.
- It does not make them "done". They are **bounded**, and the boundary is now written down.
- It does not authorise building their UI from the v3 drafts. That remains gated on the
  module recipe, and R-3 measured the per-payload idempotency defect in both drafts
  (Capacity `details.js:616`, S&OP `details.js:510`).

## What is now true and reportable

> MOD-0190 and MOD-0192 are specified, service-coded and UI-scoped. In MVP-6 they consume
> DEMAND through test-only fixtures by design, are not composed, and answer 404. They move
> when DEMAND can resolve a plan by reference.

## The measured reason, so it is not re-litigated

`DEMAND` v1 is FROZEN, owned by MOD-0188, and offers:

```
GET /api/demand/plan?itemId={uuid}&period={YYYY-MM}
  → planId, itemId, period, quantity, uomId, confidence, status, contractVersion
GET /api/demand/forecast
```

Both modules ask the opposite question. S&OP:
`MatchesAsync(scope, planId, version, checksum)`. Capacity: `IsExact(scope, plan)` where the
plan carries `DemandPlanId`, `DemandPlanVersion` and `SourceChecksum`.

DEMAND looks up **by `itemId` + `period`** and *returns* a planId. It does not accept a planId
as input, its response carries no plan version — `contractVersion` is the contract's own — and
no checksum. Q282's ruling of 2026-10-03, *impossible as framed, not expensive*, stands. CT
re-tested it on 2026-10-05 against a word count and the operation text restored it.
