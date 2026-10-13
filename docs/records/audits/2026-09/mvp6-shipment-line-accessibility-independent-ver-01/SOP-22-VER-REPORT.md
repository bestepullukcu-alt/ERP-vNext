# MVP6-SHIPMENT-LINE-ACCESSIBILITY-REWORK-01 — Independent SOP §22 VER

## Verdict

**PASS — `SHIP-UI-PRES-183-01` is independently CLOSED for the exact bounded line-input accessibility rework.** The final 354-entry successor is reproducible, the patch changes only three approved Shipment UI/test paths, and native .NET 8 plus independent browser evidence confirms unique label bindings and deterministic keyboard/focus behavior. Durable PNG remains **OPEN**. This verdict is presentation/accessibility scope only; it does not reclassify inherited Auth/backend evidence or grant CT/full-module/G5/rollout acceptance.

## Mode, baseline and authority

- Role: different verifier; product source remained read-only.
- Repository: `feature/mvp6-logistics` at `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Approved preimage manifest: `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`.
- DEV patch: `4c153b42890f10e289e421c0428ff1c1eff252f39892505ed0556cfa4efe5055`.
- DEV successor manifest: `dcc6662696db5e689d2d4f6facbb5106ca53d06a957caf21c5fabc912a0c7c88` (354 rows).
- DEV source archive: `96eae3e4c3af6679a7a48082c2becf758ca32f69b6a933e8d3029b52a8ee233c`.
- Writer marker: `WRITER-COMPLETE`, SHA-256 `cd4d11f5ca8fea62332d500b809320d48ffb521eff8656ccd2a88a3304916336`.
- Independent workspace: `/private/tmp/mvp6-shipment-line-accessibility-ver-01/`.

The verifier checked all 16 DEV artifact entries and all 354 archived source rows; no hash or size mismatch was found. The patch was independently applied to the exact presentation preimage and reproduced all 354 successor hashes.

## Exact reviewed change

Only these approved paths differ:

1. `frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml`
2. `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js`
3. `frontend/Diten.Web.Tests/Forms/ShipmentFormContractTests.cs`

The template supplies six field-label markers at `_Form.cshtml:23`. Runtime synchronization assigns deterministic IDs and matching `htmlFor` values at `create.js:15-25`; add/remove focus behavior is at `create.js:26-42`. Payload construction still uses the same six `data-field` keys and DOM order at `create.js:44-50`. The contract guard is at `ShipmentFormContractTests.cs:19-30`. No shared layout, DataTable, Auth, Gateway, backend, contract, guard or Git file appears in the patch.

## Independent build and tests

Native toolchain was SDK `8.0.417`, host/runtime `8.0.23`; no major roll-forward was used.

| Check | Result |
|---|---:|
| Focused repeatable-line contract | **1/1 PASS** |
| Bounded Shipment form/JS/controller regression | **17/17 PASS** |
| Narrow Root R2 storage/HTTP regression | **7/7 PASS** |

The 1 focused test is included in the 17-test bounded set and is not added to it. An initial sandboxed MSBuild attempt hit a local named-pipe permission denial; the same exact source was rerun with the native toolchain outside that sandbox restriction and passed. This is an environment attempt, not a product failure.

Independent Web DLL SHA-256: `8c98394b751d4964d7c07a7917e9bac837de42a07d27ebf61acde8c3c2795fcf`.

## Independent browser evidence

A freshly built Web process ran at `127.0.0.1:6043` against a lane-local stateless presentation fixture at `127.0.0.1:6042`. The fixture supplied only an ephemeral local session and no Mongo connection. It is not evidence for real Auth, Gateway, persistence, replay, tenant/LE enforcement or backend acceptance.

At 390px:

- one line exposed six unique IDs, six matching label/control pairs and six accessible names;
- keyboard Enter on Add Line produced two and then three lines, focused each new `lineNumber`, and retained 12/18 unique IDs;
- deleting the middle line reindexed to 12 unique IDs, kept values `A` and `C`, and focused `shipmentLine_1_lineNumber`;
- label activation focused `shipmentLine_0_itemId`;
- Tab order was `plannedDeliverAt` → `btnAddLine` → `shipmentLine_0_lineNumber`;
- invalid submit focused the visible `role=alert` summary;
- no document overflow was observed.

At 768px, one- and two-line states retained unique IDs, exact label/control relationships, accessible-name locator counts of two per line field, deterministic new-line focus and no document overflow.

The browser also confirmed the six field keys stayed in the existing order, no `name` attributes were introduced, and removal preserved the surviving values. This is consistent with the unchanged payload builder and the 17/17 bounded regression.

## Acceptance disposition

The complete row-level result is in `ACCEPTANCE.tsv`. B01/B02 and Root R2 stay closed through fresh, narrow regressions. Earlier real-Auth/backend results remain inherited and content-bound; they were not rerun or relabeled. Durable PNG remains OPEN because no documented supported export path was available, and no restricted workaround was attempted.

## No-change and cleanup

- No product source, shared file, contract, pack, guard, branch, staging area or Git metadata was modified by this verifier.
- Only `docs/records/audits/2026-09/mvp6-shipment-line-accessibility-independent-ver-01/` was created as the authorized verification output.
- Browser viewport override was reset and the verifier-created tab was closed.
- Fixture/Web listeners on ports 6042/6043 were stopped and confirmed absent.
- No commit, push, stash or rollout occurred.

## Evidence index

- `ACCEPTANCE.tsv` — row-level result.
- `evidence.tar.gz` — exact source/artifact checks, TRX, independent browser observations, binary/process, toolchain, cleanup and repository-state evidence.
- `ARTIFACTS.sha256` — permanent output checksums.

