namespace Diten.Platform.Infrastructure.Settings;

/// <summary>Dedicated Platform-to-MDM identity for the Legal Entity reference-validation read only.</summary>
public sealed class MdmServiceIdentityOptions
{
    public const string SectionName = "MdmServiceIdentity";
    public const string RequiredScope = "mdm.legal-entities.reference.validate";

    public bool Enabled { get; set; }
    public string Issuer { get; set; } = "diten-platform-service";
    public string Audience { get; set; } = "diten-mdm-reference-validation";
    public string CallerId { get; set; } = "Diten.Platform";
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public int TokenLifetimeSeconds { get; set; } = 30;
}
