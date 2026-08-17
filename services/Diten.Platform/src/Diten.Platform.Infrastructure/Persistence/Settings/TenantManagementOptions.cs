namespace Diten.Platform.Infrastructure.Persistence.Settings;

public sealed class TenantManagementOptions
{
    public const string SectionName = "TenantManagement";

    public string DefaultRegion { get; set; } = "US";
    public string DefaultEnvironment { get; set; } = "Production";
    public string DefaultTier { get; set; } = "Standard";
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultTimezone { get; set; } = "UTC";
    public string DefaultCurrency { get; set; } = "USD";
    public string AppUrlTemplate { get; set; } = "https://{tenant}.diten.tech";
}
