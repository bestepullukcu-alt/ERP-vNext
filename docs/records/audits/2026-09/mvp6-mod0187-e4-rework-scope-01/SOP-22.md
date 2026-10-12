# MVP6-MOD0187-E4-REWORK-SCOPE-01 — SOP §22 handoff

## Verdict

**READY AS A BOUNDED REWORK PACKAGE.** R21 has an applicable Claims-owned patch with a targeted RED→GREEN regression. R14 and the R22/R25 process-injection seam are exact, unapplied candidates because they require additional shared/composition or test-injection authority. No acceptance row is closed by this scope package.

## Exact input binding

- Branch / HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Immutable integration input: `/private/tmp/mvp6-mod0186-http-integration-01`.
- Combined 97-entry manifest SHA-256: `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`.
- Fresh verification: 97/97 entries matched; see `manifests/input-verification.tsv`.
- Program.cs baseline SHA-256: `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- Controlling findings: `mvp6-mod0187-e4-policy-01/FINDINGS.md` and `mvp6-mod0187-e4-persistence-01/row-results.md`.

The integration snapshot was treated as evidence input and was not modified. Candidate work occurred in `/private/tmp/mvp6-mod0187-e4-rework-scope-01/work` or in this audit package.

## Four-issue disposition

| Row | Cause | Narrow solution | Exact files | Authority / state |
|---|---|---|---|---|
| R14 | Kestrel rejects non-ASCII request-header bytes before Claims middleware. The existing Python probe sent 128 bytes of `e9`, not UTF-8. | Select strict UTF-8 decoding only for header name `Idempotency-Key`; keep all other header decoders unchanged. Send real UTF-8 bytes in acceptance probes and count decoded Unicode scalars in Claims middleware. | Shared `Program.cs`; probe change remains evidence-lane work. | **BLOCKED for application.** Requires exact shared Program authorization and confirmation that SHIPMENT-BUNDLE Idempotency-Key wire bytes are strict UTF-8. Candidate only. |
| R21 | Claims reused inbound business-root correlation as the outbound Shipment GET trace. Nil is a valid Claims/root value but Shipment rejects nil request trace. | Generate a separate non-nil outbound dependency trace; continue reading the authoritative root only from ShipmentDetail and compare it to the unchanged inbound Claims root. | `ClaimReferenceReader.cs`, `ClaimReferenceTests.cs`. | Existing Claims-owned paths and this WP cover the candidate. Patch is READY; E4 closure still needs applied runtime verification. |
| R22 | Running composition binds `NoOpClaimCommitProbe`; Mongo cannot be pre-seeded with the unknown generated ClaimNumber. | Evidence-only DI mode supplies an explicit fixed ID, then a fixture pre-seeds a different `_id` with the same scoped ClaimNumber. The actual unique index must produce 503 with four-collection zero delta and no renumber. | Candidate changes only `NoOpClaimCommitProbe.cs`, `ClaimPersistenceRegistration.cs`; existing probe drives it. | **BLOCKED for application.** Exact evidence-injection authorization is required because the production assembly gains a test-only DI mode. |
| R25 | External Mongo failures cannot identify every aggregate→receipt→audit→outbox stage. Existing stage probe is inaccessible from a composed process. | Same evidence-only DI mode injects named precommit/afterCommit faults and logs reached stages. A separate Mongo `commitTransaction` failpoint tests unknown-commit handling; zero writes must never be assumed. | Same two persistence files plus existing Claims probe. | **BLOCKED for application.** Same explicit injection authorization; Mongo failpoint portion itself needs no source change. |

## Candidate hashes

| Candidate | Baseline | Patch SHA-256 | Target SHA-256 |
|---|---|---|---|
| R14 Program.cs | `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c` | `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0` | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` |
| R21 Claims patch | reader `8d5fe25c7a202d1cc893aa8100ba7eeb3c26193b3275ad7224a871de0f830788`, test `ecb3641e6c6245af370f286ba3f6dc564016aa244c0790f11bc653b0f380f573` | `53aeccd053c33eb37deabc3d2be6ae3f1b7208c97b5e868e95385e3f284e90a7` | reader `94b2907241e4f23a1ca3eeacb0f2aba04d78c9a25ca36f2b021839d9f7da3223`, test `04222ca3821f823689a1d6216d37ed95548cc3c2acc9d32867e2b1229a878efb` |
| R22/R25 injection | no-op `4082bb189256e6bffbec4440913f4fd2544ed79ce88252324cd86007d069a222`, registration `0361088e461cfac00ecf3b34b39ee311a9e58f0b82d51327b82f44ae196752de` | `d0844d8c8e4b28dc3ab1f1559b6f49c45005567f6f1ea252fca6c51f84f75eca` | probe `05cb87877bf427e3232fd2a074915a26e4256d9a5c61e357c4d7ab2b8ceef68b`, registration `c4ca2690bb964529cb7d1d2a1265c514c10946f792829d4da821597636761a9c` |

All three patches pass `git apply --check` against the immutable integration baseline. R14 and R22/R25 candidates were not applied or built.

## R21 RED→GREEN

- RED: patched assertion against baseline reader failed 1/1 because outbound dependency correlation remained nil.
- GREEN: target reader passed 1/1.
- Claims reference regression: 28/28 passed.
- Product behavior changed only in the disposable candidate. No full R21 E4 runtime claim is made until the patch is applied in the designated Claims checkout and the real nil-root producer scenario is rerun.

## Evidence boundary

R14 byte capture proves Python `http.client` encoded 128 `é` values as 128 `e9` bytes. Those bytes are Latin-1 and invalid strict UTF-8. The proposed interoperable wire rule therefore needs real UTF-8 bytes (`c3a9` per `é`), not reuse of the old probe. This is a transport-policy clarification, not an ASCII-only narrowing.

The R22/R25 candidate is unavailable in every environment except exact `ClaimsEvidence`. Production startup continues to register `NoOpClaimCommitProbe`. `ClaimsEvidence` fails startup unless an explicit fixed ID or allowed fault stage is supplied, exposes no endpoint and changes no authentication. Evidence-mode process results must remain labelled test-composition evidence; they are not silently equivalent to normal production startup.

## No-change and boundaries

No canonical contract, annex, guard, Returns source, gateway, pack, actual Program.cs, historical evidence, commit, push or stash was changed. The only repository changes owned by this WP are this audit directory. No test was repeated without a candidate change.

## Next controlled sequence

1. Apply R21 patch in the designated Claims DEV checkout; run targeted unit regression, fresh build and real producer nil-root E4 scenario.
2. Obtain the exact R14 transport-policy/shared Program authorization; apply the exact candidate and run raw UTF-8 128/129 plus Shipment/Carrier/Loads/Returns/Claims regressions.
3. Obtain the exact R22/R25 evidence-mode authorization; apply the exact candidate and run fixed-number collision and each named stage in separate processes/scopes.
4. Run external Mongo unknown-commit scenario separately, record the actual post-failure DB state, restart, then retry the same key and prove one final durable write group.
5. Independent VER evaluates only these four rows; CT decides closure.

Writer complete: **yes**.
