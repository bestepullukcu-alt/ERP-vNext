# Authority binding

The real user decision in the immediately preceding conversation authorized the
single integration owner to apply only the following `Program.cs` change in an
isolated snapshot:

| Artifact | SHA256 |
|---|---|
| Baseline `Program.cs` | `a72a05a5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a` |
| Patch | `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0` |
| Target `Program.cs` | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` |

The decision requires strict UTF-8 wire decoding for `Idempotency-Key`, keeps
the existing 1–128 Unicode-scalar rule, and excludes all other header, auth,
gateway, contract, canonical, guard, Returns, migration/backfill, commit, push,
rollout, E5 and G5 changes.

This file records that real decision; it is not a synthetic approval and does
not broaden it.
