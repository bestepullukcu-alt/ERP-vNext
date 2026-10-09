namespace Diten.MdmService.Infrastructure.Workflow;

public sealed class AuthProductIdentityWorkflowServiceIdentityProviderOptions
{
    public const string SectionName = "AuthProductIdentityWorkflowServiceIdentityProvider";
    public string AuthBaseUrl { get; init; } = string.Empty;
    public string ExpectedIssuer { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ActiveClientSecret { get; init; } = string.Empty;
    public string? PreviousClientSecret { get; init; }
    public DateTimeOffset? PreviousClientSecretValidUntilUtc { get; init; }
    public int RefreshSkewSeconds { get; init; } = 30;
}
