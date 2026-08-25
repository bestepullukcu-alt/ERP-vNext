namespace Diten.MdmService.Infrastructure.ReferenceData;

public sealed class VerifiedGskuResolverOptions
{
    public const string SectionName = "VerifiedGskuResolver";

    public Uri? PlatformBaseAddress { get; set; }
    public TimeSpan Timeout { get; set; }
    public string? CredentialIdentifier { get; set; }
    public string? CredentialSecret { get; set; }
}
