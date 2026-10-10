# Q358 — Shipment permission seed: temporary Admin widening + entitlement path

- Lane: Q358, integration-agent. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 17:04:40 UTC 2026` · `feature/mvp6-logistics` · HEAD `bf1ef28f9` · dirty 4 · staged 0.
- Step 0 read: `AGENTS.md` (`ce8c12ad80f93bb5`), `git-safety.md` (`4181c697b96dc9f8`), `code-style.md`
  (`6032fbb07819dc47`), `integration-agent.md` (`f760471e69efb7dc`), the decision record
  `docs/records/decisions/2026-10/mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md`, and the Q353
  REPORT (`0b92c2e35b896b04`).
- **Labels.** `MEASURED` = read from a database in the runs below. `CODE` = static reading. `NOT MEASURED` = not run.
- **Agent verdict ≠ CT ACCEPTED.** Nothing staged or committed. No key or token value is printed in this record:
  keys appear as length plus sha256/8; tokens are minted to mode-600 files under the scratch environment and never
  printed.

## Summary

| part | result |
|---|---|
| A | written below; it is the precondition for everything else outside a dev laptop |
| B | **MEASURED.** With the entry, tenant Admin holds 5/5 (System-sourced): on the default tenant after an Auth restart, and on a **non-entitled** new tenant at activation |
| B sabotage / C control | **MEASURED.** Without the entry, Admin holds 0/5 on both tenants |
| C, Admin through entitlement alone | **MEASURED, YES.** Without the entry, entitled tenant Admin holds **5/5, every grant `GrantSource=1` (Module) with `SourceModuleCode=shipment-tracking-pod`** |
| C, Viewer read keys | Viewer holds `read` — **MEASURED** — but only System-sourced. Whether the entitlement sync **alone** grants Viewer `read` is **NOT MEASURED**: the only trigger available here (`tenant-activated`) re-runs the System baseline first |
| C, broker-driven sync | **NOT MEASURED.** RabbitMQ is not installed (no `rabbitmq-server`, `docker` or `colima`). `tenant.entitlement.added.v1` was left pending in Platform's outbox, and Auth's holdings did not change |
| D | the operator step is written below, with idempotency **MEASURED** |
| SupplyChain suite | 432/1/433 on 3 of 4 Shipments runs. One run was 431/2/433 because a timing-boundary test failed. No delta attributable to this change |

**On the retraction condition.** The decision record's condition is "the entitlement path is **measured** granting
tenant Admin the full Shipment set and Viewer the read keys".

- The Admin half is measured here, without the list entry.
- The Viewer half is only partly measured: Viewer holds `read`, but not via entitlement alone.
- The trigger used was the HTTP `tenant-activated` replay, not the broker path.

**CT rules whether this satisfies the condition. This lane does not.** Q357 stays as CT sets it.

## A — what a non-Development environment must provide, and where

Every tracked config leaves these empty or local-only. They belong in the environment's **secret store / deployment
configuration**, injected as environment variables (ASP.NET Core `Section__Key`). **Never in a tracked file.** Q303
untracked `appsettings.Development.json` for this reason, and each tracked `*.example.json` keeps the key `""`.

| service | configuration key (env-var form) | what it gates | tracked default |
|---|---|---|---|
| Platform | `AuthService__InternalApiKey` | (1) accepting non-MDM manifests: `InternalModuleRegistrationController.cs:77-79, :96-109` — empty → **401**, SupplyChain stops without retrying; (2) the permission sync to Auth: `CatalogPermissionSyncService.cs:51-56`; (3) Auth's entitlement pull: `InternalTenantEntitlementsController` reads the same key | `""` |
| Platform | `AuthService__BaseUrl` | where (2) and the tenant-activation notifier go | `http://localhost:5056` |
| Auth | `InternalEventAuth__ApiKey` | accepting `/internal/permissions/sync` and `/internal/events/*` (`InternalPermissionsController.cs:49`). **Must equal** Platform's `AuthService:InternalApiKey` | `""` |
| Auth | `PlatformService__InternalApiKey`, `PlatformService__BaseUrl` | Auth's pull of entitled modules (`PlatformTenantEntitlementClient.cs:95`). Platform checks it against its `AuthService:InternalApiKey` | `""`, `http://localhost:5057` |
| SupplyChain | `PlatformRegistration__InternalApiKey`, `PlatformRegistration__BaseUrl` | sending the manifest. **Must equal** Platform's `AuthService:InternalApiKey` | `""`, `http://localhost:5057` |
| all three | `JwtSettings__Secret` | token validation; one shared value (Q287) | `""` |
| Platform + Auth | `Eventing__Transport=RabbitMQ`, `Eventing__Host`, `Eventing__Port`, `Eventing__VirtualHost`, `Eventing__Username`, `Eventing__Password` (secret), `Eventing__UseTls` | delivery of `tenant.entitlement.added.v1` to Auth's `EntitlementSyncConsumer`. Without it, an entitlement granted **after** tenant activation never reaches Auth (measured, C) | Platform `None`, Auth `InMemory` |

