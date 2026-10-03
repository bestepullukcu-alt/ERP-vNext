using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.SupplyChainService.Api.ModuleRegistration;

public interface IModuleManifestProvider
{
    ModuleManifestDocument GetManifest();
}
