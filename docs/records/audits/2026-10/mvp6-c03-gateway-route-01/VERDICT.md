# C-03 — gateway route for /api/shipment-bundle (2026-10-03)

Pack §15 is explicit: the canonical family is `/api/shipment-bundle/**` to port 5061, matching the
OpenAPI `servers.url` (`/api/shipment-bundle`) plus its paths; the Phase B developer does not edit
`ocelot.json`; a separate integration WP adds the route **after controller endpoints exist** and
validates `Authorization`, `X-Tenant-Id`, `X-Legal-Entity-Id`, `X-Correlation-Id` and
`Idempotency-Key` propagation. Both preconditions now hold: the endpoints exist and, since Q236, the
service boots.

## Change

Two routes appended to `gateway/Diten.ApiGateway/ocelot.json`: `/api/shipment-bundle` and
`/api/shipment-bundle/{everything}`, both to `localhost:5061`, methods `GET,POST,OPTIONS`. The
contract declares 12 paths using only GET and POST, so no other verb is routed.

Routes 269 → 271. `git diff --stat`: 34 insertions, no deletions. JSON revalidated. Gateway builds
(0 errors, 2 pre-existing warnings).

## K3 contrast — the route is genuinely new

| request through the gateway on :5000 | result |
|---|---|
| `/api/no-such-module/x` (no route exists) | **HTTP 404** — Ocelot's no-route answer |
| `/api/shipment-bundle/shipments` (route added here) | **HTTP 401**, then 403, then 200 as credentials improved |

A missing route 404s. Ours does not, at any stage. Before this change the shipment family was one of
the 404s: `grep -c shipment-bundle ocelot.json` returned **0** against 269 routes.

## End-to-end, measured

All requests sent to the **gateway** on :5000. The service log is from :5061.

| attempt | result | what it proves |
|---|---|---|
| no token | 401 `INVALID_REQUEST` "Authentication required.", `X-Correlation-Id` echoed | route live; correlation header propagates |
| HS256 token, claims `sub` non-UUID, no tenant claims | **403** "Trusted tenant, legal entity and actor context required." | the token was **validated** through the gateway; the tenant guard engaged |
| HS256 token with `tenant_id`, `legal_entity_id`, `sub` all UUIDs matching the headers | **HTTP 200** `{"items":[],"page":1,"pageSize":5,"total":0,"contractVersion":"v1"}` | **first fully authenticated request through the gateway to this service** |

Service log for the 200: `Request starting HTTP/1.1 GET http://localhost:5061/api/shipment-bundle/shipments?page=1&pageSize=5` →
`200 ... 182.9502ms`. The request was sent to :5000 and arrived at :5061, so the gateway proxied it.

Tokens were signed with a secret CT generated per run (`Encoding.UTF8.GetBytes` of the string, as
`Program.cs:37` requires). The Q235 exposed value was never used. No secret was printed or written.

## R-02 is answered

`RUNTIME-REQUIRED.tsv` R-02 asked whether any user can hold `supplychain.shipments.*`. Measured in
`ShipmentContextMiddleware.cs:17-22`: trusted context comes from **token claims**, not headers —
`tenant_id`, `legal_entity_id` and `sub` must each be exactly one non-empty UUID, and must equal the
`X-Tenant-Id` / `X-Legal-Entity-Id` headers. Permissions are read from `permission` claims. No
Platform seed is needed to exercise the path; an issuer that mints those claims is.

## NEW FINDING — F-C03-1: the gateway pre-empts the pack's cross-tenant rule

Pack §249 requires cross-tenant/cross-LE reads and mutations to return **404**, and
`ShipmentContextMiddleware.cs:25` implements exactly that (`404` / `SHIPMENT_NOT_FOUND`).

Measured through the gateway with a token for tenant `4444…` and header `X-Tenant-Id: 1111…`:

```
HTTP 400
{"title":"Tenant mismatch","status":400,"detail":"The authenticated tenant and the request's other tenant signals name different tenants. ...
```

Two problems:

1. **Wrong status.** The gateway answers 400 before the service can answer 404, so the pack's
   cross-tenant requirement is satisfied only when the service is called directly, not through the
   gateway — which is the only path the UI uses.
2. **Wrong envelope.** The body is an RFC 7807 problem document with `title`/`status`/`detail`. Every
   contract response uses `{"error":{"code",...},"contractVersion":"v1"}`. A UI parsing
   `error.code` gets `undefined` here — the same class of defect as C-02.

This was invisible to static analysis and to the service's own tests, which never traverse the
gateway. It needs an owner ruling: relax pack §249 to accept 400 at the edge, or make the gateway
defer the tenant decision to the service.

Not committed. OD-Q03a stands.
