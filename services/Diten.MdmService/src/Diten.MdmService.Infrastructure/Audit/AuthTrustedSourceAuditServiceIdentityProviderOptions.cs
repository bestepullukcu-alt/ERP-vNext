namespace Diten.MdmService.Infrastructure.Audit;

public sealed class AuthTrustedSourceAuditServiceIdentityProviderOptions
{
    public const string SectionName = "AuthTrustedSourceAuditServiceIdentityProvider";
    public string AuthBaseUrl { get; init; } = string.Empty;
    public string ExpectedIssuer { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ActiveClientSecret { get; init; } = string.Empty;
    public string? PreviousClientSecret { get; init; }
    public DateTimeOffset? PreviousClientSecretValidUntilUtc { get; init; }
    public int RefreshSkewSeconds { get; init; } = 30;
}
