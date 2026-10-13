# Q201 — Shared-seam patch needs (reported separately; nothing applied)

These paths are **not** in `ADD-OVERWRITE-CONFLICT.tsv`. They belong to one integration-agent lane, not to this one:

- SR-D4 — shared files are changed "only at final integration by the single integration owner":
  `docs/records/audits/2026-09/mvp6-ct-verdicts-q59-srd4-2026-09-26.md:4`
- SOP §16.4 single-writer rule and K15 — `docs/guides/operations/control-tower-sop.md:805-816`
- `ocelot.json` is changed only by `integration-agent` — `AGENTS.md:107`

This file describes the patch each seam would need and stops there. Row-level hashes: `SHARED-SEAM-ROWS.tsv`
(66 rows: 2 CONFLICT, 8 OVERWRITE, 56 IDENTICAL). Draft hand-off files: `SHARED-INTEGRATION-MEMBERS.tsv`.
Line counts below are from an in-memory line comparison of the working-tree file with the archive member whose
sha256 equals the manifest postimage.

## 1. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` — CONFLICT (three-way)

| Version | sha256 | Lines | What it is |
|---|---|---:|---|
| HEAD blob | `74d2d2007cb8…` | 57 | committed baseline |
| Working tree (` M`) | `7fdb5ef0d322…` | 73 | HEAD + Loads and Carriers registration (hand edit; same hash as the Q198 backup spot check V3c, `mvp6-ct-verdicts-q198-2026-10-02.md:32`) |
| BASE (L2 A12-360) | `33027bcd65b7…` | 123 | `BASE-MANIFEST.tsv` row; member of `mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` |

- Working tree vs HEAD: +17 −1. Adds 7 `using` lines (Loads, Carriers), `AddCarrierPersistence()`,
  `AddLoadPersistence()`, the `ILoadReferenceReader` HttpClient, two `INVALID_REQUEST` branches (Load, Carrier),
  and replaces the single `UseMiddleware<ShipmentContextMiddleware>()` with three `UseWhen` branches.
- BASE vs working tree: **+52 −2**. The two removed lines are replaced by longer forms of the same statement.
  BASE keeps every working-tree line and adds:
  - `using` lines for Returns, Claims, CapacityPlans, SandopPlans and `Api.ModuleRegistration`;
  - Kestrel strict UTF-8 selector for the `Idempotency-Key` header;
  - `AddClaimPersistence`, `AddReturnPersistence`, `AddSandopPersistence`, `AddCapacityPersistence`, the two Capacity
    fixture readers, `CapacityEvaluationExecutor`, the S&OP demand-fixture reader, Return and Claim reference readers;
  - the registration foundation: `PlatformRegistrationOptions`, `AddHttpClient()`,
    `CarrierManagementManifestProvider`, `ShipmentTrackingPodManifestProvider`, `ModuleRegistrationHostedService`;
  - `INVALID_REQUEST` branches and `UseWhen` middleware branches for Capacity, Returns, Claims.
- **Patch needed (not written):** move the file from the working-tree version to the BASE version. By line
  comparison BASE is a superset of the working-tree edit; that is a textual observation, not a build result.
