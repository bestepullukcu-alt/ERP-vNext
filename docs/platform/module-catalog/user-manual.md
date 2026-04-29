# Tenant Module Catalog User Manual

Use **Platform > Module Catalog** to maintain the platform-owned ERP module catalog.

## List

The list supports search, domain, service, status and tenant-assignable filters. Use **Add Module** to create a record. Row actions open details, edit, or delete the module.

## Create And Edit

The form contains the 12 approved fields:

- Module Code
- Module Name
- Display Name
- Description
- Domain
- Service
- Category
- Status
- Version
- Is Core Module
- Is Tenant Assignable
- Sort Order

`ModuleCode` is saved in canonical uppercase dash-separated format. Use semantic versions such as `1.0.0`.

## Details

The details page shows catalog identity, classification, status, version and assignment flags. Core modules are protected from delete operations.

