using Choice = Diten.PlanningService.Application.Features.DemandPlanning.FixtureForecastMethodChoice;
using Eligibility = Diten.PlanningService.Application.Features.DemandPlanning.ForecastMethodEligibility;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class FixtureForecastMethodChoiceTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 6, 12, 0, 0,
        TimeSpan.Zero);
    private static readonly DateOnly Start = new(2024, 1, 1);

    [Fact]
    public void SelectOne_RecordsExplicitSeriesReasonActorTimeAndPolicyVersion()
    {
        var (workspace, actor, first, second) = Setup();
        Assert.Null(workspace.ReadSelection(actor, first));

        var request = NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
            [first], 0, " planner choice ");
        var result = workspace.Apply(actor, request, At);

        Assert.Equal(Choice.Outcome.Changed, result.Outcome);
        Assert.Equal(1, result.Version);
        Assert.Equal(Eligibility.Method.Naive, workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        var trace = Assert.Single(workspace.ReadMemoryTraces(actor));
        Assert.Equal("planner choice", trace.Reason);
        Assert.Equal(actor.ActorId, trace.ActorId);
        Assert.Equal(At, trace.OccurredAt);
        Assert.Equal("fixture-policy-v1", trace.PolicyVersion);
        Assert.Equal(Choice.Action.SelectOne, trace.Action);
        var change = Assert.Single(trace.Changes);
        Assert.Equal(first, change.Series);
        Assert.Null(change.Previous);
        Assert.Equal(Eligibility.Method.Naive, change.Current);
        Assert.Null(typeof(Choice.Request).GetProperty("IsEligible"));
    }

    [Fact]
    public void ApplyToSelected_RechecksEverySeriesAndRecordsAllChanges()
    {
        var (workspace, actor, first, second) = Setup();

        var result = workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.Naive,
                [first, second], 0, "bulk choice"), At);

        Assert.Equal(Choice.Outcome.Changed, result.Outcome);
        Assert.Equal(2, result.Changes.Count);
        Assert.Equal(Eligibility.Method.Naive, workspace.ReadSelection(actor, first));
        Assert.Equal(Eligibility.Method.Naive, workspace.ReadSelection(actor, second));
        var trace = Assert.Single(workspace.ReadMemoryTraces(actor));
        Assert.Equal([first, second], trace.Changes.Select(change => change.Series));
        Assert.All(trace.Changes, change => Assert.Null(change.Previous));
    }

    [Fact]
    public void ApplyToSelected_OneIneligibleSeriesHasNoPartialEffect()
    {
        var (workspace, actor, first, second) = Setup(secondWeeks: 25);

        var result = workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.Naive,
                [first, second], 0, "all or none"), At);

        Assert.Equal(Choice.Outcome.Ineligible, result.Outcome);
        Assert.Equal(0, workspace.Version);
        Assert.Null(workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        Assert.Empty(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void ApplyToSelected_IneligibleSeriesPreservesEarlierChoiceAndTrace()
    {
        var (workspace, actor, first, second) = Setup(secondWeeks: 25);
        Assert.Equal(Choice.Outcome.Changed, workspace.Apply(actor,
            NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
                [first], 0, "existing choice"), At).Outcome);

        var result = workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.HoltTrend,
                [first, second], 1, "blocked bulk"), At.AddMinutes(1));

        Assert.Equal(Choice.Outcome.Ineligible, result.Outcome);
        Assert.Equal(1, workspace.Version);
        Assert.Equal(Eligibility.Method.Naive, workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        Assert.Single(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void ApplyToSelected_UnknownWeekInOneSeriesBlocksEverySelection()
    {
        var (workspace, actor, first, second) = Setup(
            secondUntrusted: Eligibility.WeekEvidence.Unknown);

        var result = workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.Naive,
                [first, second], 0, "no hidden zero"), At);

        Assert.Equal(Choice.Outcome.Ineligible, result.Outcome);
        Assert.Null(workspace.ReadSelection(actor, first));
        Assert.Empty(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void SelectOne_CanTrackPreviousAndNewMethodPerSeries()
    {
        var (workspace, actor, first, second) = Setup(104, 104);
        Assert.Equal(Choice.Outcome.Changed,
            workspace.Apply(actor, NewRequest(Choice.Action.SelectOne,
                Eligibility.Method.Naive, [first], 0, "first"), At).Outcome);

        var changed = workspace.Apply(actor, NewRequest(Choice.Action.SelectOne,
            Eligibility.Method.HoltWinters, [first], 1, "seasonal choice"),
            At.AddMinutes(1));

        Assert.Equal(Choice.Outcome.Changed, changed.Outcome);
        Assert.Equal(Eligibility.Method.HoltWinters,
            workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        var change = Assert.Single(workspace.ReadMemoryTraces(actor)[1].Changes);
        Assert.Equal(Eligibility.Method.Naive, change.Previous);
        Assert.Equal(Eligibility.Method.HoltWinters, change.Current);
    }

    [Fact]
    public void ApplyToSelected_DuplicateOrAbsentTargetNeverChangesAnySeries()
    {
        var (workspace, actor, first, second) = Setup();
        var absent = first with { SkuId = Guid.NewGuid() };

        var duplicate = workspace.Apply(actor, NewRequest(Choice.Action.ApplyToSelected,
            Eligibility.Method.Naive, [first, first], 0, "duplicate"), At);
        var absentResult = workspace.Apply(actor, NewRequest(Choice.Action.ApplyToSelected,
            Eligibility.Method.Naive, [first, absent], 0, "absent"), At);

        Assert.Equal(Choice.Outcome.Invalid, duplicate.Outcome);
        Assert.Equal(Choice.Outcome.Invalid, absentResult.Outcome);
        Assert.Equal(0, workspace.Version);
        Assert.Null(workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        Assert.Empty(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void Apply_RequiresExplicitActionTargetsAndReason()
    {
        var (workspace, actor, first, second) = Setup();

        Assert.Equal(Choice.Outcome.Invalid, workspace.Apply(actor,
            NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
                [first, second], 0, "wrong action"), At).Outcome);
        Assert.Equal(Choice.Outcome.Invalid, workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.Naive,
                [first], 0, "wrong action"), At).Outcome);
        Assert.Equal(Choice.Outcome.Invalid, workspace.Apply(actor,
            NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
                [first], 0, "   "), At).Outcome);
        Assert.Equal(0, workspace.Version);
        Assert.Empty(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void Apply_SameRequestReplaysAndChangedContentConflicts()
    {
        var (workspace, actor, first, _) = Setup();
        var requestKey = Guid.NewGuid();
        var firstRequest = NewRequest(Choice.Action.SelectOne,
            Eligibility.Method.Naive, [first], 0, "choice", requestKey);

        Assert.Equal(Choice.Outcome.Changed,
            workspace.Apply(actor, firstRequest, At).Outcome);
        Assert.Equal(Choice.Outcome.Replayed, workspace.Apply(actor,
            firstRequest with { Reason = " choice " }, At.AddMinutes(1)).Outcome);
        Assert.Equal(Choice.Outcome.Conflict, workspace.Apply(actor,
            firstRequest with { Method = Eligibility.Method.HoltTrend },
            At.AddMinutes(2)).Outcome);
        Assert.Equal(1, workspace.Version);
        Assert.Single(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public async Task Apply_RacingExpectedVersionAllowsOnlyOneChange()
    {
        var (workspace, actor, first, second) = Setup();
        var requests = new[]
        {
            NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
                [first], 0, "first"),
            NewRequest(Choice.Action.SelectOne, Eligibility.Method.Naive,
                [second], 0, "second")
        };

        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstAttempt = Task.Run(async () =>
        {
            await start.Task;
            return workspace.Apply(actor, requests[0], At).Outcome;
        });
        var secondAttempt = Task.Run(async () =>
        {
            await start.Task;
            return workspace.Apply(actor, requests[1], At).Outcome;
        });
        start.SetResult();
        var outcomes = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Contains(Choice.Outcome.Changed, outcomes);
        Assert.Contains(Choice.Outcome.Conflict, outcomes);
        Assert.Equal(1, workspace.Version);
        Assert.Single(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public async Task Apply_ConcurrentSameRequestCreatesOneTrace()
    {
        var (workspace, actor, first, _) = Setup();
        var request = NewRequest(Choice.Action.SelectOne,
            Eligibility.Method.Naive, [first], 0, "same request");
        var start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return workspace.Apply(actor, request, At).Outcome;
        })).ToArray();
        start.SetResult();

        var outcomes = await Task.WhenAll(attempts);

        Assert.Contains(Choice.Outcome.Changed, outcomes);
        Assert.Contains(Choice.Outcome.Replayed, outcomes);
        Assert.Equal(1, workspace.Version);
        Assert.Single(workspace.ReadMemoryTraces(actor));
    }

    [Fact]
    public void Apply_CrossTenantLegalEntityAndMissingPermissionFailClosed()
    {
        var (workspace, actor, first, second) = Setup();
        var request = NewRequest(Choice.Action.ApplyToSelected,
            Eligibility.Method.Naive, [first, second], 0, "scope");
        var otherTenant = actor with { TenantId = Guid.NewGuid() };
        var otherLegalEntity = actor with { LegalEntityId = Guid.NewGuid() };
        var noPermission = actor with { CanUpdateDraft = false };

        Assert.Equal(Choice.Outcome.ScopeDenied,
            workspace.Apply(otherTenant, request, At).Outcome);
        Assert.Equal(Choice.Outcome.ScopeDenied,
            workspace.Apply(otherLegalEntity, request, At).Outcome);
        Assert.Equal(Choice.Outcome.ScopeDenied,
            workspace.Apply(noPermission, request, At).Outcome);
        Assert.Throws<UnauthorizedAccessException>(() =>
            workspace.ReadSelection(otherTenant, first));
        Assert.Throws<UnauthorizedAccessException>(() =>
            workspace.ReadMemoryTraces(otherLegalEntity));
        Assert.Equal(0, workspace.Version);
    }

    [Fact]
    public void Apply_ForeignTargetCannotPartiallyChangeLocalSeries()
    {
        var (workspace, actor, first, second) = Setup();
        var foreign = second with { LegalEntityId = Guid.NewGuid() };

        var result = workspace.Apply(actor,
            NewRequest(Choice.Action.ApplyToSelected, Eligibility.Method.Naive,
                [first, foreign], 0, "foreign target"), At);

        Assert.Equal(Choice.Outcome.ScopeDenied, result.Outcome);
        Assert.Null(workspace.ReadSelection(actor, first));
        Assert.Null(workspace.ReadSelection(actor, second));
        Assert.Empty(workspace.ReadMemoryTraces(actor));
    }

    private static Choice.Request NewRequest(Choice.Action action,
        Eligibility.Method method, IReadOnlyList<Eligibility.SeriesKey> targets,
        int expectedVersion, string reason, Guid? requestKey = null) =>
        new(requestKey ?? Guid.NewGuid(), action, method, targets,
            reason, expectedVersion);

    private static (Choice Workspace, Choice.FixtureActor Actor,
        Eligibility.SeriesKey First, Eligibility.SeriesKey Second) Setup(
        int firstWeeks = 26, int secondWeeks = 26,
        Eligibility.WeekEvidence? secondUntrusted = null)
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var first = new Eligibility.SeriesKey(tenant, legalEntity,
            Guid.NewGuid(), "warehouse-a");
        var second = new Eligibility.SeriesKey(tenant, legalEntity,
            Guid.NewGuid(), "warehouse-b");
        var workspace = new Choice(tenant, legalEntity,
            [History(first, firstWeeks), History(second, secondWeeks,
                secondUntrusted)],
            Eligibility.FixturePolicy.Mvp("fixture-policy-v1"));
        return (workspace, new Choice.FixtureActor(tenant, legalEntity,
            Guid.NewGuid(), true), first, second);
    }

    private static Eligibility.VerifiedHistoryFixture History(
        Eligibility.SeriesKey series, int count,
        Eligibility.WeekEvidence? lastEvidence = null)
    {
        var weeks = Enumerable.Range(0, count)
            .Select(index => new Eligibility.FixtureWeek(Start.AddDays(index * 7),
                lastEvidence.HasValue && index == count - 1
                    ? lastEvidence.Value : Eligibility.WeekEvidence.VerifiedZeroDemand,
                Array.Empty<Eligibility.FixtureDemandEvent>()))
            .ToArray();
        return new Eligibility.VerifiedHistoryFixture(series, weeks);
    }
}
