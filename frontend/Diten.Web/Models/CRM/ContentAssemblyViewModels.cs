using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.CRM;

// SCMM-14-UI (CAND-CAP-0011) Content Studio view models — ContentSet authoring (WP-SB-1R: the ContentScope console is
// retired; the set carries its country + language). TenantId is never part of any model (server-resolved). The
// ContentSet workspace is JS-driven over the same-origin proxy, so it needs only a create form model + a gateway
// response wrapper. Every reference is chosen through a searchable select2 — no raw id entry (D14-e).

/// <summary>A picker option (status / reference). Mirrors ClaimOptionViewModel.</summary>
public sealed class ContentOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>ContentSet create form model. The set is created empty (template pinned, country + language chosen); components /
/// claims / arrangement are then authored in the JS workspace against the same-origin proxy.</summary>
public sealed class ContentSetCreateViewModel
{
    [Required, StringLength(120)] public string SetCode { get; set; } = string.Empty;
    [Required, StringLength(240)] public string SetName { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Required] public Guid ConceptChainTemplateId { get; set; }

    // WP-SB-1R — the set context (the retired ContentScope picker is gone): one COUNTRY_CODES country and one of its
    // content languages. CRM validates both against BRD on create.
    [Required, StringLength(10)] public string CountryCode { get; set; } = string.Empty;
    [Required, StringLength(10)] public string LanguageCode { get; set; } = string.Empty;
}

public sealed class ContentGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
