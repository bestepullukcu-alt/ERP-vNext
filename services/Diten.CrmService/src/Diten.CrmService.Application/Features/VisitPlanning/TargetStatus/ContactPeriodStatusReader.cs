using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using ContactEntity = Diten.CrmService.Domain.Entities.Contact;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;

/// <summary>The period a status is counted in: dates are inclusive (the CyclePeriod's calendar days).</summary>
public sealed record ContactStatusPeriod(Guid? CyclePeriodId, string? CycleCode, DateOnly Start, DateOnly End)
{
    /// <summary>The period's week count — the Monday-weeks the period touches, exactly the planning engine's period weeks
    /// (<see cref="PlanningWeekCalendar.PeriodWeeks"/>; CT fix on merge with WP-VP-3A).</summary>
    public int WeekCount => Math.Max(1, PlanningWeekCalendar.PeriodWeeks(Start, End).Count);

    /// <summary>The period frame the frequency units are counted on. The status read does not call the working calendar,
    /// so only weekends are non-working here (a holiday that empties a whole week is the only possible difference).</summary>
    public PlanningPeriodFrame Frame => new(Start, End, WeekendDays());

    private List<DateOnly> WeekendDays()
    {
        var days = new List<DateOnly>();
        for (var d = Start; d <= End; d = d.AddDays(1))
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) days.Add(d);
        }
        return days;
    }
}

/// <summary>The input of one bulk status read. <paramref name="KnownContacts"/> — contact masters the caller has
/// already read (the doctors endpoint has); when given, the reader does not read them again.</summary>
public sealed record ContactPeriodStatusRequest(
    Guid TenantId,
    string ResourceId,
    ContactStatusPeriod? Period,
    IReadOnlyCollection<Guid> ContactIds,
    DateOnly Today,
    DateTimeOffset At,
    IReadOnlyCollection<ContactEntity>? KnownContacts = null,
    // WP-VP-4M (1) — the SELECTED week (its Monday): "due this week" is read for it and plannedThisWeek is said; null =
    // today's week (the 3D behaviour) and no plannedThisWeek.
    DateOnly? WeekStart = null);

/// <summary>
/// WP-VP-3D (B-6, B-9) — the SHARED reader of "where does each doctor stand in the rep's period": required / done /
/// planned / remaining, last visit, never visited, due this week, frequency status, segment badges, consent, inactive.
/// <para><b>Bulk by construction — no read per doctor.</b> For any number of doctors one call costs: the rep's plans
/// against the doctor set (1 read, <see cref="IPlannedVisitRepository.ListByResourceAndContactsAsync"/>), their reports
/// (1 read, <see cref="IVisitReportRepository.ListByPlannedVisitIdsAsync"/>), the contact masters (1 read, 0 when the
/// caller passes them), the active segment memberships (<see cref="IContactSegmentSetReader"/>: a fixed number of reads
/// PER ACTIVE SEGMENT, independent of the doctor count), the frequency policies (1 read,
/// <see cref="ContactVisitFrequency"/>) and consent (1 bulk load, 2 queries, evaluated in memory by the MOD-0164
/// engine).</para>
/// <para><b>Ownership.</b> Only the given rep's plans are read — another rep's visits never count. Tenant isolation is
/// the repositories' (every read is tenant-scoped).</para>
/// <para><b>Frequency rule.</b> The SAME rule the planning engine uses today (<see cref="ContactVisitFrequency"/>, a
/// batched form of the per-doctor resolver call in <c>FrequencyExtendPlanner</c>); WP-VP-3A binds the engine to it.</para>
/// <para>Reads only — no write, nothing cached, nothing audited.</para>
/// </summary>
public sealed class ContactPeriodStatusReader
{
    /// <summary>At most this many segment badges per doctor (the names, alphabetical).</summary>
    public const int MaxSegmentBadges = 5;

    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IContactRepository _contacts;
    private readonly IContactSegmentSetReader _segments;
    private readonly IVisitFrequencyPolicyRepository _policies;
    private readonly ISegmentConsentBulkReader? _consent;

    public ContactPeriodStatusReader(
        IPlannedVisitRepository plans,
        IVisitReportRepository reports,
        IContactRepository contacts,
        IContactSegmentSetReader segments,
        IVisitFrequencyPolicyRepository policies,
        ISegmentConsentBulkReader? consent = null)
    {
        _plans = plans;
        _reports = reports;
        _contacts = contacts;
        _segments = segments;
        _policies = policies;
        _consent = consent;
    }

    public async Task<IReadOnlyDictionary<Guid, ContactPeriodStatusDto>> ReadAsync(
        ContactPeriodStatusRequest request, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, ContactPeriodStatusDto>();
        var ids = request.ContactIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return result;
        }

        var tenantId = request.TenantId;
        var resourceId = (request.ResourceId ?? string.Empty).Trim();

