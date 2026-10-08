using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Diten.PlanningService.Persistence")]
[assembly: InternalsVisibleTo("Diten.PlanningService.Cycles.Tests")]

namespace Diten.PlanningService.Domain.Features.DemandPlanning;

public enum DraftWeekValueKind { Known, Missing, Unknown }

public enum DraftWeekSource { Manual, Method }

public enum DemandRevisionState { Draft, InReview, Approved, Published, Superseded, Invalidated }

public enum DraftReviewAction { Submitted, Approved, Rejected, Reopened }

public enum DraftReviewOutcome { Changed, Conflict, Invalid, SeparationDenied }

public sealed record ManualDraftWeekInput(
    int Number, DateOnly WeekStart, DateOnly WeekEnd,
    DraftWeekValueKind ValueKind, decimal? Quantity);

// The base unit and series identity must come from a verified reference fixture or contract.
public sealed record VerifiedDraftSeries(
    Guid SkuId, string WarehouseId, string BaseUomId,
    IReadOnlyList<ManualDraftWeekInput> Weeks);

public sealed record DraftWeekChange(
    Guid SkuId, string WarehouseId, int WeekNumber,
    DraftWeekValueKind OldKind, decimal? OldQuantity,
    DraftWeekValueKind NewKind, decimal? NewQuantity,
    Guid ActorId, DateTimeOffset OccurredAt, string Reason, string RequestKey);

public sealed record DraftSeriesExclusion(Guid SkuId, string WarehouseId,
    Guid ActorId, DateTimeOffset OccurredAt, string Reason, string RequestKey,
    DemandRevisionState PreviousState, DemandRevisionState NewState);

public sealed class DemandDraftWeek
{
    public DemandDraftWeek(int number, DateOnly weekStart, DateOnly weekEnd,
        DraftWeekValueKind valueKind, decimal? quantity, string reason,
        Guid actorId, DateTimeOffset occurredAt)
    {
        if (!DemandRevisionDraft.IsValidValue(valueKind, quantity) ||
            string.IsNullOrWhiteSpace(reason) || actorId == Guid.Empty || occurredAt == default)
            throw new ArgumentException("A manual week requires a valid value and provenance.");
        Number = number;
        WeekStart = weekStart;
        WeekEnd = weekEnd;
        ValueKind = valueKind;
        Quantity = quantity;
        ManualReason = reason.Trim();
        ManualActorId = actorId;
        ManualAt = occurredAt;
    }

    public int Number { get; }
    public DateOnly WeekStart { get; }
    public DateOnly WeekEnd { get; }
    public DraftWeekValueKind ValueKind { get; private set; }
    public decimal? Quantity { get; private set; }
    public DraftWeekSource Source { get; } = DraftWeekSource.Manual;
    public string ManualReason { get; private set; }
    public Guid ManualActorId { get; private set; }
    public DateTimeOffset ManualAt { get; private set; }

    internal void ApplyManual(DraftWeekValueKind kind, decimal? quantity,
        string reason, Guid actorId, DateTimeOffset occurredAt)
    {
        ValueKind = kind;
        Quantity = quantity;
        ManualReason = reason.Trim();
        ManualActorId = actorId;
        ManualAt = occurredAt;
    }

    internal DemandDraftWeek Copy() => new(Number, WeekStart, WeekEnd,
        ValueKind, Quantity, ManualReason, ManualActorId, ManualAt);
}

public sealed class DemandDraftSeries
{
    private List<DemandDraftWeek> _weeks = [];
    private DraftSeriesExclusion? _exclusion;

    public Guid SkuId { get; init; }
    public string WarehouseId { get; init; } = string.Empty;
    public string BaseUomId { get; init; } = string.Empty;
    public DraftSeriesExclusion? Exclusion => _exclusion;
    public IReadOnlyList<DemandDraftWeek> Weeks
    {
        get => _weeks.Select(week => week.Copy()).ToArray();
        init => _weeks = value.Select(week => week.Copy()).ToList();
    }

    internal DemandDraftWeek? FindWeek(int weekNumber) =>
        _weeks.SingleOrDefault(week => week.Number == weekNumber);

    internal bool HasCompleteManualQuantities => _weeks.Count == 52 &&
        _weeks.All(week => week.Source == DraftWeekSource.Manual &&
            week.ValueKind == DraftWeekValueKind.Known && week.Quantity.HasValue);

    internal void ApplyExclusion(DraftSeriesExclusion exclusion) => _exclusion = exclusion;

