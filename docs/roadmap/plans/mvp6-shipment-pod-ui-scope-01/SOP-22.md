# SOP §22 — MVP6-SHIPMENT-POD-UI-SCOPE-PREP-01

## Structured report

| Field | Result |
|---|---|
| Agent Verdict | **PREPARED / READY FOR OWNER REVIEW; UI DEV HELD; UI VER HELD** |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| Worktree status | Pre-existing dirty tree: 2542 porcelain entries; inventory SHA256 `f38b029fa2ed8f58ce00452994f2272e257d3ca75e2f594fb223581c17f89225` |
| Changed files | Only new files under `docs/roadmap/plans/mvp6-shipment-pod-ui-scope-01/` |
| Golden/Contract flow | Tenant shell; GoldenReferenceCompact; five published Shipment operations; 13 create inputs |
| Sub-flows | List, separate create, detail, lifecycle transition, POD reference capture |
| Failure paths | 401/403, safe 404, validation 400, changed-payload/duplicate POD 409, lifecycle 422, dependency 503, response loss/replay |
| Tests | No historical test rerun. DCP-002 fresh PASS; E1 contract/source/path/hash inspection only |
| Persistence evidence | Existing bounded backend/root CT evidence referenced; no new persistence claim |
| Security/RBAC/Tenant evidence | Design maps exact six backend literals; `reconcile` has no UI action; runtime proof remains DEV/VER work |
| Audit/Evidence | `INPUTS.tsv`, exact source hashes, contract line/schema references and output `SHA256SUMS` |
| Observability | UI must preserve error correlation trace and distinguish it from `lifecycleCorrelationId`; implementation evidence pending |
| Migration/Rollback | No migration. Proposed pack patch is unapplied. Future UI/shared changes require isolated target and per-file rollback hashes |
| Decisions | Compact; list/create/detail/transition/POD only; same-origin MVC→Gateway 5000; evidence references are strings, not upload |
| Blockers | Exact owner approval, durable source baseline/transfer, shared integration patch, pack target application and Phase 1.5 closure |
| Known gaps | Active-process inventory unavailable; no Shipment gateway route or manifest registration in inspected baseline; browser/Auth evidence absent by design |
| Out-of-scope changes | Product, backend, Auth, Carrier, gateway, shared shell, permission catalog, canonical/guard and Git: none |

## Preflight and controlling baseline

`python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0183 --name "Shipment Tracking & POD"`
returned `OK MOD-0183: proven against Blueprint/registry.` The canonical bundle is OpenAPI 3.1
`info.version: 3.0.0` with wire `x-contract-version: v1`, SHA256
`5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`.

No existing directory or record matching a MOD-0183 Shipment/POD UI preparation was found, and `git worktree list`
contains no Shipment UI worktree. The process inventory command was denied by the environment, so the finding is bounded:
no competing package/worktree/file evidence was found; CT must repeat WIP ownership immediately before a writer starts.

The selected 422-source integration snapshot and the current dirty checkout contain byte-identical Shipment controller,
models and permission literals:

- controller `149150b2f2e76eb7443415b0600e728600c8c9d89fcaba0bdafc461beb9e5793`;
- models `21b358b5f0b4af2f54e36a8cb2c219c68a433ea2faed9451ccc03a921a23a6c4`;
- permissions `46f9cebff09e584ab29d7d351c2d816d6ebff616287a569fbe9109d887e05525`.

This proves the inspected API surface, not transfer authority or integrated UI acceptance. The common checkout remains dirty
and the 422 target is an isolated selected target under its own bounded CT record.

The proposed pack preimage is `32381634163a2acff440475f4582c05defc8261957386dfa43d5c8fc7eaf962e`;
patch SHA256 is `20eacc55cb5bd7e76fc4db61e26d73b0d8c5f8c9849e2a4f5c3f93f8844f630c`; disposable
application produced target `464d10b47642df62b615548fa00696fbe712bbe311f2c2c10458d20f80b5e29a`.
`git apply --check` and the disposable application both passed. The target was not applied to the repository.

## Supported UI slice

