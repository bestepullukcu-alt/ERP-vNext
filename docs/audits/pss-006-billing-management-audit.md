# PSS-006 Billing Management Audit

## Scope
- Backend implementation and Platform admin frontend implementation.
- Domain entities, CQRS commands/queries, validators, MongoDB indexes, repository atomic guards, and Platform API controller were added under `Diten.Platform`.
- Platform admin UI was added under `/Platform/Billing` with compact DataTable pages, draft create/edit, details, issue and cancel actions.

## Financial Correctness Controls
- Draft invoices keep `InvoiceNumber = null`.
- Invoice number generation is restricted to the issue operation.
- Invoice issue uses tenant-year counter collection `billing_invoice_counters`.
- Payment application uses a guarded MongoDB update requiring `BalanceDue >= AppliedAmount`.
- Payment idempotency is enforced by unique index `TenantId + InvoiceId + IdempotencyKey`.
- Refund creation uses a guarded payment update requiring remaining refundable amount.
- Issued invoice content is immutable; update handler accepts only draft invoices.
- Billing plan revisions create new plan documents with incremented `PlanVersion`.
- Invoice stores plan snapshot fields.
- Monetary rounding uses `MidpointRounding.AwayFromZero` with 2 decimal places.

## Gateway Note
- `gateway/Diten.ApiGateway/**/ocelot.json` is a protected path in `AGENTS.md`.
- No gateway route was edited by this implementation.
- Integration-agent route addition remains required before external gateway smoke testing.

## Frontend Controls
- DataTable static contract passed: `python3 .antigravity/scripts/verify_datatable_page.py . --area Platform --module Billing --reference compact --api-profile proxy`.
- Browser JavaScript uses same-origin proxy endpoint `/Platform/Billing/api`.
- Browser JavaScript does not read cookies or create bearer tokens.
- Platform admin menu link was added to `_LayoutPlatformAdmin.cshtml`.
- Billing invoices are not deletable; generic bulk-delete verifier tokens are documented as an intentional financial exception in `index.js`.

## Verification
- `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Debug` could not run because `dotnet` is not available in the shell PATH.
- `dotnet build frontend/Diten.Web/Diten.Web.csproj -c Debug` could not run because `dotnet` is not available in the shell PATH.
