using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// SCMM-14-UI (CAND-CAP-0011) Content Studio view models — ContentScope + ContentSet authoring. TenantId is never part of
// any model (server-resolved). The ContentScope console is the Compact route-based pattern (Claims mirror); the
// ContentSet workspace is JS-driven over the same-origin proxy, so it needs only a create form model + a gateway
// response wrapper. Every reference is chosen through a searchable select2 — no raw id entry (D14-e).

/// <summary>A picker option (status / reference). Mirrors ClaimOptionViewModel.</summary>
public sealed class ContentOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>ContentScope read model (Edit source). Mirrors the CrmService ContentScopeDto shape.</summary>
public sealed class ContentScopeDetailViewModel
{
    public Guid ContentScopeId { get; set; }
    public string ScopeCode { get; set; } = string.Empty;
    public string ScopeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> ProductRefs { get; set; } = [];
    public List<string> MarketRefs { get; set; } = [];
    public List<string> AudienceRefs { get; set; } = [];
    public string? Channel { get; set; }
    public string? LanguageCode { get; set; }
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }
    public string ScopeVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

/// <summary>ContentScope create/edit form model. ScopeCode is immutable on edit. Refs are free tag lists.</summary>
public sealed class ContentScopeEditViewModel
{
    public Guid ContentScopeId { get; set; }

    [Required, StringLength(120)] public string ScopeCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string ScopeName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public List<string> ProductRefs { get; set; } = [];
    public List<string> MarketRefs { get; set; } = [];
    public List<string> AudienceRefs { get; set; } = [];
    [StringLength(120)] public string? Channel { get; set; }
    [StringLength(35)] public string? LanguageCode { get; set; }
    public DateTimeOffset? PeriodFrom { get; set; }
    public DateTimeOffset? PeriodTo { get; set; }
    [StringLength(60)] public string? ScopeVersion { get; set; }
    public string? Status { get; set; }
    public bool IsArchived { get; set; }

    // Populated server-side.
    public IReadOnlyList<string> Statuses { get; set; } = [];
}

/// <summary>ContentSet create form model. The set is created empty (template + optional scope pinned); components /
/// claims / arrangement are then authored in the JS workspace against the same-origin proxy.</summary>
public sealed class ContentSetCreateViewModel
{
    [Required, StringLength(120)] public string SetCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string SetName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Required] public Guid ConceptChainTemplateId { get; set; }
    public Guid? ContentScopeId { get; set; }
}

public sealed class ContentGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
