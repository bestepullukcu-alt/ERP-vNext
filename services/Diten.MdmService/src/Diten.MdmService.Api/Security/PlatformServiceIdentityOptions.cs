namespace Diten.MdmService.Api.Security;

/// <summary>Accepts only Platform's narrow Legal Entity reference-validation service identity.</summary>
public sealed class PlatformServiceIdentityOptions
{
    public const string SectionName = "PlatformServiceIdentity";
    public const string RequiredScope = "mdm.legal-entities.reference.validate";

    public bool Enabled { get; set; }
    public string Issuer { get; set; } = "diten-platform-service";
    public string Audience { get; set; } = "diten-mdm-reference-validation";
    public string CallerId { get; set; } = "Diten.Platform";
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int MaximumTokenLifetimeSeconds { get; set; } = 60;
    public int ClockSkewSeconds { get; set; } = 5;
}
