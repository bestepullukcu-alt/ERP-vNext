# MVP6-RETURNS-CLAIMS-DISPATCH-DECISION-01 — SOP §22

## CT disposition

**Decision: READY FOR OWNER DECISION; runtime dispatch remains HELD.**

The Returns and Claims final-pack deltas are internally consistent with the inspected final-release proposal and accepted model rework. They are proposed pack changes only; neither module is promoted and no runtime or publication action is authorized by this review.

## Exact artifacts and hashes

### Returns

- Directory: `docs/roadmap/plans/mvp6-final-pack-delta-01/` (the exact supplied path).
- `README.md`: `ad9c8f72c254fa6cb85ec89bcf925db92216a214b148c405ff0ec7446885d675`.
- `proposed-pack.patch`: `731622d2e6104ebd1e161082481860a9ea45ea0d39b25ef06bb226c4dbf2ab7d`.
- DEV HELD: `c6bd445ec0a6f9d80ba615fda9eb32108c8dd5fca7f5d2856d95146f4143a1c3`.
- VER HELD: `f8553b1783799d7a54da5da2e8d8fbafcab23ffcdc2be7986a7e88a85409c93d`.
- Phase 1.5 exact paths: `c13c590d59938521c532e1eb284ed7ce154b2e289ad1e9d94126a5198e8563e4`.

Returns scope remains the three frozen operations, accepted UUID value equality/lexical validation, authoritative Root R2 dependency, manual Received, opaque Inventory reference and Pending-only outbox.

### Claims

- Directory: `docs/roadmap/plans/mod-0187-final-pack-delta-01/`.
- `proposed-pack.patch`: `afe36ff9123088593ec920191657176e5343270655b7b0a2be4106bc05820187`.
- SOP §22: `396fd0ace2f135619413caecb461dbf20ee2d41f95e74c68327a60b25d7539b7`.
- Phase 1.5/integration: `06c0941a95f9fc8587e8425e45786adcc14df0d89207adb2bffc6a28bdc911d3`.
- DEV HELD: `d6ff2051d0777388cca0afa21f1ac3b1c706995c2d0e05fb5c2c5d2c4f8fa562`.
- VER HELD: `0d8936c9bec3d236df317dbb8a18a592850d3d69b0a3ebb8f4c4357f4022a7c7`.

Claims scope remains its 47-path proposal, stateful accepted rework, exact root/error/replay policy, Pending-only outbox and no worker/publisher.

## Final release and producer parity

The combined final-release VER is bounded proposal consistency only; its exact 3.0.0 YAML and annex publication remain held. The Root Producer CT acceptance is isolated implementation acceptance, not canonical contract uptake. Therefore consumer runtime may use only a later explicitly released exact contract/annex target; no old consent or candidate hash is silently transferred.

The final release decision texts distinguish: consumer consent, exact publication authority and guard activation. They remain separate owner decisions. Existing Root/Returns/Claims design decisions are not reopened.

## Ownership and composition

- Returns owned runtime/test paths are the exact list in `mvp6-final-pack-delta-01/owned-paths-v1.0.md`.
- Claims owned runtime/test paths are the exact 47-path proposal in `mod-0187-final-pack-delta-01/prospective-owned-paths.txt`.
- Intersection: **zero** between the two module-owned paths.
- `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` is shared and belongs to one separately leased integration writer. Neither consumer DEV writer may edit it.
- No gateway, shared permission registry, contract, guard, serializer, csproj or other module path is owned.

## Exact decisions for owner

1. Keep both packs draft until the exact final contract/annex publication and pack promotion are recorded.
2. Approve the Returns delta’s exact owned paths and Phase 1.5 proposal for a future isolated DEV dispatch; keep its HELD prompts immutable.
3. Approve the Claims 47-path proposal and Phase 1.5 exceptions for a future isolated DEV dispatch; keep its HELD prompts immutable.
4. Assign one integration owner for the exact Program.cs composition diff after each module writer is complete; serialize shared composition.
5. Require exact contract uptake/compatibility verification before consumer runtime acceptance. Root Producer acceptance alone is insufficient.

## Parallel dispatch order after gates

`final publication + approved pack/Phase1.5` → one integration-owner composition lease → Returns and Claims DEV in disjoint isolated worktrees (parallel-safe) → each independent VER → CT acceptance. Program.cs composition is serialized after writer handoffs; no downstream rollout or E5/G5 is implied.

## Remaining missing authority

- Pack promotion and Phase 1.5 approval for both modules.
- Exact final contract publication/uptake and consumer consent for the selected hash.
- Separate Program.cs integration-owner lease/diff.
- Fresh DEV dispatch prompts and isolated worktrees/baselines.
- Runtime evidence for Returns/Claims; model evidence is not runtime acceptance.

## No-change

Only this CT decision directory was written. Canonical contracts, packs, guard, Program.cs, runtime and historical records were not modified.
