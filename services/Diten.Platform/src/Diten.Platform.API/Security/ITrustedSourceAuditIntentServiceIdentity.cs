namespace Diten.Platform.API.Security;

public interface ITrustedSourceAuditIntentServiceIdentity
{
    Task<IdentityResult> ResolveAsync(HttpContext httpContext);

    public sealed record IdentityResult(
        bool IsAuthenticated,
        bool IsAuthorized,
        bool IsUnavailable,
        Guid? TenantId)
    {
        public static IdentityResult Unauthenticated { get; } = new(false, false, false, null);
        public static IdentityResult Forbidden { get; } = new(true, false, false, null);
        public static IdentityResult Unavailable { get; } = new(false, false, true, null);
    }
}
