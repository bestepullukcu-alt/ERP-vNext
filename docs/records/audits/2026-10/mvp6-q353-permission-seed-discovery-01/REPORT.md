# Q353 — Shipment permission seed: discovery

- Lane: Q353, integration-agent, discovery only. Recorded 2026-10-03.
- Preflight: `Sat Oct  3 16:47:31 UTC 2026` · `feature/mvp6-logistics` · HEAD `bf1ef28f9` · dirty 2 (`CT-QUEUE.tsv`,
  one untracked `restart.env` of another lane) · staged 0.
- Step 0 read: `AGENTS.md`, `git-safety.md` and `integration-agent.md` (hashes unchanged since this session's Q339
  reads), and the MOD-0183 pack (`2285fe0d…`).
- **No repository file changed.** Every service ran from a scratch copy in `~/mvp6-env/q353-20261003-1948/`: 3,787
  files, 0 mismatches against the repo. No key or token value is printed here; keys appear as length plus sha256/8
  only.
- **Labels.** `MEASURED` means read from a database in the run below. `CODE` means a static reading only.

## Run (MEASURED)

| item | value |
|---|---|
| mongod | `127.0.0.1:57353`, replica set `rsq353`, isolated `dbpath`, stopped at the end |
| Auth | 5056, Development, DB `q353_auth` |
| Platform | 5057, Development, DB `q353_platform` |
| SupplyChain | 58353, Development, DB `q353_supplychain` |
| JWT | one throwaway 64-byte secret shared by all three, never printed |
| internal keys | the copies' own `appsettings.Development.json` (untracked) |
| RabbitMQ | absent: `/health` = 503 on Auth and Platform; both listened and served the internal routes |

1. Auth and Platform start and seed.
2. Baseline query of the Auth catalogue (below).
3. SupplyChain starts and registers: `Module manifest self-registered with Platform. ModuleCode=shipment-tracking-pod
   Attempt=1`.
4. Platform reconciles the manifest and syncs each key to Auth.
5. Query Auth and Platform.
6. Restart **my** Auth process (pid 68937 → 69820) so the startup seeder runs again, then query roles again.
7. Stop my three processes and mongod. Ports 5056, 5057, 58353 and 57353 were free afterwards.

Queries: `evidence/queries/q-auth.js`, `q-roles.js`, `q-platform.js`. Raw outputs: `evidence/db/*.txt`.

### Auth catalogue before registration

```
permission collection: permissions  total docs: 411
supplychain.* permissions: 0
```

### Auth catalogue after registration (`db-auth-after.txt`)

```
permission collection: permissions  total docs: 416
supplychain.* permissions: 5
{ Key: 'supplychain.shipments.cancel',      Module: 'shipment-tracking-pod', Resource: 'shipments',     Action: 'cancel',   Scope: 0, IsSystem: false, IsDeleted: false, DisplayName: 'Cancel Shipment' }
{ Key: 'supplychain.shipments.create',      Module: 'shipment-tracking-pod', Resource: 'shipments',     Action: 'create',   Scope: 0, IsSystem: false, IsDeleted: false, DisplayName: 'Save' }
{ Key: 'supplychain.shipments.dispatch',    Module: 'shipment-tracking-pod', Resource: 'shipments',     Action: 'dispatch', Scope: 0, IsSystem: false, IsDeleted: false, DisplayName: 'Change Status' }
{ Key: 'supplychain.shipments.pod.capture', Module: 'shipment-tracking-pod', Resource: 'shipments.pod', Action: 'capture',  Scope: 0, IsSystem: false, IsDeleted: false, DisplayName: 'Capture POD' }
{ Key: 'supplychain.shipments.read',        Module: 'shipment-tracking-pod', Resource: 'shipments',     Action: 'read',     Scope: 0, IsSystem: false, IsDeleted: false, DisplayName: 'Shipment Details' }
```

(Reformatted to one line per document; the field values are unchanged. `Scope: 0` = `PermissionScope.Tenant`,
`Domain/Entities/…PermissionScope` `Tenant = 0`.)

### Platform descriptors after registration (`db-platform-after.txt`)

- `platform_module_page_descriptors`, 3 rows: `SHIPMENTS` and `SHIPMENT_DETAILS` → `supplychain.shipments.read`;
  `SHIPMENT_CREATE` → `supplychain.shipments.create`.
- `platform_module_page_action_descriptors`, 4 rows: `SAVE` → `create`, `CHANGE_STATUS` → `dispatch`,
  `CANCEL` → `cancel`, `CAPTURE_POD` → `pod.capture`.
