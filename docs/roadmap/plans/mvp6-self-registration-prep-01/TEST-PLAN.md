# Test plan — Supply Chain self-registration

Nothing here has run. Test locations are proposals for the writers named in OWNED-PATHS.md.

## 1. Foundation (service-level) — `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/`

| ID | Test | Pass condition |
|---|---|---|
| F-01 | All registered providers are pushed | With N `IModuleManifestProvider` registrations, exactly N POSTs to `/api/internal/module-catalog/register-manifest`, one per ModuleCode |
| F-02 | Missing BaseUrl or key fails closed | No request is sent; a warning is logged; startup is not blocked |
| F-03 | One failure does not block others | Provider 1 gets 500×5 and provider 2 gets 200 → provider 2 is still registered |
| F-04 | Retry classification | 5xx/connection refused → retry with backoff (fake delay); 4xx → stop without retry |
| F-05 | Auth header per D2 | D2=A: `X-Internal-Api-Key` present, no credential headers. D2=B: credential id + secret headers and no legacy key (mixed headers are rejected by Platform) |
| F-06 | DI wiring | Service host resolves every provider and the hosted service (Program.cs lines exist exactly once) |

## 2. Per-module provider tests (one file per module, in the service test project; pattern `GoldenCompactManifestProviderTests` / MDM tests)

| ID | Test (both directions, standard §3) | Pass condition |
|---|---|---|
| M-01 | Identity | Exact ModuleCode (clean slug), Domain `SupplyChainExecution`, Service `DitenSupplyChainService`, `IsTenantAssignable` true |
| M-02 | Manifest → real keys | Every `RequiredPermission`/`PermissionKey` is in the reflected set of `public const string` fields of the module's `*Permissions` class. **Returns (D5=A):** the set also includes every non-null value of `ReturnPermissions.ForTarget` over the frozen `ReturnStatus` enum. D5=B: constants only |
| M-03 | Real keys → manifest (no silent omission) | Every reflected key is either in the manifest **or** on an explicit "API-only, no UI" allow-list with a reason (e.g. `supplychain.shipments.reconcile`, `supplychain.loads.transition`, `supplychain.returns.transition`) |
| M-04 | Pages ↔ frontend view routes | The set of manifest RoutePaths equals the frontend controller's view-route set, and the counts are equal (literal set in the service test, cross-checked by W-01) |
| M-05 | Actions ↔ UI buttons | Expected action table (MANIFESTS.md) equals manifest actions per page: code, key, placement and dangerous flag |
| M-06 | Uniqueness | PageCodes, RoutePaths and ActionCodes (per page) are unique, case-insensitive |
| M-07 | Nav | Exactly one `IsNavigationVisible` page, with null parent; every other page has a parent |
| M-08 | Scope | No RoutePath starts with `/Platform/` (tenant scope, §2c) |

## 3. Frontend-side guards — `frontend/Diten.Web.Tests/` (derive, do not copy)

| ID | Test | Pass condition |
|---|---|---|
| W-01 | View routes derived from code | Reflect `SupplyChain*Controller` view actions (non-`api` `[HttpGet]` returning a view) → the route set equals the RoutePaths parsed from the matching `*ManifestProvider.cs` (same source-parsing approach as `NavManifestL10nGuardTests`) |
| W-02 | UI permission keys ↔ manifest keys | Keys used in the module's Razor `Perms.Has(...)` calls and permission JSON equal the manifest page/action keys, minus the allow-listed conjunction keys (`supplychain.shipments.read` on Returns/Claims; `supplychain.returns.transition`) |
| W-03 | Nav l10n | Existing `NavManifestL10nGuardTests` goes green without edits once the providers and the 11 keys (NAV-L10N-KEYS.tsv) are in all 7 files: no missing key, empty value, or key echoing its own name |
| W-04 | Cross-module uniqueness | Across all SupplyChain providers: unique ModuleCodes and RoutePaths; nav-visible PageCodes not used by any other manifest in `services/` |

## 4. Reconcile-state (standard §4, mandatory; unit tests do not catch orphans)

| ID | Test | Pass condition |
|---|---|---|
| R-01 | Platform-side reconcile with a SupplyChain fixture | In the Platform test project (pattern `ModuleManifestReconcilePruneTests`): push manifest A (a serialized copy of a SupplyChain provider), then push B with one action moved and one page removed → catalog pages/actions == B, the removed items are soft-deleted, and the moved action's route is freed. Permissions are not deleted (additive by design) |
| R-02 | Runtime idempotence | Isolated DB-010 Mongo, Platform + SupplyChain Release binaries: start → module, pages and actions appear with `Origin=SelfRegistered`, status Active; restart → no duplicates, identical counts; derived permission scope = Tenant for all keys |
| R-03 | Operator guard | A manual edit of a HARD field of a self-registered SupplyChain module returns 409 `MODULE_MANAGED_BY_CODE`; SOFT fields are preserved across restart |
| R-04 | Nav end-to-end (Phase 4.5 channel A/B/C) | A tenant user with read sees the Supply Chain Execution heading, the module and its page in the localized sidebar and Ctrl+K in all 7 cultures; a user without read does not see them |

## 5. What is not tested here

Gateway routes, business behaviour of the modules, and the UI pages themselves (covered by each module's UI acceptance matrix). R-02 and R-04
need a native executor (see the recent STEP-0 stops) and the integrated target (Q14/Q15).
