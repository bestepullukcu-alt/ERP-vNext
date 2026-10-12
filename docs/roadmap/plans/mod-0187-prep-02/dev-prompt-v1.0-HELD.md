# HELD — MOD-0187 DEV Prompt v1.0

**HELD — yürütülemez.** Owner D187-01…06 decisions, any required contract amendments, Phase 1.5 approval and pack promotion are open. This draft is not a dispatch or DEV GO.

## NE

Implement only the approved Claims backend slice after the hold is released: `queryClaims`, `createClaim`, and `transitionClaim` under frozen SHIPMENT-BUNDLE wire v1. Use only the exact owned paths in MOD-0187 pack §25.

## NEDEN

Claims needs a tenant/LE-scoped operational lifecycle with immutable evidence references, decimal-safe amounts, idempotency, audit and Pending outbox consistency. Settlement is an operational status and must not create finance or payment records.

## NASIL

1. Re-read the owner decision package `docs/roadmap/plans/mod-0187-prep-02/owner-decisions-v1.0.md` and require signed outcomes for D187-01…06.
2. Revalidate frozen YAML operation/schema parity and any published amendment before coding.
3. Implement the five layers, CQRS, validators, scoped repository and exact §25 files only.
4. Resolve Shipment/Carrier only through approved GET seams; keep evidence IDs opaque unless a real published evidence seam is approved.
5. Use arbitrary-precision decimal handling without changing the wire string contract.
6. Persist Claim, receipt, audit and Pending outbox atomically; no publisher/worker, finance posting or operational migration.
7. Any Program.cs composition change requires a separate single-writer release; do not edit gateway/shared registrations.

## YAPMA

Do not modify frozen contracts, `.antigravity`, gateway, DCP, other modules, Shipment/Carrier source, UI, finance/payment systems, Warehouse ingress or operational MongoDB. Do not invent error statuses, replay headers, permission keys, evidence APIs, settlement references or duplicate-claim rules. Do not start while this document says HELD. No commit/push/stash.

## DOĞRULA

After explicit release, run DCP-002, OpenAPI/ref/examples, build, full SupplyChain tests and Claims tests. Cover A01–A12, every lifecycle edge, tenant/LE/RBAC, decimal lexical/arithmetic cases, dependency refusal/timeout, atomic rollback, failpoint/index/startup failure, replay without reread/side effect, restart durability, Pending outbox and protected hash preservation. Produce E2/E3/E4 evidence and an immutable manifest. Developer PASS is not independent VER or CT acceptance.

## Gates

- D187-01…06 owner decisions: **PENDING**
- Contract clarification/amendment disposition: **PENDING**
- Phase 1.5 checklist and CT release: **PENDING**
- Pack status: `draft`
- DEV dispatch: **HELD**