| Surface | Contract fact | UI disposition |
|---|---|---|
| List | `queryShipments`; status/sourceDocumentId/page/pageSize | In scope; server page controls plus client rendering |
| Create | `createShipment`; 7 top-level and 6 line inputs | In scope; separate Compact form |
| Detail | `getShipment`; lines, optional POD and nullable root | In scope; read-only Details route |
| Status | `transitionShipment`; frozen matrix | In scope; exact target action, no generic edit |
| POD | `captureProofOfDelivery`; string evidence references | In scope; bounded action; no file upload |
| Lifecycle history/POD query/reconcile/assignment/edit/delete/bulk | no standalone frozen operation | Excluded; no inferred endpoint or control |

The create count is 13: seven top-level user fields and six fields per repeating line. `lines` itself is a collection,
not an additional input. Since the count is above eight and detail is a real route, GoldenReferenceCompact applies. There
is no Edit route because the backend exposes no edit command. Transition and POD are bounded actions and do not change the
create-form count.

## Permission and state boundary

Read, create, dispatch, cancel and POD capture are independent. The backend controller permits the transition route when
the actor has either dispatch or cancel; the handler then requires `.cancel` for target `Cancelled` and `.dispatch` for
all other targets. The UI mirrors that two-stage rule and never treats either key as a grant for the other. `reconcile`
remains backend/internal scope with no screen action.

The UI displays only allowed transitions from the frozen matrix, but stale-state/race handling remains server-authoritative.
POD is offered only in Dispatched/InTransit. Tenant and legal entity never enter a form or query. Cross-scope and deleted
records use safe 404 presentation. UAS-001 removes the entire list/detail surface when read is absent.

## Replay, errors and correlation

Create, transition and POD use one idempotency key per logical intent. A network/unknown-result retry reuses the exact key
and body. A changed payload is a new intent; same-key changed payload stays a 409 conflict. The UI does not infer state from
the receipt: after replay/success it reloads list/detail. It distinguishes `POD_ALREADY_CAPTURED`,
`INVALID_SHIPMENT_TRANSITION`, validation and dependency failures using the published status/body.

The request/response correlation is a support trace. Detail `lifecycleCorrelationId` is an optional authoritative persisted
business root. Missing/null root is rendered unavailable; it is never derived from the request trace, shipment ID or other
identity. Malformed producer root remains a backend failure and is not repaired by UI code.

## Phase 1.5 and authority disposition

Design items are complete in `PHASE15-ACCEPTANCE.tsv`; four execution gates remain:

1. owner approval of the exact proposed pack bytes while preserving bounded backend acceptance;
2. a durable registered target checkout plus immutable source/transfer manifest;
3. one integration owner for exact gateway, module/permission registration and navigation/resource changes;
4. application evidence closing UI Phase 1.5 and releasing a versioned DEV prompt.

Until those gates close, the proposed patch is not applied, the existing pack's backend `ready-for-dev` label is not UI
authority, and both prompts remain HELD. No pack promotion or runtime authorization occurred in this lane.

## Assumptions and gaps

- **ASSUMPTION-UI-183-01:** same-origin MVC adapters use Gateway 5000 as their only downstream base; browser code never
  calls 5061 or handles bearer storage.
- **ASSUMPTION-UI-183-02:** identifier/reference inputs remain exact strings/UUIDs because the frozen Shipment surface
  does not publish lookup endpoints for warehouse, ship-to, item, SKU or UoM.
- **ASSUMPTION-UI-183-03:** existing shared shell, access-denied and SweetAlert wrappers are consumed unchanged. A missing
  primitive becomes an integration-owner gap, not implicit shared write authority.
- **GAP-UI-183-01:** no current Shipment Gateway route was found in `ocelot.json` hash `b0121d...`.
- **GAP-UI-183-02:** no Shipment module manifest/catalog/navigation registration was found in the inspected source.
- **GAP-UI-183-03:** final integration target/preimages and transfer authority are absent.
- **GAP-UI-183-04:** pack delta, UI Phase 1.5 and UI DEV/VER have no owner authorization.
