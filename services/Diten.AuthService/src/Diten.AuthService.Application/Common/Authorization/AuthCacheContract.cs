namespace Diten.AuthService.Application.Common.Authorization;

public static class AuthCacheContract
{
    public const int DefaultTtlSeconds = 300;

    public static string BuildKey(Guid tenantId, Guid userId, long roleAssignmentVersion)
        => $"auth:{tenantId}:{userId}:v{roleAssignmentVersion}";
}

public sealed record AuthorizationSnapshot(
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, string> AbacAttributes,
    DateTimeOffset ExpiresAt,
    long RoleAssignmentVersion);

public sealed record RoleAssignmentChangedEvent(Guid TenantId, Guid UserId, long RoleAssignmentVersion);
