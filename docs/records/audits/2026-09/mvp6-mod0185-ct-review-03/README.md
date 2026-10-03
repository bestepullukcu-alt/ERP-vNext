# MVP6-MOD0185-CT-REVIEW-03

Date: 2026-09-19  
Role: Control Tower  
Verdict: **ACCEPTED for A04-PERSIST evidence closure**

## Decision scope

This decision closes only the two findings from CT-REVIEW-02:

1. API binary provenance for the A04 runtime evidence.
2. Collection-specific negative evidence completeness.

It does not declare full MOD-0185 completion, E5/G5, live ingress, gateway uptake, or downstream GO.

## Manifest and source chain

- DEV handoff: `/private/tmp/mod0185-a04-rework02-runtime/SOP-22-DEV-HANDOFF.md`
- DEV manifest: `/private/tmp/mod0185-a04-rework02-runtime/changed-files.json`
- VER report: `/private/tmp/mod0185-a04-ver02-runtime/SOP-22-VER-REPORT.md`
- VER manifest: `/private/tmp/mod0185-a04-ver02-runtime/manifest.json`
- Branch: `feature/mvp6-logistics`
- HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

The source delta is limited to:

`services/Diten.SupplyChainService/tests/loads/failure_probe.py`

Source SHA-256 is `8f6cc448b3d54808e1fdb5f572575fab8c71e65dbfa1edaedeead6bf4ac75a63` in the repository, DEV manifest and VER disposable copy. The protected Shipment-BUNDLE, Loads annex and Carrier annex hashes match the prior CT record.

## Finding 1 — binary provenance: CLOSED

VER-02 performed a fresh restore/build in `/private/tmp/mod0185-a04-ver02-copy` with exit 0, zero warnings and zero errors. The exact API binary used by the probe is:

`/private/tmp/mod0185-a04-ver02-copy/services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/bin/Debug/net8.0/Diten.SupplyChainService.Api.dll`

Its SHA-256 is:

`c8736e3b37e33a7c79ffc164de1f3ddeef6c2632ae2dff1361219f8fd2c682`

The runtime evidence records the same path and hash and starts the process with `dotnet <exact DLL path>`. The Loads source-set hash is `246f20f85b28a0c2abf5af40992a20056fce57ec0041dfd5cc58eb370eab33c3`. A different hash from the DEV build is acceptable because the complete source-to-build-to-process-to-evidence chain is present.

## Finding 2 — collection-specific negatives: CLOSED

The probe evidence contains 20 explicit records, each with `caseId`, `collection`, `mutant` and `rejected: true`:

| Collection | Missing | Invalid | Nonzero delta | Query failure |
|---|---:|---:|---:|---:|
| `loads` | PASS | PASS | PASS | PASS |
| `load_assignments` | PASS | PASS | PASS | PASS |
| `loads_receipts` | PASS | PASS | PASS | PASS |
| `loads_audit` | PASS | PASS | PASS | PASS |
| `loads_outbox` | PASS | PASS | PASS | PASS |

Query-failure mutants exit 1. Runtime before/after query exits are all 0 and are recorded for both scenarios.

## Refusal/timeout evidence

The probe used the same isolated API Mongo database, `diten_mod0185_failure_probe`, with fresh tenant and legal-entity scopes per scenario.

- Connection-refused: HTTP 503 `DEPENDENCY_UNAVAILABLE`; correlation preserved; all five deltas zero.
- Timeout: HTTP 503 `DEPENDENCY_UNAVAILABLE`; correlation preserved; all five deltas zero.

Independent aggregate queries in `/private/tmp/mod0185-a04-ver02-runtime/independent-query.json` returned all five scoped counts as zero for both before and after states, with exit 0.

Evidence hashes:

- `failure-paths.json`: `e540570e0e7cd2067927a2e9f97e99500ec927e5193f05657561bb1fa7e9315e`
- `independent-query.json`: `809c477858ea90401ffffe07a971afaa68b7817cc9c97c42543fffd7b0874e5c`
- VER manifest: `2bb887ffefdf3bf74261564a600e9a76f838cd486b22fdf7a3548643c67e81cd`

## Inherited decisions

A12 and A07 remain inherited/content-bound PASS findings. No new drift was found, so unrelated suites and closed decisions were not reopened.

## Exact accepted work package

CT accepts closure of the A04-PERSIST evidence gap for the already approved bounded Loads slice:

- refusal/timeout dependency failure handling;
- tenant/legal-entity scoped persistence checks for the five Loads collections;
- correlation preservation;
- collection-specific fail-closed negative controls;
- fresh disposable binary provenance.

The underlying bounded MOD-0185 scope remains subject to its separate acceptance gates. No contract, runtime, pack, gateway, migration, E5/G5 or downstream decision is changed by this record.

## No-change and CT-owned inventory

Only this new CT record was written:

`docs/records/audits/2026-09/mvp6-mod0185-ct-review-03/README.md`

No source, contract, pack, historical audit, commit, push or stash operation occurred.

**Central decision: ACCEPTED — CT-REVIEW-02 binary provenance and collection-specific negative evidence findings are closed.**
