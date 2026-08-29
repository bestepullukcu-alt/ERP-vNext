namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU02 — Subject. The top-most content area a piece of knowledge belongs to (Pharma · German · QMS ·
/// Onboarding · Sales Training · …). <see cref="SubjectCode"/> is the stable business key (rename is done through
/// <see cref="SubjectName"/> / <see cref="Alias"/> only, so existing content classification never breaks). Closing a
/// subject is the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete, and an archived subject accepts no
/// new content (existing content stays classified and readable).
/// </summary>
public sealed class Subject : EntityBase
{
    /// <summary>Stable business key, unique per tenant among non-archived rows. Never renamed.</summary>
    public string SubjectCode { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    /// <summary>Optional broader subject this one sits under, so the subject layer can be a tree (Pharma ›
    /// Pharmacovigilance › Signal Detection). A self-parent, an archived parent or a cycle is rejected. Archiving does
    /// not cascade: children keep pointing at an archived parent and stay readable.</summary>
    public Guid? ParentSubjectId { get; set; }

    public string? Description { get; set; }

    /// <summary><see cref="TaxonomyStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = TaxonomyStatuses.Draft;

    public int SortOrder { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>Former names/codes kept for search and historical references.</summary>
    public List<string> Alias { get; set; } = new();

    public List<KnowledgeExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>Lifecycle shared by the Subject / Topic / AudienceProfile taxonomy aggregates. Hard delete does not exist.
/// In-domain (structural) vocabulary — validated here rather than through MOD-0048 so the runtime never fails open.</summary>
public static class TaxonomyStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}
