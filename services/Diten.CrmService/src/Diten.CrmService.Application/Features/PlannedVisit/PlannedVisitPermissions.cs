namespace Diten.CrmService.Application.Features.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). This file seeds NOTHING:
/// the catalog entries and the tenant-97c5 Admin grant live in AuthService's DataSeeder (WP-MOB-B03).
/// <para><c>.confirm</c> is a SEPARATE key from <c>.manage</c> so the actor who authors a plan and the actor who
/// confirms it can be split (SoD). WP-MOB-B03 removed the former DEV-ONLY <c>crm.territory.*</c> fallback
/// (under which manage and confirm collapsed onto one key); <c>PlannedVisitsController</c> now enforces these keys.</para>
/// </summary>
public static class PlannedVisitPermissions
{
    public const string Read = "crm.planned-visit.read";
    public const string Manage = "crm.planned-visit.manage";
    public const string Confirm = "crm.planned-visit.confirm";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, Confirm };
}
