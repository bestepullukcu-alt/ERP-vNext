namespace Diten.Platform.Application.Features.InterfaceRegistry;

public sealed record InterfaceDiscoveryBatchDto(
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
    int RejectedCount,
    DateTimeOffset ImportedAtUtc,
    string? ErrorMessage);
