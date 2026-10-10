# Owner decision — tenant Admin holds the Shipment keys by BOTH paths, list first

- date: 2026-10-03
- owner: ny@gmgroup.ch (repository owner)
- status: ACCEPTED
- ledger: Q353, Q355, Q357
- evidence: `docs/records/audits/2026-10/mvp6-q353-permission-seed-discovery-01/`

## What was decided

`shipment-tracking-pod` is added to `DefaultRolePermissionTemplate.AdminModules`
(`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:36`)
so MVP-6 can open now, **and** the entitlement path is measured and proven in parallel.
Once the entitlement path is proven to grant tenant Admin the full Shipment set, the
list entry is **removed again**.

## What this overrides

The comment at `:32` records an earlier owner decision: *"the Admin baseline stays a
curated module list."* This decision does not revoke that principle — it suspends it for
one module, for a bounded period, and the retraction below restores it.

## What it costs while it stands

Every tenant's Admin role receives `supplychain.shipments.{read, create, dispatch,
cancel, pod.capture}` **whether or not that tenant is entitled to Shipments**. Measured
baseline before the change: Admin holds 0 of the 5 (Q355). A tenant that never bought
Shipments will have an Admin who can dispatch and cancel shipments and capture proof of
delivery. POD is a regulated record.

## Retraction — this is the part that is normally forgotten

| | |
|---|---|
| condition | the entitlement path (`EntitlementPermissionSyncService`) is **measured**, not read in code, granting tenant Admin the full Shipment set and Viewer the read keys |
| action | remove `"shipment-tracking-pod"` from `AdminModules:36` and re-measure that Admin still holds the keys **through entitlement only** |
| owner | ny@gmgroup.ch |
| expiry | **2026-11-03.** If the entitlement path is not proven by then, this decision does not lapse quietly — the widening is either re-approved in writing with a new expiry, or the list entry comes out and MVP-6 goes back to hand-assigned roles |
| ledger guard | Q357 stays open and RED until the retraction is measured. It does not close when the entry is added |

K4: a half-applied decision is worse than the defect. The widening and its retraction are
one piece of work, not two, and the first half does not count as done on its own.

## Amendment proposed 2026-10-03, after Q358 measured the retraction condition

Q358 ran the entitlement path and the condition above turns out to ask for two different
things, one of which does not describe how the system behaves.

**Proven.** Without the `AdminModules` entry, a tenant entitled to Shipments has its Admin
holding all five keys, every grant entitlement-sourced. That was the real doubt and it is
settled, measured from the database.

**Mis-specified.** The clause "and Viewer the read keys" cannot be measured the way it is
written: `EnsureDefaultRolesAsync` grants Viewer `read` as a baseline before entitlement is
evaluated, and restores it even when it is deleted first. Viewer gets `read` whether or not
the tenant is entitled, by a path that is measured and idempotent. The clause asks for proof
of a mechanism that is not the one in use. CT proposes striking it.

**Genuinely still missing.** Production delivers the entitlement change over RabbitMQ:
Platform queues `tenant.entitlement.added.v1` and Auth's `EntitlementSyncConsumer` reads it.
Q358 had no broker on the machine, so it triggered Auth's `tenant-activated` HTTP endpoint
instead — a different code path reaching the same grant logic. What is proven is the grant,
not the delivery.

**CT's ruling on the condition as written: NOT MET.** The widening stands, Q357 stays RED,
and the single remaining gap is the broker-delivered path. The owner decides whether to
strike the Viewer clause; CT does not amend an owner decision on its own.
