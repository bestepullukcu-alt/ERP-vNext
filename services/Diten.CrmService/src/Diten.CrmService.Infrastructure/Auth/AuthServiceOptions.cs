namespace Diten.CrmService.Infrastructure.Auth;

/// <summary>
/// S2S connection to AuthService's internal endpoints (WP-SEG-DETAILS6). Mirrors the Platform <c>AuthService</c>
/// section verbatim: a direct base URL (the internal endpoints are NOT behind the Gateway JWT surface) plus the shared
/// internal API key. When either is blank the display-name reader degrades to "no names" rather than failing a read.
/// </summary>
public sealed class AuthServiceOptions
{
    public const string SectionName = "AuthService";

    public string BaseUrl { get; set; } = "http://localhost:5056";
    public string InternalApiKey { get; set; } = string.Empty;
}
