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
/// <param name="Filter">
/// Optional narrowing. ⚠ It NARROWS the caller's scope and can never widen it — see
/// <see cref="WorkReportFilter"/>. Nothing here is a permission.
/// </param>
public sealed record WorkReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    WorkReportGroupBy GroupBy,
    string CorrelationId,
    WorkReportFilter? Filter = null,
    bool ComparePrevious = false)
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
    private readonly IWorkReportRepository _reports;

    /// <summary>
    /// ⚠ The ONE scope resolution, shared with the items handler — see <see cref="IWorkReportScopeSource"/>.
    /// It used to live inline here, and Dilim 1c moved it out rather than letting a second handler copy it.
    /// </summary>
    private readonly IWorkReportScopeSource _scope;

    public WorkReportQueryHandler(IWorkReportRepository reports, IWorkReportScopeSource scope)
    {
        _reports = reports;
        _scope = scope;
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

        var scope = await _scope.ResolveAsync(ct);
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

        /*
         * ⚠ SCOPE FIRST, FILTER SECOND — AND THE ORDER IS THE SECURITY BOUNDARY.
         *
         * The scope is resolved above from the caller's data entitlements; the filter is what the caller TYPED.
         * Handing both to the criteria in this order is what makes the filter an intersection: a query string
         * naming somebody else's id narrows an already-narrowed set to nothing rather than reaching past it.
         * There is no arrangement of these two in which the filter grants anything.
         */
        var criteria = new WorkReportCriteria(
            query.From, query.To, scope, query.GroupBy, query.Filter, query.ComparePrevious);
        var report = await _reports.AggregateAsync(criteria, ct);

        return Response<WorkReportDto>.Success(report, 200, query.CorrelationId);
    }
}
