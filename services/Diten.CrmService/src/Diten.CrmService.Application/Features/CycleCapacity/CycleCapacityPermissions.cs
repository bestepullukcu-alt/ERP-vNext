namespace Diten.CrmService.Application.Features.CycleCapacity;

/// <summary>
/// MOD-0155 FU06 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant.
/// <para>There is deliberately no <c>.calculate</c> key. The calculation is a VIEW over inputs the reader can already
/// see; anyone able to read the record could do the arithmetic themselves, so a third key would be a key nobody ever
/// grants.</para>
/// <para>The RBAC catalog does not carry <c>crm.cycle-capacity.*</c> yet, so the endpoints run on the same documented
/// DEV-ONLY fallback MOD-0165 FU06 / MOD-0167 FU04 / MOD-0164 FU02 already use. The fallback <b>widens no guard</b>:
/// tenant isolation, the pin check, the closed-period lock and the fail-closed calendar read all still run behind it.
/// Closed by follow-up F-RBAC.</para>
/// <para><b>F-RBAC-WC — a cross-namespace dependency worth stating out loud.</b> The calculation endpoint forwards the
/// caller's token to the platform working calendar, which requires
/// <see cref="WorkingCalendarOverrideRead"/> — a <c>platform.*</c> key. A CRM user without it gets a distinct
/// <c>calendar_forbidden</c> answer rather than a silent zero or a wrong "no calendar" label. Granting it is an RBAC
/// operation outside this FU's authority.</para>
/// </summary>
public static class CycleCapacityPermissions
{
    public const string Read = "crm.cycle-capacity.read";
    public const string Manage = "crm.cycle-capacity.manage";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    /// <summary>The platform key the working-calendar read seam needs from the CALLER. Declared here for the contract
    /// and the diagnostics, never enforced by this service — the platform enforces it (F-RBAC-WC).</summary>
    public const string WorkingCalendarOverrideRead = "platform.working-calendar.override.read";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage };
}
