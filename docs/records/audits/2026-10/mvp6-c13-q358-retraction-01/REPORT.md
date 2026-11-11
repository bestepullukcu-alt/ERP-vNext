# C-13 — Q358 retraction: the tenant Admin's Shipment keys through entitlement only, measured over the broker

- date: 2026-10-19
- branch: `feature/sce/mod-0183-q358-grant-removal` (from `origin/main` `0b2539258`)
- decision being retracted: `docs/records/decisions/2026-10/mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`
- owner rulings for this run: `docs/records/decisions/2026-10/mvp6-q358-retraction-owner-decision-01.md`
- XMC-001: AuthService is outside SCE; the repository owner consented in the MVP-6 Control Tower conversation, 2026-10-19.

## Summary

| | result |
|---|---|
| Broker path (the gap Q358 left open) | **MEASURED.** Platform API entitlement → `tenant_module_entitlements` → outbox `tenant.entitlement.added.v1` **Published** → RabbitMQ queue `EntitlementSync` (1 consumer, 0 left) → Auth `EntitlementSyncConsumer` (same EventId in `integrationEventInbox`) → tenant A's Admin holds **5/5, every grant Module-sourced** (`1:shipment-tracking-pod`), ~4 s after the API call. No HTTP trigger was used. |
| Control (no entitlement, no list entry) | tenant B's Admin **0/5**; tenant D (registered after the final rebuild) **0/5**. |
| Sabotage (list entry put back) | tenant C, **no entitlement**, Admin **5/5 System-sourced** (`(0)`) — the entry grants every tenant; removing it removes that. |
| **New finding — existing tenants keep the widening** | After the entry came out and Auth restarted, C still holds 5/5 System; replaying `tenant-activated` for C (the documented operator step) leaves it at 5/5. Default-role setup only **adds**; the entitlement reconcile prunes stale **Module** grants only (`EntitlementPermissionSyncService.cs:734`). Removing the code entry does **not** revoke what tenants activated since Q358 already hold. A data clean-up is needed — owner decision (see "Open"). |

## Stack (lane, Development)

- Environment `~/mvp6-env/c13-220606/`. MongoDB 8.0.18 replica set `rsc13` on `127.0.0.1:35994` (databases `c13_auth`,
  `c13_platform`, `c13_supplychain`); the operational 27017 untouched — its database count 67 before and after.
- RabbitMQ 4.4.0 (Homebrew), node `c13@localhost`, listener `127.0.0.1:5672` only, guest loopback-only, no plugins
  (`evidence/scripts/rabbitmq.conf`). Platform and Auth both logged `Bus started: rabbitmq://127.0.0.1/`.
- Platform 5057, Auth 5056, SupplyChain 5066 (manifest registration only), all `Eventing__Transport=RabbitMQ`, each Mongo
  setting overridden to the lane URI and refused if it names 27017 (`evidence/scripts/start.sh`). One throwaway JWT secret
  and one internal key in 600 files in the lane folder — not in this record. Platform admin token minted from them
  (`evidence/scripts/mint.py`, dev seed `DevSeeds__PlatformAdmin=true`).
- Tenants registered with `POST /api/admin/tenants`, plan FREE (no plan-feature mappings exist, so no plan-derived
  entitlement): A `…a0c8d5cd0c65` (entitled), B `…b8d2de805cb0` (control), C `…711c08aa273f` (sabotage control),
  D `…9dcf8dbb1eae` (final control). Requests and responses: `evidence/http/`.
- Holdings query: `evidence/scripts/q-holdings.js` — `GrantSource` `0` = System (baseline), `1` = Module (entitlement).
  The "/5" label is the query's; it now lists every `supplychain.*` key, the five Shipment ones by their short names.

## Runs (in order)

1. **Before any entitlement** (`m1-before-entitlement.txt`): A Admin 0, B Admin 0; `SHIPMENT` entitlement rows 0.
2. **Entitle A** (`POST /api/platform/tenants/{A}/commercial/module-entitlements`, 201) — `m2-after-entitlement.txt`,
   `m2-outbox-inbox.txt`, `m2-inbox-detail.txt`, `m2-rabbit-queues.txt`: A Admin 5/5 Module; B Admin 0/5. Outbox
   `tenant.entitlement.added.v1` status 2 = Published (`OutboxEventStatus`); Auth inbox row with the same EventId
   `38560d65-b2db-41e4-babc-dbb13c151faa`, written by the consumer.
3. **Sabotage** — the entry back in `AdminModules`, Auth rebuilt and restarted, tenant C registered
   (`m3-sabotage-with-entry.txt`): C Admin 5/5 System with 1 SHIPMENT entitlement row in Platform (A's).
   (A first sabotage attempt filled the disk and the lane mongod stopped; space was freed, the same data restarted
   intact and the run repeated.)
4. **Final** — the entry out again, Auth rebuilt and restarted, tenant D registered (`m4-final-without-entry.txt`):
   D Admin 0/5; A still 5/5 Module; C still 5/5 System.
5. **Operator replay for C** (`POST /internal/events/tenant-activated`, 200 processed; `m5-replay-c.txt`): C 5/5 System — unchanged.

## The change

`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs`: `"shipment-tracking-pod"`
removed from `AdminModules`; the Q358 comment replaced by a retraction note. G7's two temporary entries
(`sop-workflow-signoffs`, `capacity-planning`, same expiry) are **not** part of C-13 and are unchanged.

## Open — owner decisions

1. **Clean-up of existing tenants' System-sourced Shipment grants on the Admin role** — every tenant activated while the
   entry was in place holds them. Not done here. Shape (proposal, not approval): a one-off, audited, idempotent runner like
   `ExportGrantBackfillRunner`, removing `supplychain.shipments.*` grants with `GrantSource = System` from tenant Admin
   roles; tenants entitled to Shipments keep or regain them through entitlement (their Module grants). Needs measurement of
   the entitled-and-System overlap case before it runs.
2. G7 entries (S&OP / Capacity) — same expiry, separate decision.

## Not measured

- Linux / mongod 7.0.28 (local: macOS / 8.0.18; owner: 8.0 acceptable locally, CI is the measure).
- The grant behaviour for a tenant that held the System grants **and** is then entitled (the overlap case above).
