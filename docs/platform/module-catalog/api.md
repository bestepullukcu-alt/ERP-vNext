# Tenant Module Catalog API

Module pack: `PSS-005-tenant-module-catalog`

Base path through Gateway: `/api/platform/module-catalog`

## Endpoints

- `GET /api/platform/module-catalog`
  - Filters: `search`, `domain`, `service`, `status`, `isCoreModule`, `isTenantAssignable`, `page`, `pageSize`, `sort`
- `GET /api/platform/module-catalog/assignable`
  - Returns only `Status=Active`, `IsTenantAssignable=true`, `IsDeleted=false`.
- `GET /api/platform/module-catalog/{id}`
- `GET /api/platform/module-catalog/by-code/{moduleCode}`
- `POST /api/platform/module-catalog`
- `PUT /api/platform/module-catalog/{id}`
- `POST /api/platform/module-catalog/{id}/activate`
- `POST /api/platform/module-catalog/{id}/deactivate`
- `DELETE /api/platform/module-catalog/{id}`
- `DELETE /api/platform/module-catalog/bulk`

## Payload Rules

- `TenantId` is not accepted in request payloads.
- `ModuleCode` is trimmed, uppercased, converts whitespace/underscore separators to `-`, collapses repeated separators, and removes leading/trailing separators.
- `ModuleCode` must be 2-80 characters after normalization and match `^[A-Z0-9]+(-[A-Z0-9]+)*$`.
- `ModuleCode` is immutable after create; update requests that change it are rejected.
- `Status` accepts only `Draft`, `Active`, `Inactive`, `Deprecated`.
- `Version` must match `major.minor.patch`.
- `SortOrder` defaults to `0` and must be non-negative.
- `IsCoreModule=true` records cannot be deleted.
- Deprecated records are read-only except `DisplayName`, `Description`, and `SortOrder`.

