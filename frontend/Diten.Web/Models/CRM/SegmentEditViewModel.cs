using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// The create/edit view model lives in its own file, apart from the read models: several of them carry
// same-named properties (SubjectType, SegmentStatus), and keeping the bound model separate means the
// required contract - backend validator, view model, razor attribute, label marker - is read off exactly
// one class with nothing shadowing it.

/// <summary>
/// MOD-0167-FU02 Segment create/edit view model (Compact). Optional numeric/date fields are nullable so no spurious
/// data-val-required is generated; required fields carry both the label marker and the HTML required attribute.
/// <para>The criteria tree and the manual membership rows are NOT bound here: both are embedded sub-editors on the
/// page, driven by the segment sub-routes (the tree through the segment payload, the rows through /targets). There is
/// no member list, member count or any other runtime-state field, and there never will be — a segment is a definition.</para>
/// </summary>
public sealed class SegmentEditViewModel
{
    public Guid? SegmentId { get; set; }

    [Required]
    public string SegmentCode { get; set; } = string.Empty;
    [Required]
    public string SegmentName { get; set; } = string.Empty;
    [Required]
    public string SegmentType { get; set; } = "dynamic";
    [Required]
    public string SubjectType { get; set; } = "contact";
    [Required]
    public string SegmentStatus { get; set; } = "draft";
    [Required]
    public string MatchMode { get; set; } = "all";
    public string? BusinessUnitId { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    [Required]
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public bool IsArchived { get; set; }
    public bool IsCriteriaFrozen { get; set; }
    public int SegmentVersion { get; set; } = 1;

    /// <summary>The embedded tree, serialised into the page for the criteria editor and posted back as JSON.</summary>
    public string CriteriaJson { get; set; } = "[]";

    // Contract-driven option lists. Never hardcoded in the view or in JS.
    public string? ContractError { get; set; }
    public IReadOnlyList<string> SegmentTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> SubjectTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> SegmentStatuses { get; set; } = new List<string>();
    public IReadOnlyList<string> MatchModes { get; set; } = new List<string>();
    public int MaxCriteriaNodes { get; set; }
    public int MaxCriteriaDepth { get; set; }
    public int MaxChildrenPerGroup { get; set; }
    public int MaxCandidateSet { get; set; }

    /// <summary>True when the actor may use the global-product picker for a concept.affinity value. Without it the
    /// value field is DISABLED with a reason rather than degraded to a free-text Guid box.</summary>
    public bool CanPickGlobalProducts { get; set; }

    /// <summary>(P1a) The entity-picker kinds this actor is actually allowed to browse. A picker the actor cannot use
    /// falls back to a plain id field with a reason shown, rather than to a dropdown that would always be empty.</summary>
    public IReadOnlyList<string> AvailablePickers { get; set; } = new List<string>();
}
