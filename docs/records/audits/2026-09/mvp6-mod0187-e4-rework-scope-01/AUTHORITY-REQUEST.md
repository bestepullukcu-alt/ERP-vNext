# Exact additional authority required

R21 needs no new business or contract decision. Its patch follows the published distinction between dependency request trace and authoritative Shipment lifecycle root.

The following two decisions remain required; no broader authority is requested.

## R14 shared transport/composition decision

Approve Program.cs baseline `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`, patch `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0`, and target `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` for the single integration owner. Confirm that SHIPMENT-BUNDLE `Idempotency-Key` non-ASCII wire bytes use strict UTF-8 before the existing 1–128 Unicode-scalar check. This does not authorize any other Program.cs, auth, proxy, gateway or contract change.

## R22/R25 evidence-injection decision

Approve Claims persistence baseline files `4082bb189256e6bffbec4440913f4fd2544ed79ce88252324cd86007d069a222` and `0361088e461cfac00ecf3b34b39ee311a9e58f0b82d51327b82f44ae196752de`, patch `d0844d8c8e4b28dc3ab1f1559b6f49c45005567f6f1ea252fca6c51f84f75eca`, and targets `05cb87877bf427e3232fd2a074915a26e4256d9a5c61e357c4d7ab2b8ceef68b` and `c4ca2690bb964529cb7d1d2a1265c514c10946f792829d4da821597636761a9c` for evidence-only use. The mode is exact `ClaimsEvidence`, has no endpoint or auth bypass, fails closed without explicit bounded configuration, and normal production startup retains `NoOpClaimCommitProbe`.
