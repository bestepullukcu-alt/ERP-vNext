using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU02 — immutable manifest produced when a baseline is published. Deterministic and reproducible
/// for identical input. Never mutated after publication.
/// </summary>
public sealed class BaselineSnapshotManifest : TenantScopedEntity
{
    public required string ManifestId { get; set; }
    public required Guid BaselineReleaseId { get; set; }
    public required string ManifestVersion { get; set; }

    /// <summary>Ordered canonical ids of the captured definitions.</summary>
    public IReadOnlyList<string> DefinitionIds { get; set; } = [];

    /// <summary>Per-definition deterministic hashes, aligned with <see cref="DefinitionIds"/>.</summary>
    public IReadOnlyList<string> DefinitionHashes { get; set; } = [];

    public required string StructuralControlsHash { get; set; }

    /// <summary>Deterministic top-level structural hash for the whole tree.</summary>
    public required string SnapshotHash { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
