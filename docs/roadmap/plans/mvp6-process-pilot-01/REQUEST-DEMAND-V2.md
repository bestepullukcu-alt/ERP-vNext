# Request to the MOD-0188 owner — DEMAND v2, one additive operation

- raised: 2026-10-05 by Control Tower, MVP-6
- consumers: MOD-0190 S&OP Workflow & Sign-offs · MOD-0192 Capacity Planning
- blocks: both modules' composition. Neither can run on real demand data without it.
- owner decision backing this request:
  `docs/records/decisions/2026-10/mvp6-0190-0192-fixture-bound-owner-decision-01.md`

## What is asked

**One additive operation on DEMAND: resolve a plan *by reference*.**

DEMAND v1 answers *"what is the demand plan for this item in this period?"* Both MVP-6
consumers need the inverse: *"is this exact plan, at this version, with this checksum,
published for this tenant and legal entity?"*

Shape, as the consumers already ask it in code:

```
GET /api/demand/plan/resolve
  ?planId={opaque}&version={opaque}&checksum={opaque, optional}
  → 200 { planId, version, checksum, status: Published|…, contractVersion }
  → 404 when no such plan reference exists in scope
Tenant + LegalEntity resolved from the JWT, as in v1.
```

The consumers need only a yes/no, but a resolved object lets them record provenance without a
second call.

## Why additive, and why it costs v1 nothing

AGENTS.md K16: extension is additive. v1's `GET /plan` and `/forecast` are untouched, their
schemas unchanged, and every existing consumer — 0172, 0176, 0189, 0192 per DEMAND's own
`description` — is unaffected. A new path and a new response schema is the whole surface.

## Why the consumers cannot work around it

- `GET /plan?itemId=&period=` cannot be inverted: the consumers hold a planId and do not hold
  the itemId that produced it.
- The response carries no plan version. `contractVersion: v1` is the contract's version.
- There is no checksum anywhere in DEMAND v1. CT measured: `checksum` appears **0 times**.
- Both packs make the checksum part of an immutable-provenance guarantee that their sign-off
  and scenario-evaluation features rest on, so dropping it is not a workaround but a
  redefinition of the modules.

## What MVP-6 has done meanwhile

Both modules are specified, service-coded and UI-scoped with owner-approved scope. They are
deliberately **not** composed; Q381 made their absence honest by returning 404 rather than 500.
No MVP-6 lane will ask MOD-0188 for anything else, and no MVP-6 lane will build a local
substitute — the packs forbid it and CT has upheld that twice.

## What is needed from the MOD-0188 owner

1. Whether the operation is accepted, rejected, or accepted in a different shape.
2. If accepted, whether it lands as DEMAND v2 or an additive v1 revision.
3. No date is requested here. MVP-6 is not waiting on it; this request exists so the
   dependency is owned rather than rediscovered.
