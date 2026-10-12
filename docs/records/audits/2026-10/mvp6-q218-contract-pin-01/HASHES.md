# Q218 — Hashes (all computed in this lane with `shasum -a 256`)

| sha256 | path | note |
|---|---|---|
| `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | canonical contract on disk — info.version 3.1.0 (`:13`), x-status FROZEN, git state ` M` |
| `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` | `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml` | recovered 3.0.0 bytes — info.version 3.0.0 (`:13`); folder is untracked (`??`) |
| `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` | `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/artifacts/shipment-bundle-v3.1.0-final-proposed.openapi.yaml` | 3.1.0 final-proposed bytes — equal to the canonical file |
| `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11` | `docs/analysis/contracts/returns-semantics-v3.0.0.md` | Returns annex — equals pin MOD-0186 `:584` |
| `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63` | `docs/analysis/contracts/claims-semantics-v3.0.0.md` | Claims annex — equals pin MOD-0187 `:533` |
| `7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af` | `docs/analysis/contracts/shipment-root-semantics-v3.0.0.md` | root annex — equals pin MOD-0186 `:584`, MOD-0187 `:533` |
| `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee` | `docs/analysis/contracts/carrier-semantics-v1.1.0.md` | Carrier annex — equals the value quoted at MOD-0186 `:168`, MOD-0187 `:169` |
| `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1` | `docs/analysis/contracts/loads-semantics-v2.0.0.md` | Loads annex v2 — equals MOD-0185 `:480` |
| `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` | `docs/analysis/contracts/loads-semantics-v3.1.0.md` | Loads annex v3.1.0 — new with the 3.1.0 publication; equals owner decision A/B target |
| `65a8ccbdd09acb97be8bed0195b3732fba241667e494a314d4dc4a192b660a4e` | `docs/reference/architecture/docs-path-authority.json` | guard authority; `:11` binds the contract to `6dc1dd48…` |
| `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5` | `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/publication.patch` | publication patch named in owner decision B (`dc0ad05b…`) |
| `e12d59750c4846269bf1b84341f9c437060e5b891740bb4077e8917ebf3eea9f` | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-a-01.md` | owner decision A |
| `36fe774a11a31c2b6e4f3c623f76cf22b4f2fe272474a8780f1b701753e5f35f` | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md` | owner decision B |
| `2a65ce1d516c1850bef74f9d26e5d54dfc6042acae45ad5199730ef43ddf2c83` | `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` | pack, ` M` |
| `35bead97350624074d5d67e4119eaafba59a9eb68dd15f8e1f5729564291dc21` | `execution/domains/supply-chain-execution/module-packs/MOD-0184-carrier-management.md` | pack, ` M` |
| `9ec4ef1bc1db21e07e01d5e8c0671ea4809840e6245d8749fb4da0a8b75abd2e` | `execution/domains/supply-chain-execution/module-packs/MOD-0185-routing-load-planning.md` | pack, ` M` |
| `933e89262713f36982a7885b9b95a84e3fbfad62b276b721a300c0ec6c8521fe` | `execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md` | pack, ` M` (same hash as Q210 recorded: `933e8926…`) |
| `3d1a00e2f0a0e7e58d19f44b340de9ab0d6bf84e7e2997db451c65d2ddb74cae` | `execution/domains/supply-chain-execution/module-packs/MOD-0187-claims-management.md` | pack, ` M` |
| `eaa0aa73d1a2c6e296d57dbfc6ac278cef2081ec1a48cf6510f4b27c0dc2eea5` | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs` | Returns consumer |
| `3529fa558ea124681495366765e9671bfb9f8157e78250b3eaeef90df71e7670` | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimReferenceReader.cs` | Claims consumer |

## Pin against measurement

| Pin (source) | Pinned value | Measured on the canonical path | Result |
|---|---|---|---|
| SHIPMENT-BUNDLE YAML — MOD-0186 `:584`, `:484`, `:619`; MOD-0187 `:533`, `:501`, `:569` | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` (3.0.0) | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` (3.1.0) | **DIFFERS** |
| Returns annex — MOD-0186 `:584` | `00990a28…8a11` | `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11` | equal |
| Claims annex — MOD-0187 `:533` | `16e65c26…4eb63` | `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63` | equal |
| Root annex — both packs | `7d1327a1…b8af` | `7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af` | equal |
| Guard authority `docs-path-authority.json:11` | `6dc1dd48…96aa2` | `6dc1dd48…96aa2` | equal |

## Where each version of the contract exists

| Version | sha256 | Where |
|---|---|---|
| 3.1.0 | `6dc1dd48…96aa2` | canonical path (working tree, ` M`) · release-prep `artifacts/` copy |
| 3.0.0 | `5dfe7c1b…9d21c` | **one file only**: `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml` (untracked folder). The same folder's `PUBLICATION-MANIFEST.sha256` lists this hash and the three annex hashes; the six files under `publication/` were re-hashed here and agree. |
| 1.0.0 | `f6415bbfda42a61a9845e7e1fc843be087bf249cac6bcdc9cb678284766450a1` | the git blob at `HEAD:docs/analysis/contracts/shipment-bundle.openapi.yaml` (read with `git cat-file -p`), and 57 of the 85 candidate files found (the lane work trees under `~/mvp6-env/`) |
| 2.0.0 (`93c696e2…`) and 1.1.0 (`ba9d85f0…`) | — | **not found** among the 85 candidate files hashed. No wider search was made. They exist here only as pins. |

Git history of the path, all refs (`git log --all`): one commit, `4a8d4d4b3` (2026-09-16). Git therefore holds 1.0.0 only.
3.0.0 was never committed; neither was 3.1.0.

Search method: every `*.yaml`/`*.yml` under the repository (outside `.git`) that mentions the bundle, plus every file named
`shipment-bundle*` under `~/mvp6-env/` — 85 files, 25 distinct hashes. Sealed `tar.gz` archives were **not** opened.
