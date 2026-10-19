namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Fixture-only, in-memory core. It is not a Draft edit, authorization source or durable audit.
internal sealed class FixtureForecastMethodChoice
{
    internal enum Action { SelectOne, ApplyToSelected }
    internal enum Outcome { Changed, Replayed, Conflict, Invalid, ScopeDenied, Ineligible }

    internal sealed record FixtureActor(Guid TenantId, Guid LegalEntityId,
        Guid ActorId, bool CanUpdateDraft);

    internal sealed record Request(Guid RequestKey, Action Action,
        ForecastMethodEligibility.Method Method,
        IReadOnlyList<ForecastMethodEligibility.SeriesKey> Targets,
        string? Reason, int ExpectedVersion);

    internal sealed record SeriesChange(ForecastMethodEligibility.SeriesKey Series,
        ForecastMethodEligibility.Method? Previous,
        ForecastMethodEligibility.Method Current);

    internal sealed record MemoryTrace(Guid RequestKey, Action Action,
        Guid ActorId, DateTimeOffset OccurredAt, string Reason,
        string PolicyVersion, int Version, IReadOnlyList<SeriesChange> Changes);

    internal sealed record Result(Outcome Outcome, int Version,
        IReadOnlyList<SeriesChange> Changes);

    private sealed record RequestIntent(Guid ActorId, Action Action,
        ForecastMethodEligibility.Method Method, string Reason,
        int ExpectedVersion,
        IReadOnlyList<ForecastMethodEligibility.SeriesKey> Targets);

    private readonly object _gate = new();
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;
    private readonly ForecastMethodEligibility.FixturePolicy _policy;
    private readonly Dictionary<ForecastMethodEligibility.SeriesKey,
        ForecastMethodEligibility.VerifiedHistoryFixture> _history;
    private readonly Dictionary<ForecastMethodEligibility.SeriesKey,
        ForecastMethodEligibility.Method?> _selected;
    private readonly Dictionary<Guid, RequestIntent> _requests = new();
    private readonly List<MemoryTrace> _traces = [];
    private int _version;

    internal FixtureForecastMethodChoice(Guid tenantId, Guid legalEntityId,
        IReadOnlyList<ForecastMethodEligibility.VerifiedHistoryFixture> histories,
        ForecastMethodEligibility.FixturePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(histories);
        ArgumentNullException.ThrowIfNull(policy);
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || histories.Count == 0 ||
            string.IsNullOrWhiteSpace(policy.Version))
            throw new ArgumentException("Incomplete fixture workspace.");
        _tenantId = tenantId;
        _legalEntityId = legalEntityId;
        _policy = policy;
        _history = new();
        _selected = new();
        foreach (var history in histories)
        {
            if (history is null || history.Series.TenantId != tenantId ||
                history.Series.LegalEntityId != legalEntityId ||
                !_history.TryAdd(history.Series, history))
                throw new ArgumentException("Fixture series scope or uniqueness is invalid.",
                    nameof(histories));
            _selected.Add(history.Series, null);
        }
    }

    internal int Version
    {
        get { lock (_gate) return _version; }
    }

    internal ForecastMethodEligibility.Method? ReadSelection(FixtureActor actor,
        ForecastMethodEligibility.SeriesKey series)
    {
        lock (_gate)
        {
            if (!HasScopeAndPermission(actor) || series.TenantId != _tenantId ||
                series.LegalEntityId != _legalEntityId)
                throw new UnauthorizedAccessException("Fixture scope is denied.");
            return _selected.TryGetValue(series, out var method)
                ? method : throw new KeyNotFoundException("Fixture series is absent.");
        }
    }

    internal IReadOnlyList<MemoryTrace> ReadMemoryTraces(FixtureActor actor)
    {
        lock (_gate)
        {
            if (!HasScopeAndPermission(actor))
                throw new UnauthorizedAccessException("Fixture scope is denied.");
            return Array.AsReadOnly(_traces.Select(trace => trace with
            {
                Changes = Array.AsReadOnly(trace.Changes.ToArray())
            }).ToArray());
        }
    }

    internal Result Apply(FixtureActor actor, Request request, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            Result Fail(Outcome outcome) => new(outcome, _version,
                Array.Empty<SeriesChange>());

            if (!HasScopeAndPermission(actor))
                return Fail(Outcome.ScopeDenied);
            if (request.RequestKey == Guid.Empty || occurredAt == default ||
                !Enum.IsDefined(request.Action) || !Enum.IsDefined(request.Method) ||
                request.Targets is null || request.Targets.Count == 0 ||
                string.IsNullOrWhiteSpace(request.Reason) || request.ExpectedVersion < 0)
                return Fail(Outcome.Invalid);

            var targets = request.Targets.ToArray();
            if (targets.Any(target => target is null) ||
                targets.Length != targets.Distinct().Count() ||
                request.Action == Action.SelectOne && targets.Length != 1 ||
                request.Action == Action.ApplyToSelected && targets.Length < 2)
                return Fail(Outcome.Invalid);

            var reason = request.Reason.Trim();
            var orderedTargets = targets.OrderBy(target => target.TenantId)
                .ThenBy(target => target.LegalEntityId)
                .ThenBy(target => target.SkuId)
                .ThenBy(target => target.WarehouseId, StringComparer.Ordinal)
                .ToArray();
            var intent = new RequestIntent(actor.ActorId, request.Action, request.Method,
                reason, request.ExpectedVersion, Array.AsReadOnly(orderedTargets));
            if (_requests.TryGetValue(request.RequestKey, out var previousIntent))
                return Fail(SameIntent(previousIntent, intent)
                    ? Outcome.Replayed : Outcome.Conflict);
            if (request.ExpectedVersion != _version)
                return Fail(Outcome.Conflict);

            // Every target is checked before any selection or memory trace changes.
            var changes = new List<SeriesChange>(targets.Length);
            foreach (var target in targets)
            {
                if (target.TenantId != _tenantId ||
                    target.LegalEntityId != _legalEntityId)
                    return Fail(Outcome.ScopeDenied);
                if (!_history.TryGetValue(target, out var history))
                    return Fail(Outcome.Invalid);
                if (!ForecastMethodEligibility.Assess(history, request.Method, _policy)
                    .IsEligible)
                    return Fail(Outcome.Ineligible);
                changes.Add(new SeriesChange(target, _selected[target], request.Method));
            }

            foreach (var change in changes)
                _selected[change.Series] = change.Current;
            _version++;
            _requests.Add(request.RequestKey, intent);
            var immutableChanges = Array.AsReadOnly(changes.ToArray());
            _traces.Add(new MemoryTrace(request.RequestKey, request.Action,
                actor.ActorId, occurredAt, reason, _policy.Version,
                _version, immutableChanges));
            return new Result(Outcome.Changed, _version, immutableChanges);
        }
    }

    private bool HasScopeAndPermission(FixtureActor actor) =>
        actor is not null && actor.ActorId != Guid.Empty && actor.CanUpdateDraft &&
        actor.TenantId == _tenantId && actor.LegalEntityId == _legalEntityId;

    private static bool SameIntent(RequestIntent first, RequestIntent second) =>
        first.ActorId == second.ActorId && first.Action == second.Action &&
        first.Method == second.Method && first.Reason == second.Reason &&
        first.ExpectedVersion == second.ExpectedVersion &&
        first.Targets.SequenceEqual(second.Targets);
}
