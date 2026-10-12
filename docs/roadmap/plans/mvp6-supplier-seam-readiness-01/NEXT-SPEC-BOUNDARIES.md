# Next preparation boundaries

## Common gate before separation

The lanes may split only after SS-01 through SS-09 have exact dispositions or the affected items are explicitly scoped out. Both module packs remain `draft`; no Phase 1.5 or runtime dispatch follows from this readiness review.

Shared files and seams keep one writer: `supplier.openapi.yaml`, `supplier-performance.openapi.yaml`, domain/DCP/registry authorities, actor mapping, permission registry, gateway and service composition.

## MOD-0147 next spec preparation

In scope:

- the nine frozen evaluation/scorecard/risk operations;
- module-owned evaluation, immutable scorecard and SupplierRisk records;
- opaque Supplier reference validation through the approved identity seam;
- exact Metric Registry and Risk dependency adapters after their decisions;
- tenant/LE isolation, lifecycle, replay, concurrency, audit and Pending outbox acceptance;
- backend-only owned paths under `Features/SupplierPerformance/**` and matching tests.

Held/out of scope:

- all `/portal/**` operations and actor mapping;
- Supplier master/profile/contact/KYC replication;
- Metric Registry or Risk Register implementation/clone;
- UI, gateway, shared permission/DI composition, live producer, publisher and G5;
- any score, rounding, threshold or transition rule not approved in SS-07/08.

## MOD-0148 next spec preparation

In scope:

- the five frozen `/portal/**` operations;
- portal access projection and draft→submitted module-owned lifecycle;
- server-derived TenantId, LegalEntityId and mapped SupplierId;
- own-record predicate and 403/404/503 precedence after SS-03/05;
- exact replay tuple after SS-06;
- idempotency, concurrency, audit and Pending outbox acceptance;
- backend-only owned paths under `Features/SupplierPortal/**` and matching tests.

Held/out of scope:

- internal review command/permission, because no frozen operation exists;
- supplier-facing UI and localization;
- actor-binding implementation, gateway and permission registry;
- Supplier master/KYC/credentials or local identity tables;
- MOD-0147 evaluation/risk paths, live producer and G5.

## Parallelism result

After the common gate, MOD-0147 and MOD-0148 spec work is parallel-safe because their operation sets and feature paths are disjoint. They must not concurrently edit the joint contract or any shared seam. Each spec returns proposed contract needs to the single seam owner rather than applying them.
