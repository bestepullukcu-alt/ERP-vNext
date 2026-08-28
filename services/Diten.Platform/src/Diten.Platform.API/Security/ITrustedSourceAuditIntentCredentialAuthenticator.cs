namespace Diten.Platform.API.Security;

public interface ITrustedSourceAuditIntentCredentialAuthenticator
{
    AuthenticationResult Authenticate(string? identifier, string? secret, string? audience);

    public sealed record AuthenticationResult(
        bool IsAuthenticated,
        bool IsForbidden,
        IReadOnlySet<Guid> AllowedTenantIds)
    {
        public static AuthenticationResult Unauthenticated { get; } = new(false, false, new HashSet<Guid>());
        public static AuthenticationResult Forbidden { get; } = new(false, true, new HashSet<Guid>());
    }
}
