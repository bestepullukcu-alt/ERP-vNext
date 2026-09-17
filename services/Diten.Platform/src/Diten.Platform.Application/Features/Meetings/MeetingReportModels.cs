using MediatR;
using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.Meetings;

/// <summary>
/// MOD-0357 S12 (pack §23) — the meeting report and its follow-up (action) register: period/type/organizer-
/// filtered meeting list, attendance rate, published decisions, and the open/overdue/closed actions born from
/// a meeting. Read-only: no new persisted field, no new writer, same posture MOD-0024's own work-report pack
/// takes toward <c>TaskItem</c> (<c>MOD-0024-task-closure-and-reporting.md</c> §3).
///
/// <para><b>ONE visibility call site.</b> Both the screen query and the export query resolve the SAME filtered,
/// visible meeting set through <c>MeetingReportCore.ResolveVisibleMeetingsAsync</c> — which itself calls
/// <c>MeetingEligibility.CanView</c>, the identical predicate <c>GetMeetingListHandler</c> already applies for
/// the ordinary Meetings list (pack §23.6: "the report never widens what a read holder could already see one
/// meeting at a time"). Neither query re-derives visibility on its own.</para>
/// </summary>
public sealed record GetMeetingReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? MeetingTypeId,
    Guid? OrganizerUserId,
    string CorrelationId)
    : IRequest<Response<MeetingReportDto>>;

/// <summary>
/// S12 export — same filters, same visible meeting set, one of three grains at a time (pack §23.7: "a
/// 'toplantı satırı + karar satırı + aksiyon satırı' CSV would need three different column sets fighting for
/// one header row").
/// </summary>
/// <param name="Dataset">One of <see cref="MeetingReportDatasets.Meetings"/>/<see cref="MeetingReportDatasets.Decisions"/>/
/// <see cref="MeetingReportDatasets.Actions"/>.</param>
/// <param name="Format"><c>csv</c> (default) or <c>json</c>.</param>
/// <param name="Locale">The reader's own UI language (<c>window.CurrentLanguage</c>, forwarded by the Web
/// proxy) — used ONLY to pick which of the seven hardcoded sentences
/// <see cref="Services.MeetingReportExportDisclaimer"/> prepends to the file. Platform has no
/// <c>IStringLocalizer</c> (the same measured fact <c>WorkReportModels.cs</c> already records for its own
/// export's column headers); this is not a general localization mechanism, just the one sentence the owner's
/// decision requires at the top of every downloaded file.</param>
public sealed record ExportMeetingReportQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid? MeetingTypeId,
    Guid? OrganizerUserId,
    string Dataset,
    string? Format,
    string? Locale,
    string CorrelationId)
    : IRequest<Response<MeetingReportExportResultDto>>;

/// <summary>The report's own totals tile row.</summary>
/// <param name="AttendanceRatePercent">Responded (Accepted or Declined) ÷ invited, over every attendee row on
/// every visible meeting in the filtered set — null when the set has no attendee rows at all (never a
/// fabricated 0%, the same "null is a real answer" posture <c>WorkReportBucket.Label</c> already takes).</param>
public sealed record MeetingReportTotalsDto(
    int MeetingCount,
    double? AttendanceRatePercent,
    int DecisionCount,
    int OpenActionCount,
    int OverdueActionCount);

/// <summary>One row of the filtered meeting list.</summary>
public sealed record MeetingReportMeetingRowDto(
    Guid Id,
    string Title,
    Guid MeetingTypeId,
    string MeetingTypeName,
    DateTimeOffset StartAt,
    Guid OrganizerUserId,
    int AttendeeCount,
    int RespondedCount,
    double? AttendanceRatePercent);

/// <summary>One PUBLISHED decision — never a draft's (pack §23.13/3, owner decision 2026-09-15).</summary>
public sealed record MeetingReportDecisionRowDto(
    Guid MeetingId,
    string MeetingTitle,
    string Code,
    string Text,
    Guid? DecidedByUserId);

