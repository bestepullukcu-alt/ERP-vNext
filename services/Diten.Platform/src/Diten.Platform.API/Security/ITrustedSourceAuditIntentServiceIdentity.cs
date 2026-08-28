namespace Diten.Platform.API.Security;

public interface ITrustedSourceAuditIntentServiceIdentity
{
    Task<IdentityResult> ResolveAsync(
        HttpContext httpContext,
        IReadOnlySet<Guid> allowedTenantIds);

    public sealed record IdentityResult(bool IsAuthenticated, bool IsAuthorized, Guid? TenantId)
    {
        public static IdentityResult Unauthenticated { get; } = new(false, false, null);
        public static IdentityResult Forbidden { get; } = new(true, false, null);
    }
}
