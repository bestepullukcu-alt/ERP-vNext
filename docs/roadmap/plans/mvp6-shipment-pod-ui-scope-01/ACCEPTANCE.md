# UI acceptance — MOD-0183 Shipment/POD

| ID | Acceptance |
|---|---|
| UI183-A01 | An authenticated `.read` user gets `/SupplyChain/Shipments` in the tenant shell with DataTables v2. Browser traffic is same-origin MVC and downstream service traffic goes only through Gateway 5000. |
| UI183-A02 | List sends only frozen `status`, `sourceDocumentId`, `page`, `pageSize`; exact enum/casing and `pageSize<=200` are retained. A valid empty 200 renders a localized empty state. |
| UI183-A03 | Unauthenticated access uses the standard 401 surface. Authenticated missing-read renders only UAS-001 access denial: no title, filter, table, skeleton, action or redirect. Direct adapters still return 401/403. |
| UI183-A04 | Create serializes exactly the seven top-level properties plus line properties in `CreateShipmentCommand`; tenant/LE/status/correlation/idempotency and unknown properties are absent from the body. At least one line is required. |
| UI183-A05 | The Compact create form exposes 13 user inputs: seven top-level values (`sourceModule`, `sourceType`, `sourceDocumentId`, `warehouseReferenceId`, `shipToReference`, `plannedShipAt`, optional `plannedDeliverAt`) and six repeatable line values (`lineNumber`, `itemId`, `skuId`, `quantity`, `uomId`, optional `inventoryReferenceId`). Decimal quantity remains a lexical string. |
| UI183-A06 | Detail renders summary, source/warehouse/ship-to fields, lines, nullable carrier/load, optional POD and nullable `lifecycleCorrelationId`. Missing/null root is shown as unavailable; it is never derived from request trace or IDs. |
| UI183-A07 | Create is independently gated by `.create`; transition actions by exact target (`Cancelled` => `.cancel`, otherwise `.dispatch`); POD by `.pod.capture`. Read permission does not grant a mutation. Hidden controls alone do not satisfy RBAC. |
| UI183-A08 | Transition choices are computed only from the frozen lifecycle matrix. Backend 422 `INVALID_SHIPMENT_TRANSITION` remains authoritative under stale UI/races, makes no false success, and refreshes detail/list. |
| UI183-A09 | POD appears only for Dispatched/InTransit and submits recipient, receivedAt, evidence reference strings and optional note. It never uploads binaries. Success becomes Delivered; 409 `POD_ALREADY_CAPTURED` and 422 retain exact code/status and refresh current detail. |
| UI183-A10 | One logical create/transition/POD intent has one stable `Idempotency-Key`. Unknown response/500/503 retries preserve exact payload and key. Same-key changed payload 409 stops retry. Replay is recognized only from the returned `idempotentReplay` field; the UI then reloads current state. |
| UI183-A11 | Every request uses a valid UUID `X-Correlation-Id`. Returned error `error.correlationId` is displayed/copied as support trace, while `lifecycleCorrelationId` is labeled business root; the two are never substituted. |
| UI183-A12 | Cross-tenant/cross-LE/deleted/unknown detail and mutations use the same safe 404 presentation. The UI never accepts tenant/LE in query/form/body and never reveals existence. |
| UI183-A13 | All labels, filters, validation, empty states, confirmations, lifecycle/POD/replay/error text exist in en/tr/fr/es/zh/ar/ru. Arabic RTL, 390/768/desktop layouts, keyboard focus and accessible error summary pass browser checks. Enum/error codes remain untranslated wire tokens. |
| UI183-A14 | DataTables uses `window.DtDefaults`, `stateSave:false`, supported server paging adapter behavior, responsive control/action columns and the repository quality gate. Unsupported edit/delete/bulk/assign/reconcile/history affordances are absent. |
| UI183-A15 | Premium SweetAlert2/shared wrappers handle destructive/uncertain confirmations and result messages; no native `alert/confirm`, inline handler or secret/bearer storage exists. |
| UI183-A16 | Exact source build, controller/Razor/JS/resource tests, permission/UAS tests and composed Gateway-backed browser flows pass on one authorized immutable baseline. UI unit/model PASS alone is not runtime or E4 acceptance. |

## Falsification set

- Any browser request to port 5061, any invented endpoint, unknown body property or upload request fails parity.
- A user with only `.read` cannot create/transition/POD; a user with mutation permission but no `.read` cannot see list/detail.
- A `Cancelled` transition with only `.dispatch`, or a non-cancel transition with only `.cancel`, must be denied.
- A new idempotency key after an unknown result, normalized/reordered body on retry or treating a response trace as the lifecycle root fails replay/correlation acceptance.
- Missing one language/key, raw key display, English duplication used as a placeholder, Arabic layout overflow or inaccessible error focus fails localization/browser acceptance.

