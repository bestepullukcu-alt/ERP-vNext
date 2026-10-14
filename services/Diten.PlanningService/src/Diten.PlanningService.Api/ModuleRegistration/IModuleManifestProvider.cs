using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.PlanningService.Api.ModuleRegistration;

public interface IModuleManifestProvider
{
    ModuleManifestDocument GetManifest();
}
