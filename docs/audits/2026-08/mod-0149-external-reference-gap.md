# MOD-0149 — Account External Reference — GAP (deferred)

**Date:** 2026-07-17 · **Status:** OPEN (backlog) · **Severity:** Medium · **Blocks release:** No

## Current state

The Account Create/Edit form (Integration section) exposes a single free-text **External Reference** field. On save it is
persisted by the backend as one `AccountExternalReference` record:

- `ExternalId` = the entered value
- `SourceSystem` = **hardcoded `"default"`** (the UI never asks which system the id came from)
- Uniqueness enforced on `(TenantId, SourceSystem, ExternalId)` → duplicate ⇒ 409
- Shown read-only on Details/360 as `ExternalId (SourceSystem)`

Backend already models a richer shape — `AccountExternalReference(SourceSystem, ExternalId, SourceEntity, DisplayName,
Notes)` and the create/update handlers accept a single `ExternalReferenceInput` — but the UI only wires one id with the
default source, and there is **no lookup-by-external-id query endpoint** yet.

## The gap (what we will fix later)

1. **SourceSystem selection (dropdown).** The user must be able to choose *which* external system the id belongs to,
   instead of the silent `"default"`. Source of the dropdown options: MOD-0048 published reference set (e.g.
   `external-source-system` / integration-source) — **not hardcoded**, consistent with the rest of the form.
2. **Multiple external references per account.** One account must support **N** external references (e.g. legacy-ERP id
   + e-commerce id + e-invoice id), each `(SourceSystem + ExternalId)` unique — not a single field. UI: an add/remove
   repeater in the Integration section; backend already keys on source+external so multiple rows are supported at the
   data layer.

## Follow-on (nice-to-have, same theme)

3. **Lookup-by-external-id endpoint** so integration/import flows can find the matching account by its external id
   (match-or-create / dedup) — the reason the uniqueness index exists. Currently stored but not queryable.
4. Optionally surface `SourceEntity` / `DisplayName` / `Notes` (already on the entity) in the UI.

## Scope note

Fixing 1–2 is UI + a small controller/view-model change (backend entity + handlers already support source + multiple
rows); item 3 is a new query endpoint. No Account business-logic change, no CRM local seed, no hardcoded fallback.

## Reference

- Field wiring: `frontend/Diten.Web/Views/CRM/Accounts/_Form.cshtml` (Integration section), `Controllers/CRM/AccountsController.cs` (`ToPayload`), `Models/CRM/AccountViewModels.cs` (`ExternalReference`, `ExternalReferenceInputPayload`).
- Backend: `services/Diten.CrmService/.../Handlers/CommandHandlers/CreateAccountHandler.cs` (`DefaultSourceSystem = "default"`), `Domain/Entities/AccountExternalReference.cs`.
