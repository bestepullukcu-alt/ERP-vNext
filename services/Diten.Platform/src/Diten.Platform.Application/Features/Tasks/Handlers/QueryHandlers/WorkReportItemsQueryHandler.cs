using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 Dilim 1c — THE WORK BEHIND ONE OF THE REPORT'S NUMBERS.
/// </summary>
/// <param name="Bucket">Which cell of the report was clicked.</param>
/// <param name="Argument">The outcome code, for <see cref="WorkReportBucketKind.Outcome"/> only.</param>
/// <param name="GroupKey">Which row of the breakdown, or null for the totals.</param>
/// <param name="Filter">
/// The SAME optional narrowing the numbers were computed under. ⚠ It narrows the caller's scope and can never
/// widen it — see <see cref="WorkReportFilter"/>. Nothing here is a permission.
/// </param>
public sealed record WorkReportItemsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    WorkReportBucketKind Bucket,
    string CorrelationId,
    WorkReportGroupBy GroupBy = WorkReportGroupBy.None,
    string? Argument = null,
    string? GroupKey = null,
    int Skip = 0,
    WorkReportFilter? Filter = null)
    : IRequest<Response<WorkReportItemsDto>>;

/// <summary>
/// Resolves WHOSE work may be listed, then asks for the rows behind one number.
///
/// <para><b>⚠ THE SAME SCOPE AS THE NUMBERS, FROM THE SAME PLACE.</b> This handler resolves nothing of its own:
/// it asks <see cref="IWorkReportScopeSource"/>, which is the code the report's own handler asks. A second copy
/// of that twenty-line resolution — the obvious way to write this class — would be a rule enforced in two
/// places, and the day the two fell out of step nothing would crash: one endpoint would simply start listing
/// work the other refused to count.</para>
///
/// <para><b>A CLICK IS NOT A NEW QUESTION.</b> Everything this endpoint can be asked for is a cell the report
/// already published. It cannot be pointed at a period the report did not run, a filter it did not apply, or a
/// row the scope excluded — the repository selects from the report's own row set rather than issuing a query of
/// its own. The list is a way to SEE a number's contents, never a way around the number's boundaries.</para>
///
/// <para><b>Fail-closed, and it looks like an empty page rather than an error</b>, for the same reason
/// <see cref="WorkReportDto.Empty"/> exists: "you may see no work" is a true answer, and a 403 would make the
/// screen build a second rendering path for a state that is not a failure.</para>
/// </summary>
public sealed class WorkReportItemsQueryHandler
    : IRequestHandler<WorkReportItemsQuery, Response<WorkReportItemsDto>>
{
    private readonly IWorkReportRepository _reports;
    private readonly IWorkReportScopeSource _scope;

    public WorkReportItemsQueryHandler(IWorkReportRepository reports, IWorkReportScopeSource scope)
    {
        _reports = reports;
        _scope = scope;
    }

    public async Task<Response<WorkReportItemsDto>> Handle(
        WorkReportItemsQuery query,
        CancellationToken ct)
    {
        // The same bounded, required period the report insists on — an unbounded list is the full-collection
        // scan the criteria were written to refuse, and an inverted one is a caller who confused the dates.
        if (query.To <= query.From)
        {
            return Response<WorkReportItemsDto>.Fail(
                "The report period must end after it starts.",
                400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        var scope = await _scope.ResolveAsync(ct);

        /*
         * ⚠ SCOPE FIRST, FILTER SECOND — the same order, and the same reason. The scope comes from the caller's
         * data entitlements; the bucket, the group key and the five filters are what the caller TYPED. Building
         * the criteria in this order is what makes every one of them an intersection: naming somebody else's id
         * narrows an already-narrowed set to nothing rather than reaching past it.
         */
        var criteria = new WorkReportItemsCriteria(
            new WorkReportCriteria(query.From, query.To, scope, query.GroupBy, query.Filter),
            query.Bucket,
            query.Argument,
            query.GroupKey,
            query.Skip);

        if (scope.MatchesNothing)
        {
            // "No scopes" reads as "no restrictions" to anyone skimming, which is how an authorization model
            // becomes decoration. Empty — never the unfiltered list.
            return Response<WorkReportItemsDto>.Success(
                WorkReportItemsDto.Empty(criteria), 200, query.CorrelationId);
        }

        var items = await _reports.ItemsAsync(criteria, ct);
        return Response<WorkReportItemsDto>.Success(items, 200, query.CorrelationId);
    }
}
