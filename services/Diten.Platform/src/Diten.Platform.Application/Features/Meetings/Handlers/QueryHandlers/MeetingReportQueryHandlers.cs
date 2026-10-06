using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;

/// <summary>
/// S12 (pack §23) — builds the report ONCE, the same shape whether the caller is the screen or the export.
/// Never invoked directly by MediatR — <see cref="GetMeetingReportHandler"/> and
/// <see cref="ExportMeetingReportHandler"/> both call <see cref="BuildAsync"/>, so the file a reader downloads
/// is provably the rows they saw on screen (pack §23.7: "same query, not a second one").
/// </summary>
internal static class MeetingReportCore
{
    public static async Task<MeetingReportDto> BuildAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? meetingTypeId,
        Guid? organizerUserId,
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutesVersions,
        IRecordLinkService recordLinks,
        ITaskItemRepository tasks,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions,
        CancellationToken ct)
    {
        var hasReadAll = permissions.IsPlatformActor || permissions.Has(MeetingPermissions.ReadAll);
        var callerId = currentUser.UserId;

        // ── the ONE visibility call site (pack §23.6) — the SAME predicate GetMeetingListHandler already
        // applies for the ordinary Meetings list, never a second copy of D3. ──────────────────────────────
        var all = await meetings.ListAsync(ct);
        var meetingIds = all.Select(m => m.Id).ToList();
        var attendeesByMeeting = (await attendees.ListByMeetingIdsAsync(meetingIds, ct))
            .GroupBy(a => a.MeetingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var visible = all.Where(m =>
        {
            var attendeeIds = attendeesByMeeting.GetValueOrDefault(m.Id, []).Select(a => a.UserId).ToHashSet();
            return MeetingEligibility.CanView(m, callerId, hasReadAll, attendeeIds);
        });

        var filtered = visible
            .Where(m => m.StartAt >= from && m.StartAt < to)
            .Where(m => meetingTypeId is null || m.MeetingTypeId == meetingTypeId)
            .Where(m => organizerUserId is null || m.OrganizerUserId == organizerUserId)
            .OrderByDescending(m => m.StartAt)
            .ToList();

        var titleById = all.ToDictionary(m => m.Id, m => m.Title);
        var filteredIds = filtered.Select(m => m.Id).ToList();

        var typeNameById = (await types.ListAsync(ct)).ToDictionary(t => t.Id, t => t.Name);

        // ── meetings + attendance ────────────────────────────────────────────────────────────────────────
        var meetingRows = new List<MeetingReportMeetingRowDto>(filtered.Count);
        var totalInvited = 0;
        var totalResponded = 0;
        foreach (var m in filtered)
        {
            var rows = attendeesByMeeting.GetValueOrDefault(m.Id, []);
            var responded = rows.Count(a => a.InvitationResponse != InvitationResponse.Pending);
            totalInvited += rows.Count;
            totalResponded += responded;

            meetingRows.Add(new MeetingReportMeetingRowDto(
                m.Id, m.Title, m.MeetingTypeId, typeNameById.GetValueOrDefault(m.MeetingTypeId, string.Empty),
                m.StartAt, m.OrganizerUserId, rows.Count, responded,
                rows.Count == 0 ? null : Math.Round(100d * responded / rows.Count, 1)));
        }

        var attendanceRatePercent = totalInvited == 0 ? (double?)null : Math.Round(100d * totalResponded / totalInvited, 1);

        // ── decisions — PUBLISHED minutes only (pack §23.13/3, owner decision) ──────────────────────────────
        var publishedVersions = await minutesVersions.ListPublishedByMeetingIdsAsync(filteredIds, ct);
        var latestPublishedByMeeting = publishedVersions
            .GroupBy(v => v.MeetingId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());

        var decisionRows = filtered
            .Where(m => latestPublishedByMeeting.ContainsKey(m.Id))
            .SelectMany(m => latestPublishedByMeeting[m.Id].Decisions
                .Select(d => new MeetingReportDecisionRowDto(m.Id, m.Title, d.Code, d.Text, d.DecidedByUserId)))
            .ToList();

        // ── actions (Follow-up Register) — origin links, then the newest agenda anchor per task ────────────
        var sourceLinks = await recordLinks.ListBySourceAsync(filteredIds, ct);
        var originLinks = sourceLinks
            .Where(l => l.SourceModuleCode == RecordLinkModuleCodes.Meetings
                        && l.TargetModuleCode == RecordLinkModuleCodes.Tasks
                        && (l.LinkType == RecordLinkTypes.BornFromMeeting || l.LinkType == RecordLinkTypes.Preparation))
            .GroupBy(l => l.TargetRecordId)
            .Select(g => g.OrderBy(l => l.CreatedAt).First()) // one origin per task, the FIRST such link
            .ToList();

        var actionTaskIds = originLinks.Select(l => l.TargetRecordId).Distinct().ToList();
        var taskById = (await tasks.ListByIdsAsync(actionTaskIds, ct)).ToDictionary(t => t.Id);

        var agendaLinks = actionTaskIds.Count == 0
            ? []
            : await recordLinks.ListByTargetAsync(actionTaskIds, ct);
        var latestAgendaMeetingByTask = agendaLinks
            .Where(l => l.SourceModuleCode == RecordLinkModuleCodes.Meetings
                        && l.TargetModuleCode == RecordLinkModuleCodes.Tasks
                        && l.LinkType == RecordLinkTypes.Agenda)
            .GroupBy(l => l.TargetRecordId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedAt).First().SourceRecordId);

        // Current-meeting ids may fall outside `filtered`/`titleById` (a continuation the caller cannot
        // otherwise see, or one outside this period) — resolved in one extra batched call, never per row.
        var currentMeetingIds = latestAgendaMeetingByTask.Values.Distinct()
            .Where(id => !titleById.ContainsKey(id))
            .ToList();
        if (currentMeetingIds.Count > 0)
        {
            foreach (var m in await meetings.ListByIdsAsync(currentMeetingIds, ct))
            {
                titleById.TryAdd(m.Id, m.Title);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var actionRows = new List<MeetingReportActionRowDto>();
        foreach (var link in originLinks)
        {
            if (!taskById.TryGetValue(link.TargetRecordId, out var task))
            {
                continue; // the dangling-link rule every other RecordLink read already follows (pack §13).
            }

            var currentMeetingId = latestAgendaMeetingByTask.GetValueOrDefault(link.TargetRecordId, link.SourceRecordId);
            var isOpen = task.Lifecycle != TaskLifecycle.Done && task.Lifecycle != TaskLifecycle.Cancelled;
            var isOverdue = isOpen && task.DueAt is { } due && due < now;

            actionRows.Add(new MeetingReportActionRowDto(
                task.Id, task.Title, task.Lifecycle.ToString(), task.DueAt, isOverdue, task.AssigneeUserId,
                link.SourceRecordId, titleById.GetValueOrDefault(link.SourceRecordId, string.Empty),
                currentMeetingId, titleById.GetValueOrDefault(currentMeetingId, string.Empty)));
        }

        var openActionCount = actionRows.Count(a => a.Lifecycle != nameof(TaskLifecycle.Done) && a.Lifecycle != nameof(TaskLifecycle.Cancelled));
        var overdueActionCount = actionRows.Count(a => a.IsOverdue);

        var totals = new MeetingReportTotalsDto(
            filtered.Count, attendanceRatePercent, decisionRows.Count, openActionCount, overdueActionCount);

        return new MeetingReportDto(
            from, to, hasReadAll ? MeetingReportDto.ScopeTenant : MeetingReportDto.ScopeScoped,
            totals, meetingRows, decisionRows, actionRows);
    }
}

public sealed class GetMeetingReportHandler : IRequestHandler<GetMeetingReportQuery, Response<MeetingReportDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutesVersions;
    private readonly IRecordLinkService _recordLinks;
    private readonly ITaskItemRepository _tasks;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;

    public GetMeetingReportHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutesVersions,
        IRecordLinkService recordLinks,
        ITaskItemRepository tasks,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _minutesVersions = minutesVersions;
        _recordLinks = recordLinks;
        _tasks = tasks;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Response<MeetingReportDto>> Handle(GetMeetingReportQuery query, CancellationToken ct)
    {
        if (query.To <= query.From)
        {
            return Response<MeetingReportDto>.Fail(
                "The report period must end after it starts.", 400, MeetingReasonCodes.ReportInvalidPeriod, query.CorrelationId);
        }

        var dto = await MeetingReportCore.BuildAsync(
            query.From, query.To, query.MeetingTypeId, query.OrganizerUserId,
            _meetings, _types, _attendees, _minutesVersions, _recordLinks, _tasks, _currentUser, _permissions, ct);

        return Response<MeetingReportDto>.Success(dto, 200, query.CorrelationId);
    }
}

