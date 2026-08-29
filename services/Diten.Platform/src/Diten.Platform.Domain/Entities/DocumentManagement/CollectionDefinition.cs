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

    // ── QMS register import extension — governance identity pending; additive nullable metadata. ─────
    // Carried from the GMG-QMS-LOG-0007 register package so the software never loses the register's
    // governance columns. Descriptive only in this FU: NOT part of the structural DefinitionHash, and it
    // drives no access-policy seed, approval, or IQ evidence behaviour (those are later FUs). Existing
    // (path-hash) imports leave every field null, so no backfill/migration is required.

    /// <summary>Stable external identity from the register (e.g. <c>ENT-00</c>). Survives rename/move.</summary>
    public string? RegisterFolderId { get; set; }

    /// <summary>Register parent identity (e.g. <c>ENT-ROOT</c>); null for the register root.</summary>
    public string? RegisterParentFolderId { get; set; }

    /// <summary>Original register full path snapshot (before server normalization).</summary>
    public string? RegisterFullPath { get; set; }

    public string? DepartmentDomain { get; set; }
    public string? FolderType { get; set; }
    public string? ExampleDocuments { get; set; }
    public string? OwningDepartments { get; set; }
    public string? ControlledByGqms { get; set; }
    public string? SourceOfTruth { get; set; }
    public string? OwnerFunction { get; set; }
    public string? AccessProfile { get; set; }
    public string? RetentionClass { get; set; }
    public string? ChangeControlRequired { get; set; }
    public string? GqmsScopeLink { get; set; }
    public string? LegacyCode { get; set; }
    public string? ProvisioningWave { get; set; }
    public int? ProvisioningOrder { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
