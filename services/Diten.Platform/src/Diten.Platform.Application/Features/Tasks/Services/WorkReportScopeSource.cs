using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WHICH SCOPE A READER ASKED TO SEE — Dilim 1f. A PREFERENCE, never a permission.
///
/// <para><b>⚠ THE ONE RULE THIS TYPE EXISTS TO CARRY: a preference can only NARROW, never widen.</b>
/// <see cref="Own"/> is honoured unconditionally — even from a caller who could see the whole tenant, because
/// asking to see less of what you are already entitled to is never a security question. <see cref="Tenant"/> is
/// honoured ONLY when <see cref="IActorPermissionContext"/> already says so; asked for by anyone else, it is
/// silently ignored rather than rejected — see <see cref="WorkReportScopeSource.ResolveAsync"/> for why a 403
/// is the wrong answer here.</para>
/// </summary>
/// <remarks>
/// ⚠ STRING ON THE WIRE, for the reason every other enum in this file is: a bare number reaching a client is a
/// defect this module has already shipped twice.
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum WorkReportScopePreference
{
    /// <summary>Just the caller's own — narrows even a tenant-wide grant. See the type's own remarks.</summary>
    Own = 0,

    /// <summary>The whole tenant — granted ONLY if the caller's permission already allows it.</summary>
    Tenant = 1
}

/// <summary>
/// WHOSE WORK A WORK-REPORT CALLER MAY SEE — resolved in ONE place, for every reader of the report.
///
/// <para><b>⚠ EXTRACTED IN DILIM 1c, AND EXTRACTED RATHER THAN COPIED.</b> The list endpoint needs exactly the
/// scope the numbers were computed under; the obvious way to give it one is to write the same twenty lines a
/// second time in a second handler. That is the shape this module has already been burned by twice — a rule
/// living in two places is a rule that will one day be enforced in only one of them, and here the rule is
/// "which rows may this person read". A second copy that fell behind would not crash, would not log, and would
/// render a perfectly convincing list of somebody else's work.</para>
///
/// <para><b>Two questions, two mechanisms, and keeping them apart is the design.</b> The PERMISSION decides
/// whether the caller may open a report at all and is enforced at the endpoint. The SCOPE decides whose rows
/// appear, and it comes from MOD-0018-FU15's <see cref="IDataScopeResolver"/> — the caller sees the flow of
/// work they could already open, one at a time, in the Task Center. Nothing new is granted by reading a summary
/// of it, or by clicking into one.</para>
///
/// <para><b>⚠ FAIL-CLOSED, THREE WAYS.</b> No tenant, no identifiable caller, or a resolver that threw all
/// produce <see cref="WorkReportScope.Empty"/>. The tempting reading of "no scopes" is "no restrictions", and
/// that reading is what turns an authorization model into decoration — in a report most of all, because an
/// unscoped one renders perfectly and is simply about somebody else's work.</para>
/// </summary>
public interface IWorkReportScopeSource
{
    /// <param name="preference">
    /// A DISPLAY preference, not a grant — see <see cref="WorkReportScopePreference"/>. Null means "whatever the
    /// caller's permission already defaults to", which is BOTH the pre-1f behaviour (nothing regresses when no
    /// caller ever sends this) and exactly what <see cref="WorkReportScopePreference.Tenant"/> falls back to
    /// when the permission is missing — there is one fallback path, not two.
    /// </param>
    Task<WorkReportScope> ResolveAsync(WorkReportScopePreference? preference = null, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class WorkReportScopeSource : IWorkReportScopeSource
{
    /// <summary>
    /// The module the scope is resolved FOR. The resolver takes a module code so a grant can be narrowed to one
    /// module; the report reads task rows, so it asks as the task module — not as a report-specific code nobody
    /// has ever granted anything against.
    /// </summary>
    private const string ScopeModuleCode = "tasks";

    private readonly IDataScopeResolver _scopes;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IActorPermissionContext _permissions;
    private readonly ILogger<WorkReportScopeSource> _logger;

    public WorkReportScopeSource(
        IDataScopeResolver scopes,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IActorPermissionContext permissions,
        ILogger<WorkReportScopeSource> logger)
    {
        _scopes = scopes;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _logger = logger;
    }

    public async Task<WorkReportScope> ResolveAsync(
        WorkReportScopePreference? preference = null,
        CancellationToken ct = default)
    {
        /*
         * ⚠ `Own` IS HONOURED FIRST, UNCONDITIONALLY — before the permission check below ever runs. A caller
         * entitled to the whole tenant asking to see less of what they already hold is never a security
         * question; short-circuiting here is what makes that true regardless of what the permission check
         * would otherwise have granted.
         */
        if (preference == WorkReportScopePreference.Own)
        {
            return await ResolveOwnScopeAsync(ct);
        }

        /*
         * A PLATFORM actor is above a single tenant and already passes every permission check by definition
         * (IActorPermissionContext.IsPlatformActor). Making it walk the org tree of a tenant it does not belong
         * to would resolve to nothing and report an empty page to the one caller who is entitled to everything.
         *
         * ⚠ `preference == Tenant` READS EXACTLY LIKE `preference == null` HERE, ON PURPOSE. Both ask for
         * tenant-wide if the permission allows it; the only difference is what happens when it does NOT — and
         * that difference lives entirely in the fall-through below, not in this condition. A caller who asked
         * for `Tenant` without the permission is not attempting an escalation to be rejected; they are stating a
         * DISPLAY preference for a permission they do not hold, and the honest answer is what they were always
         * going to get anyway: their own scope. See the type's own remarks for why a 403 is the wrong answer.
         */
        if (_permissions.IsPlatformActor || _permissions.Has(TaskPermissions.WorkReportReadTenantWide))
        {
            return WorkReportScope.TenantWideScope();
        }

        return await ResolveOwnScopeAsync(ct);
    }

    /// <summary>The caller's OWN resolved scope — org units, positions and people they hold, never the tenant.</summary>
    private async Task<WorkReportScope> ResolveOwnScopeAsync(CancellationToken ct)
    {
        if (!_tenantContext.IsResolved || _currentUser.UserId == Guid.Empty)
        {
            // No tenant or no identifiable caller: there is nobody to compute a scope for.
            return WorkReportScope.Empty;
        }

        try
        {
            var scopes = await _scopes.ResolveAsync(
                _tenantContext.TenantId, _currentUser.UserId, ScopeModuleCode, featureCode: null, ct);

            return WorkReportScope.FromDataScopes(scopes, _currentUser.UserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            /*
             * ⚠ A THROWING RESOLVER MUST NOT WIDEN THE REPORT. Letting this propagate would surface a 500, which
             * is honest but unhelpful; swallowing it and continuing with no scope would be catastrophic, because
             * "no scope" is one careless line away from "no filter". Empty, and loudly logged.
             */
            _logger.LogError(
                ex,
                "Work report scope could not be resolved for user {UserId} in tenant {TenantId}; "
                + "reporting an EMPTY period rather than an unscoped one.",
                _currentUser.UserId,
                _tenantContext.TenantId);

            return WorkReportScope.Empty;
        }
    }
}
