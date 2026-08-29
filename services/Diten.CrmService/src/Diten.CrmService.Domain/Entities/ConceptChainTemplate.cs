namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — ConceptChainTemplate (legacy <c>UCLNDesign</c>). The expected ORDER of concept TYPES — the chain
/// blueprint (e.g. indication → profile-need → need-benefit → key-message). <see cref="OrderedConceptTypes"/> holds at
/// least two type ids, all of the same subject, and the same type never appears twice (v1; recursion is F7). A published
/// version freezes <see cref="OrderedConceptTypes"/> — a change needs a new version — and two published versions of one
/// <see cref="ChainCode"/> may not overlap in effective window (409). <see cref="Version"/> is the business version, not
/// the technical <see cref="EntityBase.Version"/> concurrency token. This is the format-level target that resolves a
/// dangling <c>Campaign.ConceptChainTemplateId</c>; Campaign itself is never mutated here.
/// </summary>
public sealed class ConceptChainTemplate : EntityBase
{
    public Guid SubjectId { get; set; }

    /// <summary>Stable business key, shared across the versions of one logical chain.</summary>
    public string ChainCode { get; set; } = string.Empty;

    public string ChainName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Ordered ConceptType ids — min 2, all same subject, no repeat (v1). Frozen once published.</summary>
    public List<Guid> OrderedConceptTypes { get; set; } = new();

    /// <summary><see cref="ConceptChainStatuses"/> — draft / review / approved / published / inactive / archived.</summary>
    public string Status { get; set; } = ConceptChainStatuses.Draft;

    /// <summary>Business version (NOT <see cref="EntityBase.Version"/>, the concurrency token). Named like FU02's
    /// <c>ContentVersion</c> so it never shadows the base concurrency field.</summary>
    public string ChainVersion { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsPublished()
        => string.Equals(Status, ConceptChainStatuses.Published, StringComparison.OrdinalIgnoreCase);
}
