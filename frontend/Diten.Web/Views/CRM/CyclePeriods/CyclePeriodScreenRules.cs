using System.Globalization;

namespace Diten.Web.Views.CRM.CyclePeriods;

/// <summary>
/// WP-CYC-UI-1 — the Dönemler screen's display rules, in ONE testable place. The browser renders what these return and
/// decides nothing itself: the year timeline (dynamic axis, scope lanes, gaps, overlaps, today line), which panel fields
/// are editable for a period's status, the panel's live warnings (end after start, sequence taken, active overlap,
/// other-level overlap), the "find the effective period" outcome, and the "open periods without a capacity" band.
/// <para><b>None of this is authority.</b> The CrmService still enforces every rule on write (code immutable, scope
/// immutable, structural fields frozen once active, sequence unique per scope and year, active-overlap ban). These
/// rules only keep the screen from inviting an edit the runtime would refuse, and say why before the author saves.</para>
/// </summary>
public static class CyclePeriodScreenRules
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Closed = "closed";

    /// <summary>The four scope levels in resolution precedence order — the timeline lists lanes in this order.</summary>
    public static readonly IReadOnlyList<string> ScopeOrder = ["tenant", "country", "legal-entity", "business-unit"];

    // ── Timeline ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The year timeline. The axis is DYNAMIC (E10): the filtered year ± 1, or this year ± 1 when no year is chosen —
    /// never a fixed range. Rows are grouped into one lane per scope address (level + reference), lanes ordered by
    /// precedence then reference. Positions are percentages of the axis, clipped to it, so the browser only lays out.
    /// <para>A <b>gap</b> is a run of days inside the axis, between the lane's first and last period, that no period of
    /// the lane covers. An <b>overlap</b> is a run of days two periods of the same lane share; it is a
    /// <c>conflict</c> when both are active (the runtime forbids that — it can only exist as a data defect) and a
    /// <c>overlap</c> otherwise (drafts may overlap). Closed periods are drawn but take part in neither.</para>
    /// </summary>
    public static CyclePeriodTimeline BuildTimeline(
        IReadOnlyList<CyclePeriodRow> rows, int? filterYear, DateOnly today)
    {
        var centre = filterYear ?? today.Year;
        var axisStart = new DateOnly(centre - 1, 1, 1);
        var axisEnd = new DateOnly(centre + 1, 12, 31);
        var axisDays = axisEnd.DayNumber - axisStart.DayNumber + 1;

        double Pct(DateOnly day) => Math.Round((day.DayNumber - axisStart.DayNumber) * 100.0 / axisDays, 4);
        double WidthPct(DateOnly from, DateOnly to) => Math.Round((to.DayNumber - from.DayNumber + 1) * 100.0 / axisDays, 4);

        var visible = rows
            .Where(r => r.EndDate >= axisStart && r.StartDate <= axisEnd)
            .ToList();

        var lanes = visible
            .GroupBy(r => (Type: NormalizeScope(r.ScopeType), Ref: ScopeRefKey(r)))
            .OrderBy(g => ScopeRank(g.Key.Type))
            .ThenBy(g => g.Key.Ref, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var periods = g.OrderBy(r => r.StartDate).ThenBy(r => r.CycleCode, StringComparer.OrdinalIgnoreCase).ToList();
                var bars = periods.Select(r =>
                {
                    var from = Max(r.StartDate, axisStart);
                    var to = Min(r.EndDate, axisEnd);
                    return new CyclePeriodTimelineBar(
                        r.CyclePeriodId, r.CycleCode, r.CycleName, NormalizeStatus(r.CycleStatus),
                        r.StartDate, r.EndDate, Pct(from), WidthPct(from, to),
                        ClippedStart: r.StartDate < axisStart, ClippedEnd: r.EndDate > axisEnd);
                }).ToList();

                var live = periods.Where(r => NormalizeStatus(r.CycleStatus) != Closed).ToList();
                var marks = new List<CyclePeriodTimelineMark>();
                marks.AddRange(Gaps(live, axisStart, axisEnd).Select(x =>
                    new CyclePeriodTimelineMark("gap", x.From, x.To, Pct(x.From), WidthPct(x.From, x.To), [])));
                marks.AddRange(Overlaps(live, axisStart, axisEnd).Select(x =>
                    new CyclePeriodTimelineMark(x.Kind, x.From, x.To, Pct(x.From), WidthPct(x.From, x.To), x.Codes)));

                return new CyclePeriodTimelineLane(g.Key.Type, g.Key.Ref, LaneLabelRef(periods[0]), bars, marks);
            })
            .ToList();

        var years = Enumerable.Range(centre - 1, 3)
            .Select(y => new CyclePeriodTimelineTick(
                y, Pct(new DateOnly(y, 1, 1)), WidthPct(new DateOnly(y, 1, 1), new DateOnly(y, 12, 31))))
            .ToList();
        var quarters = Enumerable.Range(centre - 1, 3)
            .SelectMany(y => new[] { 1, 4, 7, 10 }.Select(m => (y, m)))
            .Select(x => Pct(new DateOnly(x.y, x.m, 1)))
            .ToList();

        return new CyclePeriodTimeline(
            axisStart, axisEnd, centre - 1, centre + 1, years, quarters,
            today >= axisStart && today <= axisEnd ? Pct(today) : null,
            lanes);
    }

    /// <summary>Days inside the axis, between the lane's first and last live period, that no live period covers.</summary>
    public static IReadOnlyList<(DateOnly From, DateOnly To)> Gaps(
        IReadOnlyList<CyclePeriodRow> live, DateOnly axisStart, DateOnly axisEnd)
    {
        var result = new List<(DateOnly, DateOnly)>();
        DateOnly? coveredTo = null;
        foreach (var row in live.OrderBy(r => r.StartDate))
        {
            if (coveredTo is { } end && row.StartDate > end.AddDays(1))
            {
                var from = Max(end.AddDays(1), axisStart);
                var to = Min(row.StartDate.AddDays(-1), axisEnd);
                if (from <= to)
                {
                    result.Add((from, to));
                }
            }

            coveredTo = coveredTo is { } c && c > row.EndDate ? c : row.EndDate;
        }

        return result;
    }

    /// <summary>Pairwise overlaps of the lane's live periods, clipped to the axis.</summary>
    public static IReadOnlyList<(string Kind, DateOnly From, DateOnly To, IReadOnlyList<string> Codes)> Overlaps(
        IReadOnlyList<CyclePeriodRow> live, DateOnly axisStart, DateOnly axisEnd)
    {
        var result = new List<(string, DateOnly, DateOnly, IReadOnlyList<string>)>();
        for (var i = 0; i < live.Count; i++)
        {
            for (var j = i + 1; j < live.Count; j++)
            {
                var a = live[i];
                var b = live[j];
                var from = Max(Max(a.StartDate, b.StartDate), axisStart);
                var to = Min(Min(a.EndDate, b.EndDate), axisEnd);
                if (from > to)
                {
                    continue;
                }

                var kind = NormalizeStatus(a.CycleStatus) == Active && NormalizeStatus(b.CycleStatus) == Active
                    ? "conflict"
                    : "overlap";
                result.Add((kind, from, to, new[] { a.CycleCode, b.CycleCode }));
            }
        }

        return result;
    }

    // ── Panel field states (K-3, K-2) ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Which panel fields may be edited. New: everything. Draft: everything except the code (set once, K-2) and the
    /// scope (half of the identity). Active: name and description only (K-3) — the panel then offers "close and open a
    /// new period". Closed: nothing.
    /// </summary>
    public static CyclePeriodFieldStates FieldStates(string? status, bool isNew)
    {
        if (isNew)
        {
            return new CyclePeriodFieldStates(
                Code: true, Name: true, Description: true, Year: true, Sequence: true, Dates: true, Scope: true,
                ReadOnly: false, OfferCloseAndReopen: false);
        }

        return NormalizeStatus(status) switch
        {
            Draft => new CyclePeriodFieldStates(
                Code: false, Name: true, Description: true, Year: true, Sequence: true, Dates: true, Scope: false,
                ReadOnly: false, OfferCloseAndReopen: false),
            Active => new CyclePeriodFieldStates(
                Code: false, Name: true, Description: true, Year: false, Sequence: false, Dates: false, Scope: false,
                ReadOnly: false, OfferCloseAndReopen: true),
            _ => new CyclePeriodFieldStates(
                Code: false, Name: false, Description: false, Year: false, Sequence: false, Dates: false, Scope: false,
                ReadOnly: true, OfferCloseAndReopen: false)
        };
    }

    /// <summary>Which lifecycle actions the details page offers: draft → Activate + Close (E3); active → Close;
    /// closed → none. The runtime still guards each with its own permission.</summary>
    public static CyclePeriodActions Actions(string? status, bool canActivate, bool canManage)
        => NormalizeStatus(status) switch
        {
            Draft => new CyclePeriodActions(Activate: canActivate, Close: canActivate, Edit: canManage),
            Active => new CyclePeriodActions(Activate: false, Close: canActivate, Edit: canManage),
            _ => new CyclePeriodActions(Activate: false, Close: false, Edit: false)
        };

    // ── Panel live checks (E4, E5, overlap) ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The panel's warnings for a draft, judged against the tenant's rows (the period itself excluded).
    /// <list type="bullet">
    /// <item><description><c>end_not_after_start</c> — the end must be after the start (E4).</description></item>
    /// <item><description><c>sequence_taken</c> — the year's sequence is held in this scope by another period, closed
    /// ones included (E5).</description></item>
    /// <item><description><c>active_overlap</c> — an ACTIVE period of the same scope shares days; activation would be
    /// refused. Links to it.</description></item>
    /// <item><description><c>other_level_overlap</c> — a non-closed period at another level shares days. Information
    /// only: different levels may overlap, precedence decides.</description></item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<CyclePeriodFormWarning> Check(CyclePeriodDraft draft, IReadOnlyList<CyclePeriodRow> rows)
    {
        var warnings = new List<CyclePeriodFormWarning>();
        var others = rows.Where(r => draft.CyclePeriodId is null || r.CyclePeriodId != draft.CyclePeriodId).ToList();
        var scope = NormalizeScope(draft.ScopeType);
        var scopeRef = NormalizeRef(scope, draft.ScopeRef);

        if (draft.StartDate is { } s && draft.EndDate is { } e && e <= s)
        {
            warnings.Add(new CyclePeriodFormWarning("end_not_after_start", "error", []));
        }

        if (draft.Year is { } year && draft.SequenceInYear is { } sequence)
        {
            var holders = others.Where(r => r.Year == year && r.SequenceInYear == sequence && SameScope(r, scope, scopeRef)).ToList();
            if (holders.Count > 0)
            {
                warnings.Add(new CyclePeriodFormWarning("sequence_taken", "error", holders.Select(Ref).ToList()));
            }
        }

        if (draft.StartDate is { } start && draft.EndDate is { } end && end > start)
        {
            var overlapping = others
                .Where(r => r.StartDate <= end && start <= r.EndDate && NormalizeStatus(r.CycleStatus) != Closed)
                .ToList();

            var sameActive = overlapping.Where(r => SameScope(r, scope, scopeRef) && NormalizeStatus(r.CycleStatus) == Active).ToList();
            if (sameActive.Count > 0)
            {
                warnings.Add(new CyclePeriodFormWarning("active_overlap", "warning", sameActive.Select(Ref).ToList()));
            }

            var otherLevel = overlapping.Where(r => NormalizeScope(r.ScopeType) != scope).ToList();
            if (otherLevel.Count > 0)
            {
                warnings.Add(new CyclePeriodFormWarning("other_level_overlap", "info", otherLevel.Select(Ref).ToList()));
            }
        }

        return warnings;
    }

    /// <summary>The first sequence (1..99) no period of this scope and year holds — the panel's suggestion when the
    /// code-suggestion service is unavailable. Closed periods hold theirs.</summary>
    public static int? NextSequence(IReadOnlyList<CyclePeriodRow> rows, string? scopeType, string? scopeRef, int year, Guid? exclude = null)
    {
        var scope = NormalizeScope(scopeType);
        var reference = NormalizeRef(scope, scopeRef);
        var taken = rows
            .Where(r => (exclude is null || r.CyclePeriodId != exclude) && r.Year == year && SameScope(r, scope, reference))
            .Select(r => r.SequenceInYear)
            .ToHashSet();
        for (var n = 1; n <= 99; n++)
        {
            if (!taken.Contains(n))
            {
                return n;
            }
        }

        return null;
    }

    // ── Finder (K-6) and the band (E11) ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The "find the effective period" answer, ACTIVE periods only (the runtime's resolve-active). Three outcomes, each
    /// with its own message — <c>none</c> is an answer, and <c>ambiguous</c> is shown as a data problem, never resolved
    /// to a period of the screen's choosing.
    /// </summary>
    public static CyclePeriodFinderView Finder(string? outcome)
        => (outcome ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "resolved" => new CyclePeriodFinderView("resolved", "success", "FinderResolved"),
            "ambiguous" => new CyclePeriodFinderView("ambiguous", "warning", "FinderAmbiguous"),
            _ => new CyclePeriodFinderView("none", "secondary", "FinderNone")
        };

    /// <summary>
    /// WP-CYC-UI-FIX-2 — "today's active periods": EVERY active period whose window covers <paramref name="day"/>, at
    /// every scope level (tenant, country, legal entity, business unit), ordered by scope precedence then reference.
    /// <para>It replaces a unit-less resolve-active call, which only ever looked at the tenant-wide level and therefore
    /// said "no active period" while a country-scoped one was in force. Read from the list rows the page already has —
    /// no extra request. Which period applies to ONE unit is the effective-period finder's question, not this one.</para>
    /// </summary>
    public static IReadOnlyList<CyclePeriodTodayActive> ActiveOn(IReadOnlyList<CyclePeriodRow> rows, DateOnly day)
        => rows
            .Where(r => NormalizeStatus(r.CycleStatus) == Active && r.StartDate <= day && r.EndDate >= day)
            .OrderBy(r => ScopeRank(NormalizeScope(r.ScopeType)))
            .ThenBy(r => ScopeRefKey(r), StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.CycleCode, StringComparer.OrdinalIgnoreCase)
            .Select(r => new CyclePeriodTodayActive(
                r.CyclePeriodId, r.CycleCode, r.CycleName, NormalizeScope(r.ScopeType), LaneLabelRef(r), r.StartDate, r.EndDate))
            .ToList();

    /// <summary>Open (draft or active) periods that have no live capacity — known only when the list carried the usage
    /// summary; a row whose summary is unknown is not listed (unknown is not "missing").</summary>
    public static IReadOnlyList<CyclePeriodRow> OpenWithoutCapacity(IReadOnlyList<CyclePeriodRow> rows)
        => rows
            .Where(r => NormalizeStatus(r.CycleStatus) != Closed && r.HasCapacity == false)
            .OrderBy(r => r.StartDate)
            .ThenBy(r => r.CycleCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The list's filters applied to the timeline's rows: scope level, country (a country-scoped period's own
    /// country) and status (any of). Empty filters keep everything.</summary>
    public static IReadOnlyList<CyclePeriodRow> Filter(
        IReadOnlyList<CyclePeriodRow> rows, string? scopeType, string? country, IReadOnlyCollection<string>? statuses)
    {
        var wantedScope = string.IsNullOrWhiteSpace(scopeType) ? null : NormalizeScope(scopeType);
        var wantedCountry = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        var wantedStatuses = (statuses ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(NormalizeStatus).ToHashSet();

        return rows.Where(r =>
                (wantedScope is null || NormalizeScope(r.ScopeType) == wantedScope)
                && (wantedCountry is null || (NormalizeScope(r.ScopeType) == "country"
                    && string.Equals(r.ScopeRef?.Trim(), wantedCountry, StringComparison.OrdinalIgnoreCase)))
                && (wantedStatuses.Count == 0 || wantedStatuses.Contains(NormalizeStatus(r.CycleStatus))))
            .ToList();
    }

    /// <summary>Inclusive day count of a window (both ends are part of the period).</summary>
    public static int DayCount(DateOnly start, DateOnly end) => end < start ? 0 : end.DayNumber - start.DayNumber + 1;

    /// <summary>The calendar months a window touches, each clipped to the window — the details page's calendar summary
    /// asks the working calendar once per row.</summary>
    public static IReadOnlyList<(int Year, int Month, DateOnly From, DateOnly To)> Months(DateOnly start, DateOnly end)
    {
        var result = new List<(int, int, DateOnly, DateOnly)>();
        if (end < start)
        {
            return result;
        }

        var cursor = new DateOnly(start.Year, start.Month, 1);
        while (cursor <= end)
        {
            var monthEnd = cursor.AddMonths(1).AddDays(-1);
            result.Add((cursor.Year, cursor.Month, Max(cursor, start), Min(monthEnd, end)));
            cursor = cursor.AddMonths(1);
        }

        return result;
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    public static string NormalizeStatus(string? status) => (status ?? string.Empty).Trim().ToLowerInvariant();

    public static string NormalizeScope(string? scopeType)
    {
        var value = (scopeType ?? string.Empty).Trim().ToLowerInvariant();
        return value.Length == 0 ? "tenant" : value;
    }

    private static string? NormalizeRef(string scope, string? scopeRef)
    {
        if (scope == "tenant" || string.IsNullOrWhiteSpace(scopeRef))
        {
            return null;
        }

        return scope == "country" ? scopeRef.Trim().ToUpperInvariant() : scopeRef.Trim();
    }

    private static int ScopeRank(string scope)
    {
        var index = ScopeOrder.ToList().IndexOf(scope);
        return index < 0 ? ScopeOrder.Count : index;
    }

    private static string ScopeRefKey(CyclePeriodRow row) => NormalizeRef(NormalizeScope(row.ScopeType), row.ScopeRef) ?? string.Empty;

    private static string? LaneLabelRef(CyclePeriodRow row) => NormalizeRef(NormalizeScope(row.ScopeType), row.ScopeRef);

    private static bool SameScope(CyclePeriodRow row, string scope, string? scopeRef)
        => NormalizeScope(row.ScopeType) == scope
           && string.Equals(NormalizeRef(scope, row.ScopeRef), scopeRef, StringComparison.OrdinalIgnoreCase);

    private static CyclePeriodRef Ref(CyclePeriodRow row)
        => new(row.CyclePeriodId, row.CycleCode, row.CycleName, NormalizeStatus(row.CycleStatus), row.StartDate, row.EndDate);

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    /// <summary>A stored period day (UTC midnight instant) as the calendar day it names.</summary>
    public static DateOnly ToDay(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);

    public static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>A period as the screen rules see it (from the CrmService list).</summary>
public sealed record CyclePeriodRow(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    int Year,
    int SequenceInYear,
    DateOnly StartDate,
    DateOnly EndDate,
    string ScopeType,
    string? ScopeRef,
    string CycleStatus,
    bool? HasCapacity = null);

/// <summary>WP-CYC-UI-FIX-2 — one of today's active periods, with the scope it answers for.</summary>
public sealed record CyclePeriodTodayActive(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    string ScopeType,
    string? ScopeRef,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CyclePeriodTimeline(
    DateOnly AxisStart,
    DateOnly AxisEnd,
    int FromYear,
    int ToYear,
    IReadOnlyList<CyclePeriodTimelineTick> Years,
    IReadOnlyList<double> QuarterPcts,
    double? TodayPct,
    IReadOnlyList<CyclePeriodTimelineLane> Lanes);

public sealed record CyclePeriodTimelineTick(int Year, double OffsetPct, double WidthPct);

public sealed record CyclePeriodTimelineLane(
    string ScopeType,
    string ScopeRefKey,
    string? ScopeRef,
    IReadOnlyList<CyclePeriodTimelineBar> Bars,
    IReadOnlyList<CyclePeriodTimelineMark> Marks);

public sealed record CyclePeriodTimelineBar(
    Guid CyclePeriodId,
    string CycleCode,
    string CycleName,
    string CycleStatus,
    DateOnly StartDate,
    DateOnly EndDate,
    double OffsetPct,
    double WidthPct,
    bool ClippedStart,
    bool ClippedEnd);

/// <summary><see cref="Kind"/>: <c>gap</c>, <c>overlap</c> (drafts may overlap) or <c>conflict</c> (two active).</summary>
public sealed record CyclePeriodTimelineMark(
    string Kind, DateOnly From, DateOnly To, double OffsetPct, double WidthPct, IReadOnlyList<string> Codes);

public sealed record CyclePeriodFieldStates(
    bool Code, bool Name, bool Description, bool Year, bool Sequence, bool Dates, bool Scope,
    bool ReadOnly, bool OfferCloseAndReopen);

public sealed record CyclePeriodActions(bool Activate, bool Close, bool Edit);

/// <summary>What the panel holds while the author types.</summary>
public sealed record CyclePeriodDraft(
    Guid? CyclePeriodId,
    int? Year,
    int? SequenceInYear,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ScopeType,
    string? ScopeRef);

/// <summary><see cref="Code"/> is a stable machine code the screen maps to a localized sentence; <see cref="Tone"/>
/// is error / warning / info; <see cref="Periods"/> are the periods it is about (the panel links to them).</summary>
public sealed record CyclePeriodFormWarning(string Code, string Tone, IReadOnlyList<CyclePeriodRef> Periods);

public sealed record CyclePeriodRef(
    Guid CyclePeriodId, string CycleCode, string CycleName, string CycleStatus, DateOnly StartDate, DateOnly EndDate);

public sealed record CyclePeriodFinderView(string Outcome, string Tone, string MessageKey);
