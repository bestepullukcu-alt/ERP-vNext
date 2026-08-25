---
id: WP-0230-06
title: Gateway route family
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-04]
gate: build/test only
status: ready
estimate: 0.5 d
owner_agent: integration-agent
---

# WP-0230-06 - Gateway route family

## Objective

Expose the MOD-0230 API through the Ocelot gateway as one route family, following NET-001, so the tenant UI can
reach it same-origin via the MVC proxy.

**This pack is integration-agent-owned.** `ocelot.json` is shared infrastructure; a careless edit breaks every
other service's routing.

## Preconditions

- [ ] WP-04 controller live at downstream `/api/v1/pv-case-intake-triage` on port 5011.
- [ ] Service responds locally: `curl http://localhost:5011/api/v1/pv-case-intake-triage` returns 401 without a token.

## File manifest

```text
gateway/Diten.ApiGateway/ocelot.json      (append two route objects only)
```

Nothing else. Do not touch `Program.cs`, other routes, or global config.

## The correction this pack applies

The original MOD-0230 draft proposed `/api/v1/pharmacovigilance/case-intake-triage` as the **upstream**
template. That violates NET-001, which puts `v1` on the **downstream** side only, and it does not match any
existing route in `ocelot.json` (`/api/legal-entities`, `/api/golden-reference-compact`, `/api/permissions/…`).

| | Template |
|---|---|
| Upstream (Gateway, 5000) | `/api/pv-case-intake-triage` |
| Downstream (`Diten.PvgService`, 5011) | `/api/v1/pv-case-intake-triage` |

## Implementation spec

Append exactly these two objects to the `Routes` array, matching the surrounding formatting:

```jsonc
{
  "DownstreamPathTemplate": "/api/v1/pv-case-intake-triage",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5011 } ],
  "UpstreamPathTemplate": "/api/pv-case-intake-triage",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "PATCH", "OPTIONS" ]
},
{
  "DownstreamPathTemplate": "/api/v1/pv-case-intake-triage/{everything}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5011 } ],
  "UpstreamPathTemplate": "/api/pv-case-intake-triage/{everything}",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "PATCH", "OPTIONS" ]
}
```

**`DELETE` is deliberately absent from `UpstreamHttpMethod`.** This is a second, independent line of defence:
even if a `[HttpDelete]` were ever introduced upstream by accident, the gateway would not route to it. Do not
add `DELETE` "for symmetry" with the other routes.

`{everything}` covers `/{id}`, `/{id}/triage`, and `/{id}/route`. Do not add per-sub-resource routes -
`/archive` and `/export` do not exist in slice 1 and must not be routable.

Auth, correlation, and tenant headers propagate through the existing global gateway configuration. Verify
propagation; do not add per-route header transforms.

## Forbidden

- `DELETE` in `UpstreamHttpMethod`.
- Explicit routes for `/archive`, `/export`, or `/bulk-delete`.
- Editing any existing route object.
- `/api/v1/...` as an **upstream** template.
- Changing global gateway config, rate limits, or CORS.
- Reserving a second port or a second downstream host.

## Acceptance criteria

- [ ] Exactly two route objects added; no existing route modified.
- [ ] `ocelot.json` is valid JSON and the gateway starts.
- [ ] Upstream has no `v1`; downstream does.
- [ ] `DELETE` absent from both route objects.
- [ ] Gateway → service round-trip works for list, detail, create, update, triage, route.
- [ ] `X-Correlation-Id` supplied at the gateway reaches the service and returns on the response.
- [ ] Tenant resolution works through the gateway identically to a direct call.
- [ ] `DELETE /api/pv-case-intake-triage/{id}` returns 404 **at the gateway** - the route does not exist.
- [ ] Every other service's routes still resolve (`run_all.sh` smoke).

## Tests

Route smoke, run against a live local stack:

```bash
TOKEN=...   # tenant user with pvg.case-intake-triage.* permissions

curl -i -H "Authorization: Bearer $TOKEN" -H "X-Correlation-Id: wp06-smoke-001" \
     http://localhost:5000/api/pv-case-intake-triage                       # 200, echoes correlation id
curl -i -H "Authorization: Bearer $TOKEN" \
     -X DELETE http://localhost:5000/api/pv-case-intake-triage/$(uuidgen)  # 404 at the gateway
curl -i http://localhost:5000/api/pv-case-intake-triage                    # 401, no token
curl -i -H "Authorization: Bearer $TOKEN" \
     http://localhost:5000/api/pv-case-intake-triage/export                # 404 - not routable
```

Regression: confirm `/api/legal-entities`, `/api/golden-reference-compact`, and `/api/permissions/me` still
resolve after the edit.

## Verify

```bash
python3 -c "import json;json.load(open('gateway/Diten.ApiGateway/ocelot.json'));print('valid json')"
grep -c "pv-case-intake-triage" gateway/Diten.ApiGateway/ocelot.json      # expect 4
grep -n "DELETE" gateway/Diten.ApiGateway/ocelot.json | grep -i "pv-case" # expect no matches
./run_all.sh
```

## Agent prompt

> Implement WP-0230-06 in `/Users/natig/Projects/ERP-vNext-recovery`. Act as the integration-agent.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-06-gateway-route.md`, the
> **Gateway / API Routing Decision** section of
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `.antigravity/rules/routes.md`, `.antigravity/rules/ports.md` (read only - it is protected).
>
> Append exactly two route objects to `gateway/Diten.ApiGateway/ocelot.json`. Upstream
> `/api/pv-case-intake-triage` (+ `/{everything}`), downstream `/api/v1/pv-case-intake-triage` on port 5011.
> `DELETE` must be absent from `UpstreamHttpMethod` - that is intentional, not an oversight.
>
> Do not modify any existing route, global config, or `.antigravity/**`. Do not add routes for `/archive`,
> `/export`, or `/bulk-delete`.
>
> Run the route smoke commands in the pack plus the regression checks on `/api/legal-entities` and
> `/api/golden-reference-compact`. Report the full curl output.
