using System.Security.Claims;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Api.Controllers;

// Process-only, default-off Development recovery for one pre-existing tenant administrator.
// The normal invitation and entitlement-provisioning paths are deliberately not involved.
public static class LocalTenantAdminPasswordResetBoundary
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid CanonicalPlatformAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static bool TryAuthorize(
        IHostEnvironment environment,
        ClaimsPrincipal principal,
        IReadOnlyDictionary<string, string?> settings,
        Guid requestedTenantId,
        string? requestedEmail,
        out Guid targetUserId,
        out Guid operatorId)
    {
        targetUserId = Guid.Empty;
        operatorId = Guid.Empty;
        if (!environment.IsDevelopment() || principal.Identity?.IsAuthenticated != true ||
            !string.Equals(Get(settings, "ENABLED"), "true", StringComparison.Ordinal) ||
            !Guid.TryParse(Get(settings, "TENANT_ID"), out var configuredTenant) ||
            !Guid.TryParse(Get(settings, "USER_ID"), out var configuredUser) ||
            !Guid.TryParse(Get(settings, "OPERATOR_ID"), out var configuredOperator) ||
            string.IsNullOrWhiteSpace(Get(settings, "EMAIL")) || string.IsNullOrWhiteSpace(requestedEmail) ||
            !string.Equals(Get(settings, "EMAIL"), requestedEmail?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            configuredTenant == Guid.Empty || configuredUser == Guid.Empty || configuredOperator != CanonicalPlatformAdminId ||
            configuredTenant != requestedTenantId ||
            !Single(principal, "actor_type", "platform_admin") ||
            !Single(principal, "tenant_id", PlatformTenantId.ToString()) ||
            !Single(principal, "pwd_change_required", "false"))
        {
            return false;
        }

        var subjects = principal.Claims.Where(c => c.Type is "sub" or ClaimTypes.NameIdentifier).ToArray();
        if (subjects.Length != 1 || !Guid.TryParse(subjects[0].Value, out var subject) || subject != configuredOperator)
        {
            return false;
        }

        targetUserId = configuredUser;
        operatorId = configuredOperator;
        return true;
    }

    private static bool Single(ClaimsPrincipal principal, string type, string expected)
    {
        var matches = principal.FindAll(type).ToArray();
        return matches.Length == 1 && string.Equals(matches[0].Value, expected, StringComparison.Ordinal);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> settings, string key) =>
        settings.TryGetValue(key, out var value) ? value : null;
}
