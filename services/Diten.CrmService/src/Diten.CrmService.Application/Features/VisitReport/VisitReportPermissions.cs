namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// MOD-0155 FU02 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant (F-RBAC).
/// <para>Read is split from record (D-RBAC) so a manager can review outcomes without recording them. Because
/// D-EXECUTION-STATUS = A would reflect the "executed" marker onto the plan, <c>record</c> ALSO requires FU01
/// <c>crm.planned-visit.manage</c> (FU05 precedent for a cross-FU write).</para>
/// <para>WP-VW-W1 (F-RBAC) — the endpoints require these canonical keys; the DEV-ONLY territory fallback
/// (<c>crm.territory.read</c> / <c>crm.territory.model.manage</c>) is removed. A role receives the keys from the
/// role-permission screen (no seed, no grant here).</para>
/// </summary>
public static class VisitReportPermissions
{
    public const string Read = "crm.visit-report.read";
    public const string Record = "crm.visit-report.record";
    public const string Amend = "crm.visit-report.amend";

    /// <summary>FU01 key that <c>record</c> ALSO requires (D-RBAC) — the executed marker reflection precondition.</summary>
    public const string PlannedVisitManage = "crm.planned-visit.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Record, Amend };
}
