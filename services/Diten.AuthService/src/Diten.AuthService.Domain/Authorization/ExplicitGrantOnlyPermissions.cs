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

    public static readonly IReadOnlySet<string> Keys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PortfoliosAssignOwner, UsersAccountKindManage, TasksReadAll, WorkReportReadTenantWide
        };
}