All five internal-key slots carry **one** value today. In Development it is len 45, sha256/8 `e96ab27d`
(Q339-R2 F-4). Rotating it means rotating all five.

## B — the widening

### The edit (the only repository change)

`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs` (+6/−1):

```
<   public static readonly IReadOnlyList<string> AdminModules = new[] { "access-governance", "legal-entity", "crm-account", "crm-contact" };
---
>   // Q358 — TEMPORARY, EXPIRES 2026-11-03. "shipment-tracking-pod" (MOD-0183) gives every tenant's Admin the five
>   // supplychain.shipments.* keys WITHOUT a Shipments entitlement, so MVP-6 can open now. It suspends the curated-list
>   // decision above for this one module only. Remove it once the entitlement path is measured granting Admin the same
>   // keys; by the expiry it is re-approved in writing or removed. Decision:
>   // docs/records/decisions/2026-10/mvp6-shipment-admin-baseline-temporary-widening-owner-decision-01.md
>   public static readonly IReadOnlyList<string> AdminModules = new[] { "access-governance", "legal-entity", "crm-account", "crm-contact", "shipment-tracking-pod" };
```

- sha256 before: `263a70310096043f2d1d21146b9dd1d66b6a76094c17c7deacb4cd81930aae43`
- sha256 after: `23e6768424a898d0a61b182ed62d10ced8c6db534169473316d23cd6403ab31e`

The widening covers exactly the five keys whose stored `Module` is `shipment-tracking-pod` (Q353). `SelectFor`'s Admin
clause also requires `Scope == Tenant`, and all five are Tenant. Nothing else in the catalogue carries that Module:
the B runs show `supplychain.*` = 5 and no Carrier key. Carrier is untouched.

### Runs

- Environment `~/mvp6-env/q358-20261003-2007/`. Two scratch copies:
  - `src-with`: the repository as edited; 0 mismatches against the repo over the copied tree.
  - `src-without`: the same copy with the pre-edit file restored. Diff = the edit only.
- One mongod on **57358**, a fresh `dbpath` per stack (`db-c`, `db-b`, `db-suite`). Each was stopped after use.
- Auth 5056, Platform 5057, SupplyChain 58358, all in Development.
- One throwaway JWT secret. A platform-admin token minted from it (`evidence/scripts/mint.py`): the claims are
  `actor_type`, `aud`, `exp`, `iat`, `iss`, `nbf`, `sub`, plus the seeded administrator's email (len 15).
- Every process was stopped by its own recorded pid (`stop.sh`). Ports 5056, 5057, 58358 and 57358 were free at the
  end.

Query: `evidence/scripts/q-holdings.js`. The `GrantSource` values in its output are `0` = System (baseline) and
`1` = Module (entitlement).

### Stack B (with the entry) — MEASURED

```
# b1 before registration
auth permissions total=411 supplychain.*=0 []
tenant 000000000001 Admin      holds 0/5
tenant 000000000001 SuperAdmin holds 0/5
tenant 000000000001 Viewer     holds 0/5
# b2 after SupplyChain registered (Attempt=1)
auth permissions total=416 supplychain.*=5 [supplychain.shipments.cancel, supplychain.shipments.create, supplychain.shipments.dispatch, supplychain.shipments.pod.capture, supplychain.shipments.read]
tenant 000000000001 Admin      holds 0/5
tenant 000000000001 SuperAdmin holds 5/5  cancel(0) create(0) dispatch(0) pod.capture(0) read(0)
tenant 000000000001 Viewer     holds 0/5
# b4 after restarting my Auth process (startup seeder)
tenant 000000000001 Admin      holds 5/5  cancel(0) create(0) dispatch(0) pod.capture(0) read(0)
tenant 000000000001 SuperAdmin holds 5/5  cancel(0) create(0) dispatch(0) pod.capture(0) read(0)
tenant 000000000001 Viewer     holds 1/5  read(0)
# b3 a NEW tenant registered through Platform's API, NOT entitled to Shipments (tenant_module_entitlements SHIPMENT rows=0)
tenant 2033a3a3ebf8 Admin      holds 5/5  cancel(0) create(0) dispatch(0) pod.capture(0) read(0)
tenant 2033a3a3ebf8 Viewer     holds 1/5  read(0)
```

