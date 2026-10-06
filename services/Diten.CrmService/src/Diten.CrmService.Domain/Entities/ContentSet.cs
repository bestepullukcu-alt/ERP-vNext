namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// SCMM-14 (CAND-CAP-0011, docx §7 "Content set and draft", DESIGN-SCMM-14) — a ContentSet: the versioned, mutable
/// DRAFT ("resolved book" / Argument Set, legacy ⑤ UCLN Book) that assembles selected content COMPONENTS
/// (<see cref="KnowledgeContent"/>) and CLAIMS onto a composition template (<see cref="ConceptChainTemplate"/>) layout,
/// within an optional reusable <see cref="ContentScope"/>. Every reference carries object + version, PINNED at selection
/// time (DESIGN D14-d) — never live-latest, never silently retargeted.
/// <para>
/// SCMM-14 is <b>mutable draft authoring only</b> — build / edit / arrange / save + a non-blocking eligibility
/// validation snapshot. It does NOT freeze, produce an immutable Revision, render output or release: those are
/// SCMM-15/16/17. <see cref="Status"/> is therefore only draft / inactive / archived — there is <b>no approved / frozen
/// state</b> here (that is the Revision). <see cref="EntityBase.Version"/> is the optimistic-concurrency token;
/// <see cref="DraftSchemaVersion"/> is the draft's own schema revision. Cloning is <b>no-inherited-approval</b>: a clone
/// starts a fresh draft and carries no validation snapshot.
/// </para>
/// </summary>
public sealed class ContentSet : EntityBase
{
    /// <summary>Stable business key, unique per tenant among non-archived rows.</summary>
    public string SetCode { get; set; } = string.Empty;

    public string SetName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Pinned reference to the composition template (③ arrangement skeleton — SCMM-10).</summary>
    public ContentSetTemplateRef Template { get; set; } = new();

    /// <summary>Optional pinned reference to a reusable content scope (④ — D14-a). Null = no scope bound.</summary>
    public ContentSetScopeRef? Scope { get; set; }

    /// <summary>Selected content components (⑥ — SCMM-13 variants), each pinned + arranged into a template slot.</summary>
    public List<ContentSetComponent> SelectedComponents { get; set; } = new();

    /// <summary>Selected claims (SCMM-12), each pinned + arranged into a template slot.</summary>
    public List<ContentSetClaim> SelectedClaims { get; set; } = new();

    /// <summary>The draft's own schema revision (bumped when the draft shape changes; NOT the concurrency token).</summary>
    public int DraftSchemaVersion { get; set; } = 1;

    /// <summary><see cref="ContentSetStatuses"/> — draft / inactive / archived (no approved / frozen — that is Revision).</summary>
    public string Status { get; set; } = ContentSetStatuses.Draft;

    /// <summary>The last non-blocking eligibility validation result (D14-c). Null until apply-eligibility runs; cleared
    /// on clone (no inherited validation).</summary>
    public ContentSetEligibilitySnapshot? EligibilitySnapshot { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}

/// <summary>SCMM-14 — pinned reference to a composition template version (id + business version snapshot). Embedded VO.</summary>
public sealed class ContentSetTemplateRef
{
    public Guid ConceptChainTemplateId { get; set; }
    public string ChainVersion { get; set; } = string.Empty;
}

/// <summary>SCMM-14 — pinned reference to a reusable content scope version (id + business version snapshot). Embedded VO.</summary>
public sealed class ContentSetScopeRef
{
    public Guid ContentScopeId { get; set; }
    public string ScopeVersion { get; set; } = string.Empty;
}

/// <summary>
/// SCMM-14 (D14-b) — where a selection sits in the template layout. <see cref="TemplateStepId"/> is the target step's
/// concept-type id (a <see cref="ConceptChainStep"/> carries no synthetic id, so the step is addressed by its
/// ConceptTypeId within a branch — see the mapping note in the arrange handler); <see cref="BranchId"/> is the template
/// branch's <see cref="ConceptChainBranch.BranchCode"/> (null on a legacy single-line template); <see cref="Position"/>
/// orders multiple selections that share one slot. Embedded VO.
/// </summary>
public sealed class ContentArrangement
{
    public Guid TemplateStepId { get; set; }
    public string? BranchId { get; set; }
    public int Position { get; set; }
}

/// <summary>SCMM-14 — a selected content component, pinned at object + version (⑥). <see cref="SelectionId"/> is a
/// synthetic per-selection key so remove / arrange can target one placement. Embedded VO.</summary>
public sealed class ContentSetComponent
{
    public Guid SelectionId { get; set; } = Guid.NewGuid();
    public Guid KnowledgeContentId { get; set; }
    public string ContentVersion { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string? Role { get; set; }
    public ContentArrangement Arrangement { get; set; } = new();
}

/// <summary>SCMM-14 — a selected claim, pinned at object + version. Embedded VO.</summary>
public sealed class ContentSetClaim
{
    public Guid SelectionId { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public string ClaimVersion { get; set; } = string.Empty;
    public ContentArrangement Arrangement { get; set; } = new();
}

/// <summary>SCMM-14 (D14-c) — the non-blocking eligibility validation result of a draft. Embedded VO.</summary>
public sealed class ContentSetEligibilitySnapshot
{
    public DateTimeOffset EvaluatedAtUtc { get; set; }
    public List<ContentSetEligibilityItem> Items { get; set; } = new();
}

/// <summary>SCMM-14 — one per-item eligibility outcome inside a snapshot. <see cref="State"/> is the disjoint
/// eligible / blocked / unresolved (SCMM-11 fail-closed). Embedded VO.</summary>
public sealed class ContentSetEligibilityItem
{
    public string ItemKind { get; set; } = string.Empty; // claim | component
    public Guid SelectionId { get; set; }
    public Guid ItemId { get; set; }                     // ClaimId / KnowledgeContentId
    public Guid? PolicyId { get; set; }
    public string State { get; set; } = string.Empty;    // eligible | blocked | unresolved
    public string? BlockingLevel { get; set; }
    public string? Reason { get; set; }
    public string? PolicyVersion { get; set; }
}

/// <summary>ContentSet lifecycle. Hard delete does not exist; closing a set is archive. No approved / frozen state (that
/// is the SCMM-15 Revision). In-domain (structural).</summary>
public static class ContentSetStatuses
{
    public const string Draft = "draft";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>Canonical SCMM-14 content-set reason / outcome codes surfaced on write outcomes and audit.</summary>
public static class ContentSetReasonCodes
{
    public const string Created = "content_set_created";
    public const string Cloned = "content_set_cloned";
    public const string Updated = "content_set_updated";
    public const string Archived = "content_set_archived";
    public const string ComponentAdded = "content_set_component_added";
    public const string ComponentRemoved = "content_set_component_removed";
    public const string ComponentArranged = "content_set_component_arranged";
    public const string ClaimAdded = "content_set_claim_added";
    public const string ClaimRemoved = "content_set_claim_removed";
    public const string ClaimArranged = "content_set_claim_arranged";
    public const string EligibilityApplied = "content_set_eligibility_applied";
    public const string DuplicateCode = "content_set_duplicate_code";
}
