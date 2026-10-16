namespace Diten.Platform.API.Configuration;

public sealed class TrustedLegalEntityScopeCredentialOptions
{
    public const string SectionName = "TrustedLegalEntityScopeCredential";

    public TrustedLegalEntityScopeCredentialBinding Mdm { get; set; } = new();
}

public sealed class TrustedLegalEntityScopeCredentialBinding
{
    public string Identifier { get; set; } = string.Empty;
    public string ActiveSecret { get; set; } = string.Empty;
    public string? PreviousSecret { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
    public bool IsRevoked { get; set; }
    public string ConsumerService { get; set; } = string.Empty;
    public string AllowedAudience { get; set; } = string.Empty;
    public List<TrustedLegalEntityScopeAllowedPair> AllowedPairs { get; set; } = [];
}

public sealed class TrustedLegalEntityScopeAllowedPair
{
    public string ModuleCode { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
}
