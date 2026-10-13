# Exact owner decision package — UNAPPROVED

The following decisions are independent. Approval of one does not imply the others.

## A — Pack artifact replacement/application

Approve `PACK-DELTA.patch` SHA-256 `20eacc55cb5bd7e76fc4db61e26d73b0d8c5f8c9849e2a4f5c3f93f8844f630c` against MOD-0183 pack preimage `32381634163a2acff440475f4582c05defc8261957386dfa43d5c8fc7eaf962e`, producing the already prepared target `464d10b47642df62b615548fa00696fbe712bbe311f2c2c10458d20f80b5e29a`. This authorizes only the documented UI pack delta; it does not authorize runtime code.

## B — Backend transfer and isolated target

Approve `BACKEND-TRANSFER-MANIFEST.tsv` SHA-256 `0cd90929fb7d2816443db05234a60e1895a5c057f271249a49ec14c7b737ae05` (40 exact files) from durable donor archive `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745` into a new registered Shipment UI checkout based on HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. A differing present file is a conflict and must not be overwritten. The 28 UI-owned preimages remain ABSENT (`UI-PREIMAGE-MANIFEST.tsv` `9bbad7bb3d01f8500711ae75c30cf1fbcb238b9af77c009fdacfb4b29efd3dfe`).

## C — Single-owner shared successor

After the Carrier shared predecessor is frozen at the exact preimages in `SHARED-BASELINE-TARGET.tsv`, approve one integration owner to apply `SHIPMENT-SHARED-SUCCESSOR.patch` SHA-256 `6d9cad8e59cdab9559a3c822a1b0135672b1a4672d7988235586983cb45ba1bd`. The target manifest SHA-256 is `1adbb4d946ac38dcc01bedf10e131e85f8713ca3485c8a490626e79dd3673b15` and covers exactly 12 paths. It adds only Shipment gateway routes/test, Shipment manifest/provider test, one DI registration and seven module/page localization pairs. It does not change Carrier, Auth, permission policy, CRM routes, canonical contracts or guard files.

The integration owner must record the existing CRM/SupplyChain port-5061 deployment disposition before composed runtime acceptance. This decision does not authorize editing CRM routes.

## D — Bounded UI DEV and independent VER

After A–C are materialized on one immutable target and their target hashes pass, approve one UI writer for the exact 28 paths in the controlling allowlist and then a separate read-only verifier. Scope remains list/create/detail/transition/POD, Compact, 13 inputs, seven languages, Gateway-only egress, exact permissions and frozen wire behavior.

No decision above grants commit/push/stash, rollout, E5/G5, UI/full-module acceptance, Carrier source changes, shared-file expansion or a new permission/route policy.
