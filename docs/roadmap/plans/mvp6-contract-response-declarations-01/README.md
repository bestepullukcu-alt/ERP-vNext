# Two frozen contracts declare fewer responses than the code returns — a measured, additive-only proposal

Prepared 2026-10-17. **Nothing here is applied.** Both contracts are frozen and a contract edit is the
owner's; this package exists so the decision is cheap rather than researched.

Owner-queue items it serves: **Q498** (v9.6 §5 item 7) and the undeclared-500 finding from
`mvp6-sandop-capacity-conformance-01`.

## What was measured

Response codes are read from the YAML, per operation.

### A — `shipment-bundle.openapi.yaml`

| operation | declares | missing vs the GET reference |
|---|---|---|
| `GET /returns` | `200,400,401,403,404,500,503` | — this is the complete GET shape |
| `GET /claims` | `200,400,401,403,500,503` | **404** |
| `GET /shipments` | `200,400,503` | **401, 403, 404, 500** |

So Q498 is two separate sizes, not one: `/claims` is short by a single code, `/shipments` by four.
Both are **under**-declaration — the code returns these, the contract does not admit them — so adding
them removes a conformance gap rather than changing behaviour.

### B — `sandop-capacity.openapi.yaml`

| | |
|---|---|
| operations | 12 |
| operations declaring `500` | **0** |
| `INTERNAL_ERROR` mentioned anywhere in the contract | **0** |
| code sites that can emit `500 INTERNAL_ERROR` | **9** across Loads, Returns, Carriers, Claims and Sandop |

**The sibling contract already solved this.** `shipment-bundle.openapi.yaml` declares `500` **12
times** and names `INTERNAL_ERROR` **24 times**. So B is not a design question — it is
`sandop-capacity` not having adopted a pattern its sibling carries, and the fix is to copy it.

For MOD-0192 this is not hypothetical: pack line `:199` records a **500 `INTERNAL_ERROR` with a zero
correlation id** as the measured answer of an authenticated Capacity request today.

## Proposed delta — additive only

Additive-only matters because these contracts are frozen; it is the same discipline
SHIPMENT-BUNDLE 3.3.0 followed (nothing removed, 12 paths before and after). **No existing response,
schema or example is touched. No operation, path or required field changes.**

### A1 · `GET /claims` — add `404`

```yaml
        '404':
          $ref: '#/components/responses/UnknownClaim'
```
Use whichever existing `components/responses` entry `GET /returns` already references for its `404`,
so no new component is introduced.

### A2 · `GET /shipments` — add `401`, `403`, `404`, `500`

```yaml
        '401':
          $ref: '#/components/responses/Unauthenticated'
        '403':
          $ref: '#/components/responses/Forbidden'
        '404':
          $ref: '#/components/responses/UnknownShipment'
        '500':
          $ref: '#/components/responses/InternalError'
```
All four `$ref` targets already exist in this contract — it declares `500` twelve times elsewhere — so
this adds no component either.

### B1 · `sandop-capacity` — add a `500` response component and reference it from all twelve operations

```yaml
  responses:
    InternalError:
      description: Unexpected server fault; no mutation is implied and the transaction is aborted
      x-error-codes: [INTERNAL_ERROR]
      headers:
        X-Correlation-Id:
          $ref: '#/components/headers/CorrelationId'
      content:
        application/json:
          schema: { $ref: '#/components/schemas/Error' }
```
then `'500': { $ref: '#/components/responses/InternalError' }` on each of the twelve.

**Why `x-error-codes` and not only an example:** that is how this contract declares its code sets —
nine of its `components/responses` entries carry `x-error-codes`, and it is the field
`mvp6-sandop-capacity-conformance-01` compared the implementation against. A `500` added without it
would be invisible to the same check.

## What this proposal does not claim

- **That the 500 path is desirable.** It is a last-resort catch around an aborted transaction. The
  argument here is only that a contract should admit what the code can return; whether those nine
  sites should exist is a separate question for their owners.
- **That the version numbers are decided.** Both contracts are frozen and re-pinned by several packs
  (MOD-0186/0187 at SHIPMENT-BUNDLE 3.1.0, MOD-0192 at SANDOP-CAPACITY 3.0.0 — and MOD-0192's pack
  `:253` still pins the 2.0.0 hash, which is its own open item). A version bump drags pack re-pins
  behind it, and CT did not choose one.
- **That it was verified.** No YAML was edited and nothing was parsed back after editing. The
  measurements above are of today's files; the snippets are reviewed, unexecuted text.