- `platform_module_catalog`: `SHIPMENT-TRACKING-POD`, `IsTenantAssignable: true`, `IsBaseline: false`.
- `tenant_module_entitlements`: total 0, for SHIPMENT 0.

### Role grants (`db-roles-after-sync.txt`, then `db-roles-after-auth-restart.txt`)

Immediately after the sync:

```
SuperAdmin @tenant 00000000-0000-0000-0000-000000000001 -> supplychain.shipments.cancel, supplychain.shipments.create, supplychain.shipments.dispatch, supplychain.shipments.pod.capture, supplychain.shipments.read
SuperAdmin: roles=1 supplychain grants=5
Admin: roles=1 supplychain grants=0
Viewer: roles=1 supplychain grants=0
```

After restarting Auth (the DataSeeder runs `AssignBaselineAsync`, `DataSeeder.cs:1008-1023`):

```
SuperAdmin @tenant 00000000-0000-0000-0000-000000000001 -> supplychain.shipments.cancel, supplychain.shipments.create, supplychain.shipments.dispatch, supplychain.shipments.pod.capture, supplychain.shipments.read
Viewer @tenant 00000000-0000-0000-0000-000000000001 -> supplychain.shipments.read
SuperAdmin: roles=1 supplychain grants=5
Admin: roles=1 supplychain grants=0
Viewer: roles=1 supplychain grants=1
```

## A — which of the six keys exist in the Auth catalogue, and which were skipped where (MEASURED)

| key | in Auth | where it came from / why not |
|---|---|---|
| `supplychain.shipments.read` | **yes** | pages `SHIPMENTS` and `SHIPMENT_DETAILS` (`ShipmentTrackingPodManifestProvider.cs:29,:52`) |
| `supplychain.shipments.create` | **yes** | page `SHIPMENT_CREATE` (`:39`) and action `SAVE` |
| `supplychain.shipments.dispatch` | **yes** | action `CHANGE_STATUS` |
| `supplychain.shipments.cancel` | **yes** | action `CANCEL` |
| `supplychain.shipments.pod.capture` | **yes** | action `CAPTURE_POD`; Auth stored it as Resource `shipments.pod`, Action `capture` |
| `supplychain.shipments.reconcile` | **no** | **skipped at no guard.** It was never sent: no page or action declares it (Platform holds 0 descriptors with it). This matches pack §22 M-03 ("API-only"), and no endpoint uses it (Q332) |

No key hit the canonical-format guard or the unconfigured-Auth guard in this run. Auth's log lists all five as
`synced (created)`, and `create` and `read` additionally as `(updated)`
(`evidence/auth-run1-supplychain-lines.log`).

## B — is Platform configured with Auth BaseUrl and InternalApiKey? (CODE + MEASURED)

**In Development, yes.** The untracked `services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json`
carries `AuthService.BaseUrl = http://localhost:5056` and `AuthService.InternalApiKey` with len 45, sha256/8
`e96ab27d`. That equals Auth's `InternalEventAuth.ApiKey` (len 45, `e96ab27d`) and SupplyChain's registration key.
The run proves the configuration works: 5 keys landed.

**In every other environment, no.** The tracked `appsettings.json` and `appsettings.Development.example.json` both have
`AuthService.InternalApiKey` empty. Outside a hand-made Development file:

- The sync fails at `CatalogPermissionSyncService.cs:51-56`, with `LogError` and `return Failed`.
- **Registration itself is also refused.** `InternalModuleRegistrationController.cs:77-79` authorises non-MDM manifests
  through `IsLegacyInternalRequestAuthorized()` (`:96-109`), and that compares against the same
  `AuthService:InternalApiKey`. An empty key → 401 → SupplyChain stops without retrying.

So "the sync skips silently" is wrong in both halves. It is not silent (`LogError`), and with an empty key the
manifest never gets far enough to sync.

## C — given the five keys that land, who can hold them? (MEASURED for the default tenant; CODE for the rest)

| role | outcome | why |
|---|---|---|
| SuperAdmin (default tenant) | **all 5**, granted at creation | `InternalPermissionsController.cs:96` → `GrantToFullCatalogRolesAsync`; `SelectFor` `:109` full catalog |
| Admin | **0 of 5** | `SelectFor` `:117-120` requires `Scope == Tenant` **and** `Module ∈ AdminModules` (`:36` = access-governance, legal-entity, crm-account, crm-contact). The stored Module is `shipment-tracking-pod` — absent. None of the keys is in `TenantSelfServicePermissions` (`:54-67`) |
| Viewer | **`read` only, but only after an Auth restart** | `SelectFor` `:127-132`: Scope Tenant, Action `read`, not self-service, not in `EntitlementOnlyViewerPermissions` (`:73-92`). The live sync grants only to full-catalog roles. Viewer receives `read` when `DataSeeder.AssignBaselineAsync` (`:1008`) or `RoleProvisioningService.EnsureDefaultRolesAsync` (`:31-53`) runs |

