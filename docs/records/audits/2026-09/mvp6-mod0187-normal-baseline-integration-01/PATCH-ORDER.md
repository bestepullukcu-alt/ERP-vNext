# Exact patch and source order

1. Start from R21-applied 308-entry source manifest (`4494ef52ad189817790a07b2e881ed850e007b678bcafbcabf2adbfd28e4a220`). R21 patch `53aeccd053c33eb37deabc3d2be6ae3f1b7208c97b5e868e95385e3f284e90a7` is **already present** and was not applied again.
2. Add the 33 byte-identical R01-only inputs from the 341-entry manifest (`60ab3d68de8196d3a390a087884ca46c1e1530d64fa0a72e3074a15d62561052`). Its only four overlaps with different hashes are the two Claims R21 files and two Returns R01 files.
3. Apply accepted Returns `product.patch` SHA-256 `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c`, which changes only two Returns files. The target hashes are `eaa0aa73d1a2c6e296d57dbfc6ac278cef2081ec1a48cf6510f4b27c0dc2eea5` and `c36c462153aadaa3774af592be3a3d9905287a0b1c388a67c718fd594c33f7c7`.
4. Apply authorized R14 `Program.cs.patch` SHA-256 `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0` from baseline `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` to target `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1`.
5. Apply malformed-map patch SHA-256 `e929f9894a8adaf54aa2fa9455d30884cc873ac2dfc254712e8b48ec4f4eef88` **after R21**; reader baseline `94b2907241e4f23a1ca3eeacb0f2aba04d78c9a25ca36f2b021839d9f7da3223` and test baseline `04222ca3821f823689a1d6216d37ed95548cc3c2acc9d32867e2b1229a878efb` matched. Targets are `3529fa558ea124681495366765e9671bfb9f8157e78250b3eaeef90df71e7670` and `e03360a93509e1a8f9a1610c00aa719b8403136808911062aced7d876a46adf8`.

All three new patch checks and applies exited 0. No R22/R25 evidence-injection patch was applied.
