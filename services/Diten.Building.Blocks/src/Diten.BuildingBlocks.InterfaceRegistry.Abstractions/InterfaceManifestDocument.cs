namespace Diten.BuildingBlocks.InterfaceRegistry.Abstractions;

public sealed record InterfaceManifestDocument(
    string SourceService,
    string SourceModuleCode,
    IReadOnlyList<InterfaceDefinitionManifest> Interfaces);
