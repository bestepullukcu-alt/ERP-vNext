# Work Package Checklist Template

## Purpose
This document provides the canonical template for work package delivery checklists, ensuring that every development chunk satisfies the required development governance gates.

## Scope
Provides checklist templates for developers and AI agents to fill out and execute during the development lifecycle of a work package.

## Not the source for
- Core code rules or linter guidelines (use `.antigravity/rules/`).
- Code review checklists (use Code Review Checklist Template).

## Current status
Placeholder.

## Source / migration note
New template matching the delivery layer of the Development Governance Matrix.

## Owner / update rule
- Owner: QA Lead / PMO
- Update Rule: Updated when global project delivery requirements or governance standards change.

---

# Template: Work Package Execution Checklist

## Pre-requisites
- [ ] Module ID is registered in `execution/registries/module-id-registry.md`.
- [ ] Module Pack is created under `execution/domains/{domain}/module-packs/` and marked as `approved` or `ready-for-dev`.
- [ ] Target branch name matches standard `feature/{domain-kısa}/{module-id}-{slug}`.

## Execution
- [ ] Code follows 5-layer architecture + CQRS standards in `.antigravity/rules/erp-architecture.md`.
- [ ] Tenant isolation rules applied (`TenantId` validation on all entities and requests).
- [ ] Database changes use soft delete (`IsDeleted`, `DeletedAt` fields).
- [ ] API responses are wrapped using the standard `Response<T>` envelope.

## Verification
- [ ] Automated unit and integration tests are written and passing.
- [ ] All verifier scripts (DataTable compliance, RESX localization) run successfully.
- [ ] Gateway routes configured in Ocelot `ocelot.json`.
