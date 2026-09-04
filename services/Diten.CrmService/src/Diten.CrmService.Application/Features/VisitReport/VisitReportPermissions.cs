namespace Diten.CrmService.Application.Features.VisitReport;

/// <summary>
/// MOD-0155 FU02 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant (F-RBAC).
/// <para>Read is split from record (D-RBAC) so a manager can review outcomes without recording them. Because
/// D-EXECUTION-STATUS = A would reflect the "executed" marker onto the plan, <c>record</c> ALSO requires FU01
/// <c>crm.planned-visit.manage</c> (FU05 precedent for a cross-FU write). The RBAC catalog does not carry
/// <c>crm.visit-report.*</c> yet, so the endpoints run on the same documented DEV-ONLY territory fallback FU01 / FU05
/// already use; the fallback widens no guard (tenant isolation, the lifecycle, immutability and the fail-closed
/// vocabulary all still run behind it), but the read/record/amend split cannot be enforced in dev — a deliberate,
/// documented gap closed by F-RBAC.</para>
/// </summary>
public static class VisitReportPermissions
{
    public const string Read = "crm.visit-report.read";
    public const string Record = "crm.visit-report.record";
    public const string Amend = "crm.visit-report.amend";

    /// <summary>FU01 key that <c>record</c> ALSO requires (D-RBAC) — the executed marker reflection precondition.</summary>
    public const string PlannedVisitManage = "crm.planned-visit.manage";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.</summary>
    public const string ReadFallback = "crm.territory.read";
    public const string ManageFallback = "crm.territory.model.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Record, Amend };
}
