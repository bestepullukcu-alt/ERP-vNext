# SOP §22 — MVP6-CARRIER-AUTH-TOKEN-REWORK-VER-02

## Verdict

**REWORK.** F-01 is independently **CLOSED**. F-02 remains **OPEN** because the running MDM validator accepts digit-only JSON strings for `iat`, `nbf`, and `exp`. Carrier E2E handoff remains **HELD**. This is an independent technical verdict, not CT acceptance or rollout approval.

## Exact inputs and provenance

- DEV patch: `b346415769da7542533b816084d56fe314eb6e6c00d8368a9a419774434572e1`
- DEV delta manifest: `5f7369c8b09ab28f37a8b21e958e2a14258dba60e1307d474d8110cca5f80153`
- Final 22-path manifest: `6221006b5e4bdccff9eb567b49a3e6c111ecb611b9e96d169ee3ff5fe57c1ad2`
- Build-source manifest: `1b3eaf5101cd6a9ee2919c14e71fd51b50c6c433a08ef4dcdbf4492cc09d914e`
- Final source archive: `47c39cdbeaa61a59b9b4891439bc758589911d535f7b84736e8ce3a4a88688e6`
- DEV evidence archive: `ba051ce775814a66b6e59abe9027c03ff67a48037376f64554c03d7c900a42a5`
- Branch/HEAD: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

The disposable extraction matched all 22 target hashes and all 3,333 build-source entries. Product source was not edited.

## Build and test evidence

Native `/Users/natig/.dotnet/dotnet` reported SDK 8.0.417 and runtime 8.0.23. Online/native restore stalled in the environment and produced no usable completion. The verifier preserved that attempt, copied only `obj` restore metadata from the exact-hash verified DEV source checkout, then ran `-t:Rebuild --no-restore`. This is a fresh compilation from the independent source extraction, but **not** a fresh restore.

- Auth rebuild: exit 0, zero errors.
- Platform rebuild: exit 0, zero errors.
- MDM rebuild: exit 0, zero errors.
- MDM validator filter: 28/28 PASS; TRX is controlling. The first sandbox attempt failed because MSBuild IPC socket binding was denied.
- Platform successor filter: 8/8 PASS; TRX is controlling. The first sandbox attempt failed for the same IPC restriction.

## Independent runtime result

The verifier used lane-owned Mongo replica set `127.0.0.1:37684`, MDM `127.0.0.1:18559`, and isolated databases. No operational port 27017 access occurred.

The raw HTTP probe executed 49 scenarios. It recorded response status/hash/byte counts and used Mongo profiler reads scoped to `mdm_legal_entities`:

- 46/49 scenario assertions passed.
- Every duplicate claim, missing claim, audience-cardinality, wrong-audience, malformed-time, reversed duplicate order, and invalid-signature rejection returned 401 with **zero** repository reads.
- Valid and accepted temporal boundaries returned 200 with one scoped repository read.
- Three exact failures: `numericdate-string-iat`, `numericdate-string-nbf`, and `numericdate-string-exp` expected 401/zero reads but returned 200/one read.

Source explains the result at `services/Diten.MdmService/src/Diten.MdmService.Api/Security/PlatformServiceTokenValidator.cs:113`: `TryGetExactlyOneNumericDate` extracts `claim.Value` and line 123 uses `long.TryParse`, which cannot distinguish a JSON numeric token from a digit-only JSON string.

## Login/refresh and chain disposition

The independent Auth→Platform→MDM login/refresh chain was not completed after the decisive F-02 product defect was reproduced. DEV evidence for login/refresh remains historical only and is not promoted to independent PASS. CHAIN-01 through CHAIN-04 therefore remain **NOT REPRODUCED** in this VER. No bearer token or secret is present in this package.

## Historical 27017 attempt

The DEV record states that an early, misconfigured MDM launch fell back to `localhost:27017` and may have run an idempotent startup migration. That attempt remains rejected. This verifier did not connect to, inspect, probe, or clean port 27017.

## Required rework

Require the validator to prove the raw JWT payload representation for `iat`, `nbf`, and `exp` is a JSON number before value parsing, while preserving exact-cardinality, signature, skew, equality, expiry, lifetime, and zero-read rejection behavior. After rework, rerun the three string-type negatives and the full 49-case independent matrix, then independently complete login/refresh re-resolution before issuing a Carrier E2E handoff.

## Cleanup and limits

Lane-owned ports 37684, 18559, 18557, and 18556 were released. No source, contract, Carrier UI, Program.cs, gateway, permission, pack, previous report, or git state was changed. No CT acceptance, rollout, or commit/push/stash occurred.
