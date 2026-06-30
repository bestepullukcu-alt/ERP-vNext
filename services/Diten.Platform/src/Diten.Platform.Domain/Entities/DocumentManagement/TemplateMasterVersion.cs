using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU02 — immutable binary/content version for a <see cref="TemplateMaster"/>.
/// </summary>
public sealed class TemplateMasterVersion : TenantScopedEntity
{
    public required Guid TemplateMasterId { get; set; }
    public int VersionNumber { get; set; }
    public required ContentRef FileRef { get; set; }
    public required string Checksum { get; set; }
    public TemplateMasterVersionStatus Status { get; set; } = TemplateMasterVersionStatus.Published;
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public string? ChangeSummary { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
