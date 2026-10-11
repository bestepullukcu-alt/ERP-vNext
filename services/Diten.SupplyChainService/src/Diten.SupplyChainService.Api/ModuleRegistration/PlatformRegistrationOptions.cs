namespace Diten.SupplyChainService.Api.ModuleRegistration;

public sealed class PlatformRegistrationOptions
{
    public const string SectionName = "PlatformRegistration";
    public string BaseUrl { get; set; } = string.Empty;
    public string InternalApiKey { get; set; } = string.Empty;
}
