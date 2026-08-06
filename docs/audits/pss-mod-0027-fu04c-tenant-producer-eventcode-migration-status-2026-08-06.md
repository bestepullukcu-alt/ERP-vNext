# MOD-0027-FU04C Tenant Producer EventCode Migration - Status Reconciliation

## Metadata
- **Date:** 2026-08-06
- **Domain:** Platform Shared Services
- **Module:** MOD-0027-FU04C Tenant Producer EventCode Migration
- **Module pack:** `execution/domains/platform-shared-services/module-packs/MOD-0027-FU04C-tenant-producer-eventcode-migration.md`
- **Related prerequisites:** MOD-0027-FU04A Tenant Management Notification Event Opt-in, MOD-0027-FU04B EventCode Dispatch Adapter
- **Status:** Implementation evidence recorded; final closeout smoke pending

## Scope
This note reconciles governance/status after finding that FU04C runtime implementation already exists on the local branch. It records evidence only; it does not authorize or add runtime changes.

FU04C scope remains limited to three tenant producer flows:
- `AdminUserInvitationService` dispatches `tenant.user.invited`.
- `TenantLifecycleNotificationConsumer` dispatches `tenant.lifecycle.suspended`.
- `TenantLifecycleNotificationConsumer` dispatches `tenant.lifecycle.reactivated`.

## Implementation Evidence
| Flow | Evidence |
|---|---|
| Admin user invitation | `AdminUserInvitationService` uses `DispatchNotificationByEventCodeCommand` with `tenant.user.invited`; the invite flow does not directly call `QueueEmailNotificationCommand`; fail-soft behavior is preserved. |
| Tenant suspended | `TenantLifecycleNotificationConsumer` uses `tenant.lifecycle.suspended`; `TenantDisplayName` is added from the loaded tenant context; controlled catalog/validation failures are logged and swallowed per ReasonCode policy. |
| Tenant reactivated | `TenantLifecycleNotificationConsumer` uses `tenant.lifecycle.reactivated`; `TenantDisplayName` is added from the loaded tenant context; provider/transient failure behavior remains retry-capable. |
| Created tenant branch | Created-tenant notification remains template-key based and out of scope because FU04A did not define a matching `tenant.created` eventCode. |

## Validation Evidence
- Platform.API build: PASS.
- Focused Application tests: PASS, 29/29.
- Eventing test project: PASS.
- `git diff --check`: PASS.

## Boundaries Confirmed
- No FU04B adapter change.
- No `QueueEmailNotificationCommand` or handler change.
- No notification template or seed change.
- No Gateway, frontend, appsettings, migration, seed, fixture-data, or AuthService change.
- No workflow/document/import producer migration; those remain FU04D/FU04M follow-ups.

## Remaining Blocker
FU04C must not be marked done until live closeout smoke proves invite, suspend, and reactivate dispatch through the live runtime path using dispatch record, log, or Mongo evidence.
