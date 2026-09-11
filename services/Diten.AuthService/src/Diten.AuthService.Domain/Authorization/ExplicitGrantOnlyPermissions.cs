namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Permissions that must never be granted through an automatic pathway — full-catalog/SuperAdmin,
/// module-entitlement sync, or default role provisioning. Only an authorized person's explicit
/// role-permission assignment may grant one (owner decision, 2026-09-11).
/// </summary>
public static class ExplicitGrantOnlyPermissions
{
    public static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ppm.portfolios.assign-owner" };
}
