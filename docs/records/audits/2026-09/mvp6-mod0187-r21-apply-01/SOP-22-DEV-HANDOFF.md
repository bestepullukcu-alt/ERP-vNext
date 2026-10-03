# MVP6-MOD0187-R21-APPLY-01 — SOP §22 DEV handoff

## 1. Verdict

**R21 PATCH APPLIED / R21 ACCEPTANCE PASS; broader regression disposition: REWORK.**

The exact authorized patch `53aeccd053c33eb37deabc3d2be6ae3f1b7208c97b5e868e95385e3f284e90a7` is applied in the registered, source-consistent integration worktree `/private/tmp/mvp6-mod0186-http-integration-01`. The patch changes only the Claims reference reader and its Claims-owned regression test. The authoritative nil business root now remains nil while the outbound Shipment dependency request receives a distinct non-nil UUID trace.

Fresh real-producer HTTP/DB evidence closes the R21 nil-root defect. A separate existing malformed-producer mapping mismatch remains open: the real producer returns `500 SHIPMENT_ROOT_INVALID`, which the current Claims reader maps to `503 CLAIM_REFERENCE_UNAVAILABLE`; the published Claims annex requires `502 CLAIM_REFERENCE_INVALID`. That behavior was not changed because this work package authorizes only R21.

## 2. Execution baseline and authority

