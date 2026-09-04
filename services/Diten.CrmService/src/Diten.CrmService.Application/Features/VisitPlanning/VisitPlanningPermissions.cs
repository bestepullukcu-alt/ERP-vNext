namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant. The split (D-RBAC = B, LOCKED) lets a manager
/// PREVIEW without commit rights:
/// <list type="bullet">
/// <item><see cref="Read"/> — open the setup console, list sessions, read a session.</item>
/// <item><see cref="Generate"/> — run a preview (dry-run generation) + create/edit a session's selection.</item>
/// <item><see cref="Apply"/> — commit (write FU01 atoms + flip to committed) + re-plan. The endpoint ALSO stacks the
/// FU01 <c>crm.planned-visit.manage</c> key, because apply/re-plan write through FU01's aggregate.</item>
/// </list>
/// The catalog rows + grants are NOT seeded by this pack (F-RBAC); like FU03's <c>crm.visit-route.preview</c> the real
/// key sits on the endpoint, so it answers 403 until an operator grants it — the intended fail-closed behaviour. There
/// is deliberately NO territory fallback here.
/// </summary>
public static class VisitPlanningPermissions
{
    public const string Read = "crm.visit-plan.read";
    public const string Generate = "crm.visit-plan.generate";
    public const string Apply = "crm.visit-plan.apply";

    /// <summary>The FU01 key apply/re-plan additionally require, because they write through FU01's aggregate (§14).</summary>
    public const string PlannedVisitManage = "crm.planned-visit.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Generate, Apply };
}
