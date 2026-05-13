namespace Diten.AuthService.Infrastructure.Settings;

public sealed class PlatformServiceOptions
{
    public const string SectionName = "PlatformService";

    public string BaseUrl { get; set; } = "http://localhost:5057";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5001";
    public string InternalApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;
}
