namespace Diten.Platform.Infrastructure.Settings;

public sealed class AuthServiceOptions
{
    public const string SectionName = "AuthService";

    public string BaseUrl { get; set; } = "http://localhost:5056";
    public string InternalApiKey { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:5001";
    public string TenantLoginUrlTemplate { get; set; } = "https://{tenantDomain}/account/login?tenantId={tenantId}";
}
