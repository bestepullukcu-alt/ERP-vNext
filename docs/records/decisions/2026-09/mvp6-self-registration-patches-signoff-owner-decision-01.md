# Owner decision — Supply Chain self-registration patches sign-off (MVP6-SELF-REGISTRATION-PATCHES-SIGNOFF-OWNER-DECISION-01)

- Decided: 2026-09-26T09:23+0300 (Istanbul), by the owner (CEO Natig Yusubov) in the CT conversation (question tool): **sign patches 1, 2, 3, 4, 6; patch 5 later**.
- approvedBy: current-role-user-message-2026-09-26
- Source text: `docs/roadmap/plans/mvp6-self-registration-patches-01/SIGN-OFF-DECISION.md` sha256 `bb8487107152f2c23bd122f29746846901a25bbe0cc5d0913861f9ac60ffdaf8`
- Package: `docs/roadmap/plans/mvp6-self-registration-patches-01/SHA256SUMS` sha256 `2acd3241cffe1dcfdcc03c1d39cc9acc6912541db6c76f782dea0ebeca822230` (11/11 OK, CT)
- Queue: Q45 → DONE (partial: 5 of 6); apply → Q47; patch 5 → rebase in Q46, then new sign-off.

## Signed patches (apply only on exact hashes)

| Patch | sha256 | Target | Before | After |
|---|---|---|---|---|
| `patches/01-DCP-009-self-registration-foundation.patch` | `4142cfdabdab927b11d723a925012a7674d1a4ee61d4cff5e29d5bed34e0b69f` | `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md` | `ce30e922a10207553c2ec8ba1f493aaf6674e053d4ca7637fc45ae0dea77d083` | `e346043dd6d8163be561353fb393ee7b2b7f424c10239c282538c4f47020cec6` |
| `patches/02-MOD-0183-self-registration.patch` | `51951ee42d30a3e12745afcf9040f83b078afe9cdf05003e150ccf49b09847bc` | `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` | `32381634163a2acff440475f4582c05defc8261957386dfa43d5c8fc7eaf962e` | `8e269efa44b47bef41a81745a946ccef9d30a26d8ddf175000e80aad2242eadf` |
| `patches/03-MOD-0184-self-registration.patch` | `0d18664d931a74aff68b0b74123092b39d45224fb5d86e2125d969c32d264b8a` | `execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md` | `2df9363b7c870672fab13b68a87e7fb849ed7a3f213c5197e87f9c2147de817e` | `346288abaf9c26c9bfeebf1c923c8201166bb5c930f2389b57528014c1bffd1e` |
| `patches/04-MOD-0185-self-registration-after-loads-ui.patch` | `f9b5ef11a89650b66c11419dab8443d2a24de1683ffb053da8f16e90e20b57e2` | `execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md` | `4868669307a1cd37b88320f27d7c0c2e1c1ce8ed649f48c067cbedf20307536f` | `45b5dd3325b4c917eeaa9cb6b5268008173579e4525f4c90f25c43630c27caa5` |
| `patches/06-MOD-0187-self-registration.patch` | `594a3401c23c66ebf7452c90dbc3ec544dcf5d7b2c69ef0d2cc682035847845f` | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | `762ab5337af85b965216d41a212217db3681af3a31bbd1514544b58dbfc7786b` | `31cb35c38fd97c91172884156a32b69ec219e3cf4f96ad5f9124dc3c8ad06626` |

## Terms (from the source text)

- Each patch is applied only if its target still has the "Before" hash and the result has the "After" hash; on any mismatch the patch is rebased and brought back, never applied by hand.
- Patch 4 (MOD-0185) is recorded as a section that stays inactive until the Loads UI scope is approved and built.
- Pack and DCP text only. No code, `Program.cs`, `.csproj`, appsettings, `SharedResource`, Platform or gateway change, no new permission key or ID, no commit, push or stash by an agent. Code stays with the single CT-appointed integration owner after Q14/Q15, each provider shipping with its module's UI and nav keys (D4).
- Recorded open gaps stay open.

## Not signed

Patch 5 (`05-MOD-0186-self-registration.patch`): its base `07a8a015…` is superseded by the Returns sign-off (`mvp6-returns-pack-signoff-owner-decision-01.md`, target `6c8fbe28…`). It is rebased onto the applied Returns pack (section §33), re-checked, and brought back for a separate sign-off.
