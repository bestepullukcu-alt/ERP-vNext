using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Services;

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
    Task<WorkReportScope> ResolveAsync(CancellationToken ct = default);
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

    public async Task<WorkReportScope> ResolveAsync(CancellationToken ct = default)
    {
        /*
         * A PLATFORM actor is above a single tenant and already passes every permission check by definition
         * (IActorPermissionContext.IsPlatformActor). Making it walk the org tree of a tenant it does not belong
         * to would resolve to nothing and report an empty page to the one caller who is entitled to everything.
         */
        if (_permissions.IsPlatformActor || _permissions.Has(TaskPermissions.WorkReportReadTenantWide))
        {
            return WorkReportScope.TenantWideScope();
        }

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
