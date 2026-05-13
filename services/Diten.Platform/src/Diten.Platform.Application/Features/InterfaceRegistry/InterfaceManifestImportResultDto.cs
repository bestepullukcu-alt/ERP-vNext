namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceManifestImportResultDto(
    Guid BatchId,
    string SourceService,
    string SourceModuleCode,
    string ManifestHash,
    string Status,
    int NewCount,
    int ChangedCount,
    int DeprecatedCount,
    int MissingCount,
    int UnchangedCount,
    IReadOnlyList<InterfaceDiscoveryDiffItemDto> DiffItems);
