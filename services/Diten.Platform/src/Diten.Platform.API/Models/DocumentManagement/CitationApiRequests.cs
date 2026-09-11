namespace Diten.Platform.API.Models.DocumentManagement;

// DCP-005 (WP-DM-2b) — controlled-document CITATION resolve payload (JSON from the TenantShell proxy). Mirrors
// ResolveEffectivenessApiRequest and reuses EffectivenessApiMapper for the shared by/identifier validation. The
// endpoint is a thin screen over the single ResolveDocumentCitationQuery resolver — request-shape validation only.

public sealed class ResolveCitationApiRequest
{
    /// <summary>Which register identity field to match against: "code" or "uid" (case-insensitive). No default (§1).</summary>
    public string? By { get; set; }

    public IReadOnlyList<string>? Identifiers { get; set; }
}
