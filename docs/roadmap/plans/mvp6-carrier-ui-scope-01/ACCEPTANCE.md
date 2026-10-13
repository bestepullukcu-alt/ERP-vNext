# UI and integration acceptance

| ID | Acceptance |
|---|---|
| UI184-A01 | An authenticated user with `.read` sees a tenant-shell DataTables v2 list; the adapter reaches Carrier only through Gateway 5000 and supplies auth, tenant, LE, and correlation headers. |
| UI184-A02 | The only backend list query emitted is absent `status` or one exact CarrierStatus token. Search/sort/paging remain client-side. Empty 200 renders a localized empty state. |
| UI184-A03 | No session produces the standard 401 surface. Authenticated missing-read renders only `_AccessDenied`, with no title/table/actions/redirect. Missing create or status grant hides only that action and direct endpoint invocation remains 403. |
| UI184-A04 | The create offcanvas has exactly four fields and serializes only the frozen `CreateCarrierCommand`; tenant/LE/status/unknown properties are absent. Contract-valid whitespace/duplicates/null/empty distinctions are not tightened. |
| UI184-A05 | Required/minItems failures are accessible inline errors; backend 400/415 are mapped without leaking raw exceptions. All labels, validation, empty state, confirmations, and messages exist in en/tr/fr/es/zh/ar/ru. |
| UI184-A06 | A same-scope duplicate code returns 409 `CARRIER_CODE_CONFLICT`, retains input, and produces no false success. Editing the payload creates a new request intent/key. |
| UI184-A07 | Status UI sends only `targetStatus` and `reasonCode`; empty reason is allowed. Active/Suspended options are exact, Retired is terminal, and a race yielding 422 refreshes the row/list. |
| UI184-A08 | Rapid repeat submission produces one logical intent. Loss/500/503 retries use the same key and exact payload; 201/200 replay is recognized via `idempotentReplay:true`, then current list state is reloaded. Changed-payload 409 stops retry. |
| UI184-A09 | Tenant/LE header mismatch and cross-scope/deleted carrier yield the same safe 404 presentation; the UI never reveals another scope or accepts scope in form/query/body. |
| UI184-A10 | Every success/error captures the current response `X-Correlation-Id`; missing/invalid header behavior follows the annex and no trace is presented as a business correlation root. |
| UI184-A11 | List rows and quick view use only `CarrierSummary`; there is no detail/edit/delete/bulk/lookup request and `externalReference` is not reconstructed. |
| UI184-A12 | Golden Slim/DataTables verifier, Razor/controller tests, JS behavior tests, seven-resource parity, route-method negative tests, permission/UAS tests, and a Gateway-backed composed smoke pass on the exact authorized baseline. Core/browser model tests alone do not satisfy the composed Gateway evidence. |

## Negative/falsification set

- Direct browser/service URL `:5061`, an unknown query key, or a by-id/edit/delete/bulk request fails the UI contract test.
- Hidden or disabled controls alone do not count as RBAC; direct MVC action tests must return 401/403 as appropriate.
- A generated new idempotency key after an unknown result, payload trimming/reordering, mode deduplication, or treating empty `reasonCode` as invalid fails replay/parity tests.
- A localized wire token, missing one of seven resource files/keys, bare `alert()/confirm()`, or rendering the denied page skeleton fails frontend verification.
- Route tests prove unsupported verbs do not create a Carrier operation; route configuration must not imply a PATCH backend capability.