        // ① the rep's plans against these doctors + their reports — two reads for the whole set.
        var plans = resourceId.Length == 0
            ? new List<PlannedVisitEntity>()
            : (await _plans.ListByResourceAndContactsAsync(tenantId, resourceId, ids, cancellationToken))
                .Where(p => p.TenantId == tenantId
                            && string.Equals(p.Resource?.ResourceId?.Trim(), resourceId, StringComparison.Ordinal))
                .ToList();
        var reportByPlan = plans.Count == 0
            ? new Dictionary<Guid, Domain.Entities.VisitReport>()
            : (await _reports.ListByPlannedVisitIdsAsync(tenantId, plans.Select(p => p.Id).ToList(), cancellationToken))
                .Where(r => r.TenantId == tenantId)
                .GroupBy(r => r.PlannedVisitId)
                .ToDictionary(g => g.Key, g => g.First());

        // ② the contact masters (inactive) — reused when the caller already read them.
        var contacts = (request.KnownContacts ?? await _contacts.ListByIdsAsync(tenantId, ids, cancellationToken))
            .Where(c => c.TenantId == tenantId)
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // ③ active segments (badges + the frequency context) and ④ frequency — one rule, batched.
        var segments = await _segments.ReadAsync(tenantId, ids, request.At, cancellationToken);
        var frequency = await ContactVisitFrequency.ResolveManyAsync(
            _policies, tenantId, ids, segments, request.At, cancellationToken);

        // ⑤ consent — one bulk load, the MOD-0164 engine in memory (visit channel, medical-visit purpose).
        var consent = await ReadConsentAsync(tenantId, ids, request.At, cancellationToken);

        var plansByContact = plans.GroupBy(p => p.ContactId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var contactId in ids)
        {
            var mine = plansByContact.GetValueOrDefault(contactId) ?? new List<PlannedVisitEntity>();
            var freq = frequency.GetValueOrDefault(contactId);
            result[contactId] = Compose(
                contactId, mine, reportByPlan, request.Period, request.Today, freq,
                SegmentBadges(segments, contactId), consent.GetValueOrDefault(contactId),
                contacts.TryGetValue(contactId, out var contact) && VisitTargetNameReader.IsInactiveStatus(contact.Status),
                request.WeekStart);
        }

