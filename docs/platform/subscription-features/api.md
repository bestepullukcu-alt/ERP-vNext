# Subscription Feature Management API

Module: PSS-007  
Gateway base paths: `/api/platform/subscription-features`, `/api/platform/feature-categories`  
Frontend surface: `/Platform/SubscriptionFeatures`

## Feature Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/subscription-features` | List feature definitions with catalog filters. |
| GET | `/api/platform/subscription-features/{id}` | Return feature detail. |
| POST | `/api/platform/subscription-features` | Create a feature definition. |
| PUT | `/api/platform/subscription-features/{id}` | Update a feature definition. |
| GET | `/api/platform/subscription-features/{id}/plan-mappings` | Return plans that map to a feature. |
| POST | `/api/platform/subscription-features/{id}/archive` | Archive a feature definition with row-version protection. |
| POST | `/api/platform/subscription-features/{id}/deactivate` | Deactivate a feature definition with row-version protection. |

## Category Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/platform/feature-categories` | List feature categories, optionally filtered by status. |
| POST | `/api/platform/feature-categories` | Create a feature category. |

## Plan Mapping Endpoint

Feature availability by plan is written through `PUT /api/platform/subscription-plans/{id}/features`.

## Rules

- Feature definitions are platform global catalog records.
- Archived features are not regular active options.
- Runtime entitlement enforcement remains outside this UI surface.

