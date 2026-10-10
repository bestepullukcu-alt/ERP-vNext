# MVP6-MOD0187-R14-APPLY-01 — SOP §22 DEV handoff

**Agent verdict:** PASS — exact R14 composition patch applied and exercised in
the isolated snapshot only. Writer complete.

**Branch / HEAD observed:** `feature/mvp6-logistics` /
`4a8d4d4b339528a88e6220fb8402e5a2c771136c`.

**Execution snapshot:** `/private/tmp/mvp6-mod0187-r14-apply-01-Q7vmHi/source`
(filesystem snapshot, deliberately not represented as a registered worktree).

## Authority and exact change

The real user decision is recorded without expansion in [AUTHORITY.md](AUTHORITY.md).
The copied baseline, patch and target independently match the three authorized
hashes:

| Artifact | SHA256 | Result |
|---|---|---|
| `artifacts/Program.cs.baseline` | `a72a05a5b7dc235cb68bb0bf7ad72cdc4889a49846ab6fdd4f01601285ab34a` | MATCH |
| `artifacts/Program.cs.patch` | `3423940b958c8a66c8300f71ecd5d3b9525103ab645662545fca8811905259f0` | MATCH |
| `artifacts/Program.cs.target` | `11c586e04e12c7ecc9c543907414bf77a6c8fe3b23643ae5f42666c580c7b0f1` | MATCH |

`git apply --check` and `git apply` succeeded against the exact baseline. The
260-entry product source/test manifest has one changed path only:
`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs`.
The final manifest equals the target manifest after build and tests.

The target selects `new UTF8Encoding(false, true)` only when the header name is
`Idempotency-Key` using ordinal case-insensitive comparison. The selector
returns `null` for every other header, so no global request-header decoder was
introduced. Existing 64 KiB total request-header size configuration remains.

## Baseline measurement

The baseline was measured first with raw TCP requests whose non-ASCII header
values were encoded using Python strict UTF-8. It already accepted valid UTF-8:

| Input | Unicode scalars | UTF-8 value bytes | Baseline observable |
|---|---:|---:|---|
| ASCII | 128 | 128 | `404 CLAIM_NOT_FOUND` |
| ASCII | 129 | 129 | `400 INVALID_REQUEST` |
| `é` repeated | 128 | 256 | `404 CLAIM_NOT_FOUND` |
| `é` repeated | 129 | 258 | `400 INVALID_REQUEST` |
| emoji repeated | 128 | 512 | `404 CLAIM_NOT_FOUND` |
| emoji repeated | 129 | 516 | `400 INVALID_REQUEST` |
| malformed bytes `c3 28` | N/A | 2 | Kestrel `400`, no application envelope |

Therefore there is no honest valid-UTF-8 RED for this baseline. The historical
Latin-1 probe is not reused. The first probe expectation that assumed rejection
is retained under `raw/discarded/baseline-expectation-attempt-01.json`; the
authoritative baseline output is observational `raw/baseline-results.json`.

## Target transport evidence

Fresh target compilation used the recorded restore assets and
`--no-restore --no-incremental`: 0 warnings, 0 errors. A network restore attempt
that did not progress beyond dependency determination was stopped and preserved
under `raw/discarded/restore-attempt-01/`; it is not presented as a fresh
restore. The target API binary launched by the recorded process has SHA256
`fdd4f77ce1889deba239757de9361cb5865889b65601b5d97b4ba20a85f492c5`.

The target produced the same exact scalar boundary behavior:

| Test | Expected/observed status and code | Correlation/body |
|---|---|---|
| ASCII 1 and 128 | `404 CLAIM_NOT_FOUND` | response header and body carry `44444444-4444-4444-8444-444444444444`; `contractVersion=v1` |
| ASCII 129 | `400 INVALID_REQUEST` | same correlation in header/body; `contractVersion=v1` |
| UTF-8 `é` 128 / 129 | `404 CLAIM_NOT_FOUND` / `400 INVALID_REQUEST` | same application correlation behavior |
| UTF-8 emoji 128 with lowercase header name / 129 | `404 CLAIM_NOT_FOUND` / `400 INVALID_REQUEST` | proves case-insensitive header selection and scalar counting |
| Invalid UTF-8 | Kestrel `400` | no Claims body, correlation header, or contract version because application middleware was not entered |
| Duplicate / missing key | `400 INVALID_REQUEST` | application correlation preserved |
| ASCII / valid UTF-8 value in unrelated header | `404 CLAIM_NOT_FOUND` | unchanged path; no global decoder change observed |

The positive `404` is the bounded oracle: header parsing and Claims validation
passed, and the request reached the intentionally absent Shipment reference.
Exact request/response hashes and bodies are in `raw/target-results.json`.

## Same-host regression

Tests ran against isolated MongoDB replica set `r14_apply` on port 27314;
operational 27017 was not used. One initial test invocation used incorrect test
environment variable names and is retained under
`raw/discarded/regression-env-attempt-01/`; it is excluded from the verdict.

| Scope | Fresh result | Evidence |
|---|---:|---|
| Claims + Returns + Loads + Carriers selected regression | 78/78 PASS | `raw/same-host-regression.log` |
| Shipment regression | 12/12 PASS | `raw/shipment-regression.log` |

These counts are separate regression scopes and are not combined into an
acceptance percentage. They demonstrate no observed same-host regression from
the header-name-specific selector; they do not establish gateway, rollout, E5
or G5 acceptance.

## Source → binary → process chain

The baseline API binary hash is
`fa06e2aa7b434c19a3e2e7c5cbdf522e9f7f2e7c322a1254838ed288225319a0`.
The target build completed before PID 11816 was launched on port 51875; the
pre-launch binary record is `raw/target-binary.sha256` and process identity is
`raw/target-process.txt`. A later test-project build reproduced the same API
binary bytes. Raw health, launch, build, test and process logs are included.

## Security, persistence, observability, rollback

- The probe used a locally generated bounded JWT and the real JWT/RBAC path;
  no auth bypass or new endpoint was added. Secrets and bearer tokens are not
  stored in this archive.
- No business persistence was required for the missing-reference oracle. The
  isolated Mongo process was stopped after the run; ports 51874, 51875 and
  27314 had no listeners at cleanup.
- The supplied rollback is the exact reverse of
  `artifacts/Program.cs.patch` inside the disposable snapshot.
- Canonical, guard, auth, gateway and common-checkout product sources were not
  written by this lane. The common checkout's pre-existing `Program.cs` state
  is outside this snapshot and was not used as the patch baseline.

## Decisions, gaps and limits

The exact authorized patch is implemented and the requested fresh
build/process/transport checks pass. Because the baseline already accepted
strictly encoded valid UTF-8, this evidence establishes explicit strict,
header-specific policy binding and stable boundary behavior; it does not claim
a valid-UTF-8 RED→GREEN correction. Invalid UTF-8 remains a server-level 400,
before Claims correlation/body generation.

No product blocker was found in this bounded lane. Independent VER, integrated
publication/rollout, E5/G5 and broader module acceptance remain separate gates.

**Out-of-scope changes:** none.
