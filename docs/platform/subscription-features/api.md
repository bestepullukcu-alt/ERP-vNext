# Subscription Feature Management API

Module: PSS-007  
Gateway base paths: `/api/platform/subscription-features`, `/api/platform/feature-categories`  
Frontend surface: `/Platform/SubscriptionFeatures` (two-tab page: **Categories** | **Features**, both GoldenReference DataTables)

Feature create/edit/details are full-page routes: `/Platform/SubscriptionFeatures/Create`, `/Edit/{id}`, `/Details/{id}`. Categories are managed via an offcanvas on the Categories tab.

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
| PUT | `/api/platform/feature-categories/{id}` | Update a category (DisplayName, Description, SortOrder, Status). `CategoryCode` is immutable. Row-version protected. |
| POST | `/api/platform/feature-categories/{id}/archive` | Archive a category with row-version protection (no hard delete). |

## Plan Mapping Endpoint

Feature availability by plan is written through `PUT /api/platform/subscription-plans/{id}/features`.

## Rules

- Feature definitions are platform global catalog records.
- Archived features are not regular active options.
- Categories have a full lifecycle (create / update / archive). `CategoryCode` is immutable once created; archive is soft (no hard delete).
- Runtime entitlement enforcement remains outside this UI surface.

