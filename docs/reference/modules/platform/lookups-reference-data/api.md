# Lookups / Reference Data API

Module: PSS-011  
Gateway base path: `/api/lookups`

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/lookups/module-catalog/domains` | Module catalog domain options. |
| GET | `/api/lookups/module-catalog/services` | Module catalog service options. |
| GET | `/api/lookups/countries` | Country options. |
| GET | `/api/lookups/currencies` | Currency options. |
| GET | `/api/lookups/locales` | Locale options. |
| GET | `/api/lookups/languages` | Language options. |
| GET | `/api/lookups/timezones` | Time zone options. |
| GET | `/api/lookups/tenant-tiers` | Tenant tier options. |
| GET | `/api/lookups/feature-categories` | Feature category options. |
| GET | `/api/lookups/subscription-cycles` | Subscription cycle options. |
| GET | `/api/lookups/audit/categories` | Audit category options. |
| GET | `/api/lookups/audit/operations` | Audit operation options. |
| GET | `/api/lookups/audit/outcomes` | Audit outcome options. |
| GET | `/api/lookups/{lookupKey}` | Generic lookup fallback by key. |

## Response Shape

Lookup responses use `Response<T>` and return option DTOs with stable machine values and display names. Frontend Select2 consumers should unwrap `response.data`.

