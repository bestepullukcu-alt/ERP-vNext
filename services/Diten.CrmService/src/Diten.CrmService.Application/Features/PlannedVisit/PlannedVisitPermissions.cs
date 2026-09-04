namespace Diten.CrmService.Application.Features.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant.
/// <para><c>.confirm</c> is a SEPARATE key from <c>.manage</c> so the actor who authors a plan and the actor who
/// confirms it can be split (SoD). The RBAC catalog does not carry <c>crm.planned-visit.*</c> yet, so the endpoints run
/// on the same documented DEV-ONLY territory fallback MOD-0165 FU06 / MOD-0167 FU04 already use. The fallback
/// <b>widens no guard</b> (tenant isolation, the lifecycle, the consent/overlap guards and the fail-closed vocabulary
/// all still run behind it), but because <c>manage</c> and <c>confirm</c> collapse onto one fallback key, SoD cannot be
/// enforced in dev — a deliberate, documented gap closed by follow-up F-RBAC.</para>
/// </summary>
public static class PlannedVisitPermissions
{
    public const string Read = "crm.planned-visit.read";
    public const string Manage = "crm.planned-visit.manage";
    public const string Confirm = "crm.planned-visit.confirm";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Confirm };
}
