namespace Diten.AuthService.Application.Features.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — how long a role's name and display name may be. THE value: the validator reads it, and the
/// Roles form (Diten.Web <c>RoleEditViewModel</c>, a project that cannot reference this one) carries the same two
/// numbers — <c>RoleErrorCodeContractTests</c> holds them equal. The form used to allow 64 / 128 while the validator
/// refused beyond 50 / 100.
/// </summary>
public static class RoleFieldLimits
{
    public const int NameMaxLength = 50;
    public const int DisplayNameMaxLength = 100;
}
