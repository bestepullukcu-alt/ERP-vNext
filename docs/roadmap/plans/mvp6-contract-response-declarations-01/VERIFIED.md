---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# The additive delta, now as a verified patch — and two corrections to README.md's snippets

    addendum   : to README.md (the prose proposal, 2026-10-17)
    author     : CT (gmg-10)
    produced   : 2026-10-17, by scratchpad builder against the frozen contracts at HEAD; patches under patches/
    status     : NOT applied. Both contracts are frozen; applying, choosing a version, and re-pinning the
                 packs stay the owner's, exactly as README.md §"what this proposal does not claim" says.

README.md stated its snippets were "reviewed, unexecuted text." Executing them found two were wrong, so
the patches below are the corrected form — measured against the real component inventory, parsed back, and
proven additive. The patches, not the README snippets, are what should be applied.

## Correction 1 — shipment-bundle has no shared `Unauthenticated` / `Forbidden` / `InternalError` response

README §A2 proposed `$ref`s to `#/components/responses/Unauthenticated`, `Forbidden`, `UnknownShipment`
and `InternalError`. **Only five generic response components exist** in this contract — `BadRequest`,
`NotFound`, `Conflict`, `Unprocessable`, `DependencyUnavailable` — so three of those four refs would not
resolve, and `UnknownClaim` / `UnknownShipment` do not exist at all. The 401/403/500 that `GET /returns`
and `GET /claims` already carry are **inlined per operation**, not shared.

So the patch follows the contract's real idiom: for `GET /shipments` it **inlines** 401/403/500 (the same
shape the sibling GETs use, with codes `UNAUTHENTICATED` / `FORBIDDEN` / `INTERNAL_ERROR`) and uses the one
component that does fit — `NotFound`, which already carries a `shipment` example — for 404. No shared
component is added, which keeps the footprint exactly where README wanted it even though README's mechanism
was unavailable.

## Correction 2 — `GET /claims` 404 is inlined, not a reusable `$ref`

README §A1 said to reuse "whichever existing entry `GET /returns` already references for its 404."
`GET /returns` **inlines** its 404 (code `RETURN_NOT_FOUND`); there is nothing to reference. The patch
inlines a 404 for claims in that operation's own style, code `CLAIM_NOT_FOUND`.

`sandop-capacity` (README §B) needed no correction: `components/headers/CorrelationId` and
`components/schemas/Error` both exist, so the new `InternalError` component resolves, and the twelve
operations each gain one `'500': { $ref: '#/components/responses/InternalError' }`.

## What the builder verified (not asserted — parsed)

Both modified files were parsed with PyYAML and compared operation-by-operation against the originals:

| contract | operations touched | response codes added | components added | removed | parses |
|---|---|---|---|---|---|
| shipment-bundle | `GET /shipments`, `GET /claims` | `/shipments`: 401,403,404,500 · `/claims`: 404 | none | **0** | yes |
| sandop-capacity | all **12** operations | `500` on each | `InternalError` (1) | **0** | yes |

Every other operation's response set is byte-for-byte unchanged; no existing response, schema, example or
required field is altered; both diffs are **insertion-only** (0 content deletions). `git apply --check`
passes clean against the contracts at HEAD.

## To apply (owner, in a registered checkout)

```
git apply docs/roadmap/plans/mvp6-contract-response-declarations-01/patches/shipment-bundle-responses.patch
git apply docs/roadmap/plans/mvp6-contract-response-declarations-01/patches/sandop-capacity-500.patch
```

Then the three things this package does **not** decide (README): whether the nine 500 code-sites should
exist, which version each contract bumps to, and the pack re-pins a bump drags behind it
(MOD-0186/0187 at SHIPMENT-BUNDLE, MOD-0192 at SANDOP-CAPACITY — plus MOD-0192 `:253` still pinning the
2.0.0 hash).

## Files

- `patches/shipment-bundle-responses.patch` — GET /shipments +401,403,404,500; GET /claims +404.
- `patches/sandop-capacity-500.patch` — new `InternalError` component + `500` on all twelve operations.
- `README.md`, `MEASURED.txt` — the original prose proposal (unchanged; its §A1/§A2 snippets superseded
  by the corrections above).
