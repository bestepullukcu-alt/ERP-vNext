---
description: "PSS-LOOKUPS-001 - Platform/Admin Lookup and Reference Data Boundary"
---

# Platform Lookup / Reference Data Standard

This rule defines how new Platform/Admin modules consume and extend system lookup data. It is intentionally limited to Platform-owned lookup/reference data and does not replace ERP Master Data Management.

## Source of Truth

- Platform/Admin system lookups are owned by `Diten.Platform`.
- The canonical API surface is `GET /api/lookups/...` behind Gateway.
- Browser-facing Platform/Admin UI must call a same-origin MVC proxy or Gateway route. It must not call service port `5057` directly.
- Existing PSS lookup module pack of record: `execution/domains/platform-shared-services/module-packs/PSS-011-lookups-reference-data.md`.

## Canonical Response Contract

All Platform lookup endpoints return the standard `Response<IReadOnlyList<LookupOptionDto>>` envelope.

Serialized lookup option fields:

```json
{
  "code": "USD",
  "name": "US Dollar",
  "value": "USD",
  "group": null,
  "sortOrder": 10,
  "metadata": {
    "symbol": "$"
  }
}
```

Rules:
- Required item fields: `code`, `name`, `value`.
- Optional item fields: `group`, `sortOrder`, `metadata`.
- `value` is the submitted machine value and defaults to `code`.
- Consumers must unwrap `Response<T>.data` before rendering options.
- Consumers must not assume ad hoc shapes such as `{ id, name }` or `{ code, name }` without `value`.
- Hardcoded fallback lists for Platform lookups are forbidden.

## Platform Lookup Decision Rule

Use this decision before adding a dropdown, select filter, setup/default value, provisioning option, or enum-like UI list in a Platform/Admin module:

| Need | Decision |
|---|---|
| Existing Platform system lookup | Consume `GET /api/lookups/{key}`. |
| New Platform-owned system lookup | Add a planned lookup key to the PSS lookup pipeline and test it in the module pack. |
| Tenant-specific ERP business lookup | Do not add it to PSS lookup. Create or use an MDM/reference-data module pack. |
| ERP Account / General Reference / Financial Reference / Territory Reference | Out of scope for PSS lookup; belongs to MDM/reference ownership. |

Examples of Platform lookup keys:
- `currencies`
- `locales` / `languages`
- `timezones`
- `tenant-tiers`
- `feature-categories`
- `module-catalog/domains`
- `module-catalog/services`
- `subscription-cycles`
- `countries` for Platform provisioning/support only

## Boundary Rules

- `countries` is Platform provisioning/support lookup only. It is not Territory Reference and must not grow MDM/Territory semantics.
- Tenant tier lookup is Platform packaging vocabulary only. It is not customer/account classification.
- Subscription cycle lookup is Platform subscription cadence only. It is not accounting or invoicing reference data.
- Feature category lookup must use the existing Platform Feature Category source of record when available.
- Platform lookup payloads must not include `TenantId`.
- Platform lookup endpoints are read-only unless a future approved pack explicitly introduces management UI/API.

## UI and Shell

- This rule does not create a Platform Admin lookup management screen.
- Do not add sidebar/menu items, DataTables, Razor pages, Create/Edit/Details forms, or navigation for lookup management only because a module consumes lookup data.
- If editable Platform lookup management is requested later, create a separate module pack with `shell: platform-admin` and the correct `golden_reference` decision.

## Module Pack Requirements

Every new Platform/Admin module pack must include a lookup decision:
- Which existing `/api/lookups/...` endpoints are consumed.
- Whether a new Platform lookup key is required.
- Whether any requested lookup is actually MDM/reference data and therefore out of scope.
- Repo scope for frontend proxy/consumer paths when lookup consumption changes.
- Acceptance criteria for endpoint path, `LookupOptionDto` shape, auth behavior, no hardcoded fallback, and Gateway/proxy usage.
- Test expectations for lookup smoke tests and response shape validation when lookup fields are used.

If a new Platform lookup key is required but the module pack does not explicitly approve it, implementation must stop and the pack must be revised or a separate PSS lookup extension pack must be prepared.
