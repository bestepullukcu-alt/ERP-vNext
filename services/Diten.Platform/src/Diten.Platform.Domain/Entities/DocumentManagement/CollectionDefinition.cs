using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU02 — a tenant-scoped imported QMS folder-definition node. Governed baseline metadata only;
/// never a physical file-system folder and never document storage.
/// </summary>
public sealed class CollectionDefinition : TenantScopedEntity
{
    /// <summary>Deterministic stable key for this node within its baseline.</summary>
    public required string CanonicalId { get; set; }

    public string? ParentCanonicalId { get; set; }

    /// <summary>The owning DRAFT/PUBLISHED baseline this definition was imported into.</summary>
    public required Guid BaselineReleaseId { get; set; }

    public required string Name { get; set; }
    public string? PurposeScope { get; set; }
    public string? RequiredByScope { get; set; }
    public bool AllowsManualChildren { get; set; }
    public bool TemplatesAllowed { get; set; }
    public string? AllowedDocClass { get; set; }
    public string? DefaultClassificationLevel { get; set; }
    public string? DefaultRetentionHint { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsAutoProvisioned { get; set; }
    public bool IsProtected { get; set; }

    /// <summary>Normalized single path segment (trimmed, whitespace-collapsed, forbidden chars rejected).</summary>
    public required string PathSegment { get; set; }

    /// <summary>Server-derived full path built from ordered normalized segments.</summary>
    public required string FullPath { get; set; }

    public int DisplayOrder { get; set; }
    public CollectionDefinitionStatus Status { get; set; } = CollectionDefinitionStatus.Active;

    /// <summary>Per-definition deterministic structural hash recorded at import.</summary>
    public required string DefinitionHash { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
