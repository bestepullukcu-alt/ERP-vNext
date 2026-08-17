# Subscription Plan Catalog API

Module: PSS-006  
Gateway base path: `/api/platform/subscription-plans`  
Frontend surface: `/Platform/SubscriptionPlans`

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/subscription-plans` | List plans with filter, paging, and sorting. |
| GET | `/api/platform/subscription-plans/active` | List active plans for selectors. |
| GET | `/api/platform/subscription-plans/by-module/{moduleKey}` | List plans that include a module key. |
| GET | `/api/platform/subscription-plans/summary` | Return plan summary counts. |
| GET | `/api/platform/subscription-plans/{id}` | Return plan detail. |
| POST | `/api/platform/subscription-plans` | Create a plan. |
| PUT | `/api/platform/subscription-plans/{id}` | Update a plan. |
| POST | `/api/platform/subscription-plans/{id}/activate` | Activate a plan. |
| POST | `/api/platform/subscription-plans/{id}/deactivate` | Deactivate a plan. |
| GET | `/api/platform/subscription-plans/{id}/features` | Return feature mappings for a plan. |
| PUT | `/api/platform/subscription-plans/{id}/features` | Replace plan feature mappings. |

## Related Data

The UI consumes module catalog and subscription feature data for module and feature selection. Plan records remain the system of record for included module keys and default commercial limits.

## Rules

- Plans can be activated or deactivated without deleting historical tenant subscription references.
- Feature mapping is managed through the plan feature endpoints.
- API responses use `Response<T>`.