The new tenant `…2033a3a3ebf8` was registered with `POST /api/admin/tenants` (201), plan STARTER. Platform's own
notifier then called Auth `tenant-activated`. Its Admin holds all five keys with **no** Shipments entitlement. That
is exactly the cost the decision record names.

### Sabotage (= C's control) — stack C (without the entry) — MEASURED

```
# c2 after registration
auth permissions total=416 supplychain.*=5 [...]
tenant 000000000001 Admin      holds 0/5
tenant 000000000001 SuperAdmin holds 5/5  cancel(0) create(0) dispatch(0) pod.capture(0) read(0)
tenant 000000000001 Viewer     holds 0/5
# c4 new tenant registered through Platform (201), not yet entitled
tenant 1cbd7c866c54 Admin      holds 0/5
tenant 1cbd7c866c54 Viewer     holds 1/5  read(0)
```

Without the entry, Admin drops to 0, on both the default and the new tenant.

## C — the entitlement path, with no `AdminModules` entry

1. **Entitle the tenant through Platform's API** (MEASURED).
   `POST /api/platform/tenants/{id}/commercial/module-entitlements` with
   `{"moduleCode":"SHIPMENT-TRACKING-POD","source":1,"isEnabled":true,...}` returned 201. Platform then holds
   `tenant_module_entitlements { TenantId: …1cbd7c866c54, ModuleCode: 'SHIPMENT-TRACKING-POD', Source: 1, IsEnabled: true }`.
2. **Without a broker** (MEASURED). Platform's `outbox_events` holds `tenant.entitlement.added.v1` with status 0
   (pending), next to 21 other pending events (`c6-outbox.txt`). **Auth's holdings are unchanged**: Admin 0/5,
   Viewer 1/5 (`c6-after-entitlement-no-broker.txt`). In production this event is what drives
   `EntitlementSyncConsumer`. **That broker path is NOT MEASURED.**
3. **HTTP trigger** (MEASURED). `POST /internal/events/tenant-activated` on Auth. The body has exactly the shape of
   Platform's `AuthServiceTenantActivationNotifier.cs:49-58`, with a fresh `eventId`, and was sent by this lane with
   the internal key. That runs `EnsureDefaultRolesAsync` and then `SyncEntitledModulesBestEffortAsync`
   (`InternalEventsController.cs:91-92`), which pulls the tenant's entitlements from Platform's real
   `entitled-modules-with-permissions` endpoint. Result:

```
{"status":"processed"}
tenant 1cbd7c866c54 Admin      holds 5/5  cancel(1:shipment-tracking-pod) create(1:shipment-tracking-pod) dispatch(1:shipment-tracking-pod) pod.capture(1:shipment-tracking-pod) read(1:shipment-tracking-pod)
tenant 1cbd7c866c54 Viewer     holds 1/5  read(0)
```

**Admin holds all five through entitlement alone.** This Auth build has no list entry. Admin held 0 immediately
before. Each grant is Module-sourced from `shipment-tracking-pod`.

**Viewer isolation attempt** (`c9-d-idempotency-and-viewer.txt`):

1. In the isolated database only, deleted the Viewer's System-sourced `read` row (`deleted=1`). Viewer: 0/5.
2. Replayed `tenant-activated`. Viewer is back to 1/5, but `read(0)`, System-sourced.

`EnsureDefaultRolesAsync` re-adds the baseline before the entitlement sync runs, so through this trigger Viewer's
`read` cannot be attributed to the entitlement. **Viewer-via-entitlement-alone: NOT MEASURED.**
`EntitlementPermissionSyncService.cs:17-21` (Viewer = read keys) is CODE only.

## D — Viewer's `read`: what an operator runs, and is it idempotent

CODE:

- Auth runs `DataSeeder.SeedAsync` on **every start**. It is called inside DI registration
  (`Diten.AuthService.Persistence/DependencyInjection.cs:88`).
- The seeder's role baseline (`AssignBaselineAsync`, `DataSeeder.cs:1008-1023`, called at `:879-888`) covers the
  **default tenant only**: `EnsureRole` picks `DefaultTenantId` (`:990-1003`).
- The seeder's only all-tenant reconcile (`ReconcileTenantAdminSelfServiceGrantsAsync`, `:1091-1126`) backfills Admin **self-service** platform keys. It does not
  touch Viewer and it does not touch module keys.
- So for every **other** tenant, Viewer's `read` arrives only through `RoleProvisioningService.EnsureDefaultRolesAsync`.
  That runs on `POST /internal/events/tenant-activated` or `tenant-admin-invited`, or through the entitlement sync.

Operator step, written down:

