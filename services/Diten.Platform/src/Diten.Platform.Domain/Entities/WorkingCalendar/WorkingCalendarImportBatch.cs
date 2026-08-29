using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.WorkingCalendar;

public sealed class WorkingCalendarImportBatch : GlobalEntity
{
    public string BatchCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public int CalendarYear { get; set; }
    public Guid TargetCalendarId { get; set; }
    public string TargetCalendarCodeSnapshot { get; set; } = string.Empty;
    public bool IncludeNonPublicTypes { get; set; }
    public string ImportStatus { get; set; } = WorkingCalendarImportStatus.Fetching;
    public string ProviderKey { get; set; } = string.Empty;
    public string ProviderEndpoint { get; set; } = string.Empty;
    public DateTimeOffset? ProviderFetchedAt { get; set; }
    public string? ProviderPayloadHash { get; set; }
    public string? ProviderOutcome { get; set; }
    public List<WorkingCalendarImportCandidate> Candidates { get; set; } = new();
    public int CandidateCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int UndecidedCount { get; set; }
    public int SkippedNonPublicCount { get; set; }
    public int DuplicateSourceRowCount { get; set; }
    public string TriggerSource { get; set; } = WorkingCalendarImportTriggerSource.Manual;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public string? ScheduledRunKey { get; set; }
    public string? AppliedBy { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public List<Guid>? AppliedDayIds { get; set; }
    public int? TargetCalendarVersionAtApply { get; set; }
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }

    public void RecalculateCounts()
    {
        CandidateCount = Candidates.Count;
        ApprovedCount = Candidates.Count(x => x.Decision == WorkingCalendarImportDecision.Approved);
        RejectedCount = Candidates.Count(x => x.Decision == WorkingCalendarImportDecision.Rejected);
        UndecidedCount = Candidates.Count(x => x.Decision == WorkingCalendarImportDecision.Undecided);
    }
}

public sealed class WorkingCalendarImportCandidate
{
    public Guid CandidateId { get; set; } = Guid.NewGuid();
    public string ProviderDayKey { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string? ProviderLocalName { get; set; }
    public List<string> ProviderTypes { get; set; } = new();
    public bool ProviderIsNationwide { get; set; }
    public List<string>? ProviderSubdivisions { get; set; }
    public string? MappedDayType { get; set; }
    public string MappedDayCode { get; set; } = string.Empty;
    public string MappedDayName { get; set; } = string.Empty;
    public string ChangeKind { get; set; } = WorkingCalendarImportChangeKind.New;
    public Guid? ExistingDayId { get; set; }
    public List<string> Flags { get; set; } = new();
    public string Decision { get; set; } = WorkingCalendarImportDecision.Undecided;
    public string? DecisionReason { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? AppliedDayId { get; set; }
}

public static class WorkingCalendarImportStatus
{
    public const string Fetching = "fetching";
    public const string PendingReview = "pending-review";
    public const string InReview = "in-review";
    public const string Applied = "applied";
    public const string Discarded = "discarded";
    public const string Failed = "failed";
    public static readonly IReadOnlyList<string> All = [Fetching, PendingReview, InReview, Applied, Discarded, Failed];
    public static readonly IReadOnlyList<string> Open = [Fetching, PendingReview, InReview];
}

public static class WorkingCalendarImportDecision
{
    public const string Undecided = "undecided";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public static readonly IReadOnlyList<string> All = [Undecided, Approved, Rejected];
}

public static class WorkingCalendarImportChangeKind
{
    public const string New = "new";
    public const string AlreadyPresent = "already-present";
    public const string DateShift = "date-shift";
    public const string ConflictsManual = "conflicts-manual";
    public static readonly IReadOnlyList<string> All = [New, AlreadyPresent, DateShift, ConflictsManual];
}

public static class WorkingCalendarImportFlags
{
    public const string TypeNotPublic = "type_not_public";
    public const string SubdivisionScoped = "subdivision_scoped";
    public const string DateOutsideCalendarYear = "date_outside_calendar_year";
    public const string ExistingManualDay = "existing_manual_day";
    public const string DayCodeCollision = "day_code_collision";
    public const string UnmappedType = "unmapped_type";
    public static readonly IReadOnlyList<string> All = [TypeNotPublic, SubdivisionScoped, DateOutsideCalendarYear, ExistingManualDay, DayCodeCollision, UnmappedType];
}

public static class WorkingCalendarImportTriggerSource
{
    public const string Manual = "manual";
    public const string Scheduled = "scheduled";
    public static readonly IReadOnlyList<string> All = [Manual, Scheduled];
}
