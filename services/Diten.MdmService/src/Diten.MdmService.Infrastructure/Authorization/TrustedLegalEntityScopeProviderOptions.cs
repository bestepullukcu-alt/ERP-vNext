namespace Diten.MdmService.Infrastructure.Authorization;

public sealed class TrustedLegalEntityScopeProviderOptions
{
    public const string SectionName = "TrustedLegalEntityScopeProvider";

    public Uri? PlatformBaseAddress { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
    public string? CredentialIdentifier { get; set; }
    public string? CredentialSecret { get; set; }
}
