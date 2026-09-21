using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// SCMM-11-UI (CAND-CAP-0011) eligibility policy authoring view models. TenantId is never part of any model
// (server-resolved). The console is the Compact route-based pattern (Claims/ContentScopes mirror); the evaluate panel is
// JS-driven over the same-origin proxy. Condition/context VALUES are opaque reference strings (not entity ids) — free
// multi-tag entry; Dimension / Match / the policy picker are searchable select2 (no raw entity-id — D14-e).

/// <summary>One authored condition row (create/edit form).</summary>
public sealed class EligibilityConditionRowViewModel
{
    public string Dimension { get; set; } = string.Empty;
    public string Match { get; set; } = "includes";
    public List<string> Values { get; set; } = [];
    public bool Required { get; set; }
}

/// <summary>Eligibility policy read model (Edit source). Mirrors the CrmService EligibilityPolicyDto shape.</summary>
public sealed class EligibilityPolicyDetailViewModel
{
    public Guid EligibilityPolicyId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<EligibilityConditionRowViewModel> Conditions { get; set; } = [];
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>Eligibility policy create/edit form model. PolicyCode is immutable on edit.</summary>
public sealed class EligibilityPolicyEditViewModel
{
    public Guid EligibilityPolicyId { get; set; }

    [Required, StringLength(120)] public string PolicyCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string PolicyName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(60)] public string? PolicyVersion { get; set; }
    public string? Status { get; set; }
    [Required] public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<EligibilityConditionRowViewModel> Conditions { get; set; } = [];

    // A published or archived policy is view-only in this console (publish/freeze lifecycle is out of SCMM-11-UI scope).
    public bool IsPublished { get; set; }
    public bool IsArchived { get; set; }
    public bool IsReadOnly => IsPublished || IsArchived;

    // Populated server-side.
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public IReadOnlyList<string> Dimensions { get; set; } = [];
    public IReadOnlyList<string> Matches { get; set; } = [];
}
