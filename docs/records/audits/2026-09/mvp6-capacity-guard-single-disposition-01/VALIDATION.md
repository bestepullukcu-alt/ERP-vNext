# Disposable validation

## Exact inputs

- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Production reader source SHA-256: `da5ec0cc0f180d7237bdf599c7d71eb1fb96962ed14496427a1ea789b18c5050`.
- Authority baseline SHA-256: `063e0cde7c436cf0926101ac9a300b9b1e6147dd5bea5c7dbfd100e65a868fe9`.
- Historical JSON SHA-256: `edfeaace44b96c2699e62da3bd8fe9f16b6f836e5ef5d1988ced3f657b43d21a`.
- Provenance SHA-256: `401174ea2ff6e12861a9f2e375f5990f42bd879623ecc20b8c83e21f93f2ceda`.

## Patch applicability and preservation

`git apply --check` and disposable `git apply` succeeded against the exact authority baseline. The applied output was byte-identical to `authority-candidate.json.txt`. `PRESERVATION.tsv` shows that the two existing canonical targets and all 34 previous seals are byte-identical after canonical JSON serialization; the candidate only appends the 35th seal. No policy, reader or schema change is required.

## Real-reader test

The candidate was copied to the disposable root recorded in `DISPOSABLE-PATH.txt`. Only in that disposable root, it was bound to a synthetic structural decision and marked `APPROVED`; this is not owner consent or production activation. A fresh build of the actual architecture test project then ran:

```text
/Users/natig/.dotnet/dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj -c Debug --no-restore --filter FullyQualifiedName~DocsPathGuardTests --logger trx;LogFileName=DISPOSABLE-DOCS-PATH-GUARD.trx
```

Result: **39 passed, 0 failed, 0 skipped**. The set includes the production-mode positive scan plus the actual reader's negative cases for unapproved/missing/invalid decision, wrong source hash, changed history, wrong or missing provenance, payload tampering, unknown path/kind, duplicate source/key, wildcard/traversal/symlink and empty/presence failures. Raw result: `DISPOSABLE-DOCS-PATH-GUARD.trx`, SHA-256 `d57427bca327f45ed89fef711cd37ade383fe1d9fc51d15474d3b50fccc57143`.

The first sandboxed VSTest attempt aborted because localhost test-platform socket binding was denied. The authorized retry completed successfully; the aborted attempt is an environment event, not a test result.

## Production boundary

The repository authority remains unchanged and approved under its prior decision. The new candidate remains `UNAPPROVED` with `decision:null`, so the real reader will reject it until an actual owner decision is created and bound. A production-mode full DocsPathGuard run after activation remains mandatory.