    internal DemandDraftSeries Copy()
    {
        var copy = new DemandDraftSeries
        {
            SkuId = SkuId, WarehouseId = WarehouseId,
            BaseUomId = BaseUomId, Weeks = _weeks
        };
        if (_exclusion is not null) copy.ApplyExclusion(_exclusion);
        return copy;
    }
}

// One revision covers the selected SKU x Warehouse series, not one series per revision.
// Persistence reconstructs through ManualDraftFactory; this is not a published snapshot.
public sealed class DemandRevisionDraft : EntityBase
{
    private readonly object _editGate = new();
    private List<PlanningWeek> _weeks = [];
    private List<DemandDraftSeries> _series = [];
    private readonly List<DraftWeekChange> _changes = [];
    private readonly List<DraftSeriesExclusion> _exclusions = [];
    private readonly List<DraftSeriesExclusion> _sourceExclusions = [];
    private readonly HashSet<Guid> _sourceContributorIds = [];

    public Guid PlanningCycleId { get; init; }
    public DemandRevisionState State { get; private set; } = DemandRevisionState.Draft;
    public int StateVersion { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public Guid LegalEntityId { get; init; }
    public string PlanningPeriodKey { get; init; } = string.Empty;
    public DateOnly AsOfDate { get; init; }
    public string CalendarId { get; init; } = string.Empty;
    public string CalendarVersion { get; init; } = string.Empty;
    public string TimeZoneId { get; init; } = string.Empty;
    public DateOnly HorizonStart { get; init; }
    public DateOnly HorizonEnd { get; init; }
    public IReadOnlyList<PlanningWeek> Weeks
    {
        get => _weeks.Select(week => new PlanningWeek
        {
            Number = week.Number, WeekStart = week.WeekStart, WeekEnd = week.WeekEnd
        }).ToArray();
        init => _weeks = value.Select(week => new PlanningWeek
        {
            Number = week.Number, WeekStart = week.WeekStart, WeekEnd = week.WeekEnd
        }).ToList();
    }
    public IReadOnlyList<DemandDraftSeries> Series
    {
        get => _series.Select(series => series.Copy()).ToArray();
        init => _series = value.Select(series => series.Copy()).ToList();
    }
    public Guid CreatedBy { get; init; }
    public Guid? UpdatedBy { get; private set; }
    public string CreationReason { get; init; } = string.Empty;
    public IReadOnlyList<DraftWeekChange> Changes => _changes.ToArray();
    public IReadOnlyList<DraftSeriesExclusion> Exclusions =>
        _sourceExclusions.Concat(_exclusions).ToArray();
    public IReadOnlyList<DraftSeriesExclusion> NewExclusions => _exclusions.ToArray();
    public IReadOnlyList<Guid> SourceContributorIds => _sourceContributorIds.ToArray();

    internal void RestoreSourceExclusions(IReadOnlyList<DraftSeriesExclusion> exclusions)
    {
        if (_sourceContributorIds.Count != 0 || _sourceExclusions.Count != 0 ||
            _exclusions.Count != 0 || Version != 0 ||
            State != DemandRevisionState.Draft || exclusions.Any(x =>
                x.SkuId == Guid.Empty || string.IsNullOrWhiteSpace(x.WarehouseId) ||
                x.ActorId == Guid.Empty || x.OccurredAt == default ||
                string.IsNullOrWhiteSpace(x.Reason)) ||
            exclusions.Select(x => (x.SkuId, x.WarehouseId)).Distinct().Count() != exclusions.Count)
            throw new InvalidDataException("Source exclusion lineage is invalid.");
        var sourceAuthors = _series.Where(x => x.Exclusion is null)
            .SelectMany(x => x.Weeks).Select(x => x.ManualActorId).ToArray();
        if (sourceAuthors.Length == 0 || sourceAuthors.Contains(Guid.Empty))
            throw new InvalidDataException("Source week authorship is incomplete.");
        foreach (var actorId in sourceAuthors) _sourceContributorIds.Add(actorId);
        foreach (var exclusion in exclusions)
        {
            var series = _series.SingleOrDefault(x => x.SkuId == exclusion.SkuId &&
                x.WarehouseId == exclusion.WarehouseId);
            if (series is null || series.Exclusion is not null || series.Weeks.Count != 0)
                throw new InvalidDataException("Source exclusion scope is invalid.");
            series.ApplyExclusion(exclusion);
            _sourceExclusions.Add(exclusion);
            _sourceContributorIds.Add(exclusion.ActorId);
        }
    }

    public bool HasCompleteManualQuantities => _series.Any(series => series.Exclusion is null) &&
        _series.Where(series => series.Exclusion is null)
            .All(series => series.HasCompleteManualQuantities);

    public bool HasSignificantContribution(Guid actorId) => actorId == CreatedBy ||
        _sourceContributorIds.Contains(actorId) ||
        _changes.Any(change => change.ActorId == actorId) ||
        _exclusions.Any(exclusion => exclusion.ActorId == actorId);

    // Published content is frozen; only its lifecycle state may advance.
    internal bool ApplyPublicationTransition(DemandRevisionState next,
        Guid actorId, int expectedContentVersion, int expectedStateVersion,
        bool enforcePublisherSeparation)
    {
        lock (_editGate)
        {
            var allowed = next switch
            {
                DemandRevisionState.Published => State == DemandRevisionState.Approved,
                DemandRevisionState.Superseded => State == DemandRevisionState.Published,
                DemandRevisionState.Invalidated => State is DemandRevisionState.Published or
                    DemandRevisionState.Superseded,
                _ => false
            };
            if (actorId == Guid.Empty || Version != expectedContentVersion ||
                StateVersion != expectedStateVersion || !allowed ||
                (enforcePublisherSeparation && HasSignificantContribution(actorId)))
                return false;
            State = next;
            StateVersion++;
            return true;
        }
    }

    public DraftEditOutcome ExcludeSeries(Guid tenantId, Guid legalEntityId,
        Guid actorId, Guid skuId, string warehouseId, string reason,
        string requestKey, int expectedVersion, DateTimeOffset occurredAt,
        DemandRevisionState previousState)
    {
        lock (_editGate)
        {
            if (tenantId != TenantId || legalEntityId != LegalEntityId ||
                actorId == Guid.Empty) return DraftEditOutcome.ScopeDenied;
            if (skuId == Guid.Empty || string.IsNullOrWhiteSpace(warehouseId) ||
                string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(requestKey) ||
                occurredAt == default) return DraftEditOutcome.Invalid;
            var normalizedReason = reason.Trim();
            var previous = _exclusions.SingleOrDefault(x => x.RequestKey == requestKey);
            if (previous is not null)
                return previous.ActorId == actorId && previous.SkuId == skuId &&
                    previous.WarehouseId == warehouseId && previous.Reason == normalizedReason &&
                    previous.PreviousState == previousState
                    ? DraftEditOutcome.Replayed : DraftEditOutcome.Conflict;
            if (Version != expectedVersion || State != DemandRevisionState.Draft)
                return DraftEditOutcome.Conflict;
            var series = _series.SingleOrDefault(x => x.SkuId == skuId &&
                x.WarehouseId == warehouseId);
            if (series is null || series.Exclusion is not null ||
                _series.Count(x => x.Exclusion is null) <= 1)
                return DraftEditOutcome.Invalid;
            var exclusion = new DraftSeriesExclusion(skuId, warehouseId,
                actorId, occurredAt, normalizedReason, requestKey,
                previousState, DemandRevisionState.Draft);
            series.ApplyExclusion(exclusion);
            _exclusions.Add(exclusion);
            UpdatedBy = actorId;
            UpdatedAt = occurredAt;
            Version++;
            return DraftEditOutcome.Changed;
        }
    }

    public DraftReviewOutcome ApplyReviewTransition(DraftReviewAction action,
        Guid actorId, string? reason, int expectedVersion, int expectedStateVersion,
        DateTimeOffset occurredAt)
        => ApplyReviewTransitionCore(action, actorId, reason, expectedVersion,
            expectedStateVersion, occurredAt, enforceManualCompleteness: true);

    // Replays already persisted decisions, including legacy incomplete submissions.
    // New transitions must use ApplyReviewTransition and enforce the current gate.
    internal DraftReviewOutcome ReplayPersistedReviewTransition(DraftReviewAction action,
        Guid actorId, string? reason, int expectedVersion, int expectedStateVersion,
        DateTimeOffset occurredAt)
        => ApplyReviewTransitionCore(action, actorId, reason, expectedVersion,
            expectedStateVersion, occurredAt, enforceManualCompleteness: false);

    private DraftReviewOutcome ApplyReviewTransitionCore(DraftReviewAction action,
        Guid actorId, string? reason, int expectedVersion, int expectedStateVersion,
        DateTimeOffset occurredAt, bool enforceManualCompleteness)
    {
        lock (_editGate)
        {
            if (actorId == Guid.Empty || occurredAt == default ||
                (action != DraftReviewAction.Submitted && string.IsNullOrWhiteSpace(reason)))
                return DraftReviewOutcome.Invalid;
            if (Version != expectedVersion || StateVersion != expectedStateVersion)
                return DraftReviewOutcome.Conflict;
            var next = action switch
            {
                DraftReviewAction.Submitted when State == DemandRevisionState.Draft =>
                    DemandRevisionState.InReview,
                DraftReviewAction.Approved when State == DemandRevisionState.InReview =>
                    DemandRevisionState.Approved,
                DraftReviewAction.Rejected when State == DemandRevisionState.InReview =>
                    DemandRevisionState.Draft,
                DraftReviewAction.Reopened when State is DemandRevisionState.InReview or
                    DemandRevisionState.Approved => DemandRevisionState.Draft,
                _ => (DemandRevisionState?)null
            };
            if (next is null) return DraftReviewOutcome.Conflict;
            if (enforceManualCompleteness &&
                action is (DraftReviewAction.Submitted or DraftReviewAction.Approved) &&
                !HasCompleteManualQuantities)
                return DraftReviewOutcome.Invalid;
            if (action is (DraftReviewAction.Approved or DraftReviewAction.Rejected) &&
                HasSignificantContribution(actorId))
                return DraftReviewOutcome.SeparationDenied;
            State = next.Value;
            StateVersion++;
            if (action is DraftReviewAction.Approved or DraftReviewAction.Rejected)
            {
                ReviewedBy = actorId;
                ReviewedAt = occurredAt;
                ReviewReason = reason!.Trim();
            }
            else if (action is DraftReviewAction.Submitted or DraftReviewAction.Reopened)
            {
                ReviewedBy = null;
                ReviewedAt = null;
                ReviewReason = null;
            }
            return DraftReviewOutcome.Changed;
        }
    }

    public DraftEditOutcome EditManualWeek(Guid tenantId, Guid legalEntityId, Guid actorId,
        Guid skuId, string warehouseId, int weekNumber, DraftWeekValueKind newKind,
        decimal? newQuantity, string reason, string requestKey, int expectedVersion,
        DateTimeOffset occurredAt)
    {
        lock (_editGate)
        {
            if (tenantId != TenantId || legalEntityId != LegalEntityId ||
                actorId == Guid.Empty)
                return DraftEditOutcome.ScopeDenied;
            if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(requestKey) ||
                !IsValidValue(newKind, newQuantity) || occurredAt == default)
                return DraftEditOutcome.Invalid;

            var normalizedReason = reason.Trim();
            var previous = _changes.SingleOrDefault(change => change.RequestKey == requestKey);
            if (previous is not null)
                return previous.ActorId == actorId && previous.SkuId == skuId &&
                       previous.WarehouseId == warehouseId && previous.WeekNumber == weekNumber &&
                       previous.NewKind == newKind && previous.NewQuantity == newQuantity &&
                       previous.Reason == normalizedReason
                    ? DraftEditOutcome.Replayed : DraftEditOutcome.Conflict;

            if (expectedVersion != Version)
                return DraftEditOutcome.Conflict;
            if (State != DemandRevisionState.Draft)
                return DraftEditOutcome.Conflict;
            var series = _series.SingleOrDefault(item => item.SkuId == skuId &&
                item.WarehouseId == warehouseId);
            if (series?.Exclusion is not null) return DraftEditOutcome.Invalid;
            var week = series?.FindWeek(weekNumber);
            if (week is null)
                return DraftEditOutcome.Invalid;

            _changes.Add(new DraftWeekChange(skuId, warehouseId, weekNumber,
                week.ValueKind, week.Quantity, newKind, newQuantity,
                actorId, occurredAt, normalizedReason, requestKey));
            week.ApplyManual(newKind, newQuantity, normalizedReason, actorId, occurredAt);
            UpdatedBy = actorId;
            UpdatedAt = occurredAt;
            Version++;
            return DraftEditOutcome.Changed;
        }
    }

    public static bool IsValidValue(DraftWeekValueKind kind, decimal? quantity) =>
        Enum.IsDefined(kind) && (kind == DraftWeekValueKind.Known
            ? quantity is >= 0 : quantity is null);
}

public enum DraftEditOutcome { Changed, Replayed, Conflict, Invalid, ScopeDenied }
