# Owned, protected and single-writer paths (proposals; nothing written)

Paths are repo-relative and bind to the integrated target CT selects (Q14/Q15). The common checkout's `Program.cs` is `7fdb5ef0…`,
an older baseline than the accepted Returns/Claims compositions. Final preimages are taken from the target, not from here.

## A. Foundation — single integration owner (DCP-009 follow-up)

| Path | Change | Preimage (common checkout today) |
|---|---|---|
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/IModuleManifestProvider.cs` | new | — |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/PlatformRegistrationOptions.cs` | new (add credential fields only if D2=B) | — |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/ModuleRegistration/ModuleRegistrationHostedService.cs` | new (pattern: MDM/DevEnablement, testable delay seam as in the Carrier candidate) | — |
| `services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/ModuleRegistration/ModuleRegistrationHostedServiceTests.cs` | new (F-01…F-06) | — |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Diten.SupplyChainService.Api.csproj` | **single writer**: add the `Diten.BuildingBlocks.ModuleRegistration.Abstractions` project reference | `538e96c6…` |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Program.cs` | **single writer**: `Configure<PlatformRegistrationOptions>`, one `AddSingleton<IModuleManifestProvider, …>` per shipped module, `AddHostedService<ModuleRegistrationHostedService>()` | `7fdb5ef0…` (to be re-taken from the target) |
| `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/appsettings.json` (+ a `Development` file if the target has one) | **single writer**: empty `PlatformRegistration` section in the base file; local values only in Development; no secret in the base file | `8bd8b168…` |

## B. Per-module — each module's writer, only with that module's UI

| Module | Provider | Tests |
|---|---|---|
| 0183 | `…Api/ModuleRegistration/ShipmentTrackingPodManifestProvider.cs` | `…Tests/ModuleRegistration/ShipmentTrackingPodManifestProviderTests.cs` |
| 0184 | `…Api/ModuleRegistration/CarrierManagementManifestProvider.cs` | `…Tests/ModuleRegistration/CarrierManagementManifestProviderTests.cs` |
| 0185 | `…Api/ModuleRegistration/RoutingLoadPlanningManifestProvider.cs` | `…Tests/ModuleRegistration/RoutingLoadPlanningManifestProviderTests.cs` |
| 0186 | `…Api/ModuleRegistration/ReverseLogisticsManifestProvider.cs` | `…Tests/ModuleRegistration/ReverseLogisticsManifestProviderTests.cs` |
| 0187 | `…Api/ModuleRegistration/ClaimsManagementManifestProvider.cs` | `…Tests/ModuleRegistration/ClaimsManagementManifestProviderTests.cs` |

Frontend guards W-01/W-02/W-04: `frontend/Diten.Web.Tests/Navigation/SupplyChainManifestRouteGuardTests.cs` (new), writer = integration owner
(frontend test project is shared).

## C. Single-writer shared surfaces

| Path | Writer | Change |
|---|---|---|
| `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx` (today `a0c4daa2…`, `94c0aca6…`, `33e02669…`, `6b355550…`, `b78c7b05…`, `32a874b6…`, `390d926b…`) | integration owner; values reviewed by the l10n agent (tenant 7 languages, no English placeholders) | the 11 keys in NAV-L10N-KEYS.tsv, added per module as it ships |
| Platform `ModuleRegistrationCredentialOptions`, the controller credential branch and Platform config | **only if D2=B**; Platform owner | a SupplyChain credential and ModuleCode allow-list |
| Platform test project (R-01 fixture test) | Platform owner or integration owner | new test file only |
| Module packs 0183–0187 and DCP-009 | module-pack author, after owner approval | "Self-registration" sections (pack) and a follow-up section (DCP) |

## D. Protected (nobody in this work edits)

`.antigravity/**`; `gateway/**`; contracts/annexes; business/backend feature code (`Features/**`, including `ReturnPermissions.cs`, unless D5=B is approved as a Returns-owned change);
layouts and shared partials; other services; existing records; `NavNameLocalizer.cs` and `NavManifestL10nGuardTests.cs` (consumed as they are); Git state.
