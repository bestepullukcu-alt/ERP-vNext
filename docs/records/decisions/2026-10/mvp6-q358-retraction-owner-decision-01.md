# Owner decision — Q358 retraction conditions settled (C-13)

- date: 2026-10-19
- owner: ny@gmgroup.ch (repository owner), in the MVP-6 Control Tower conversation
- status: ACCEPTED
- amends the retraction condition of: `mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`
  (that record is unchanged)
- evidence: `docs/records/audits/2026-10/mvp6-c13-q358-retraction-01/`

## What was decided

1. **The Viewer clause is struck.** "…and Viewer the read keys" is not a retraction condition: Viewer receives `read` as a
   baseline before entitlement is evaluated (measured by Q358), so the clause asked for proof of a mechanism that is not
   the one in use.
2. **The broker path is measured, not waived.** RabbitMQ was installed on the dev machine for this; the retraction
   condition — the entitlement path granting tenant Admin the full Shipment set — is met over the production delivery
   path (Platform outbox → RabbitMQ → Auth `EntitlementSyncConsumer`).
3. **XMC-001 consent** to change `DefaultRolePermissionTemplate.cs` in AuthService for this retraction.

## Consequence

The list entry `"shipment-tracking-pod"` is removed from `AdminModules`. The retraction is not complete until the
open item in the evidence report — existing tenants' System-sourced grants — is decided and, if so decided, applied.
