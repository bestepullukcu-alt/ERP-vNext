using Diten.BuildingBlocks.ModuleRegistration.Abstractions;

namespace Diten.Platform.Application.Contracts;

/// <summary>
/// MC-3b — a Platform-internal module declares its catalog manifest in code (mirroring its real controller routes
/// and <c>[HasPermission]</c> keys, zero drift). The startup self-registration worker collects every provider and
/// reconciles it IN-PROCESS via <c>RegisterModuleManifestCommand</c> (no HTTP — that path is for cross-service
/// modules like DevEnablement/goldenslim). Compatible in shape with DevEnablement's IModuleManifestProvider.
/// </summary>
public interface IModuleManifestProvider
{
    ModuleManifestDocument GetManifest();
}
