namespace Diten.Platform.API.Security;

/// <summary>
/// Marks an action as INTENTIONALLY login-only: a signed-in principal and NO permission key
/// (DCP-004 "Decision amendment 2026-09-15", BL-410).
///
/// <para><b>What it is.</b> A named, reviewable declaration, not a filter. Authentication stays with
/// <c>[Authorize]</c>; which actor type may call the route stays with <c>TenantResolutionMiddleware</c>
/// (a tenant route requires <c>actor_type = tenant_user</c>, BL-413; an <c>/api/platform</c> admin path requires a
/// platform actor). This attribute adds no access of its own. It says "the missing <c>[HasPermission]</c> here is a
/// decision, and this is why".</para>
///
/// <para><b>When it is right.</b> The endpoint answers about the caller's OWN identity or a universal,
/// non-sensitive list, and the caller cannot name another subject: my notifications, my menu, my saved views, my
/// inbox, my team as the org chart defines it. Anything that reads or writes a RESOURCE takes a key instead. A write
/// reached from a login-only surface is still authorized by the source module's own key (see
/// <c>WorkItemsController.DispatchAction</c>).</para>
///
/// <para><b>Guarded.</b> <c>LoginOnlyEndpointGuardTests</c> fails when an action in this assembly has neither a
/// permission gate, a platform policy, an allow-listed anonymous route, nor this attribute, and it pins the exact
/// list of login-only actions so adding one is a reviewed change.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LoginOnlyAttribute : Attribute
{
    public LoginOnlyAttribute(string reason)
    {
        Reason = reason;
    }

    /// <summary>Why no permission key applies to this action. Must not be blank (the guard test checks).</summary>
    public string Reason { get; }
}
