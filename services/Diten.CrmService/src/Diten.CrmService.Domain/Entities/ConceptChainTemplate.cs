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

    /// <summary>Ordered ConceptType ids — min 2, all same subject, no repeat (v1). The backward-compatible SPINE: it is
    /// still required, still drives template conformance (§6.1) and is still frozen once published. SCMM-10 adds the
    /// richer <see cref="Branches"/> alongside it; a legacy template with no branches reads back as one branch derived
    /// from this list (read-time migration).</summary>
    public List<Guid> OrderedConceptTypes { get; set; } = new();

    /// <summary>SCMM-10 (③, RM2) — optional parallel-branch structure. Each branch is an ordered concept-type sequence
    /// with per-position cardinality (min/max). Empty on legacy templates (they use <see cref="OrderedConceptTypes"/>
    /// only). Frozen alongside the spine once published. This is STRUCTURE ONLY — no engine advances, assigns or
    /// evaluates it (D8).</summary>
    public List<ConceptChainBranch> Branches { get; set; } = new();

    /// <summary>SCMM-10 (WP-A, D-a/D-b) — template-level delivery role: WHO presents the whole chain (the "book"). A
    /// <c>content-moderator-role</c> ValueCode (<c>position</c> / <c>client</c> / <c>system-auto</c>) — the vocabulary is
    /// reference-driven (WP-B); the backend only stores a trimmed, non-empty value. Null = unspecified. Frozen alongside
    /// the spine once published (D-f). Config only — no engine resolves a role or assigns a moderator (D8). Phase 2 will
    /// add a <c>ModeratorPositionRef</c> when this is <c>position</c>.</summary>
    public string? ModeratorRoleType { get; set; }

    /// <summary>SCMM-10 (WP-A, D-a/D-d) — template-level target audience: FOR WHOM the whole chain is intended. Zero or
    /// more <see cref="AudienceProfile"/> references (reuse of the reference-driven profiles). Empty = unspecified. Each
    /// id is validated (exists + non-archived) before persist; frozen alongside the spine once published (D-f). Config
    /// only — a reference, never a membership evaluation (D8).</summary>
    public List<Guid> ForWhomAudienceProfileIds { get; set; } = new();

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

/// <summary>
/// SCMM-10 (③, RM2) — one parallel branch of a chain blueprint: an ordered sequence of <see cref="ConceptChainStep"/>.
/// A template may declare several branches that run alongside each other (the legacy single line is one branch). Embedded
/// value object: no <c>TenantId</c>, no <c>Version</c>, no repository — it lives inside the template document.
/// </summary>
public sealed class ConceptChainBranch
{
    /// <summary>Stable, machine-readable key unique within the template (a change to a published template needs a new version).</summary>
    public string BranchCode { get; set; } = string.Empty;

    public string? BranchName { get; set; }

    /// <summary>Deterministic branch ordering for display; not the step order (that is <see cref="Steps"/>' order).</summary>
    public int SortOrder { get; set; }

    /// <summary>Ordered steps of this branch (at least one). Order is the list order.</summary>
    public List<ConceptChainStep> Steps { get; set; } = new();
}

/// <summary>
/// SCMM-10 (③, RM2) — one position in a branch. It carries the expected concept TYPE plus the per-position cardinality
/// (<see cref="MinSelection"/> / <see cref="MaxSelection"/>) the legacy flat list could not hold. This is STRUCTURE
/// ONLY — no engine advances or evaluates it (D8). Moderator / for-whom moved to the TEMPLATE level (SCMM-10 WP-A,
/// D-a/D-e): the legacy step-level <c>AllowedRoleRefs</c> / <c>AudienceDimensionRefs</c> were removed (no downstream
/// consumer read them) and any such element on a legacy branch document is ignored on read (see the persistence map).
/// </summary>
public sealed class ConceptChainStep
{
    public Guid ConceptTypeId { get; set; }

    /// <summary>Minimum number of nodes to pick for this position (RM2 min selections). 0 = optional.</summary>
    public int MinSelection { get; set; } = 1;

    /// <summary>Maximum number of nodes for this position (RM2 max selections). Null = unbounded; when set it is ≥ 1 and
    /// ≥ <see cref="MinSelection"/>.</summary>
    public int? MaxSelection { get; set; }
}
