# MVP6 Shipment A08/A09/A12 CT disposition — SOP §22

- Work package: `MVP6-SHIPMENT-A08-A09-A12-CT-DISPOSITION-01`
- Date: 2026-09-25
- Role: Control Tower evidence reviewer
- Runtime/test execution: **none**; existing evidence only
- Product/source mutation: none
- Verdict: **BOUNDED CLOSE for UI183-A08, UI183-A09-409 and UI183-A09-422 only**

## Controlling inputs

| Input | Binding |
|---|---|
| Evidence package | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/evidence/a08-a09-a12-01/` |
| Evidence package artifact list | `ARTIFACTS.sha256`; `ACCEPTANCE.tsv` hash `76d44272f8168574f3e8ce90d070856ca36edb9f15b4963092a10ab73c4b764f` |
| Final source archive | `490d51be87d249265a2cc9fe6d1f8f3b23bd6976f4e5830d733bff2dfff613a3` |
| Final source manifest | `7d7bec63a1f2e9e906864e5dea6dc96a00e94a280b830dd06f95a8b68246b4fc`; evidence records `360/360` matched |
| Auth overlay archive | `f50350b8e541a4c1cbc640926c581cf510b4c2dbb57b31ef4193bf4c25c9e2cd` |
| Auth overlay manifest | `b9713185aba02bf325a9149b37a8cc6ffe3b1dd0e979febdaa773946ae9b1731`; evidence records `22/22` matched |
| Base HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| Execution plan | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/EXECUTION-PLAN.md` |
| Prior gap matrix | `docs/records/audits/2026-09/mvp6-shipment-remaining-acceptance-disposition-01/ACCEPTANCE-GAP-MATRIX.tsv` |

This CT disposition reviews the already-captured package. It does not rerun tests, start browser/runtime processes, create an A10 proxy, edit source, or change pack/contract/guard/git state.

## Evidence disposition

| Row | CT disposition | Browser evidence | HTTP evidence | Zero-write / persistence evidence | Boundary |
|---|---|---|---|---|---|
| UI183-A08 | **CLOSED for exact stale Draft-to-Planned ordered race** | `raw/browser-observations.json` shows actor-a and actor-b both saw `Draft`; actor-a committed `Draft -> Planned`; actor-b submitted stale `Draft -> Planned`, saw `INVALID_SHIPMENT_TRANSITION` support reference, refreshed to `Planned`. | `raw/exact-failure-adapter-results.json` records key `d1000000-0000-4000-8000-000000000008`, payload SHA `42657b48a310b434b6dc852e67f2594de809eacfb2d08e98223d4b173eccdc9b`, HTTP `422`, code `INVALID_SHIPMENT_TRANSITION`, matching correlation header/body. | `raw/a08-exact-probe-before.json` and `raw/a08-exact-probe-after.json` both show counts `1/2/2/2/2`, state `Planned`, version `2`, POD `null`. | Proves stale browser handling after actor-a commit. It is not a general simultaneous backend scheduler/concurrency guarantee. |
| UI183-A09-409 | **CLOSED for exact duplicate stale POD modal after first POD success** | `raw/browser-observations.json` shows both sessions opened POD while `Dispatched`; actor-a captured POD and reached `Delivered`; actor-b stale modal saw `POD_ALREADY_CAPTURED`, refreshed to `Delivered`, authoritative POD visible, POD action removed. | `raw/exact-failure-adapter-results.json` records key `d2000000-0000-4000-8000-000000000009`, payload SHA `0c15c6eacbd92fc031dbd4ea521bd6780e35c77d7e1b27e80702282c4706780c`, HTTP `409`, code `POD_ALREADY_CAPTURED`, matching correlation header/body. | `raw/a09-409-after-actor-a.json`, `raw/a09-409-after-actor-b.json`, `raw/a09-409-exact-probe-before.json` and `raw/a09-409-exact-probe-after.json` all preserve the post-success counts `1/4/4/4/5`, `Delivered`, version `4` and the same POD record after the failed stale request. | Closes only duplicate stale POD rejection and no-second-write behavior for this fixture. |
| UI183-A09-422 | **CLOSED for exact stale POD eligibility after Dispatched-to-Exception transition** | `raw/browser-observations.json` shows actor-b held a POD modal while actor-a changed `Dispatched -> Exception`; actor-b stale POD got `INVALID_SHIPMENT_TRANSITION`, refreshed to `Exception`, POD action removed. | `raw/exact-failure-adapter-results.json` records key `d3000000-0000-4000-8000-000000000009`, payload SHA `fe3153462c85ad89c4931ab38a7f42414166bbb5e4c7db400d22240cbd35f651`, HTTP `422`, code `INVALID_SHIPMENT_TRANSITION`, matching correlation header/body. | `raw/a09-422-after-actor-a.json`, `raw/a09-422-after-actor-b.json`, `raw/a09-422-exact-probe-before.json` and `raw/a09-422-exact-probe-after.json` all preserve post-transition counts `1/4/4/4/4`, `Exception`, version `4`, POD `null` after the failed stale request. | Closes only stale eligibility rejection and zero-write behavior for this fixture. |
| UI183-A12 data/API isolation | **PASS retained, not full A12 closure** | `raw/browser-observations.json` shows LE-A positive list/detail and LE-B list with zero rows. It also shows no shipment values/actions leak for cross-LE/unknown/deleted, but summary/lines/POD skeleton remains. | `raw/a12-adapter-results.json` records LE-B transition and POD requests returning HTTP `404`, code `SHIPMENT_NOT_FOUND`, same root/correlation behavior. | `raw/a12-before.json` and `raw/a12-after.json` both show counts `1/1/1/1/1`, `Draft`, version `1`, POD `null`. | API/data isolation stays PASS. Presentation remains FAIL/REWORK because safe-not-found DOM retained `Loading...`, summary labels, empty lines table and POD placeholder. |

## CT verdict

`UI183-A08`, `UI183-A09-409` and `UI183-A09-422` are accepted as bounded CT closures for the exact immutable source, browser flows, status/code behavior and zero-write evidence described above.

`UI183-A12` is not closed as a whole. Its data/API isolation evidence remains PASS, and its safe-not-found presentation defect remains FAIL/REWORK.

This disposition does not close `UI183-A10`, `UI183-A13`, `UI183-A14`, durable PNG, `UI183-A16`, full Shipment UI, rollout, E5/G5 or production readiness. The historical `49 PASS / 35 FAIL` generic result remains historical evidence, not a waiver.

## Effort disposition

No new delivered hours are credited in this CT disposition. The reviewed package provides evidence closure for specific acceptance rows, but no existing effort report in the reviewed inputs provides a separated A08/A09 delivered-hour allocation. Generating new hours here would double count or invent effort.

## No-change statement

This CT review added only `docs/records/audits/2026-09/mvp6-shipment-a08-a09-a12-ct-disposition-01/`. It did not modify product source, pack files, contracts, guard authority, runtime configuration, Git staging, commits, pushes or stashes.
