using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 Faz 5a — how work is flowing, over a period, for the work the caller may see.
/// </summary>
/// <param name="From">Inclusive start.</param>
/// <param name="To">EXCLUSIVE end.</param>
public sealed record WorkReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    WorkReportGroupBy GroupBy,
    string CorrelationId)
    : IRequest<Response<WorkReportDto>>;

/// <summary>
/// Resolves WHOSE work is counted, then asks the database to count it.
///
/// <para><b>Two questions, two mechanisms, and keeping them apart is the design.</b> The PERMISSION
/// (<c>TaskPermissions.WorkReportRead</c>) decides whether the caller may open a report at all — enforced at the
/// endpoint. The SCOPE decides whose rows appear, and it comes from MOD-0018-FU15's
/// <see cref="IDataScopeResolver"/>: the caller sees the flow of work they could already see, one at a time, in
/// the Task Center. Nothing new is granted by reading a summary of it.</para>
///
/// <para>Oracle frames worklist reports the same way — the scope is the user's groups or their reportees'
/// groups, not the company — which is why widening to the whole tenant needs its own permission rather than a
/// flag on the request.</para>
///
/// <para><b>⚠ FAIL-CLOSED, and the failure looks like an empty report rather than an error.</b> Every path that
/// cannot establish a scope returns <see cref="WorkReportDto.Empty"/>: no tenant, no user, no resolved scopes,
/// or a resolver that threw. "Show everything" is the wrong default here in a way that would never be noticed —
/// an unfiltered report renders perfectly and is simply wrong about who may read it.</para>
/// </summary>
public sealed class WorkReportQueryHandler : IRequestHandler<WorkReportQuery, Response<WorkReportDto>>
{
    /// <summary>
    /// The module the scope is resolved FOR. The resolver takes a module code so a grant can be narrowed to one
    /// module; the report reads task rows, so it asks as the task module — not as a report-specific code nobody
    /// has ever granted anything against.
    /// </summary>
    private const string ScopeModuleCode = "tasks";

    private readonly IWorkReportRepository _reports;
    private readonly IDataScopeResolver _scopes;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IActorPermissionContext _permissions;
    private readonly ILogger<WorkReportQueryHandler> _logger;

    public WorkReportQueryHandler(
        IWorkReportRepository reports,
        IDataScopeResolver scopes,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IActorPermissionContext permissions,
        ILogger<WorkReportQueryHandler> logger)
    {
        _reports = reports;
        _scopes = scopes;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _logger = logger;
    }

    public async Task<Response<WorkReportDto>> Handle(WorkReportQuery query, CancellationToken ct)
    {
        /*
         * The period is REQUIRED and BOUNDED, and both halves are the same argument. An unbounded report is a
         * full-collection scan; an inverted one is a caller who has confused the two dates and would otherwise
         * get a confident, empty answer rather than a correction.
         */
        if (query.To <= query.From)
        {
            return Response<WorkReportDto>.Fail(
                "The report period must end after it starts.",
                400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        var scope = await ResolveScopeAsync(ct);
        if (scope.MatchesNothing)
        {
            /*
             * ⚠ EMPTY, NOT UNFILTERED — the whole point of this handler.
             *
             * A person with no active position assignment resolves to no scopes (the resolver fails closed three
             * separate ways), and the tempting reading of "no scopes" is "no restrictions". That reading is what
             * turns a permission model into decoration, and a report is where it would go unnoticed longest: the
             * page renders, the numbers look plausible, and nobody can tell they are somebody else's.
             */
            return Response<WorkReportDto>.Success(
                WorkReportDto.Empty(query.From, query.To, query.GroupBy), 200, query.CorrelationId);
        }

        var criteria = new WorkReportCriteria(query.From, query.To, scope, query.GroupBy);
        var report = await _reports.AggregateAsync(criteria, ct);

        return Response<WorkReportDto>.Success(report, 200, query.CorrelationId);
    }

    private async Task<WorkReportScope> ResolveScopeAsync(CancellationToken ct)
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
