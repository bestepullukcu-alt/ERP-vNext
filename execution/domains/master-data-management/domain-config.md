# Master Data Management - Domain Config

> This file records MDM-specific ownership and boundary decisions. Engineering implementation standards live in
> `.antigravity/rules/` and are referenced, not repeated here.

## Purpose

Master Data Management (MDM) owns ERP business master-data systems of record and the read-only contracts consumed
by Platform Shared Services and tenant business modules.

## In-Scope Reserved Module

- `MOD-0220 Corporate Secretarial / Entity Management`
  - First delivery slice: Legal Entity Foundation
  - Status: ready-for-dev for the explicitly approved minimal backend-only slice after schema reconciliation review.
  - Authoritative planning Excel mapping confirmed.
  - Authoritative Enterprise Blueprint repository migration pending as a non-blocking governance follow-up for
    the minimal backend slice.

## Domain-Level Owned Boundaries

- MDM owns the Legal Entity system of record for the Legal Entity Foundation slice.
- MOD-0040 is not the Legal Entity owner.
- MOD-0040 consumes Legal Entity only through a read-only `LegalEntityId` lookup / validation contract.
- MDM may own future ERP business reference-data catalogs only through separately approved module packs.

## Legal Entity Foundation V1 Decisions

- Legal Entity Foundation v1 uses a tenant-scoped only ownership model.
- `TenantId` is set from server-side tenant context.
- `TenantId` is not accepted from request body, DTO, form, or other client payload.
- Legal Entity records may be read and changed only within their tenant scope; cross-tenant access must fail closed.
- Global/shared and mixed ownership models are out of scope for v1.

Lifecycle boundary:

- V1 lifecycle states are `DRAFT`, `ACTIVE`, and `ARCHIVED`.
- Only `ACTIVE` Legal Entity records are referenceable.
- `IsDeleted` is technical soft-delete.
- `ARCHIVED` is a business lifecycle status.

MOD-0040 minimal validation is same-tenant only:

- `LegalEntityId` exists.
- `LegalEntity.TenantId` equals the current tenant ID.
- Legal Entity is referenceable.

## Country Boundary

- PSS-011 countries lookup is Platform provisioning/support only.
- PSS-011 is not the MDM business-country source of record.
- MDM business-country canonical ownership is a separate governance follow-up.
- Legal Entity Foundation must not silently default business-country ownership to PSS-011.

## Domain-Level Repo Scope

Governance scope for this milestone:

- `execution/domains/master-data-management/**`
- `execution/registries/module-id-registry.md`
- `execution/portfolio/master-development-plan.md`
- `execution/portfolio/blueprint-master-plan-reconciliation.md`

MOD-0220 Legal Entity Foundation is ready-for-dev for the explicitly approved minimal backend-only slice after
schema reconciliation review.

Authorized MDM-owned implementation scope:

- `services/Diten.MdmService/**`
- repo-standard `Diten.MdmService` test paths

Protected implementation boundaries for the first slice:

- `frontend/**`
- `gateway/**`
- other domain services

Gateway route changes require integration-agent and separate approved implementation scope.

## Current Governance-Milestone Restrictions

This governance milestone authorizes domain scaffold, module-pack promotion, and cross-document synchronization.

Not authorized in this milestone:

- frontend implementation
- gateway route implementation
- test-code implementation

After this renewed MOD-0220 promotion to ready-for-dev, the same milestone branch may execute the minimal
MDM-owned backend scope under `services/Diten.MdmService/**` and repo-standard `Diten.MdmService` test paths.

Frontend and gateway remain outside the first implementation slice. Gateway route changes require separately
approved scope and integration-agent ownership.

## Protected Paths

- `.antigravity/**` - do not edit without explicit user approval.
- other domains' `services/**` paths - not owned by MDM module packs.
- other domains' `execution/domains/**` paths - not owned by MDM module packs.
- archive / frozen paths - reference-only unless explicitly approved.
- `gateway/**` route modifications require integration-agent and approved implementation scope.
- `execution/domains/platform-shared-services/**` - protected except explicitly approved minimal reconciliation
  updates to DCP/MOD-0040 boundary references.

## Cross-Domain Dependencies

- MOD-0040 depends on the MDM Legal Entity read-only `LegalEntityId` lookup / validation contract.
- MOD-0018 / MOD-0018-FU15 own permission evaluation and data-scope resolution; MDM does not implement those
  algorithms.
- PSS-011 remains Platform provisioning/support lookup ownership and is not consumed as MDM business-country SoR.

## Runtime Decisions

- Production MDM service scaffold does not exist yet.
- `Diten.MdmService` is the service name referenced by repo standards for the authorized MOD-0220 minimal
  backend slice.
- MOD-0220 is ready-for-dev for the explicitly approved minimal backend-only slice after schema reconciliation
  review under `services/Diten.MdmService/**` and repo-standard `Diten.MdmService` test paths.
- Frontend and gateway remain outside the first implementation slice.
- MDM module packs reference global engineering standards instead of duplicating them:
  - `.antigravity/rules/module-pack-standard.md`
  - `.antigravity/rules/erp-architecture.md`
  - `.antigravity/rules/multi-tenancy.md`
  - `.antigravity/rules/entity-base-template.md`
  - `.antigravity/rules/security-jwt.md`
  - `.antigravity/rules/routes.md`

## Open Decisions

- Complete authoritative Enterprise Blueprint repository migration for `MOD-0220` as a non-blocking governance
  follow-up for the minimal backend slice.
- Decide whether Legal Entity v1 remains backend/contract-only or later includes UI.
- Define MDM business-country reference ownership in a separate follow-up.
