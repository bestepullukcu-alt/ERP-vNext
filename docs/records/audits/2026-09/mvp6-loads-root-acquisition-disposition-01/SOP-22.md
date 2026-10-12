# MVP6-LOADS-ROOT-ACQUISITION-DISPOSITION-01 — SOP §22

**Date:** 2026-09-24  
**Task class:** contract/consumer design disposition; repository documentation only  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **DECISION READY — runtime and publication remain HELD**

## 1. Exact disposition

Use one versioned Loads contract amendment to add `lifecycleCorrelationId` to each existing `LoadSummary` returned by `queryLoads`. The value is the persisted `LoadPlan.CorrelationRoot`, which is the accepted correlation of the original Load create command. It is not a Shipment root, the current GET trace, a user-entered value, a browser cache value, or a value derived from IDs.

The field is proposed as optional and nullable UUID for wire compatibility. An upgraded producer must emit a non-null UUID when the persisted Load root is present. A consumer must treat missing, null, malformed, or non-authoritative values as **transition unavailable** and must not submit a transition. No backfill or legacy-record mutation is part of this decision.

This is an amendment proposal. It does not change the current frozen contract, producer, UI, pack, Gateway, or Git state.

## 2. Why the current contract is insufficient

- `queryLoads` is the only published read surface for Loads. It returns `LoadSummary`; there is no by-ID Loads read operation.
- `LoadSummary` contains `loadId`, `loadNumber`, `carrierId`, `shipmentIds`, and `status`, but no root.
- `LoadResponse` contains mutation result fields but no root.
- The persisted aggregate already owns a non-null `CorrelationRoot`; transition compares the inbound correlation to that stored value before lifecycle evaluation.
- The Loads annex defines the Load event/root as the first Load create command correlation. It does not define the Load root as any selected Shipment root.
- Shipment detail can expose each Shipment's own `lifecycleCorrelationId`, but no published rule maps one or several Shipment roots to the Load root. The combined release records keep multi-Shipment root policy open. Therefore Shipment detail fan-out cannot supply the transition root.

The only current-contract case in which a client knows the Load root is the same create intent that generated it. Retaining that value in session or browser storage does not establish a durable producer read and does not cover reload, another actor, another device, or an existing row. It is rejected as the mandatory UI solution.

## 3. Existing seam and ownership

| Concern | Current authority/reality | Disposition |
|---|---|---|
| Load identity | `queryLoads` returns scoped `loadId` and `shipmentIds` | Reuse the existing list; no new lookup or detail operation |
| Load root | Persisted `LoadPlan.CorrelationRoot`; omitted by list and mutation response | Project only this persisted value into `LoadSummary.lifecycleCorrelationId` |
| Shipment root | `ShipmentDetail.lifecycleCorrelationId` is optional/nullable and producer emission has a separate open rework gate | Do not derive, merge, select, or compare it for Load transition acquisition |
| Tenant/LE | JWT claims are authoritative; headers only validate them; cross-scope is non-disclosing | Existing `queryLoads` tenant+LE filtering remains the source boundary |
| Permissions | Read=`supplychain.loads.read`; transition=`supplychain.loads.transition` | Root can be returned only through the authorized list. UI renders action only with transition permission; direct POST remains independently authorized |
| Lifecycle/replay | Same-root transition and receipt-first replay are already normative | Amendment adds no new transition or replay policy |
| Contract owner | SHIPMENT-BUNDLE / Loads contract owner | Owns candidate, compatibility, consumer consent, publication |
| Producer owner | MOD-0185 / SupplyChain Loads | Owns projection from persisted root and fail-closed legacy handling |
| Consumer owner | Loads UI | Owns hidden root transport to the existing transition proxy; no editable root control |
| Shared integration | Single integration owner | Owns only already-approved route/registration changes after exact amendment uptake |

## 4. Multi-Shipment/root boundary

The recommended acquisition seam does **not** resolve or narrow the separate multi-Shipment root policy. A Load containing several Shipment IDs still has one persisted Load root established by its own create command. This decision does not require Shipment roots to match, does not select a first Shipment root, does not forbid consolidation, and does not invent multi-root events.

If a later golden-flow rule requires relationship validation between the Load root and one or more Shipment roots, that is a separate owner decision and acceptance surface. It must not be inferred from this UI acquisition amendment.

## 5. Acceptance examples

The exact executable matrix is in `ACCEPTANCE.tsv`. Core outcomes:

