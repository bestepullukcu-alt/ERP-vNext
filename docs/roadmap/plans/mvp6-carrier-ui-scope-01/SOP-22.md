# SOP §22 Handoff — MVP6-CARRIER-UI-SCOPE-01

## Metadata

| Item | Value |
|---|---|
| Date | 2026-09-23 |
| Role | module-pack-author + frontend/integration preparation |
| Profile | spec-only |
| Repository | `/Users/natig/Projects/ERP-vNext-recovery` |
| Branch / HEAD | `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| DCP-002 | PASS: `MOD-0184` / `Carrier Management` |
| Pack input | `2df9363b7c870672fab13b68a87e7fb849ed7a3f213c5197e87f9c2147de817e` |
| Frozen contract input | SHIPMENT-BUNDLE `3.0.0`, wire `v1`, SHA256 `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` |
| Carrier annex | `carrier-semantics-v1.1.0.md`, SHA256 `87557ef9f5b5a4427862effbece2bb528ebc1b95361c29416373709223021fee` |
| Backend acceptance | bounded E4 CT ACCEPTED; UI/gateway/catalog/E5/G5 excluded |
| Verdict | **PREPARED / UI DEV HELD / UI VER HELD** |

## Result

The maximum contract-faithful first UI slice is a tenant Carrier list with create and status actions. The frozen surface has exactly `queryCarriers`, `createCarrier`, and `changeCarrierStatus`; there is no detail-by-id, edit, delete, bulk, lookup, server search, sort, or pagination operation. The UI therefore uses a client-side DataTables v2 table and sends only the optional exact `status` query to the backend.

The create form has four user fields: required `carrierCode`, required `displayName`, required `supportedModes`, and optional nullable `externalReference`. Tenant and legal-entity values are signed context/headers and never form fields. Four is within the repository's `<=8` threshold, so `golden_reference: slim` is exact. The status action's `targetStatus` and `reasonCode` are a row command and do not alter the create-form count.

The proposed pack patch preserves the current backend-scoped `ready-for-dev` state, changes the UI metadata to `tenant`, `slim`, and `form_field_count: 4`, and adds an explicit UI HELD section. This avoids reopening §30's bounded backend acceptance while preventing that backend status from being used as UI authority. The UI still requires an explicit UI-scope/Phase 1.5 decision and versioned frontend dispatch.

## Contract and acceptance binding

| Concern | Exact source | UI consequence |
|---|---|---|
| Operations | YAML lines 204–417 | list/create/status only |
| Create fields | YAML `CreateCarrierCommand`, lines 3121–3129 | four fields; no hidden scope/status field |
| Status fields | YAML `ChangeCarrierStatusCommand`, lines 3130–3136 | exact target token; `reasonCode` required but empty string valid |
| List shape | YAML `CarrierSummary`/`CarrierListResponse`, lines 3137–3161 | row summary only; no externalReference/detail fetch |
| Enums | YAML lines 3012–3013 | status: Active/Suspended/Retired; modes: Road/Air/Sea/Rail/Parcel |
| Errors/replay | Carrier annex §§3–7 | preserve exact status/code/precedence and same-key retry |
| Backend acceptance | `mvp6-mod0184-ct-accept-02-2026-09-18/README.md` | C01–C12 backend evidence is not UI authority |

## Phase 1.5 UI delta

| Check | Disposition | Evidence / gate |
|---|---|---|
| Shell | PASS design | tenant module uses `_LayoutTenantShell.cshtml`; shared shell stays protected |
| Golden reference | PASS design | four create fields => GoldenReferenceSlim |
| DataTable v2 | PASS design | marker and golden partial topology in `OWNED-PATHS.txt`; client-side data, one exact status query |
| Required/null parity | PASS design | no trim/max/pattern or hidden required fields; optional null and empty-string distinctions retained |
| UAS-001 | PASS design | authenticated/no-read renders `_AccessDenied` only; no redirect, title, table, or action shell |
| Action authorization | PASS design | read/create/status.change independent; missing action grant hides that action; backend remains authoritative |
| Localization | PASS design | module text in seven `.resx` files; wire enum/error tokens remain untranslated |
| Modal standard | PASS design | create-only offcanvas; status confirmation and result/error messaging through Premium SweetAlert2 |
| Gateway-only egress | GAP | no current Carrier route in `ocelot.json`; separate integration owner required |
| Permission/catalog/navigation | GAP | backend literals exist, but shared frontend/Auth catalog and menu registration are not proven |
| Source baseline | GAP | Lane A selection is HELD; successor must authorize and bind an exact target checkout/source manifest |
| Pack-delta/UI runtime authority | GAP | no actual owner approval for this UI delta, Phase 1.5, exact application, or UI DEV exists |

## Dirty/no-change evidence

The working tree was dirty before this lane, including modified pack/contract/shared files and many untracked audit/runtime artifacts. This lane wrote only this new plan directory. It did not change the pack, contract, frontend, gateway, permission catalog, shell, backend, index, or git state. `INPUTS.sha256` binds the inspected inputs; final output hashes are in `SHA256SUMS`.

## Assumptions and exact gaps

- **ASSUMPTION-UI-184-01:** Same-origin MVC endpoints are UI adapters. Their only service egress is Gateway `http://localhost:5000`; the browser never calls port 5061.
- **ASSUMPTION-UI-184-02:** Client-side search/sort/paging does not change the Carrier API. Only `status` may be sent as a query parameter.
- **ASSUMPTION-UI-184-03:** Existing shared shell/access-denied/confirmation components are reused. A missing shared primitive becomes an integration-owner gap rather than an implicit shared-file grant.
- **GAP-UI-184-01:** Lane A successor must select and authorize the exact source baseline/checkout transfer. Current `SELECTED-SOURCE.json` is a HELD selection record, not transfer authority.
- **GAP-UI-184-02:** Integration owner must provide an explicit Carrier Gateway route without adding backend operations.
- **GAP-UI-184-03:** Permission owner must bind the three exact keys to the shared catalog/grant model and the page/action descriptor.
- **GAP-UI-184-04:** Owner must approve `OWNER-DECISION-TEXT.md`, the proposed pack target bytes, UI Phase 1.5, and a versioned dispatch. Until then both prompts remain HELD.

No new Carrier business decision is requested. The remaining decisions concern the UI scope, shared integration, target baseline, and dispatch authority.