| situation | what to run |
|---|---|
| default tenant, after new keys first land | **restart Auth once** |
| any other tenant | re-send `tenant.activated` for that tenant: `POST {Auth}/internal/events/tenant-activated`, header `X-Internal-Api-Key`, a **fresh** `eventId`, body shape as `AuthServiceTenantActivationNotifier.cs:49-58` |
| entitled tenants, once the broker runs | nothing; it should happen when the entitlement event is consumed (NOT MEASURED) |

**Idempotency — MEASURED** (`c9-d-idempotency-and-viewer.txt`):

```
tenant rolePermissions rows=85
{"status":"processed"} HTTP 200          # replay with a new eventId
tenant rolePermissions rows=85           # no duplicate grants
{"status":"noop_duplicate"} HTTP 200     # same eventId resent
tenant rolePermissions rows=85
```

The seeder's baseline is idempotent by construction: existing pairs are skipped (`DataSeeder.cs:1015-1022`). Restarts
in B did not duplicate rows: Admin stayed at 5/5, never above.

## Suites

### SupplyChain vs Q335 (432/1/433)

Q335's `run-modules.sh` was used with only the port, replica-set name and paths changed (diff in
`evidence/suite/p-suite.txt`). One URI variable per module, all pointing at this lane's 57358.

| module | Q335 | Q358 run 1 | delta |
|---|---|---|---|
| Shipments | 90/0/90 | **89/1/90** | −1 (see below) |
| Carriers | 36/0/36 | 36/0/36 | 0 |
| Loads | 33/0/33 | 33/0/33 | 0 |
| Returns | 78/0/78 | 78/0/78 | 0 |
| Claims | 128/1/129 | 128/1/129 | 0 (same restart-mode test) |
| SandopPlans | 19/0/19 | 19/0/19 | 0 |
| CapacityPlans | 48/0/48 | 48/0/48 | 0 |
| TOTAL | 432/1/433 | **431/2/433** | −1 |

The extra failure is `ShipmentTelemetryTests.OperationDuration_WhenOperationsComplete_RecordsOneTaggedMillisecondValueEachSoP95CanBeReported`:
"p95 56.9 ms is below the 19th slowest delay". The test injects delays of 3…60 ms and asserts p95 ≥ 57 ms
(`ShipmentTelemetryTests.cs:61,71`) with **zero margin**, and a `Task.Delay(57)` was measured at 56.9 ms.

Three immediate re-runs of the Shipments module on the same build passed 90/90 each
(`p-suite-shipments-reruns.txt`). The SupplyChain sources in the copy are byte-identical to the repository. This
lane's only change is in AuthService, which the SupplyChain tests do not reference. **No delta attributable to this
change. A flaky boundary assertion: 1 failure in 4 runs.**

### AuthService role and entitlement tests

Filter `Roles|DefaultRolePermission|Entitlement`: **240/240 with the edit, 240/240 without**
(`p-auth-tests.txt`). No existing test notices the widening, so no test will notice its retraction either.

## Findings

- **F-Q358-1** C's Admin half is measured through entitlement alone. Viewer-via-entitlement-alone and the broker path
  are NOT MEASURED. Whether the retraction condition is met is CT's ruling.
- **F-Q358-2** An entitlement granted **after** activation never reaches Auth without the broker. Measured: the event
  stays pending in Platform's `outbox_events`, and Auth holdings are unchanged. Every environment needs part A's
  `Eventing__*` keys, or the operator step from D after each entitlement change.
- **F-Q358-3** With the entry, a non-entitled tenant's Admin holds all five Shipment keys at activation (measured,
  b3). This is the cost the decision describes; it is now evidenced.
- **F-Q358-4** No AuthService test pins the Admin breadth for `shipment-tracking-pod` in either direction. 240/240
  both ways.
- **F-Q358-5** `ShipmentTelemetryTests.cs:71` has a zero-margin timing assertion and fails intermittently (1 in 4
  here).
- **F-Q358-6** The decision record cites "Measured baseline before the change: Admin holds 0 of the 5 (Q355)". The
  measurement it refers to is Q353's (`mvp6-q353-permission-seed-discovery-01`, `db-roles-after-auth-restart.txt`).
  Ledger ids may differ; this is recorded, not corrected.
- **F-Q358-7** Platform plan IDs are generated per seed. The STARTER id differs between databases, so any recipe must
  look it up, not hard-code it.

## Not measured

- The broker path (`EntitlementSyncConsumer`), because RabbitMQ is not available on this machine.
- Viewer receiving `read` through the entitlement sync alone.
- Revoking an entitlement: not asked, not run.

Nothing committed, nothing pushed, nothing staged.
