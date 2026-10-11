# MVP6 Shipment A08/A09/A12 evidence — SOP §22

- Work package: `MVP6-SHIPMENT-REMAINING-EVIDENCE-A08-A09-A12-01`
- Date: 2026-09-25
- Role: evidence-only verifier
- Overall verdict: **REWORK**
- Source mutation: none
- A10 proxy: neither created nor used

## Authority and scope

The existing handoff authorizes `PLAN-A08`, both `PLAN-A09` variants and `PLAN-A12` on an isolated DB/port set (`INDEPENDENT-VER-HANDOFF.md:8-24`). The controlling steps and expected results are `EXECUTION-PLAN.md:15-54`. This run did not execute A10.

## Immutable source and runtime binding

| Input | SHA256 / result |
|---|---|
| 360-source archive | `490d51be87d249265a2cc9fe6d1f8f3b23bd6976f4e5830d733bff2dfff613a3` |
| 360-source manifest | `7d7bec63a1f2e9e906864e5dea6dc96a00e94a280b830dd06f95a8b68246b4fc`; **360/360** matched |
| Auth successor archive | `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` |
| Auth successor manifest | `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`; **22/22** matched |
| Base HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| SDK/runtime | .NET SDK `8.0.417`; ASP.NET Core and .NET runtime `8.0.23` |
| Mongo | isolated replica set on `127.0.0.1:42994`; operational `27017` untouched |
| Runtime ports | Gateway `5900`, Web `5901`, Auth `5956`, Platform `5957`, MDM `5959`, SupplyChain `5961` |
| Disposable Gateway route config | `776cfe9e2a1342b9dff78a35451ca33ea2392c8fa7c599bb1d9752a0a33d5ad3` |

The six Release binaries and their hashes are in `raw/binary-sha256.txt`. Auth and Platform restore used `--ignore-failed-sources -p:NuGetAudit=false`; the other projects used package assets copied from the same source package because package references were unchanged, followed by fresh native .NET 8 compilation. This is fresh compilation evidence, not a claim of a fully online restore for every project.

Three real-Auth browser profiles were used without cookie swapping: actor-a (`T1/LE-A`), actor-b (`T1/LE-A`) and actor-le-b (`T1/LE-B`). Tokens and cookie values stayed in process/browser memory and are absent from this archive.

## Acceptance result

| Row | Result | Reproduced behavior | Persistence result |
|---|---|---|---|
| UI183-A08 | **PASS** | Two independent browser sessions rendered the same Draft shipment. Actor-a committed Draft→Planned. Actor-b submitted its already-open stale transition, received HTTP `422 INVALID_SHIPMENT_TRANSITION`, saw the support root and refreshed to Planned. | Final counts are shipment/history/receipt/audit/outbox `1/2/2/2/2`. Supplemental exact-key adapter probe returned the same 422/code/root; before/after counts and state were equal. |
| UI183-A09, duplicate POD | **PASS** | Both browser sessions opened POD before mutation. Actor-a captured POD and reached Delivered. Actor-b's stale modal returned HTTP `409 POD_ALREADY_CAPTURED`, refreshed to the authoritative POD and lost the POD action. | Pre `1/3/3/3/3`; after actor-a `1/4/4/4/5`; after actor-b unchanged. Supplemental exact-key probe was also zero-write. |
| UI183-A09, stale eligibility | **PASS** | Actor-b held an open POD modal while actor-a committed Dispatched→Exception. Actor-b then received HTTP `422 INVALID_SHIPMENT_TRANSITION`, refreshed to Exception and lost POD eligibility. | Pre `1/3/3/3/3`; after actor-a `1/4/4/4/4`; after actor-b unchanged. Supplemental exact-key probe was also zero-write. |
| UI183-A12, data/API isolation | **PASS** | LE-A list/detail showed the fixture and authoritative root. LE-B list returned zero data rows. Cross-LE, unknown and soft-deleted detail requests returned indistinguishable localized safe-not-found messages apart from correlation. LE-B transition and POD adapter requests returned `404 SHIPMENT_NOT_FOUND`. | A12 owner-scope counts stayed `1/1/1/1/1`, state stayed Draft/version 1 and POD stayed null. |
| UI183-A12, safe-not-found presentation | **FAIL / REWORK** | No shipment values, lines or actions leaked, but the page retained `Loading…`, Shipment Summary labels, the empty lines table and POD placeholder after the 404. `EXECUTION-PLAN.md:51` requires only the permitted support reference and no shipment/line surface. | No write; this is a UI presentation defect. |

The first A09-409 POD submission used a timestamp earlier than the fixture's DispatchedAt and correctly returned 422 with zero writes. It is preserved as a discarded setup attempt and is not counted as the planned A09-409 result.

## Exact error probes

The browser run proves two-session rendering, stale-modal behavior and refresh. Because failed browser-generated UUID keys do not persist, a separate real-Auth same-origin adapter probe captured exact key/payload/code/root while holding the already-reached state constant:

| Case | Key | Payload SHA256 | Result |
|---|---|---|---|
| A08 | `d1000000-0000-4000-8000-000000000008` | `42657b48a310b434b6dc852e67f2594de809eacfb2d08e98223d4b173eccdc9b` | `422 INVALID_SHIPMENT_TRANSITION`; zero-write |
| A09 duplicate | `d2000000-0000-4000-8000-000000000009` | `0c15c6eacbd92fc031dbd4ea521bd6780e35c77d7e1b27e80702282c4706780c` | `409 POD_ALREADY_CAPTURED`; zero-write |
| A09 eligibility | `d3000000-0000-4000-8000-000000000009` | `fe3153462c85ad89c4931ab38a7f42414166bbb5e4c7db400d22240cbd35f651` | `422 INVALID_SHIPMENT_TRANSITION`; zero-write |

These supplemental requests corroborate exact wire behavior; they do not replace the two-browser observations.

## Evidence boundary and disposition

- A08 and both A09 branches close their previously named browser/race gaps for this exact immutable source.
- A12 closes tenant/LE list, detail, mutation and zero-write isolation. It remains **REWORK** only for the safe-not-found DOM surface described above.
- This report does not close A10, PNG, other A01-A16 rows, full Shipment UI, E5/G5, rollout or production readiness.
- No source, pack, contract, guard or Git state was changed. The repository already contained unrelated dirty/untracked work; this lane added only this evidence directory.

## Raw evidence index

- `raw/browser-observations.json`: browser states and localized messages.
- `raw/http-status-lines.log`: process-level HTTP status timeline.
- `raw/exact-failure-adapter-results.json`: exact status/code/key/payload/root results.
- `raw/a08-*.json`, `raw/a09-*.json`, `raw/a12-*.json`: DB before/after and fixture records.
- `raw/mutation-records-full.jsonl`: isolated Mongo write-set inventory.
- `raw/source-manifest-verification.txt`, `raw/runtime-and-input-hashes.txt`, `raw/binary-sha256.txt`: source→binary binding.
- `raw/process-list-before-cleanup.txt`: isolated listener inventory.

## Cleanup

All lane-owned browser tabs, six .NET processes and the isolated Mongo process were stopped after evidence capture. The disposable secret file, password hash and fixture DB directory were removed with the lane workspace. Cleanup verification is recorded in `CLEANUP.txt`.
