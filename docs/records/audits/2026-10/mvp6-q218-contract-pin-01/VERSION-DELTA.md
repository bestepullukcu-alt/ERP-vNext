# Q218 — What changed between SHIPMENT-BUNDLE 3.0.0 and 3.1.0

**3.0.0 bytes are recoverable.** They were diffed. Nothing below is inferred from a description.

| | 3.0.0 | 3.1.0 |
|---|---|---|
| File | `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml` | `docs/analysis/contracts/shipment-bundle.openapi.yaml` |
| sha256 | `5dfe7c1b…9d21c` | `6dc1dd48…96aa2` |
| Lines | 4010 | 4012 |

Tool: plain `diff -u` (not `git diff`). Full output: `delta-3.0.0-to-3.1.0.diff` in this folder (7 hunks; 7 lines removed, 9 added).

## The seven hunks

| # | 3.1.0 line | Inside | Change | Kind |
|---|---|---|---|---|
| 1 | 13 | `info` | `version: 3.0.0` → `3.1.0` | metadata |
| 2 | 456 | `/loads` GET (`queryLoads`) 200 example | adds `lifecycleCorrelationId: 18500000-…-0000000000aa` to the example item | example |
| 3 | 610-611 | `/loads` GET | `x-loads-semantics` and description: `loads-semantics-v2.0.0.md` → `v3.1.0.md` | annex pointer |
| 4 | 990-991 | `/loads` POST (`createLoadPlan`) | same pointer change | annex pointer |
| 5 | 1001 | `/loads/{loadId}/transition` | description: same pointer change | annex pointer |
| 6 | 1363 | `/loads/{loadId}/transition` | `x-loads-semantics`: same pointer change. (The next line, `/returns:`, is diff context only and is unchanged.) | annex pointer |
| 7 | 3197 | schema `LoadSummary` | adds `lifecycleCorrelationId: { type: [string, 'null'], format: uuid }`, not in a `required` list | **the one wire change** |

## Parsed comparison (both files loaded with PyYAML 6.0.3)

For each of the 17 operations the operation object was compared, and then every schema, parameter, response and header
it reaches through `$ref`, transitively.

| Result | Operations |
|---|---|
| Operation differs **and** a reachable schema differs | `queryLoads` (reaches `LoadSummary`) |
| Operation text differs (annex pointer), reachable schemas equal | `createLoadPlan`, `transitionLoad` |
| Operation equal and everything it reaches equal | the other 14: `queryShipments`, `createShipment`, **`getShipment`**, `captureProofOfDelivery`, `transitionShipment`, **`queryCarriers`**, `createCarrier`, `changeCarrierStatus`, `queryReturns`, `createReturn`, `transitionReturn`, `queryClaims`, `createClaim`, `transitionClaim` |

- The set of 12 paths is the same in both files.
- `components.schemas`: one differing schema, `LoadSummary`. `components.parameters`, `.responses`, `.headers`, `.securitySchemes`: equal.
- Wire `contractVersion` stays `v1` in both.

## What the change is, in one sentence

3.1.0 adds one optional, nullable response field to the Loads list (`LoadSummary.lifecycleCorrelationId`) and points
the three Loads operations at a new Loads annex. Nothing under `/shipments`, `/carriers`, `/returns` or `/claims` changed,
and the Returns, Claims, Carrier and root annexes are byte-equal to their pins (`HASHES.md`).

## Origin of 3.1.0 (records read, not re-audited)

| Step | Record | What it says |
|---|---|---|
| Owner decision A, 2026-09-25 | `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-a-01.md` | selects 3.1.0 / wire v1; consents to YAML `6dc1dd48…96aa2`; external consumers: none |
| Owner decision B, 2026-09-25 | `…/mvp6-loads-root-amendment-owner-decision-b-01.md` | authorizes one patch, preimage `5dfe7c1b…` → target `6dc1dd48…`; "does not authorize … pack promotion" |
| Publication, 2026-09-26 | `docs/records/audits/2026-09/mvp6-ct-loads-publication-intake-2026-09-26.md` | lane AL-MVP6-LOADS-PUB01 wrote the YAML and the new annex |
| CT ledger | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv:26`, `:33` | Q25 "DONE (CT ACCEPTED after Q32)"; Q32 independent VER "DONE (PASS 6/6)" |
| Guard | `docs/reference/architecture/docs-path-authority.json:11` | binds the canonical path to `6dc1dd48…96aa2` |
| Release-prep consumer matrix | `docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/CONSUMER-MATRIX.tsv` (row "Returns and Claims runtimes") | "No direct wire impact established … No consent inferred or requested from unrelated consumers" |

So the file on disk is not an unexplained edit. It is the owner-authorized, CT-accepted publication of 2026-09-26.
What was never done is the update of the two packs that still say "at acceptance and today" for `5dfe7c1b…`.

## Not done

- The patch file `publication.patch` (`dc0ad05b…`, hash confirmed in `HASHES.md`) was not re-applied or compared hunk by hunk with this diff.
- The two Loads annexes were not compared with each other. Loads is outside this work package's parity scope.
