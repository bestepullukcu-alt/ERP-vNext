# MVP6-MOD0185-CT-REVIEW-02

Date: 2026-09-19  
Role: Control Tower  
Verdict: **REWORK**

## Decision

The A04-PERSIST rework is directionally correct, but the closure evidence is not yet admissible for CT acceptance. The bounded MOD-0185 implementation remains **REWORK** solely for A04-PERSIST evidence provenance and negative-control completeness. A12 and A07 remain inherited PASS findings and are not reopened.

## Reviewed chain

- Prior CT record: `docs/records/audits/2026-09/mvp6-mod0185-ct-review-01/README.md`
- DEV handoff: `/private/tmp/mod0185-a04-rework-01/SOP-22-DEV-HANDOFF.md`
- DEV manifest: `/private/tmp/mod0185-a04-rework-01/changed-files.json`
- DEV evidence: `/private/tmp/mod0185-a04-rework-01/failure-paths.json`
- Independent VER evidence: `/private/tmp/mod0185-a04-ver01-runtime/failure-paths.json`
- Independent query log: `/private/tmp/mod0185-a04-ver01-runtime/query-exit.json`
- VER disposable copy: `/private/tmp/mod0185-a04-ver01-copy`

The DEV manifest correctly declares only `services/Diten.SupplyChainService/tests/loads/failure_probe.py` as the source change, with SHA-256 `678c6d8439829a02589fbc90eb16839a067e0969632d8be911391e5efa420176`. Frozen Shipment, Loads annex and Carrier annex hashes match the prior CT record.

## Evidence reproduced

The independent probe used fresh tenant, legal-entity, correlation and idempotency values. Both connection-refused and timeout scenarios returned HTTP 503 `DEPENDENCY_UNAVAILABLE`; body/header correlation matched the request correlation. `loads`, `load_assignments`, `loads_receipts`, `loads_audit` and `loads_outbox` each had zero before/after/delta for both scenarios.

The independent query log records four aggregate queries with exit code 0 and zero results:

- `/private/tmp/mod0185-a04-ver01-runtime/query-exit.json`
- SHA-256: `44b69c7ca4c259c61d4c86e8b587babd406b51a1f3a2a7594edec7a61f864841`

The DEV/VER evidence also records the three fail-closed categories: missing measurement, nonzero delta and query failure.

## Blocking findings

### 1. API binary provenance is not bound to the prior independent build

The prior independent VER-02 runtime evidence binds its API binary to SHA-256:

`e10b6de0a7a1917b98703eba8c40c19309e3a26e82d1cfe430c8685f4a888737`

The A04 VER disposable copy actually used a different API binary:

`99f249fbf0f72c91c7e7160de29627faadb602bcb1c8b07d4dda1c04bea9a5a9`

Therefore the A04 runtime result cannot be attributed to the previously independently built binary. The A04 VER did not perform a fresh build and must not claim one. Rework must either rerun against the prior binary with an exact hash binding or produce a fresh build and record its hash and source-set binding.

### 2. Negative controls do not exercise each collection independently

The implementation checks all five collection keys and all five deltas in normal execution, but the recorded negative controls mutate only representative cases:

- missing measurement removes `loads` only;
- nonzero delta changes `loads` only;
- query failure uses one failed aggregate query.

The evidence therefore proves fail-closed handling for the validator categories, but does not independently demonstrate a negative mutant for each of the five collections. A04 closure requires collection-specific mutants (or an equivalent parameterized record) for `loads`, `load_assignments`, `loads_receipts`, `loads_audit` and `loads_outbox`, with each rejection recorded.

## Inherited findings

- **A12:** inherited/content-bound PASS from the prior independent VER-02 runtime capture. No new drift observed.
- **A07:** inherited/content-bound PASS from the prior independent VER-02 startup/failpoint evidence. No new drift observed.
- No contract, pack, runtime source, gateway, shared path or historical record was changed by this review.

## Exact rework required

1. Bind the A04 runtime evidence to the prior independent API binary SHA, or perform and record a fresh disposable build with source-set and binary hashes.
2. Add parameterized negative evidence covering each of the five collection measurements for missing, invalid/nonzero and query-failure conditions as applicable.
3. Rerun only the A04-PERSIST verification. Do not reopen A12/A07 or rerun unrelated suites unless their protected inputs change.

## Scope and boundaries

The accepted bounded implementation scope remains the approved Loads slice and its five-layer service, tenant/legal-entity isolation, RBAC, idempotency, lifecycle, audit/receipt/outbox consistency, GET-only reference boundary and Pending-only outbox. This record grants no full-module completion, E5/G5, live ingress, gateway uptake or downstream GO.

## CT-owned change inventory

Only this new file was written:

`docs/records/audits/2026-09/mvp6-mod0185-ct-review-02/README.md`

No commit, push or stash occurred. The prior CT record and all source, contract, pack and historical evidence files remain unchanged.

**Central decision: REWORK — A04-PERSIST closure is not yet admissible because binary provenance and per-collection negative evidence are incomplete.**
