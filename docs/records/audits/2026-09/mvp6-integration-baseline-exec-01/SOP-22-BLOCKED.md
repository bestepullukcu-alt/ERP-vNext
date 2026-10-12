# MVP6-INTEGRATION-BASELINE-EXEC-01 — SOP §22 BLOCKED handoff

**Verdict: INPUTS PASS / EXECUTION BLOCKED — exact target-bound owner decision bulunamadı.**

## Scope and identity

- Repository: `/Users/natig/Projects/ERP-vNext-recovery`
- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
- Dispatch: `docs/roadmap/plans/mvp6-integration-baseline-selection-01/INTEGRATION-DISPATCH-v1.0-HELD.md`
- Transfer order: `docs/roadmap/plans/mvp6-integration-baseline-selection-01/TRANSFER-ORDER.md`
- Writer model: one future source/composition writer in one new registered worktree

The common checkout was already dirty and was not used as the integration target. No registered worktree, branch, source transfer, patch application to a checkout, build, startup, test, commit, push or stash was performed by this lane.

## Completed preflight

The full baseline-selection package checksum passed. The exact source inputs also matched:

| Input | SHA-256 / result |
|---|---|
| MOD-0190 archive | `8fa00d40814a81472ec4221ad909321d058dea18a11f64dd2ac16ea800ecb745` |
| MOD-0190 manifest | `ae7ef59e0158113c22389ccd3eec84207418f3a859c40480e70b42ee888a9bef`; 379 rows |
| Capacity archive | `753843251ea261473e308e5fb100de834b1e5ab88fa7576ff50ad22bf332274e` |
| Capacity manifest | `36114191e1576b8b5654b9910f3347432d5d3bf36aac87339a4280cc85a852f8`; 43 rows |
| Manifest intersection | 0 paths |
| Program patch | `78cc0fa61dd37019f525c621c572d1dc2ef518eb0b4faf520a280f01909db82e` |
| Program preimage → target | `a2a216be15d07fa656f5fd7da43711aa989c4a8d52cd51c58e8ded48d5d735e5` → `50c48a2b895804162f36740703bca20d3f0c3f96f2511133e843d75fcf7830f0` |
| Test-only patch | `b6744cddfd498536abeb4b350d8b8b2bd1c30c0824875757522026ed5f01df81` |
| Test preimage → target | `91999330faf2a50f5ff2cda3595506ecded50d5b87ffb7bf393dd655f71e51db` → `2c133ac1b6b431db074b0054b0337bbc3957fce5869c7aca9291454a53681c43` |
| Derived selection | 422 rows; 74,155 bytes; `cdc6228aa3d215f34f2f2f9d5b784cf57ac20b6eb8f2c2f26bdcace9dcd4d76c` |

Both archives were extracted into a unique disposable directory. Every manifest entry was rehashed, both patches were applied in the required order against their exact preimages, and the derived manifest was regenerated. `PRECHECK.json` records the machine-readable result; `SELECTED-422-PREFLIGHT.tsv` is the regenerated prospective manifest. These disposable results are input readiness only, not integrated evidence.

## Authority disposition

The located B decision authorizes the bounded MOD-0192 X01/X07 production rework. It does not bind a new integration checkout to the 379/43 transfers, shared `Program.cs` target, test-only target and final 422-row manifest. Historical hosted integration authority was already exercised in its named lane and is not portable.

The controlling HELD dispatch states: “this document is not authority and must not be executed until the exact target-bound owner decision below is given.” No durable decision record or user message containing that exact integration authority was found. The current execution prompt identifies the dependency as “gerçek B kararı”; it does not itself supply the target-bound hash decision required by the dispatch. Approval is therefore not inferred.

## Remaining action

Give the exact decision in `AUTHORITY-REQUEST.md`. After that, one writer may create a unique registered worktree at the pinned HEAD, repeat the input gate, execute the transfer order, run the bounded integration regression, archive immutable source/build/process/HTTP/DB evidence, and issue the independent VER handoff.

Duplicate-name uptake, canonical/guard, gateway/shared permissions, pack changes, rollout, E5/G5 and git commit/push/stash remain excluded.
