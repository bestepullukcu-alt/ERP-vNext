# SOP §22 — MVP6-CARRIER-NUMERICDATE-EXEC-VER-01

## Verdict

**PARTIAL PASS / CARRIER E2E HELD.** F-01 cardinality and F-02 NumericDate raw-JSON typing are independently **CLOSED** for the exact approved two-file MDM delta. A fresh real Auth login/refresh and Legal Entity re-resolution chain was **NOT RUN to completion**, so no Carrier real-Auth E2E handoff is issued.

This is a bounded independent technical verdict. It is not CT acceptance, rollout approval, or full-module acceptance.

## Authority and immutable inputs

- Owner decision: `395c3746db4c88aac94964b447c112181c2508573fde04d895bb412cbb19f923`
- Patch: `180143bccc08ad3316eb89f79a3e9386da2ce881b9a4b02f00a725b0be27ef4b`
- Final 22-path manifest: `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`
- Final source archive: `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd`
- DEV evidence archive: `5f8c30998f063a5eee535dc66d32393a767474e2a6e031fa40da13e5f1b2033f`

The archive verified 22/22 final paths and 3,333/3,333 build-source entries. Independent predecessor comparison found exactly two modified paths and no additions/removals; see `PREDECESSOR-DIFF.tsv`.

## Source findings

The final validator enforces issuer, audience, signature and exact claim cardinality before repository access (`PlatformServiceTokenValidator.cs:43-84`). NumericDate handling verifies exactly one normalized claim, then independently parses `RawPayload`, requires one matching JSON property, requires JSON `Number`, and requires `Int64` (`PlatformServiceTokenValidator.cs:114-139`). This prevents a normalized string claim from masquerading as a raw JSON number while preserving duplicate detection.

## Independent build and tests

- Native SDK/runtime: 8.0.417 / 8.0.23; no major roll-forward.
- Fresh MDM Release build: PASS, zero errors, five inherited warnings.
- Initial sandbox VSTest: ABORTED due local IPC denial and retained.
- Controlling VSTest: **33/33 PASS**.
- Rebuilt MDM API binary: `461b1991d173ae1bcca556a0f4ef8ce41485108769a1d36bf2e0ae80e9b9239f`.

## Independent HTTP/Mongo runtime

The verifier ran the rebuilt MDM binary on `127.0.0.1:18859` against DB-010-style fixed database `DitenMdm_CarrierNumericDateVer01` on verifier-owned replica set `rsCarrierNumericDateVer01` at `127.0.0.1:37984`.

The independently executed raw-signed-payload matrix passed **49/49**. Every rejected case returned HTTP 401 and caused zero scoped `mdm_legal_entities` reads. This includes same-value and conflicting duplicates, reverse duplicate ordering, duplicate/incorrect audience, missing claims, malformed values, signature failure, skew/lifetime boundaries, `exp <= iat`, and string/fraction/out-of-range NumericDate forms. Valid numeric controls returned 200 with exactly one scoped read. Exact rows are in `ACCEPTANCE.tsv` and the raw JSON archive.

## F-01 / F-02 disposition

| Finding | Independent verdict | Boundary |
|---|---|---|
| F-01 cardinality | **CLOSED** | 33/33 test regression and HTTP/Mongo duplicate/missing matrix pass; rejected cases have zero reads. |
| F-02 NumericDate JSON type | **CLOSED** | Raw signed JSON strings for `iat`, `nbf`, `exp` return 401/zero reads; numeric controls and time boundaries retain expected behavior. |
| Real Auth login/refresh | **NOT RUN / HELD** | Fresh Platform build did not complete before bounded finalization; Auth build and three-service runtime were not reached. Historical login/refresh evidence is not relabeled. |

## Failed/partial attempts retained

`ATTEMPT-DISPOSITION.md` retains the sandbox test abort, the DEV 48/49 historical attempt, and the incomplete cross-service build. The Platform cancellation produced `MSB6006 / csc exit 130`; it is an interrupted attempt, not a product defect or PASS.

## No-change and cleanup

No product source was edited. The only repository writes are this verifier's owned audit artifacts. Source integrity was rechecked from the immutable source archive. Ports 18859, 37984 and 37985 had no listeners after cleanup. The operational Mongo port 27017 was never contacted. Ephemeral signing material and bearer material were not archived and were deleted.

## Successor handoff

**Carrier real-Auth E2E remains HELD.** The next bounded verification must fresh-build Auth and Platform from this same 3,333-entry source set, run them with the already closed MDM validator source, and independently reproduce login, refresh and refresh-time LE re-resolution. It must not substitute diagnostic service tokens or historical JSON evidence.