/// <summary>
/// One action born from a meeting — the Follow-up Register row.
/// </summary>
/// <param name="Lifecycle">A STRING, not the enum — the same wire convention <c>WorkReportItem.Lifecycle</c>
/// already follows, for the identical reason: <c>TaskLifecycle</c> carries no
/// <c>JsonStringEnumConverter</c>.</param>
/// <param name="IsOverdue">Open AND <c>DueAt</c> is in the past AT QUERY TIME — never frozen to the filter
/// period's own end (pack §23.4, §23.9).</param>
/// <param name="OriginMeetingId">
/// The meeting the action's <c>bornFromMeeting</c>/<c>preparation</c> <c>RecordLink</c> names — what the
/// action counts AGAINST, and never reassigned by a later S7 carry-forward (pack §23.4).
/// </param>
/// <param name="CurrentMeetingId">
/// Owner decision 2026-09-15 (pack §23.13/2) — the continuation meeting the action's open agenda line
/// CURRENTLY sits on (the newest <c>agenda</c>-type <c>RecordLink</c> for this task), or the origin meeting
/// itself when it was never carried forward. Secondary, non-attributing — see <see cref="OriginMeetingId"/>.
/// </param>
public sealed record MeetingReportActionRowDto(
    Guid TaskId,
    string Title,
    string Lifecycle,
    DateTimeOffset? DueAt,
    bool IsOverdue,
    Guid? AssigneeUserId,
    Guid OriginMeetingId,
    string OriginMeetingTitle,
    Guid CurrentMeetingId,
    string CurrentMeetingTitle);

/// <param name="ScopeApplied">
/// <see cref="MeetingReportDto.ScopeTenant"/> for a <c>read-all</c> holder, <see cref="MeetingReportDto.ScopeScoped"/>
/// otherwise — stated, never implied, the same reason <c>WorkReportDto.ScopeApplied</c> exists: so a reader
/// can tell "there are no meetings" from "there are no meetings I may see".
/// </param>
public sealed record MeetingReportDto(
    DateTimeOffset From,
    DateTimeOffset To,
    string ScopeApplied,
    MeetingReportTotalsDto Totals,
    IReadOnlyList<MeetingReportMeetingRowDto> Meetings,
    IReadOnlyList<MeetingReportDecisionRowDto> Decisions,
    IReadOnlyList<MeetingReportActionRowDto> Actions)
{
    public const string ScopeTenant = "tenant";
    public const string ScopeScoped = "scoped";

    public static MeetingReportDto Empty(DateTimeOffset from, DateTimeOffset to) => new(
        from, to, ScopeScoped,
        new MeetingReportTotalsDto(0, null, 0, 0, 0),
        [], [], []);
}

/// <summary>The three grains an export may ask for — never blended into one file (pack §23.7).</summary>
public static class MeetingReportDatasets
{
    public const string Meetings = "meetings";
    public const string Decisions = "decisions";
    public const string Actions = "actions";

    public static bool IsValid(string? value) =>
        value is Meetings or Decisions or Actions;
}

public static class MeetingReportExportFormats
{
    public const string Csv = "csv";
    public const string Json = "json";

    public const string FilePrefixEnglish = "meeting-report";
    public const string FilePrefixTurkish = "toplanti-raporu";

    public static bool TryParse(string? value, out string format)
    {
        format = string.IsNullOrWhiteSpace(value) ? Csv : value.Trim().ToLowerInvariant();
        return format is Csv or Json;
    }

    /// <summary>
    /// <c>toplanti-raporu-aksiyonlar_2026-08-11_2026-09-10.csv</c> — the same "last day COUNTED, not the
    /// exclusive <c>To</c>" rule <c>WorkReportExportFormats.FileName</c> already follows.
    /// </summary>
    public static string FileName(string dataset, DateTimeOffset from, DateTimeOffset to, string format, string? locale)
    {
        var prefix = string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) ? FilePrefixTurkish : FilePrefixEnglish;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{prefix}-{dataset}_{from.ToUniversalTime():yyyy-MM-dd}_{to.ToUniversalTime().AddTicks(-1):yyyy-MM-dd}.{format}");
    }
}

/// <summary>The audit export's own limit, copied rather than invented (pack §23.7).</summary>
public static class MeetingReportExportLimits
{
    public const int MaxRows = 50_000;
}

public sealed record MeetingReportExportResultDto(
    byte[] Content,
    string ContentType,
    string FileName,
    int RowCount);
