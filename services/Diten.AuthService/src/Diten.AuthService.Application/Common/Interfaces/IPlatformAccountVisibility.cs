namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// BL-521 FIX6 item 13 (D') / FIX8 item 2 — WHETHER THE CURRENT CALLER MAY SEE AND ACT ON PLATFORM ACCOUNTS. Inside a request,
/// ONLY a platform-door actor (<c>platform_admin</c> / <c>partner_admin</c>) may; every other authenticated caller — a tenant
/// actor (<c>actor_type=tenant_user</c>) at best a tenant's administrator, and equally a token with no, an empty or an unknown
/// actor type — may not: the platform administrators' own accounts are not its to list, read, disable, enable, reset, re-role,
/// update or delete — and, so it cannot even tell whether one exists, they are not "forbidden" to it but ABSENT (the
/// user-management reads answer as for an id that does not exist). A request with no authenticated principal (the anonymous
/// doors: sign-in, MFA verification, refresh, the platform door — and the internal service calls, which authenticate by the
/// internal key) and a call outside any request (background work) are not hidden from anything.
///
/// <para>One rule in one place: the user repository and the user list reader both ask it, so no endpoint carries its own copy.</para>
/// </summary>
public interface IPlatformAccountVisibility
{
    /// <summary>True when platform accounts must not be shown to, or acted on by, the current caller.</summary>
    bool HidesPlatformAccounts { get; }
}
