using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.PersonReferenceDirectory;

public sealed class PersonReferenceDirectoryHealthSnapshot : TenantScopedEntity
{
    public required string SnapshotKey { get; set; }
    public int? ProjectionCount { get; set; }
    public int? ValidatedCount { get; set; }
    public int? ConflictCount { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? RedactedStatus { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
