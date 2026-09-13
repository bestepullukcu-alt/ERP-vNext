namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Permissions that must never be granted through an automatic pathway — full-catalog/SuperAdmin,
/// module-entitlement sync, or default role provisioning. Only an authorized person's explicit
/// role-permission assignment may grant one (owner decision, 2026-09-11).
/// </summary>
public static class ExplicitGrantOnlyPermissions
{
    /// <summary>MOD-0117-FU01 — portfolio owner assignment.</summary>
    public const string PortfoliosAssignOwner = "ppm.portfolios.assign-owner";

    /// <summary>
    /// WP-INFRA-AUTH-ACCOUNT-KIND-01 — classifying a tenant user account (Unknown/Human/Service). Separate from
    /// auth.users.create/update on purpose: creating or editing a user does not include deciding what KIND of
    /// account it is, and no default role — SuperAdmin included — receives it (owner decision 2026-09-11).
    /// </summary>
    public const string UsersAccountKindManage = "auth.users.account-kind.manage";

    public static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PortfoliosAssignOwner, UsersAccountKindManage };
}
