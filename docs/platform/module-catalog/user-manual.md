# Tenant Module Catalog User Manual

Use **Platform > Module Catalog** to maintain the platform-owned ERP module catalog.

## List

The list supports search, domain, service, status and tenant-assignable filters. Use **Add Module** to create a record. Row actions open details, edit, or delete the module.

## Create And Edit

The form contains the approved catalog fields:

- Module Code
- Module Name
- Display Name
- Description
- Domain
- Service
- Status
- Version
- Is Core Module
- Is Tenant Assignable
- Sort Order

`ModuleCode` is saved in canonical uppercase dash-separated format. Use semantic versions such as `1.0.0`.
On create, Module Code is generated from Display Name or Module Name. You can override it before saving. After the module is created, Module Code becomes read-only.

## Details

The details page shows catalog identity, classification, status, version, assignment flags, page descriptors, and assignment inspection.

## Pages And Actions

Use page management to document which application pages belong to a module and which actions are available on those pages.

## Assignments

The Assignments tab shows plan assignment data from subscription plans. Tenant assignment information may show a degraded state until a dedicated tenant assignment source is implemented. The screen does not invent tenant assignment rows from subscription plan data.

Core modules are protected from delete operations.
