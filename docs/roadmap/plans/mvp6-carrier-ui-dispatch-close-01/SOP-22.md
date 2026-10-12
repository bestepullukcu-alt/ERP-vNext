# SOP §22 — MVP6-CARRIER-UI-DISPATCH-CLOSE-01

## Verdict

**PREPARED / PHASE 1.5 DESIGN COMPLETE / APPLICATION HELD / UI DEV HELD / UI VER HELD.**

The existing SCOPE-01 decisions are consumed unchanged: 21 UI-owned paths, four-field GoldenReferenceSlim, tenant shell, and exact list/create/status surface. DCP-002 and operation parity were not re-produced. This lane prepared exact shared-integration bytes and a start decision; it did not apply them.

## Repository and authority baseline

| Item | Exact value | Disposition |
|---|---|---|
| Repository | `/Users/natig/Projects/ERP-vNext-recovery` | dirty multi-lane checkout preserved |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` | identity only; HEAD is not the source baseline |
| SCOPE-01 pack preimage | `2df9363b7c870672fab13b68a87e7fb849ed7a3f213c5197e87f9c2147de817e` | unchanged |
| SCOPE-01 pack patch / target | `93ee76da36fd100069878b0e3b0621f6e60884d312d0e142c2e7fcb24267777d` / `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f` | unapplied; no real UI owner approval found |
| BC successor manifest | `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634` | 422 rows; static composition PASS only |
| BC transfer authority | `mvp6-bc-successor-exec-01/SOP-22.md` SHA `c4363a0ec9d360865e6fdadf38de0af806fa1ef267b3ed28730860ea137e6975` | BLOCKED; final target-bound owner handoff absent |
| Frozen Carrier contract | YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`; annex `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee` | unchanged |

The BC manifest contains no `frontend/**` or `gateway/**` rows. It is therefore a dependency for the SupplyChain registration preimages only. Gateway and seven shared-resource preimages are independently pinned in `candidates/BASELINE-PATCH-TARGET.tsv`. No record is treated as authority for a surface it does not contain.

## Exact integration candidate

| Candidate | Patch SHA256 | Baseline | Result |
|---|---|---|---|
| Gateway | `0ecc88eb0783088e008d5f8cddbda50482e4e9dfd57277f302a3a5a7621ebee4` | `ocelot.json` `b0121d2f5b7f809dc3dfd9a406b411bfd1ebb0b5b57faaf42b9ca69fabc1229f` | target `239e1903ca632a7fed677260887fb0ebcbe73d79363cd3691cdd2321e8817c90` |
| Permission/module registration | `18001827f4156942028851a55f912b81f7a2cdc2b887fce8d434310bda5168cc` | BC successor `dec28b6…`; Program `50c48a2b…`, API project `538e96c6…`, five absent paths plus appsettings | nine exact targets in TSV |
| Navigation localization | `e752b46e904d934bc3dc71c50fe0006a4febf603b5cdf656821ac678639aa8bf` | seven independently pinned current RESX files | seven exact targets in TSV |
| Combined single-owner artifact | `3fdc9188f635a2428e3ffc5c4ef07c9c4b747b2f74a4196d8e6207cd85ccf1d5` | all preimages above must match together | unapplied |

The gateway candidate exposes only collection GET/POST/OPTIONS and subpath POST/OPTIONS to port 5061. It does not add PATCH, PUT, DELETE, or a new Carrier endpoint. The module manifest declares one read-gated tenant page and only the existing create/status actions. The seven localization files add the normalized domain/module/page keys only. `_LayoutTenantShell.cshtml` is unchanged.

## Disposable validation

- Existing BC successor workspace was revalidated: 422/422 manifest rows matched before preparation.
- The final patches applied cleanly to their respective disposable pinned-preimage trees; 18/18 target hashes matched.
- Gateway JSON parsed and all seven RESX files parsed as XML.
- Candidate SupplyChain test project compiled on the disposable BC source: build exit 0, 0 warnings, 0 errors.
- A disposable Gateway-test build reached compilation but the copied/offline assets did not resolve the repository's existing `Microsoft.AspNetCore.TestHost` dependency; NU1900 also recorded unavailable nuget.org vulnerability metadata. No Gateway focused-test PASS is claimed. `DISPOSABLE-VALIDATION.md` records the boundary, and independent VER must execute the generated route test on the exact integrated source.

## Phase 1.5 conclusion

`PHASE15-ACCEPTANCE.tsv` completes the technical mapping without claiming runtime evidence. Field/wire parity, owned paths, tenant shell, DataTables v2, UAS-001, SweetAlert2, seven languages, independent permissions, gateway-only egress, and exact error/replay behavior all have named implementation and falsification evidence. Remaining gates are authority/application/runtime gates:

1. real owner approval for the SCOPE-01 pack target and UI Phase 1.5;
2. separate final target-bound authorization to materialize BC successor `dec28…` in the chosen checkout;
3. one integration owner authorized for combined patch `3fdc9188…`, with all preimages rechecked after BC materialization;
4. active UI DEV v2.0 release; then writer-complete;
5. independent UI VER v2.0 and composed Gateway/JWT evidence.

No backend acceptance was widened to UI. No permission meaning, layout, backend endpoint, contract, gateway policy, rollout, E5/G5, commit, push, or stash authority is claimed.
