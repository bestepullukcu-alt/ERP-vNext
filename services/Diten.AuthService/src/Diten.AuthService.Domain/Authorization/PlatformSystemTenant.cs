namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// BL-521 FIX5 item 13 — THE PLATFORM SYSTEM TENANT: the realm every platform administrator account lives in and every
/// platform token's <c>tenant_id</c> claim names. One definition: the tenant sign-in door, the tenant refresh, the MFA
/// verification and the tenant-resolution guard all ask <see cref="Is"/>; the platform door and its handlers name it by
/// <see cref="Id"/> instead of each re-declaring the literal.
/// </summary>
public static class PlatformSystemTenant
{
    /// <summary>The platform system tenant's id (Platform's <c>SystemTenantRules.PlatformSystemTenantId</c>).</summary>
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Whether <paramref name="tenantId"/> is the platform system tenant.</summary>
    public static bool Is(Guid tenantId) => tenantId == Id;
}