Per key, for a tenant role, whether it can be **manually** assigned: all five are `Scope = Tenant`, so
`IsTenantAssignable` (`:150-151`) is true and manual assignment is permitted (CODE).
`ExplicitGrantOnlyPermissions` (`SelectFor` `:104`) holds none of them (CODE).

**The second path, which CT's trace did not name (CODE, not measured).** `EntitlementPermissionSyncService.cs:17-21,
:53-69, :124-126` grants a module's permissions when a tenant is entitled to that module: **Admin gets the full set,
Viewer gets the read keys.** It resolves by `Permission.Module == normalize(ModuleCode)` (`ModulePermissionResolver.cs:50-51,
:93-102`). `shipment-tracking-pod` matches the stored Module exactly. It is triggered by `InternalEventsController.cs:62`
(`tenant-activated`), `:97` (`tenant-admin-invited`) and the entitlement event consumer. In this run
`tenant_module_entitlements` was 0, and the consumer needs RabbitMQ, so the path was not exercised.

**Therefore `AdminModules` (`:36`) is not the only door.** It is the entitlement-independent baseline. Adding
`shipment-tracking-pod` there gives every tenant's Admin the Shipment keys whether or not the tenant is entitled to
Shipments. Leaving it out makes the entitlement the gate.

## D — Q279: a Return or Claim creator also needs `supplychain.shipments.read` (CODE) — CONFIRMED

- `ReturnReferenceReader.cs:72-73` sends `GET api/shipment-bundle/shipments/{id}` with **the caller's own**
  `Authorization` header.
- `ClaimReferenceReader.cs:91,:115` does the same.
- That endpoint is `ShipmentsController.cs:21-22` `[HttpGet("{shipmentId:guid}")] [HasPermission(ShipmentPermissions.Read)]`.
  A caller without `read` gets 403.
- `ReturnReferenceReader.cs:83` maps that 403 to **503 `DEPENDENCY_UNAVAILABLE`**, so the user sees "unavailable",
  not "forbidden".

Additional finding: when the claim names a carrier, `ClaimReferenceReader.cs:123-127` also calls
`GET api/shipment-bundle/carriers` with the caller's token. That needs `supplychain.carriers.read`
(`CarrierPermissions.cs:4`). In this run Auth held **0** `supplychain.carriers.*` keys (MEASURED), because the Carrier
provider is unregistered (MOD-0184:473). So a claim with an explicit carrier cannot succeed for any role until Carrier
ships.

## E — the ordered edits a seed needs (none made)

| # | file | edit | unblocks | note |
|---|---|---|---|---|
| 1 | deployment secret store for Platform (not a tracked file) | supply `AuthService:InternalApiKey` and `AuthService:BaseUrl` in every non-Development environment | §18.0 row 2 (No-shell) beyond a dev laptop | Without it, registration is refused (401) **and** the sync fails. Tracked files must keep the key empty (Q303) |
| 2 | **owner decision first**, then either (a) a Platform `tenant_module_entitlements` record per tenant for `SHIPMENT-TRACKING-POD`, or (b) `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/DefaultRolePermissionTemplate.cs:36` + `"shipment-tracking-pod"` | gives tenant Admin the 5 keys | row 2, and row 8 (RBAC) for a non-SuperAdmin user | (a) is the entitlement-gated path the code already has. (b) widens every tenant's Admin regardless of entitlement. The comment at `:32` records "Owner decision: the Admin baseline stays a curated module list". This lane recommends (a) and makes neither |
| 3 | none (operational) | after keys first land, run Auth startup or tenant provisioning once, so Viewer gets `read` | row 8 for Viewer | measured: Viewer got `read` only after the restart |
| 4 | owner decision: entitlement bundling for Returns (MOD-0186) and Claims (MOD-0187) | a tenant entitled to Returns or Claims must also be entitled to Shipments, or its users get 503 on create | rows 2 and 8 for MOD-0186/0187 | D above; pack MOD-0183 §22 open gap 4 (G-SHIPREAD) names this |
| 5 | `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Features/Returns/ReturnReferenceReader.cs:83` (and the Claims equivalent) | map 401/403 from the shipment read to a forbidden-class error, not 503 | row 12 (UX states, honest denial) | separate fix, not part of a seed |
| 6 | none | `supplychain.shipments.reconcile` | — | API-only by pack (§22 M-03). It reaches Auth only if a page/action declares it or it is hand-seeded. No endpoint uses it today |
| 7 | none | the four-segment-key question | — | **not real.** Platform accepts ≥3 segments (`ModulePageDescriptorNormalizer.cs:66-73`); Auth parses first = module, last = action, middle = resource and rebuilds the key losslessly (`PermissionKeyParser.cs:39-41`, `InternalPermissionsController.cs:67`). Measured: stored as `shipments.pod` / `capture` with the same Key |
| 8 | `…/ModuleRegistration/ShipmentTrackingPodManifestProvider.cs` (optional, cosmetic) | the permission DisplayName is last-writer-wins: `create` shows "Save", `read` shows "Shipment Details" | none | measured; Auth `(updated)` lines |

Carrier keys (`supplychain.carriers.*`) stay out until MOD-0184:473 is satisfied. That is a finding, not an edit.

## CT facts

| # | verdict | detail |
|---|---|---|
| 1 | **CONFIRMED** | `RegisterModuleManifestCommandHandler.cs:168` (page), `:203` (action) call `TrySyncPermissionAsync` (`:488`) |
| 2 | **PARTLY FALSE** | POST to `{Auth}/internal/permissions/sync` at `:61-63` — CONFIRMED. Canonical guard at `:43-48` (warning, `InvalidFormat`) — CONFIRMED. Unconfigured guard: the check is at **`:51`**; `:53-55` is the log call; it is **not silent** (`LogError`, `Failed`). Catalog save unaffected — CONFIRMED, but `:95` is only the exception-path log; the non-throwing paths return before any throw, by design (`:13-14`). Also missed: with an empty key the manifest itself is refused at `InternalModuleRegistrationController.cs:77-79` |
| 3 | **CONFIRMED with an addition** | `:36` exact; `supplychain` absent. `ClassifyScope :44`, `TenantSelfServicePermissions :54`, `EntitlementOnlyViewerPermissions :73`, `SelectFor :99`, `IsPlatform :140`, `IsTenantAssignable :150` all exact. Omitted gate: `ExplicitGrantOnlyPermissions` (`:104`). Omitted grant path: `EntitlementPermissionSyncService` (see C). `ClassifyScope` does not decide these keys' scope: the sync passes Scope explicitly (`Tenant`, from the route) |
| 4 | **CONFIRMED, the concern is FALSE** | pages declare `RequiredPermission` at `:29, :39, :52` (read, create, read): **two** distinct keys, not three. Actions declare create, dispatch, cancel and pod.capture, so 5 distinct keys reach the catalogue and `reconcile` does not. `pod.capture` passes `:43` (≥3 segments) and is stored losslessly |

## Pack note

The dispatch asks for "MOD-0183 §18.0 row 2". The pack has no §18.0. SOP §18.0 is the slice gate; the pack's §18 is
the Ready-for-dev checklist. Row 2 is used here in the sense of Q231/Q332: SOP §18.0 row 2, No-shell.

## Findings

- **F-Q353-1** An empty `AuthService:InternalApiKey` refuses SupplyChain's registration with 401
  (`InternalModuleRegistrationController.cs:77-79, :96-103`), not only the permission sync. Every tracked config has it
  empty.
- **F-Q353-2** Tenant Admin holds 0 Shipment keys, measured. Only the entitlement path (CODE) or an `AdminModules` edit
  changes that. Owner decision.
- **F-Q353-3** Viewer receives `read` only after the next seeder or provisioning run, not at sync time. Measured.
- **F-Q353-4** A Return or Claim creator needs `supplychain.shipments.read`, and lacking it surfaces as 503
  `DEPENDENCY_UNAVAILABLE` (`ReturnReferenceReader.cs:83`).
- **F-Q353-5** A Claim with an explicit carrier needs `supplychain.carriers.read`, which exists nowhere in Auth
  (measured 0) until Carrier registers.
- **F-Q353-6** The permission DisplayName is overwritten by whichever descriptor syncs last ("Save" for `create`).
- **F-Q353-7** Housekeeping: this lane created an empty, mis-extracted log file in its own record folder
  (`evidence/auth-supplychain-lines.log`, 0 bytes) and moved it out to the scratch folder with `mv -n` rather than
  deleting it. Nothing else under the repository was touched.

## Not done

- Entitlement grant path: not exercised (no entitled tenant, no RabbitMQ).
- No token was minted, so no end-to-end 403 / 503 for D was observed. D is CODE.
- Platform's Mongo also received a `diten_background_jobs_dev` database: its background-jobs DB name came from the
  copy's config and was not overridden. It was in the isolated mongod only.

Nothing committed, nothing pushed, nothing staged.
