namespace Diten.Platform.API.Security;
public interface ITrustedLegalEntityScopeCredentialAuthenticator
{
    TrustedLegalEntityScopeCredentialResult Authenticate(string? identifier, string? secret, string? audience);
    bool AllowsPair(string moduleCode, string permissionKey);
}

public sealed record TrustedLegalEntityScopeCredentialResult(bool Authenticated, bool Forbidden)
{
    public static TrustedLegalEntityScopeCredentialResult Unauthenticated { get; } = new(false, false);
    public static TrustedLegalEntityScopeCredentialResult Denied { get; } = new(false, true);
}
