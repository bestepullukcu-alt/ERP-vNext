# MVP6-MOD0187-MALFORMED-MAP-REWORK-01 — SOP §22 DEV Handoff

## Verdict

**DEV PASS — writer complete.** The separate Claims-owned malformed-producer mapping rework is implemented and evidenced. This is not independent VER, CT acceptance, canonical publication, or E5/G5.

## Scope and authority

- Work package: `MVP6-MOD0187-MALFORMED-MAP-REWORK-01`.
- Repository HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Separate detached worktree: `/private/tmp/mvp6-mod0187-malformed-map-rework-01`.
- Exact input: the 308-entry R21 source manifest, SHA-256 `4494ef52ad189817790a07b2e881ed850e007b678bcafbcabf2adbfd28e4a220`.
- Manifest verification: 308 present, zero missing, exactly two approved Claims-owned deltas.
- Published Claims annex SHA-256: `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63`.
- `Program.cs` remained byte-identical at `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.

## Exact behavior

The reader now recognizes only an HTTP 500 response whose standard error body has `contractVersion: v1`, exact code `SHIPMENT_ROOT_INVALID`, a string message, and a UUID correlation. It maps that producer failure to Claims `502 CLAIM_REFERENCE_INVALID`.

Unknown 500 errors, malformed error JSON, incomplete error bodies, invalid producer correlation, wrong contract version, and 501 remain `503 CLAIM_REFERENCE_UNAVAILABLE`. Refusal and timeout remain 503. Missing/null Shipment roots remain incomplete; a malformed root in a successful 200 payload remains invalid. The dependency trace stays a separate non-nil UUID, while the authoritative nil business root remains unchanged.

## RED → GREEN

| Check | Result | Evidence |
|---|---|---|
| Baseline exact producer 500 mapping | RED: expected 502, actual 503 | `evidence/results/malformed-map-red.trx` |
| Narrow reader tests | GREEN: 36/36 | `evidence/results/malformed-map-green-final.trx` |
| Claims regression | GREEN: 128/128, 0 skipped | `evidence/results/malformed-map-claims-final.trx`, `evidence/raw/claims-regression.log` |
| Fresh API rebuild | PASS, 0 warnings, 0 errors | `evidence/raw/fresh-build.log` |
| Real producer runtime | PASS, six cases | `evidence/raw/runtime-results.json` |
| Patch applicability | `git apply --check` and disposable apply exit 0 | `evidence/raw/patch-check.txt` |

The targeted/full counts overlap and are not added together.

## Runtime and persistence evidence

The executed API DLL SHA-256 is `2accc97643c306c0008e42d1bae71c0d1d03b4ce448038a5769a1c0b5c258497`; `evidence/raw/processes.json` binds its path and process to the HTTP run. The environment used API `5083`, forward-only Shipment capture `5183`, and DB-010 replica set `27954`; all were closed after the run.

For the malformed record:

- Direct Shipment detail returned `500 SHIPMENT_ROOT_INVALID`.
- Claims create returned `502 CLAIM_REFERENCE_INVALID`.
- Claims response header/body correlation matched the inbound Claims correlation, while producer/dependency correlations remained separate.
- `claims`, `claims_receipts`, `claims_audit`, and `claims_outbox` were all 0 before and 0 after.

The same binary also passed non-nil, persisted nil, missing, null, malformed, and mismatch scenarios. The nil scenario preserved the authoritative nil root across aggregate, receipt, audit, and Pending outbox and retained a separate outbound trace.

## Changed files

| Path | Baseline SHA-256 | Target SHA-256 |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Claims/ClaimReferenceReader.cs` | `94b2907241e4f23a1ca3eeacb0f2aba04d78c9a25ca36f2b021839d9f7da3223` | `3529fa558ea124681495366765e9671bfb9f8157e78250b3eaeef90df71e7670` |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Claims/ClaimReferenceTests.cs` | `04222ca3821f823689a1d6216d37ed95548cc3c2acc9d32867e2b1229a878efb` | `e03360a93509e1a8f9a1610c00aa719b8403136808911062aced7d876a46adf8` |

Patch SHA-256: `e929f9894a8adaf54aa2fa9455d30884cc873ac2dfc254712e8b48ec4f4eef88`.

## Artifacts

- `malformed-producer-map.patch`
- `changed-files.tsv`
- `baseline-source-manifest.tsv`
- `target-source-manifest.tsv`
- `evidence.tar.gz` — SHA-256 `77af9761aacbd5898062f581d3b26de1801d9fb2ae501f2dfa59d921c1f87aba`
- `WRITER-COMPLETE.md`

## Remaining gates

- Independent read-only VER is still required.
- CT acceptance is still required.
- R14, R22, and R25 are outside this rework and their prior dispositions are unchanged.
- No producer, Returns, `Program.cs`, persistence injection, contract, guard, gateway, commit, push, or stash change was made.

## Cleanup

The API, proxy, runtime Mongo, and Claims-test Mongo listeners were closed. The source writer has completed and no source writer remains active for this work package.