        return result;
    }

    /// <summary>The per-doctor arithmetic — pure, so every rule is testable on its own.</summary>
    internal static ContactPeriodStatusDto Compose(
        Guid contactId,
        IReadOnlyList<PlannedVisitEntity> plans,
        IReadOnlyDictionary<Guid, Domain.Entities.VisitReport> reportByPlan,
        ContactStatusPeriod? period,
        DateOnly today,
        VisitFrequencyResolveResult? frequency,
        IReadOnlyList<string> badges,
        string? consentStatus,
        bool inactive,
        DateOnly? weekStart = null)
    {
        // Cancelled / archived plans never count — not as done, not as planned, not as the last visit.
        var counting = plans.Where(p => !p.IsCancelled() && !p.IsArchived()).ToList();

        var completed = counting
            .Select(p => (Plan: p, Report: reportByPlan.GetValueOrDefault(p.Id)))
            .Where(x => x.Report is not null && x.Report.IsCompleted())
            .ToList();

        DateTimeOffset? lastVisit = completed.Count == 0 ? null : completed.Max(x => x.Report!.ExecutedAt);

        var done = 0;
        var planned = 0;
        DateOnly? lastInPeriod = null;
        if (period is not null)
        {
            var inPeriod = completed.Where(x => x.Plan.PlannedDate >= period.Start && x.Plan.PlannedDate <= period.End).ToList();
            done = inPeriod.Count;
            lastInPeriod = inPeriod.Count == 0
                ? null
                : DateOnly.FromDateTime(inPeriod.Max(x => x.Report!.ExecutedAt).UtcDateTime);
            var from = today > period.Start ? today : period.Start;
            planned = counting.Count(p => p.PlannedDate >= from && p.PlannedDate <= period.End && !reportByPlan.ContainsKey(p.Id));
        }

        int? required = period is null ? null : RequiredInPeriod(frequency, period);
        int? remaining = required is { } req && period is not null ? Math.Max(0, req - done - planned) : null;

        var unknown = IsUnknown(frequency);
        return new ContactPeriodStatusDto(
            contactId,
            required,
            frequency?.FrequencyStatus ?? FrequencyStatus.Unknown,
            frequency?.PeriodType,
            done,
            planned,
            remaining,
            lastVisit,
            NeverVisited: completed.Count == 0,
            DueThisWeek: IsDueThisWeek(remaining, required, period, lastInPeriod, ReferenceDay(weekStart, today, period)),
            badges,
            consentStatus,
            inactive)
        {
            FrequencyDefault = unknown ? FrequencyDefaults.Weekly : null,
            // WP-VP-4L (2) — an extra visit counts as done / planned; what goes beyond the requirement is said, remaining
            // never drops under 0.
            OverFrequency = required is { } over && period is not null ? Math.Max(0, done + planned - over) : 0,
            // WP-VP-4M (2) — a WRITTEN (planned / approved, not cancelled / archived) visit in the selected week; a draft
            // week's visits live in the preview only, so they are not counted here. Null without a selected week.
            PlannedThisWeek = weekStart is { } ws
                ? counting.Any(p => p.PlannedDate >= ws && p.PlannedDate <= ws.AddDays(6))
                : null
        };
    }

    /// <summary>WP-VP-4M (1) — the day "this week" is read on: the selected week's Monday (its first day inside the period
    /// when the period starts mid-week), else today (the 3D behaviour).</summary>
    public static DateOnly ReferenceDay(DateOnly? weekStart, DateOnly today, ContactStatusPeriod? period)
    {
        if (weekStart is not { } ws)
        {
            return today;
        }

        return period is not null && ws < period.Start && period.Start <= ws.AddDays(6) ? period.Start : ws;
    }

    private static bool IsUnknown(VisitFrequencyResolveResult? frequency)
        => frequency?.RequiredVisitCount is not > 0
           || string.Equals(frequency.FrequencyStatus, FrequencyStatus.Unknown, StringComparison.Ordinal);

    /// <summary>CT fix on merge with WP-VP-3A — the SAME rule as the planning engine (<see cref="FrequencyExtendPlanner"/>):
    /// the policy count × its period-type units in the period; WP-VP-4L (1) — no count / unknown cadence ⇒ the weekly
    /// default, through the ONE helper the engine uses (<see cref="FrequencyDefaults.UnknownRequiredInPeriod"/>).</summary>
    public static int RequiredInPeriod(VisitFrequencyResolveResult? frequency, ContactStatusPeriod period)
    {
        if (IsUnknown(frequency))
        {
            return FrequencyDefaults.UnknownRequiredInPeriod(period.Frame);
        }

        return frequency!.RequiredVisitCount!.Value * FrequencyExtendPlanner.UnitsIn(frequency.PeriodType, period.Frame);
    }

    /// <summary>
    /// <b>dueThisWeek</b> (B-6, documented rule). Even distribution: the period's weeks divided by the required count is
    /// the STRIDE — how many weeks may pass between two visits.
    /// <list type="number">
    /// <item>Not due when nothing remains (remaining ≤ 0), there is no requirement at all (required null — a frequency that
    /// could not be resolved to a number), there is no period, or the reference day is outside it. WP-VP-4L/4M — an unknown
    /// frequency HAS a requirement now (the weekly default), so such a doctor is due like any other.</item>
    /// <item>No completed visit in the period yet ⇒ due (the period's first slot is its first week — the engine's base
    /// week, <c>FrequencyExtendPlanner</c> week 0 — so every week from the start counts).</item>
    /// <item>Otherwise due when the whole weeks between the last completed visit's week (Monday) and this week (Monday)
    /// reach the stride: <c>weeksSince ≥ periodWeeks / required</c>.</item>
    /// </list>
    /// Example: 4-week period, required 2 ⇒ stride 2; visited in week 1 ⇒ due again from week 3.
    /// </summary>
    public static bool IsDueThisWeek(
        int? remaining, int? required, ContactStatusPeriod? period, DateOnly? lastCompletedInPeriod, DateOnly today)
    {
        if (remaining is not > 0 || required is not > 0 || period is null
            || today < period.Start || today > period.End)
        {
            return false;
        }

        if (lastCompletedInPeriod is null)
        {
            return true;
        }

        var stride = (double)period.WeekCount / required.Value;
        var weeksSince = (WeekStart(today).DayNumber - WeekStart(lastCompletedInPeriod.Value).DayNumber) / 7;
        return weeksSince >= stride;
    }

    /// <summary>The Monday of the ISO week containing <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The doctor's segment badges: the names of their active segments, alphabetical, at most
    /// <see cref="MaxSegmentBadges"/>. W2-BE-d — public: the visit workspace cards show the SAME badges.</summary>
    public static IReadOnlyList<string> SegmentBadges(ContactSegmentSet segments, Guid contactId)
        => segments.For(contactId)
            .Select(id => segments.SegmentNames.GetValueOrDefault(id))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .Take(MaxSegmentBadges)
            .ToList();

    private async Task<IReadOnlyDictionary<Guid, string>> ReadConsentAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, string>();
        if (_consent is null)
        {
            return map;
        }

        var snapshot = await _consent.LoadAsync(tenantId, ConsentSubjectType.Contact, ids, cancellationToken);
        var purpose = PlannedVisitValidation.ToConsentPurpose(PlannedVisitPurpose.MedicalVisit);
        foreach (var id in ids)
        {
            var verdict = ConsentEvaluationEngine.Evaluate(
                new ConsentEvaluationRequest(
                    SubjectType: ConsentSubjectType.Contact,
                    SubjectId: id,
                    Channel: ConsentChannel.Visit,
                    Purpose: purpose,
                    EffectiveAt: at,
                    IncludeDiagnostics: false),
                snapshot.Consents,
                snapshot.Preferences,
                at);
            map[id] = verdict.EligibilityStatus;
        }

        return map;
    }
}
