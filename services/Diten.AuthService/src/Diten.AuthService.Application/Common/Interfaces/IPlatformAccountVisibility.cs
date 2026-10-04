namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// BL-521 FIX6 item 13 (D') — WHETHER THE CURRENT CALLER MAY SEE AND ACT ON PLATFORM ACCOUNTS. A caller whose token is a
/// tenant actor (<c>actor_type=tenant_user</c>) is a tenant's administrator at best: the platform administrators' own
/// accounts are not its to list, read, disable, enable, reset, re-role, update or delete — and, so it cannot even tell whether
/// one exists, they are not "forbidden" to it but ABSENT (the user-management reads answer as for an id that does not exist).
/// A platform-door actor (platform_admin / partner_admin) and a request with no tenant-actor token (internal service calls,
/// background work) are not hidden from anything.
///
/// <para>One rule in one place: the user repository and the user list reader both ask it, so no endpoint carries its own copy.</para>
/// </summary>
public interface IPlatformAccountVisibility
{
    /// <summary>True when platform accounts must not be shown to, or acted on by, the current caller.</summary>
    bool HidesPlatformAccounts { get; }
}
