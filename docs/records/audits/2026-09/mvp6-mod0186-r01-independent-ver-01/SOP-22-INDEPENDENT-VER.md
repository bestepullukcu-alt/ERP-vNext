# MVP6-MOD0186-R01-INDEPENDENT-VER-01 — SOP §22 report

**Verification verdict:** PASS for the bounded R01 product and evidence-packaging rework. This is an independent VER verdict only; it is not Control Tower acceptance, E5, deployment approval, full-module acceptance, or capability completion.

**Mode and isolation:** VER lane, disposable source root `/private/tmp/mvp6-mod0186-r01-independent-ver-01/source`; API `127.0.0.1:51863`; MongoDB `127.0.0.1:27286`, replica set `returns_r01_ind_ver`, database `returns_r01_ind_ver_db`. The verifier did not author the R01 rework.

## Input and writer-complete gate

- `product.patch` SHA256 is exactly `0bb36d02d3972f65d6644a5b12eaabc7a8820a1a50da13b17769ef7a5148e82c`.
- DEV evidence archive SHA256 is exactly `12c254757a64e4e5e159ec66d151f86e5a86e1d8d0d6f87b1f846fde0b869b2e`.
- `ARTIFACTS.sha256`, `EVIDENCE-MANIFEST.sha256`, and all published-input bindings verify.
- `input-writer-complete.json` declares writer-complete and names the same patch SHA.
- The independently reconstructed target matches `source-final-341.tsv` at 341/341. Baseline-to-target comparison changes exactly two paths and leaves 339 snapshot paths byte-identical: `ReturnReferenceReader.cs` and `ReturnReferenceTests.cs` are the only changed Returns-owned files.

## RED → GREEN and exact behavior

The RED arrangement kept the target regression test byte-for-byte but restored the product reader to its declared baseline SHA. The direct producer condition is a Shipment response `500 SHIPMENT_ROOT_INVALID`; the baseline reader classified that producer response through its generic dependency branch, so the Returns consumer emitted `503 DEPENDENCY_UNAVAILABLE`. The exact test failed with expected `502`, actual `503`.

With the exact target reader restored, the bounded R01 set passed 10/10:

- producer `500 SHIPMENT_ROOT_INVALID` → Returns `502 RETURN_SHIPMENT_ROOT_INVALID`;
- unrelated `500`, malformed `500` JSON, wrong contract version, `501`, `401`, and `403` → `503 DEPENDENCY_UNAVAILABLE`;
- connection refusal and timeout → `503 DEPENDENCY_UNAVAILABLE`;
- malformed root inside a successful Shipment payload → `502 RETURN_SHIPMENT_ROOT_INVALID`.

The live composed probe independently observed:

- Shipment detail with malformed persisted root: `500 SHIPMENT_ROOT_INVALID`;
- Returns create against that Shipment: `502 RETURN_SHIPMENT_ROOT_INVALID`;
- missing successful-payload root: `503 RETURN_SHIPMENT_ROOT_UNAVAILABLE`;
- explicit-null successful-payload root: `503 RETURN_SHIPMENT_ROOT_UNAVAILABLE`.

This preserves the published annex distinctions. The historical `mvp6-mod0186-http-01` wording remains present and byte-unmodified by this verifier; its hashes and exact text are retained in `raw/historical-null-record.txt` rather than rewritten.

## Build, tests, runtime, and persistence

- Fresh Release build: PASS, 0 warnings, 0 errors.
- Exact Returns namespace regression: 78/78 PASS.
- Fresh main HTTP/DB phase: 52/52 PASS.
- New-process restart phase: 4/4 PASS.
- API binary SHA256: `daeefa6c9b4b2b7159cabcf397852b23c82c962a3b0e18262b4e8032004549b7`.
- Main PID `95316` and restart PID `96056` both started after the binary timestamp and listened on `51863`.
- Final bounded DB state: 11 returns, 9 entitlements, 19 receipts, 19 audit rows, 19 Pending outbox rows, 0 non-Pending rows, and no Inventory/Warehouse/stock collection.
- Runtime log scan found no Inventory/Warehouse/stock outbound request.

The 10 targeted cases are a subset of the 78-test Returns namespace run and must not be added to 78. The 52 HTTP/DB assertions and four restart assertions are separately executed runtime phases; several validate the same requirements at a different layer, so assertion counts are evidence inventory rather than unique acceptance-criterion counts.

Two discarded harness attempts are retained and excluded from PASS counts: sandboxed VSTest could not open its local communication socket, and the first 78-test command omitted `RETURNS_MONGO_URI` and reached the default unused port `27886`. The successful run used the declared lane replica set on `27286`.

## Security, scope, failure, and recovery evidence

The 52-assertion phase includes JWT 401, server-side RBAC 403, tenant and legal-entity isolation, real Shipment endpoint uptake, root-before-payload precedence, replay, changed-payload conflict, transition permissions and lifecycle, quantity/UoM checks, concurrent 6+6 and 4+6, source drift, entitlement release, soft delete, all five transaction write-stage rollbacks, unknown-commit recovery/no-receipt behavior, and response-loss recovery. Restart verifies durable replay/list, Pending-only outbox, and zero stock collections.

HTTP archives redact Authorization values. The executed probe reads its synthetic signing secret only from a runtime environment variable; the stored probe and redacted launch record contain no secret. The archive-candidate secret/Bearer existence scan exited `1` (no match).

## Boundaries and cleanup

No source fix, Claims snapshot edit, `Program.cs` edit, canonical/guard/pack change, staging, commit, push, stash, or branch switch was performed. The repository branch and HEAD remain `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Tracked and staged diffs match the verifier baseline. A separate concurrent lane added untracked `mvp6-mod0187-e4-policy-01/` during this run; it is outside this lane and is recorded explicitly rather than attributed to the verifier.

API `51863` and Mongo `27286` both returned listener-check exit `1` after shutdown. The only persistent verifier output is this assigned audit directory.

## Known limits

This report verifies the bounded R01 correction and its declared regression/runtime evidence. It does not infer full Returns coverage merely from test counts and does not claim CT acceptance or E5/G5.
