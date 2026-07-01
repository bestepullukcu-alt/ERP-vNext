using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — immutable versioned template file. Same versioning/content model as
/// <see cref="ControlledDocumentVersion"/>.
/// </summary>
public sealed class TemplateVersion : TenantScopedEntity
{
    public required Guid TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public required ContentRef FileRef { get; set; }
    public required string Checksum { get; set; }
    public required string UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ChangeSummary { get; set; }
    public DocumentVersionStatus VersionStatus { get; set; } = DocumentVersionStatus.Active;
    public DateTimeOffset? DeletedAt { get; set; }
}