1. A same-scope reader receives a row with the persisted root; a transition-authorized actor can submit the existing transition with that UUID as `X-Correlation-Id`.
2. Missing/null/malformed root disables the action and emits no transition request. The UI does not ask the user for a value.
3. A stale row whose Load was deleted or moved out of visible scope receives existing `404 LOAD_NOT_FOUND`; no existence leak occurs.
4. A stale or tampered different root receives existing `409 CORRELATION_ROOT_MISMATCH`; the stored root is not disclosed or replaced.
5. A stale lifecycle receives existing `422 INVALID_LOAD_TRANSITION` after root validation.
6. Same key/payload/root replay returns the original success with `idempotentReplay:true` and no new audit/event. Changed payload and changed root retain existing precedence.
7. Query or persistence failure leaves transition unavailable; failure is not converted to an empty list or guessed root.

## 6. Detail and searchable lookup decisions

- **Independent Loads detail page:** remains an open UI scope decision. It is not required to acquire the transition root under this recommendation.
- **Searchable Carrier/Shipment/location lookup:** remains a separate usability/integration decision. It does not become a root authority.
- **Shipment detail:** remains the authoritative read for Shipment state/root where a separate business rule needs it. It is not the Load root seam.

## 7. Dependency order

1. Owner approves the exact decision text in `OWNER-DECISION-PACK.md`.
2. Contract owner prepares one versioned candidate changing only `LoadSummary`, its list example, and the Loads annex authority text; compatibility and actual consumer inventory are measured.
3. Independent contract VER checks apply, schema/ref/example parity, unchanged non-Loads operations, and missing/null/malformed/UUID fixtures.
4. Separate owner consent/publication applies the exact candidate. No approval is transferred to changed bytes.
5. Loads producer implements persisted-root projection and independently verifies tenant/LE, permission, legacy absence, restart, and no-write list behavior.
6. UI/integration owner consumes the published field in the existing list/transition flow and runs real-Auth/browser/restart acceptance.
7. Separate multi-Shipment live-producer and golden-flow gates remain visible.

## 8. O/M/P impact without double counting

| Work | Existing O/M/P | Treatment after this disposition |
|---|---:|---|
| Transition UI | `16/28/48` | Already counted as net new; do not add again |
| Transition shared integration | `4/8/16` | Already absorbed by `0185-5-LIVE-REMAINING 12/20/32` |
| Independent transition verification | `8/16/28` | Already absorbed by `0185-6-LIVE-REMAINING 8/16/28` in the controlling estimate |
| Root disposition/candidate/repin | `4/8/16` | O/M covered by `0185-2-REMAINING 4.8/8/12.8`; only pessimistic `3.2h` remains unallocated uncertainty |
| Producer projection and legacy-presence handling | `UNESTIMATED` | Apply `0185-3` and `0185-5` reserves first after an exact patch exists; no new numeric hours are asserted here |
| Detail page / searchable lookup | `UNESTIMATED` | Remain outside the numeric denominator until separately selected |
| Shipment root-emission rework | `UNESTIMATED` | Separate Shipment work; not charged to this Loads seam |

The decision removes shape uncertainty but does not make implementation effort measurable enough to replace the existing `UNESTIMATED` producer row.

## 9. ASSUMPTION register

- **ASSUMPTION-185-ROOT-01:** Adding an optional response property is the lowest-impact wire shape; compatibility still requires measured consumer review and exact owner consent.
- **ASSUMPTION-185-ROOT-02:** The persisted Load root remains non-secret business correlation data. Authorization is still mandatory; possession never grants transition permission.
- **ASSUMPTION-185-ROOT-03:** Legacy/malformed persistence may exist until an operational inventory proves otherwise. Missing data is not coerced to nil UUID and is not backfilled by reads.
- **ASSUMPTION-185-ROOT-04:** The historical `NOT PUBLISHED` sentence in the Loads annex is superseded for publication status by the exact publication record; its business clauses remain the current normative Loads behavior.

## 10. Remaining uncertainties and gates

- Exact amendment bytes, target version, target hashes, consumer consent, and publication authority do not yet exist.
- Producer implementation must distinguish a genuinely stored nil UUID from an absent/malformed stored field; current typed projection alone is not sufficient evidence of presence.
- Operational Loads data inventory is unknown; this report authorizes no migration or backfill.
- Shipment create root emission has a GREEN candidate but application remains held. That does not block exposing the independent Load root, but it still blocks claims of complete live multi-module root uptake.
- Multi-Shipment root relationship, detail UX, searchable lookup, Gateway/shared integration, rollout, E5/G5, and full-module acceptance remain separate.

## 11. Scope and no-change statement

This work continued the two existing Loads planning packages and added only this decision record. It did not create a competing UI/preparation package. No product source, module pack, canonical contract, Gateway, guard, or Git state was changed. Existing dirty work was preserved and not attributed to this task.

`INPUTS.tsv` pins the inspected planning, contract, pack, publication, and producer-acceptance bytes. `SHA256SUMS` pins every output in this package.
