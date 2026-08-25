namespace Diten.Platform.API.Configuration;

public sealed class VerifiedGskuResolverCredentialOptions
{
    public const string SectionName = "VerifiedGskuResolverCredential";

    public VerifiedGskuResolverServiceCredentialOptions Mdm { get; set; } = new();
}

public sealed class VerifiedGskuResolverServiceCredentialOptions
{
    public string Identifier { get; set; } = string.Empty;
    public string ActiveSecret { get; set; } = string.Empty;
    public string? PreviousSecret { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
    public bool IsRevoked { get; set; }
    public string ConsumerService { get; set; } = string.Empty;
    public string AllowedAudience { get; set; } = string.Empty;
}
