namespace Diten.CrmService.Application.Features.CyclePeriod;

/// <summary>
/// MOD-0165 FU06 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant, no repository or collection reference.
/// <para>There is deliberately no <c>.resolve</c>-style key: resolving which period is in force exposes no personal
/// data (unlike a segment's membership), so <see cref="Read"/> is the right guard and a fourth key would only invite
/// the assumption that reading a period is somehow privileged.</para>
/// <para><c>.activate</c> covers BOTH activate and close, because they are the same governance responsibility — the
/// decision to put a period live and the decision to end it. A separate <c>.close</c> key would be a key nobody ever
/// grants. The RBAC catalog does not carry <c>crm.cycle-period.*</c> yet, so the endpoints run on the same documented
/// DEV-ONLY fallback MOD-0167 FU04 / MOD-0165 FU04 / MOD-0164 FU02 already use. The fallback <b>widens no guard</b>:
/// tenant isolation, the lifecycle, the overlap ban and the fail-closed vocabulary all still run behind it. Under the
/// fallback <c>activate</c> collapses onto manage, so SoD cannot be enforced in dev — a deliberate, documented gap
/// closed by follow-up F-RBAC.</para>
/// </summary>
public static class CyclePeriodPermissions
{
    public const string Read = "crm.cycle-period.read";
    public const string Manage = "crm.cycle-period.manage";
    public const string Activate = "crm.cycle-period.activate";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Activate };
}