- **Then, on top of BASE,** one provider line per UI draft (each draft's `supplychain-Program.cs.patch.txt`):

  | Draft | Line the draft specifies |
  |---|---|
  | S&OP v3 | `builder.Services.AddSingleton<IModuleManifestProvider, SopWorkflowSignoffsManifestProvider>();` |
  | Claims v4 | `builder.Services.AddSingleton<IModuleManifestProvider, ClaimsManagementManifestProvider>();` |
  | Capacity v3, Returns v3 | one line each, same form; see the member in each archive |

  Each note says: the line exists exactly once (guard F-06), ships only with that module's UI (DCP-009 §21.4), and
  lands in the same change as the nav keys (W-03). These four lines are in **no** layer of BASE-STACK v2.
- Authoritative side for the three-way: no record decides this file by name. SR-D4 assigns the work; it does not
  pick a version. **Insufficient evidence — CT decides.**

## 2. Other `Program.cs` files — OVERWRITE (working tree = HEAD or = BASE)

| Path | Layer | Change |
|---|---|---|
| `frontend/Diten.Web/Program.cs` | BASE L2 | +26 −0. Adds `using Diten.Web.Security` and the `OnRedirectToLogin` JSON-challenge branch for `JsonAdapterEndpointAttribute`. All four UI drafts depend on it (their `frontend-Program.cs.note.txt`); none of them edits this file. |
| `services/Diten.CrmService/src/Diten.CrmService.Api/Program.cs` | BASE L2 | +1 −1, a comment: port 5061 → 5065. |
| `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs` | BASE L3 | +4 −0. `PlatformServiceIdentityOptions` + `IPlatformServiceTokenValidator`. |
| `services/Diten.HumanCapitalService/src/…Api/Program.cs` | Q117 | +2 −1. `ClockSkew = JwtValidationDefaults.ClockSkew`. Working tree = BASE postimage. |
| `services/Diten.TalentEcosystemService/src/…Api/Program.cs` | Q117 | +2 −1. Same change. |

Patch needed: replace each with its layer postimage. The preimage is clean in all five.

## 3. Common DI / pipeline registration — OVERWRITE (working tree = HEAD)

| Path | Layer | Change |
|---|---|---|
| `services/Diten.AuthService/src/Diten.AuthService.Infrastructure/DependencyInjection.cs` | BASE L3 | +6 −0. HttpClient registration for `ITenantLegalEntityScopeClient`. |
| `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs` | BASE L3 | +4 −0. `MdmServiceIdentityOptions`, `IInternalScopeResolutionContext`, `IMdmServiceIdentityTokenProvider`. |

39 other registration files matched the pattern and are already identical. Supply Chain has no separate
registration file in the stack; its registration is `Program.cs` (§1).

## 4. Gateway — `gateway/Diten.ApiGateway/ocelot.json` — OVERWRITE (working tree = HEAD)

- BASE L2 vs working tree: +129 −35. Routes 269 → 275.
  - Working tree: 35 routes on port 5061, all `/api/crm/…`; no Supply Chain route.
  - BASE: the CRM routes move to 5065; 5061 carries 6 `/api/shipment-bundle/` routes (shipments ×4, carriers ×2).
- Not in any layer — each UI draft carries a fragment for the integration owner:

  | Draft | Fragment | Routes (all → port 5061) |
  |---|---|---|
  | S&OP v3 | `gateway-sandop-routes.ocelot-fragment.json` | 4 × `/api/supply-chain/sandop-plans…` |
  | Capacity v3 | `gateway-capacity-routes.ocelot-fragment.json` | 6 × `/api/supply-chain/capacity-plans…` |
  | Returns v3 | `gateway-returns-routes.ocelot-fragment.json` | 2 × `/api/shipment-bundle/returns…` |
  | Claims v4 | `gateway-claims-routes.ocelot-fragment.json` | 2 × `/api/shipment-bundle/claims…` |

- Related, in the bulk list as OVERWRITE: `gateway/Diten.ApiGateway.Tests/OcelotConfigurationTests.cs` (BASE L2).
  Claims carries `gateway-tests-ocelot-count.patch.txt`: `Assert.Equal(6, supplyChainRoutes.Length)` → `8`,
  to be applied with the Claims routes. Returns adds two more `/api/shipment-bundle/` routes and has no such patch
  (F-Q201-8).
- Patch needed: (1) the BASE postimage; (2) merge of the four fragments, 14 routes; (3) the route-count guard set to
  the count that results. Port 5061 = SupplyChain Service, `AGENTS.md:86`.

## 5. Shared permission catalog / seed

- Six files matched; all are already identical (`PpmPermissionCatalog.cs` and five `*PermissionSeedTests.cs` in
  AuthService, BASE L3). No patch needed for them.
- The drafts add no key. S&OP `platform-registration.md` item 4: "the four existing keys only … Role seeding … is a
  platform/environment step, not a new key". Claims item 3 asks for a role-template grant
  (`supplychain.shipments.read`, G-SHIPREAD).
- Patch needed: none found in a file. The role/seed steps in each draft's `platform-registration.md` are checklist
  items for the integration owner. Whether a Supply Chain role seed file exists: **insufficient evidence**.

## 6. Canonical SANDOP / DEMAND artifacts

| Path | State | Note |
|---|---|---|
| `docs/analysis/contracts/sandop-capacity.openapi.yaml` | **CONFLICT** — LOCAL-EDIT, stack unchanged | Stack = HEAD blob. Working tree (` M`) is +283 −45 against HEAD: `version: 1.0.0` → `3.0.0`, adds `x-semantics-annex: sandop-capacity-semantics-v3.0.0.md` and 400/401/403/503 responses. |
| `docs/analysis/contracts/demand.openapi.yaml` | IDENTICAL | no patch |

- Patch needed from the stack: **none** — the stack does not change this file. The risk is the reverse: writing the
  stack copy would replace the v3.0.0 contract with v1.0.0.
- Authoritative side: no record found that names this file. **Insufficient evidence — CT decides.**

## 7. Adjacent shared files (left in the bulk list, flagged in its `shared_seam` column)

Outside the Q201 seam list, but SR-D4 names them as shared (`.csproj`, `SharedResource`):

- 7 × `frontend/Diten.Web/Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx` — OVERWRITE, BASE L2. Each UI draft
  then adds 2 nav-key rows × 7 languages through `sharedresource-nav-keys/*.fragment.xml` (in no layer).
- 3 × `.csproj` — `Diten.SupplyChainService.Api.csproj` (BASE L2), HumanCapital and TalentEcosystem Api (Q117).
- Other checklist items in the drafts, no file in any layer: `icon-map.proposal.md`
  (`frontend/Diten.Web/tests/diten-field-icons.test.js`), and the DCP-009 §21.1 exclusion row for MOD-0190.
  `DCP-009-supply-chain-inventory.md` is itself a LOCAL-EDIT conflict row in the bulk list.

Stop. Nothing above was applied.
