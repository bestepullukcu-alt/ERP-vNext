using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

/// <summary>
/// MOD-0024 Dilim 1e — THE REPORT'S ROWS, AS A FILE.
/// </summary>
/// <param name="Format"><c>csv</c> (the default) or <c>json</c> — the audit export's two.</param>
/// <param name="Filter">
/// The SAME five filters the report on screen was computed under. ⚠ They narrow the caller's scope and can
/// never widen it — see <see cref="WorkReportFilter"/>.
/// </param>
/// <param name="ScopePreference">
/// The SAME preference the loaded report used, for the reason <see cref="WorkReportItemsQuery"/> takes it: a
/// file counted under "your scope" beside a screen counted under the whole tenant is two answers to one question.
/// </param>
public sealed record WorkReportExportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string? Format,
    string CorrelationId,
    WorkReportFilter? Filter = null,
    WorkReportScopePreference? ScopePreference = null)
    : IRequest<Response<WorkReportExportResultDto>>;

/// <summary>
/// Resolves WHOSE work may be exported, then asks for the report's own rows.
///
/// <para><b>⚠ THE SCOPE COMES FROM <see cref="IWorkReportScopeSource"/> AND NOWHERE ELSE</b> — the same object
/// the report's handler and the list's handler ask. So the file is the numbers' own row set: someone who sees 12
/// on screen downloads 12, not 13. The sabotage this guards against is an export wired to its own query that
/// skips the resolver, and <c>WorkReportExportTests</c> turns red on it.</para>
///
/// <para><b>The permission is the report's own</b> (<c>WorkReportRead</c>, at the endpoint). Downloading the rows
/// grants nothing a reader did not already have: they could open every one of them from the tiles, fifty at a
/// time. A separate export permission would be one more row in the catalogue that nobody remembers to grant.</para>
///
/// <para><b>Too many rows is REFUSED, not trimmed.</b> A cut file looks complete; the reader would pivot it,
/// get numbers that disagree with the screen, and have nothing to tell them why. Refused, they narrow the filter.</para>
///
/// <para><b>⚠ NOT META-AUDITED, and that is a gap, not a choice</b> — the audit export writes an
/// <c>AuditMetaAuditWriter</c> record, but that writer stamps every event <c>PlatformAdministrator</c> /
/// <c>IsPlatformGlobal</c> / <c>SourceModule = "Audit"</c>. A tenant user's download recorded through it would
/// say a platform administrator did it. Recorded in the pack as an open item rather than written wrongly.</para>
/// </summary>
public sealed class WorkReportExportQueryHandler
    : IRequestHandler<WorkReportExportQuery, Response<WorkReportExportResultDto>>
{
    private readonly IWorkReportRepository _reports;
    private readonly IWorkReportScopeSource _scope;

    public WorkReportExportQueryHandler(IWorkReportRepository reports, IWorkReportScopeSource scope)
    {
        _reports = reports;
        _scope = scope;
    }

    public async Task<Response<WorkReportExportResultDto>> Handle(WorkReportExportQuery query, CancellationToken ct)
    {
        if (!WorkReportExportFormats.TryParse(query.Format, out var format))
        {
            return Response<WorkReportExportResultDto>.Fail(
                "The export format must be csv or json.",
                400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        // The report's own bounded period — the export is the same read and refuses the same unbounded scan.
        if (query.To <= query.From)
        {
            return Response<WorkReportExportResultDto>.Fail(
                "The report period must end after it starts.",
                400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        var scope = await _scope.ResolveAsync(query.ScopePreference, ct);

        /*
         * ⚠ SCOPE FIRST, FILTER SECOND — the report's own order, so every filter is an intersection. No group
         * axis and no comparison: the file is the totals' rows, and every group column is in it to pivot by.
         */
        var criteria = new WorkReportCriteria(query.From, query.To, scope, WorkReportGroupBy.None, query.Filter);

        // Fail-closed, and shaped like a real answer: a header and no rows. "You may export no work" is true.
        var set = scope.MatchesNothing
            ? WorkReportExportSet.Empty
            : await _reports.ExportAsync(criteria, WorkReportExportLimits.MaxRows, ct);

        if (set.Total > WorkReportExportLimits.MaxRows)
        {
            return Response<WorkReportExportResultDto>.Fail(
                $"The report covers {set.Total} tasks; an export is limited to {WorkReportExportLimits.MaxRows}. "
                + "Narrow the period or the filters.",
                400, TaskReasonCodes.WorkReportExportTooLarge, query.CorrelationId);
        }

        var isJson = format == WorkReportExportFormats.Json;

        return Response<WorkReportExportResultDto>.Success(
            new WorkReportExportResultDto(
                isJson ? WorkReportExportSerializer.ToJson(set.Rows) : WorkReportExportSerializer.ToCsv(set.Rows),
                isJson ? "application/json" : "text/csv; charset=utf-8",
                WorkReportExportFormats.FileName(query.From, query.To, format),
                set.Rows.Count),
            200,
            query.CorrelationId);
    }
}
