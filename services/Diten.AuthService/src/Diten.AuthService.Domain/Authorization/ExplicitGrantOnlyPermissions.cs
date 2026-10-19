namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Permissions that must never be granted through an automatic pathway — full-catalog/SuperAdmin,
/// module-entitlement sync, or default role provisioning. Only an authorized person's explicit
/// role-permission assignment may grant one (owner decision, 2026-09-11).
/// </summary>
public static class ExplicitGrantOnlyPermissions
{
    /// <summary>MOD-0117-FU01 — portfolio owner assignment.</summary>
    public const string PortfoliosAssignOwner = "ppm.portfolios.assign-owner";

    /// <summary>
    /// WP-INFRA-AUTH-ACCOUNT-KIND-01 — classifying a tenant user account (Unknown/Human/Service). Separate from
    /// auth.users.create/update on purpose: creating or editing a user does not include deciding what KIND of
    /// account it is, and no default role — SuperAdmin included — receives it (owner decision 2026-09-11).
    /// </summary>
    public const string UsersAccountKindManage = "auth.users.account-kind.manage";

    /// <summary>
    /// MOD0024-TASK-READ-ACCESS-01 (BL-349) — reading every task in a tenant, bypassing
    /// <c>Diten.Platform</c>'s <c>ITaskReadAccessPolicy</c> relationship legs (assignee, pool, creator, watcher,
    /// parent, scope) entirely. The policy's whole point is that an ordinary Admin or Viewer sees only tasks they
    /// hold a relationship to; if this key reached either role through the module-entitlement sync that grants a
    /// module's other <c>platform.tasks.*</c> keys, every one of them would read every task in the tenant the
    /// moment Task Engine was entitled, and the policy would have nothing left to enforce (owner decision,
    /// 2026-09-13).
    /// </summary>
    public const string TasksReadAll = "platform.tasks.read-all";

    /// <summary>
    /// WP-PSS-MOD0024-BL392-WORK-REPORT-READ-EXPLICIT-01 (BL-392) — reading the Work Report across the WHOLE
    /// tenant, ignoring the caller's own data scope (<c>Diten.Platform</c>'s <c>WorkReportScopeSource</c>). It is
    /// declared as the Work Report page's manifest action, so the module-entitlement sync would hand it to every
    /// tenant Admin the moment the module was entitled, and the full-catalog path to SuperAdmin on first sync —
    /// every one of them would then read the whole tenant's report and the scope would have nothing left to
    /// narrow. A broad reporting view is held by far fewer people than the report itself and, as in SAP/Oracle,
    /// is given by explicit assignment only. Enrolling it here stops NEW automatic grants; it removes no existing
    /// role-permission row — today's holders are listed and re-granted explicitly during migration (owner
    /// decision, 2026-09-14).
    /// </summary>
    public const string WorkReportReadTenantWide = "platform.tasks.work-report.read-tenant-wide";

    /// <summary>
    /// MOD-0280-FU01 (Time Entry & Weekly Timesheet, pack D11) — worked-time totals across a team or the whole tenant.
    /// Time entries are personal working-time data (Swiss ArGV 3 Art. 26 on behaviour monitoring, KVKK proportionality):
    /// the approver sees only the weeks submitted to them, and a wider view is given by explicit assignment only, never
    /// by the module-entitlement sync or the SuperAdmin full-catalog path (CT decision on the owner's delegation,
    /// 2026-09-29).
    /// </summary>
    public const string TimeEntryTeamTotalsRead = "time-entry.team-totals.read";

    /// <summary>
    /// MOD-0280-FU01 (pack D11) — a per-person breakdown of worked time. Stricter than team totals: it names people, so
    /// it is explicit-grant only and its use is logged (workcenter-reporting-and-time-decisions §5, Z-4).
    /// </summary>
    public const string TimeEntryPersonReportsRead = "time-entry.person-reports.read";

    /// <summary>
    /// WP-WCN-TASK-REQUEST-01 (BL-509) — sending a task REQUEST to somebody outside the sender's assignment scope,
    /// typically in another legal entity of the same tenant. Addressing a person across the company boundary shows
    /// that they exist and what seat they hold (the BL-057 boundary is a data-protection one: Poland is inside
    /// GDPR, Turkey is not), so the tenant switches the feature on by granting this key to a role. It never
    /// arrives with the module-entitlement sync or the SuperAdmin full-catalog path (owner decision, 2026-10-02).
    /// </summary>
    public const string TaskRequestsCreate = "platform.tasks.requests.create";

    /// <summary>
    /// WP-VP-2 (B-1) — every planned visit and visit report in the tenant. Without it a field rep sees and acts on
    /// only their own planned visits (a foreign one answers 404); if the module-entitlement sync handed it to every
    /// tenant Admin, that ownership rule would have nothing left to enforce (owner decision, 2026-10-06).
    /// </summary>
    public const string PlannedVisitReadAll = "crm.planned-visit.read-all";

    /// <summary>
    /// WP-VP-2 (B-1) — every visit planning session in the tenant, and planning for another representative. Same
    /// reasoning as <see cref="PlannedVisitReadAll"/>: the rep plans their own week (K-1) unless explicitly widened.
    /// </summary>
    public const string VisitPlanReadAll = "crm.visit-plan.read-all";

    /// <summary>
    /// WP-VW-W4 (BE-c) — every doctor's insights in the tenant (profile, patient potential, share, opportunity). Without it a
    /// rep sees their own doctors and a manager their territory subtree (K-W7); if the module-entitlement sync handed it to
    /// every tenant Admin, that visibility rule would have nothing left to enforce. Admins still get it — by an explicit
    /// grant (W4-CFG script), a person's decision (owner decision, 2026-10-13).
    /// </summary>
    public const string DoctorInsightsReadAll = "crm.doctor-insights.read-all";

    public static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PortfoliosAssignOwner, UsersAccountKindManage, TasksReadAll, WorkReportReadTenantWide,
            TimeEntryTeamTotalsRead, TimeEntryPersonReportsRead, TaskRequestsCreate, PlannedVisitReadAll, VisitPlanReadAll, DoctorInsightsReadAll
        };
}
