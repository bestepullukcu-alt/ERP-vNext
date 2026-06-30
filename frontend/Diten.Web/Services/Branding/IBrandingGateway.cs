namespace Diten.Web.Services.Branding;

/// <summary>
/// Server-side, pre-auth lookup of a tenant's branding for theming the login screen.
/// Calls the Platform internal branding endpoint with the shared internal API key; the browser
/// never touches it directly. Returns <c>null</c> on any miss/failure so callers fall back to the
/// platform default branding.
/// </summary>
public interface IBrandingGateway
{
    Task<TenantBranding?> GetTenantBrandingAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>Presentation-only tenant branding. Carries no sensitive data.</summary>
public sealed record TenantBranding(
    string DisplayName,
    string? LogoDataUrl,
    string? FaviconDataUrl);
