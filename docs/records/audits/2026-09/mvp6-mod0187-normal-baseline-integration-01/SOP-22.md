# MVP6-MOD0187-NORMAL-BASELINE-INTEGRATION-01 — SOP §22

## Verdict and exact scope

**Integration DEV PASS; writer complete.** A single hash-bound disposable normal-source baseline now combines R21, accepted Returns R01, authorized R14, and the separate malformed-producer mapping rework. This is an integration handoff for independent R14/R21/malformed HTTP verification, not independent VER or CT acceptance. R22/R25 evidence-injection is absent.

The repository remained on `feature/mvp6-logistics` at `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Initial common-checkout dirty inventory had 158 status rows. Existing dirty files were preserved. The assembled source is a separate disposable filesystem snapshot at `/var/folders/f_/xfqgm56x3msgx1mh0s8j7zq40000gn/T/mvp6-mod0187-normal-integration-akf5p5ta`; the permanent, hash-verified archive below is the handoff authority.

## Authority and patch chain

- R21-applied manifest: 308/308 exact, SHA-256 `4494ef52ad189817790a07b2e881ed850e007b678bcafbcabf2adbfd28e4a220`. R21 patch `53aeccd053c33eb37deabc3d2be6ae3f1b7208c97b5e868e95385e3f284e90a7` was already present and **not reapplied**.
- Returns R01 CT decision: `mvp6-mod0186-r01-ct-close-01/SOP-22.md`, bounded **ACCEPTED**. Its 341-entry manifest SHA-256 is `60ab3d68de8196d3a390a087884ca46c1e1530d64fa0a72e3074a15d62561052`. The 33 R01-only paths were byte-identical in the source checkout. The four overlapping differences were exactly two R21 Claims files and two R01 Returns files.
- Returns R01 patch: `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c`; only the two Returns files changed to the CT-accepted target hashes.
- R14 real owner decision: `mvp6-mod0187-r14-apply-01/AUTHORITY.md`. Program.cs baseline `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`, patch `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0`, target `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1`.
- Separate malformed-map patch: `e929f9894a8adaf54aa2fa9455d30884cc873ac2dfc254712e8b48ec4f4eef88`, applied **after** R21. Claims reader/test targets `3529fa558ea124681495366765e9671bfb9f8157e78250b3eaeef90df71e7670` / `e03360a93509e1a8f9a1610c00aa719b8403136808911062aced7d876a46adf8`.
- Published inputs were read-only: SHIPMENT-BUNDLE YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`, Claims annex `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63`, Returns annex `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11`.

`PATCH-ORDER.md` records exact baseline/target hashes. All three new `git apply --check` and disposable apply operations exited 0. `delta-matrix.tsv` shows exactly five changed paths against R21 input: two Returns, one Program.cs, two Claims. Compared with accepted R01 final manifest, only Program.cs and the two Claims files differ. No shared, business, producer or R22/R25 injection edit was added.

## Fresh source → build → binary evidence

The permanent `normal-source.tar.gz` contains exactly 341 source/test inputs. `combined-source-manifest.tsv` hashes each extracted path and independently verified **341/341**. The archive SHA-256 is `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21`; combined manifest SHA-256 is `92879d2098e5c50fb4c2862ee52060cbe8ba1aab2e038e77f513f49680e80f80`.

Fresh restore and rebuild completed with exit 0; build had 0 warnings and 0 errors. The resulting API DLL SHA-256 is `60b427538ff34cb192ade08a1bd3ebd433512b12f1b83e13fe32241922423636`. The build and test logs, commands, exits and TRX files are in `test-evidence.tar.gz` (SHA-256 `c49bc090b704baca8e42536e64814ecfc4fe0ac3b2b4e45447fa7e0f5cf66dcf`), whose 42 internal entries independently matched its manifest.

The first `--no-restore` build failed because this new path had no project assets; an isolated restore fixed that setup issue. The first broad same-host test attempt used host .NET 10 and exposed TestHost runtime incompatibility; it is retained as failed evidence. The successful tests used `/Users/natig/.dotnet/dotnet` 8.0.417 with its ASP.NET Core 8 runtime. Neither setup failure was counted as PASS.

## Tests and limits

| Suite | Exact fresh result | Evidence |
|---|---:|---|
| Claims reference | 36/36 PASS | `test-evidence-net8/claims-reference.trx` |
| Claims + Returns + Loads + Carriers same-host | 272/272 PASS | `test-evidence-net8/same-host.trx` |
| Exact `ShipmentTests` class | 12/12 PASS | `test-evidence-shipment-exact/shipment-exact.trx` |

The 36 reference tests are included in the 272-test same-host set and are **not added**. The first overbroad `~Shipment` filter selected 51 tests and yielded 12 `ShipmentTests` failures from repeated Mongo `Guid` serializer registration in that mixed test process. The corrected exact-class run used a new process and replica set, with 12/12 PASS. The failed 51-test attempt remains in the evidence archive; this lane did not change serializer or product code to hide it.

The Mongo replica sets were lane-owned, on ports 27963 and 27964 for successful regressions; their cleanup records show no listener. Operational port 27017 was untouched. Tests here establish integration build and regression consistency, **not** fresh authenticated R14/R21/malformed HTTP acceptance. The R14 baseline already accepted correctly encoded valid UTF-8; no valid-UTF-8 RED claim is made. Its earlier Latin-1 probe finding remains historical.

## Handoff and remaining gates

Independent VER should extract the exact archive, recheck all hashes, and run fresh authenticated HTTP against the composed normal source for R14 header decoding, R21 nil-root/trace separation, and exact malformed producer 500 mapping. It must keep R22/R25 injection separate. This package grants no canonical/guard change, rollout, commit, push, stash, E5/G5 or module completion.

The integration source writer has completed; all task-owned Mongo listeners are closed. Only this new audit directory was written in the common checkout.