- Branch/HEAD authority baseline: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Applied registered worktree: `/private/tmp/mvp6-mod0186-http-integration-01`, detached at the same HEAD.
- Initial Claims worktree inspection found `Program.cs` at the approved combined hash but without the authorized Returns source transfer. Its fresh build failed on four missing Returns namespaces. The temporary R21 application there was exactly reversed. The immutable failure record remains in the raw archive.
- Consistent runtime baseline: the registered integration worktree contains the combined Claims/Returns source set and `Program.cs` SHA-256 `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- Patch target hashes match the rework package exactly. `git apply -R --check` succeeds after application, proving that the authorized patch is present.

## 3. Exact source delta

| Path | Baseline SHA-256 | Applied SHA-256 |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimReferenceReader.cs` | `8d5fe25c7a202d1cc893aa8100ba7eeb3c26193b3275ad7224a871de0f830788` | `94b2907241e4f23a1ca3eeacb0f2aba04d78c9a25ca36f2b021839d9f7da3223` |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimReferenceTests.cs` | `ecb3641e6c6245af370f286ba3f6dc564016aa244c0790f11bc653b0f380f573` | `04222ca3821f823689a1d6216d37ed95548cc3c2acc9d32867e2b1229a878efb` |

The implementation generates a new dependency request correlation at `ClaimReferenceReader.cs:76-78`; it does not replace the root read from ShipmentDetail. The regression asserts a non-nil trace distinct from the Claims root at `ClaimReferenceTests.cs:32-33`.

The exact applied diff is `R21-applied.patch`, SHA-256 `53aeccd053c33eb37deabc3d2be6ae3f1b7208c97b5e868e95385e3f284e90a7`. `changed-files.tsv` and `source-manifest.tsv` bind the delta and current build source set.

## 4. Contract alignment

- Claims create consumes the original Shipment lifecycle root and treats GET correlation as request trace only (`claims-semantics-v3.0.0.md:141-145`).
- Shipment root is optional/nullable on the wire, explicitly emitted by the upgraded producer, authoritative only, and never derived/backfilled (`shipment-root-semantics-v3.0.0.md:3-6`).
- R21 follows those rules: the dependency trace is transport metadata; the persisted Claims root comes only from the Shipment response and must equal the inbound Claims root.
- Frozen contract and annex bytes were read and hashed but not edited.

## 5. Build and tests

| Check | Result | Evidence |
|---|---|---|
| Fresh consistent service rebuild | PASS, exit 0 | `raw/integration-build-escalated.log`, `raw/integration-build-escalated.exit` |
| Fresh API binary | `e1b3bce58aded9a5ff107b230dad8cb57cd799ce24233f80c52424412dc40dd2` | `raw/integration-binary.sha256` |
| Targeted ClaimReference suite | PASS 28/28 | `results/r21-reference-integration-escalated.trx`, `raw/integration-reference-tests-escalated.log` |
| Claims ordinary suite | PASS 120/120 | `results/r21-claims-120.trx`, `raw/claims-120-tests.log` |
| Explicit two-process restart test in ordinary invocation | Correctly excluded by its own mode guard; the unfiltered exact namespace run recorded 120 PASS / 1 explicit-mode FAIL | `results/r21-claims-exact.trx`, `raw/claims-exact-tests.log` |
| Process cleanup | PASS; ports 27941/5081/5181/27942/5082/5182/27943 closed | green/red cleanup JSON and `raw/claims-tests-cleanup.json` |

The earlier broad filter `FullyQualifiedName~Claims` also matched Carrier test method names containing “Claims”; its 24 environment failures are retained but are not used as Claims acceptance evidence.

## 6. HTTP RED → GREEN and persistence evidence

Both runs used fresh binaries, DB-010 lane-specific replica sets, ephemeral JWT validation through the real middleware, and a forward-only capture proxy. The proxy generated no Shipment payload; it forwarded each GET to the same real SupplyChain Shipment detail endpoint and recorded only safe trace/scope metadata.

| Scenario | Pre-patch HTTP RED | Patched HTTP GREEN |
|---|---|---|
| Nil Shipment root / nil Claims root | `502 CLAIM_REFERENCE_INVALID`; outbound trace was nil; second attempt reread the dependency; zero writes | `201`; outbound trace non-nil and distinct; replay `201` with `idempotentReplay=true`; no dependency reread |
| Nil persistence | No records | Exactly one aggregate, receipt, audit and Pending outbox record; every business/event root remains nil |
| Claims response root | Error correlation remained nil | Response correlation remained nil and success body did not invent a root field |

Fresh patched control cases:

| Case | Result | Scoped persistence |
|---|---|---|
| Non-nil same root | `201` | one aggregate/receipt/audit/outbox |
| Missing producer root | `503 CLAIM_REFERENCE_INCOMPLETE` | zero writes |
| Null producer root | `503 CLAIM_REFERENCE_INCOMPLETE` | zero writes |
| Different valid root | `409 CLAIM_CORRELATION_MISMATCH` | zero writes |
| Same-key nil replay | original `201`, `idempotentReplay=true` | no extra writes; no dependency reread |
| Malformed producer root | **Observed `503 CLAIM_REFERENCE_UNAVAILABLE`; expected `502 CLAIM_REFERENCE_INVALID`** | zero writes; open non-R21 gap |

Raw request/response hashes, safe headers, producer detail results, dependency captures, tenant/LE-scoped before/after documents and cleanup are in `evidence.tar.gz`.

## 7. Preserved boundaries

- `Program.cs` remains `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- No Shipment producer, Returns, contract, guard, gateway, pack, migration, commit, push or stash operation was performed.
- The nil root was not derived, normalized, replaced or backfilled.
- JWT validation was not bypassed. Bearer tokens and signing secrets are absent from the archive.
- R14 and R22/R25 candidate patches were not applied.

## 8. Remaining gap and handoff

**GAP-0187-R21: CLOSED by DEV evidence, pending independent VER.**

**GAP-0187-MALFORMED-PRODUCER-MAP: OPEN.** Real Shipment malformed persisted root produces `500 SHIPMENT_ROOT_INVALID`; `ClaimReferenceReader.cs:83-84` collapses all dependency 5xx to `503 CLAIM_REFERENCE_UNAVAILABLE`. The controlling Claims annex requires malformed root to surface as `502 CLAIM_REFERENCE_INVALID`. Closing it requires a separate scoped decision/rework because changing producer-error translation is outside this exact R21 patch.

The source writer is complete and no task-owned process remains running. This handoff is a DEV result, not independent verification or CT acceptance.