/// <summary>
/// S12 export — one dataset per call (pack §23.7). Every file handed out leaves exactly one
/// <see cref="AuditCategory.DataExport"/> record or is not handed out (BL-347): the record goes through
/// <see cref="IDataExportAuditWriter"/>, which resolves the actor and tenant from the authenticated request —
/// this handler names neither. When the record cannot be written the export is refused
/// (<see cref="DataExportAuditReasonCodes.NotRecorded"/>, 503), the same posture
/// <c>WorkReportExportQueryHandler</c> already takes.
/// </summary>
public sealed class ExportMeetingReportHandler : IRequestHandler<ExportMeetingReportQuery, Response<MeetingReportExportResultDto>>
{
    internal const string AuditSourceModule = "MOD-0357";
    internal const string AuditRequestType = "Meetings.ExportMeetingReportQuery";

    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutesVersions;
    private readonly IRecordLinkService _recordLinks;
    private readonly ITaskItemRepository _tasks;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;
    private readonly IDataExportAuditWriter _exportAudit;

    public ExportMeetingReportHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutesVersions,
        IRecordLinkService recordLinks,
        ITaskItemRepository tasks,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions,
        IDataExportAuditWriter exportAudit)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _minutesVersions = minutesVersions;
        _recordLinks = recordLinks;
        _tasks = tasks;
        _currentUser = currentUser;
        _permissions = permissions;
        _exportAudit = exportAudit;
    }

    public async Task<Response<MeetingReportExportResultDto>> Handle(ExportMeetingReportQuery query, CancellationToken ct)
    {
        if (!MeetingReportExportFormats.TryParse(query.Format, out var format))
        {
            return Response<MeetingReportExportResultDto>.Fail(
                "The export format must be csv or json.", 400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        if (!MeetingReportDatasets.IsValid(query.Dataset))
        {
            return Response<MeetingReportExportResultDto>.Fail(
                "The dataset must be meetings, decisions or actions.", 400, TaskReasonCodes.ValidationFailed, query.CorrelationId);
        }

        if (query.To <= query.From)
        {
            return Response<MeetingReportExportResultDto>.Fail(
                "The report period must end after it starts.", 400, MeetingReasonCodes.ReportInvalidPeriod, query.CorrelationId);
        }

        // ⚠ SAME QUERY, NOT A SECOND ONE (pack §23.7) — the export reads through the identical
        // MeetingReportCore.BuildAsync the screen calls; someone who sees N on screen downloads N.
        var report = await MeetingReportCore.BuildAsync(
            query.From, query.To, query.MeetingTypeId, query.OrganizerUserId,
            _meetings, _types, _attendees, _minutesVersions, _recordLinks, _tasks, _currentUser, _permissions, ct);

        var isJson = format == MeetingReportExportFormats.Json;
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var disclaimer = MeetingReportExportDisclaimer.For(query.Locale, generatedAtUtc);

        int rowCount;
        byte[] content;
        switch (query.Dataset)
        {
            case MeetingReportDatasets.Meetings:
                rowCount = report.Meetings.Count;
                if (rowCount > MeetingReportExportLimits.MaxRows)
                {
                    return TooLarge(rowCount, query.CorrelationId);
                }

                content = isJson
                    ? MeetingReportExportSerializer.ToJson(report.Meetings, disclaimer)
                    : MeetingReportExportSerializer.ToCsv(report.Meetings, disclaimer);
                break;

            case MeetingReportDatasets.Decisions:
                rowCount = report.Decisions.Count;
                if (rowCount > MeetingReportExportLimits.MaxRows)
                {
                    return TooLarge(rowCount, query.CorrelationId);
                }

                content = isJson
                    ? MeetingReportExportSerializer.ToJson(report.Decisions, disclaimer)
                    : MeetingReportExportSerializer.ToCsv(report.Decisions, disclaimer);
                break;

            default: // Actions — validated above
                rowCount = report.Actions.Count;
                if (rowCount > MeetingReportExportLimits.MaxRows)
                {
                    return TooLarge(rowCount, query.CorrelationId);
                }

                content = isJson
                    ? MeetingReportExportSerializer.ToJson(report.Actions, disclaimer)
                    : MeetingReportExportSerializer.ToCsv(report.Actions, disclaimer);
                break;
        }

        // ⚠ AFTER THE FILE EXISTS, BEFORE IT IS RETURNED — WorkReportExportQueryHandler's own ordering,
        // reused verbatim: earlier, a serializer failure would leave a record of a file nobody received;
        // later, the file would already be on its way whatever the audit answered.
        var audit = await _exportAudit.RecordAsync(
            new DataExportAuditEntry(
                AuditSourceModule,
                AuditRequestType,
                "MeetingReport",
                format,
                rowCount,
                AuditFilterSummary(query),
                Dataset: query.Dataset,
                RequestCorrelationId: query.CorrelationId),
            ct);

        if (!audit.IsRecorded)
        {
            return Response<MeetingReportExportResultDto>.Fail(
                "The export could not be recorded in the audit trail, so the file was not issued. Try again later.",
                503, DataExportAuditReasonCodes.NotRecorded, query.CorrelationId);
        }

        return Response<MeetingReportExportResultDto>.Success(
            new MeetingReportExportResultDto(
                content,
                isJson ? "application/json" : "text/csv; charset=utf-8",
                MeetingReportExportFormats.FileName(query.Dataset, query.From, query.To, format, query.Locale),
                rowCount),
            200,
            query.CorrelationId);
    }

    private static Response<MeetingReportExportResultDto> TooLarge(int total, string correlationId) =>
        Response<MeetingReportExportResultDto>.Fail(
            $"The report covers {total} rows; an export is limited to {MeetingReportExportLimits.MaxRows}. "
            + "Narrow the period or the filters.",
            400, MeetingReasonCodes.ReportExportTooLarge, correlationId);

    /// <summary>The filters the file was cut under, as the audit record carries them (pack §23.7) — the
    /// ORGANIZER is a person, so only the fact that it was applied is (<see cref="DataExportFilterSummary.Person"/>).</summary>
    internal static IReadOnlyDictionary<string, string?> AuditFilterSummary(ExportMeetingReportQuery query) =>
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["from"] = query.From.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["to"] = query.To.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["meetingTypeId"] = query.MeetingTypeId?.ToString(),
            ["organizer"] = DataExportFilterSummary.Person(query.OrganizerUserId)
        };
}
