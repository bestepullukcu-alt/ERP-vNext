using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.DevEnablementService.Api.ModuleRegistration;

/// <summary>Supplies this service's self-registration manifest (the code-owned source of truth for its catalog entry).</summary>
public interface IModuleManifestProvider
{
    ModuleManifestDocument GetManifest();
}
