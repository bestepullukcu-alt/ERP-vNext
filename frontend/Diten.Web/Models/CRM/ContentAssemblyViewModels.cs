namespace Diten.Web.Models.CRM;

// SCMM-14-UI (CAND-CAP-0011) content-composition view models. WP-SB-1R retired the ContentScope console and WP-KP-4
// the ContentSet console (the Knowledge Path Studio took its job); what remains is shared by the other
// content-composition consoles (Eligibility Policies). TenantId is never part of any model (server-resolved).

/// <summary>A picker option (status / reference). Mirrors ClaimOptionViewModel.</summary>
public sealed class ContentOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

public sealed class ContentGatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
