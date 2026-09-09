# Platform Administrators User Guide

Open `/Platform/Administrators` from the Platform Administration shell.

## Main Screen

The page shows KPI cards, inline filters and a DataTable v2 list. Use the toolbar to search, export, filter, save a personalized view or invite a new administrator.

## Invite Administrator

1. Select `Invite Administrator`.
2. Enter email and display name.
3. Choose `Platform Admin` or `Partner Admin`.
4. For `Partner Admin`, enter the partner id and one or more allowed tenant ids.
5. Select at least one role.
6. Save.

The record is created as `Active` with `Pending Invitation` metadata and a seven-day invite expiry.

## Edit And Quick View

Use the row actions to open quick view or edit. Quick view shows identity, role, invitation, scope and audit-ready metadata. Edit uses the same Slim offcanvas surface as create.

## Status And Invite Actions

Row actions support suspend, reactivate and resend invite. Suspend requires a reason. Accepted invitations cannot be resent.

## Concurrency

Updates and lifecycle actions include the persisted version. If another user changed the same administrator, reload the list and retry.
