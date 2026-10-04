namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// The platform system tenant: the realm every platform administrator account lives in and every platform token's
/// <c>tenant_id</c> claim names. One definition — the platform door and its handlers name it by <see cref="Id"/> instead of
/// each re-declaring the literal.
///
/// <para>⚠ BL-521 FIX6 item 13 — it is NOT "the platform's tenant only": the same tenant is the default BUSINESS tenant
/// (its ordinary users sign in at the tenant door like anyone's), so no rule may be keyed on this id to separate platform
/// accounts from the rest. Platform accounts are told apart by the account itself (<c>User.IsPlatformAccount</c>); FIX5's
/// whole-tenant refusal locked every ordinary user of this tenant out and was withdrawn.</para>
/// </summary>
public static class PlatformSystemTenant
{
    /// <summary>The platform system tenant's id (Platform's <c>SystemTenantRules.PlatformSystemTenantId</c>).</summary>
    public static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
