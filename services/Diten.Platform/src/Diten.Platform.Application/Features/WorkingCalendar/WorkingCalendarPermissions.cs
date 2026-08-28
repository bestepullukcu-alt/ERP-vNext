namespace Diten.Platform.Application.Features.WorkingCalendar;

/// <summary>
/// PKS-001 permission keys (lowercase-dotted, ≥3 segments). DEFINITION ONLY — this file seeds nothing.
/// <para>
/// The split is the tenant-vs-platform boundary made explicit. <c>platform.working-calendar.*</c> governs the COUNTRY
/// layer and is reachable only from <c>/Platform/WorkingCalendars</c>; <c>platform.working-calendar.override.*</c>
/// governs a tenant's OWN override layer and lives on the tenant-routed page. Because self-registration derives
/// permission scope from each page's RoutePath, the override keys become tenant-assignable automatically even though
/// they sit in the <c>platform.</c> namespace — no self-service allow-list entry is needed.
/// </para>
/// <para>
/// <b>Why <c>.override.read</c> is its own key:</b> <see cref="Read"/> opens the entire country layer (every country,
/// every holiday list). Handing that to tenants would break the "a tenant cannot see the country layer" boundary at
/// the permission level, so the tenant surface gets a narrower key instead of a shared one.
/// </para>
/// <para>
/// <b>Why <c>activate</c> is separate but <c>override.activate</c> is not:</b> activating a COUNTRY calendar changes
/// the working-day answer for every tenant in that country — the widest blast radius in the module, so it is worth a
/// segregation-of-duties key. Activating an override affects only the owning tenant, which is already bounded.
/// </para>
/// <para>No fallback key is used: no existing Platform permission is an honest proxy for this resource, so both
/// surfaces answer 403 until the RBAC catalog carries these keys. That is expected, not a defect.</para>
/// </summary>
public static class WorkingCalendarPermissions
{
    // ── Country layer (platform-admin shell, /Platform/… route ⇒ PlatformAdmin scope) ──
    public const string Read = "platform.working-calendar.read";
    public const string Manage = "platform.working-calendar.manage";
    public const string Activate = "platform.working-calendar.activate";

    // ── Tenant override layer (tenant shell, non-/Platform route ⇒ Tenant scope) ──
    public const string OverrideRead = "platform.working-calendar.override.read";
    public const string OverrideManage = "platform.working-calendar.override.manage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Read, Manage, Activate, OverrideRead, OverrideManage
    };
}
