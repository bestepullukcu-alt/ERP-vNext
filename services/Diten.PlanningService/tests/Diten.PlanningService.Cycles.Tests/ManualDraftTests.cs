using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ManualDraftTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OtherActor = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SkuA = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SkuB = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateOnly FirstMonday = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static PlanningCycle Cycle() => new()
    {
        TenantId = Tenant,
        LegalEntityId = LegalEntity,
        AsOfDate = new DateOnly(2026, 10, 2),
        CalendarId = "ISO-8601",
        CalendarVersion = "1",
        TimeZoneId = "Asia/Baku",
        HorizonStart = FirstMonday,
        HorizonEnd = FirstMonday.AddDays(363),
        PlanningPeriodKey = "2026-10-05",
        Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
        {
            Number = index + 1,
            WeekStart = FirstMonday.AddDays(index * 7),
            WeekEnd = FirstMonday.AddDays(index * 7 + 6)
        }).ToList()
    };

    private static VerifiedDraftSeries Series(Guid skuId, string warehouseId = "WH-1",
        Func<int, ManualDraftWeekInput>? overrideWeek = null) =>
        new(skuId, warehouseId, "EA", Enumerable.Range(1, 52).Select(number =>
            overrideWeek?.Invoke(number) ?? new ManualDraftWeekInput(number,
                FirstMonday.AddDays((number - 1) * 7),
                FirstMonday.AddDays((number - 1) * 7 + 6),
                DraftWeekValueKind.Known, number == 1 ? 0m : 10m)).ToArray());

    private static Response<DemandRevisionDraft> Create(PlanningCycle cycle,
        IReadOnlyList<VerifiedDraftSeries> series, string reason = "Manual baseline after verified insufficient history",
        Guid? tenant = null, Guid? legalEntity = null) =>
        ManualDraftFactory.Create(cycle, tenant ?? Tenant, legalEntity ?? LegalEntity,
            Actor, reason, Now, series);

    [Fact]
    public void Create_TwoSeries_SharesOneRevisionAndCopiesLocked52Weeks()
    {
        var cycle = Cycle();
        var result = Create(cycle, [Series(SkuA), Series(SkuB, "WH-2")]);

        Assert.True(result.IsSuccessful);
        var draft = Assert.IsType<DemandRevisionDraft>(result.Data);
        Assert.Equal(cycle.Id, draft.PlanningCycleId);
        Assert.Equal(DemandRevisionState.Draft, draft.State);
        Assert.Equal(cycle.PlanningPeriodKey, draft.PlanningPeriodKey);
        Assert.Equal(2, draft.Series.Count);
        Assert.All(draft.Series, series => Assert.Equal(52, series.Weeks.Count));
        Assert.Equal(cycle.CalendarVersion, draft.CalendarVersion);
        Assert.Equal(cycle.TimeZoneId, draft.TimeZoneId);
        Assert.Equal(cycle.Weeks[51].WeekEnd, draft.Weeks[51].WeekEnd);
        Assert.True(draft.HasCompleteManualQuantities);
        Assert.Equal(0m, draft.Series[0].Weeks[0].Quantity);
        Assert.Equal(DraftWeekValueKind.Known, draft.Series[0].Weeks[0].ValueKind);
        Assert.Equal("EA", draft.Series[0].BaseUomId);
        Assert.Equal(DraftWeekSource.Manual, draft.Series[0].Weeks[0].Source);
        Assert.Equal(Actor, draft.Series[0].Weeks[0].ManualActorId);
        Assert.Equal(Now, draft.Series[0].Weeks[0].ManualAt);
        Assert.Equal(draft.CreationReason, draft.Series[0].Weeks[0].ManualReason);

        cycle.Weeks[0].WeekStart = FirstMonday.AddDays(7);
        Assert.Equal(FirstMonday, draft.Weeks[0].WeekStart);
    }

    [Fact]
    public void Create_MissingAndUnknownWeeks_RemainDistinctFromTrueZero()
    {
        var series = Series(SkuA, overrideWeek: number => new ManualDraftWeekInput(number,
            FirstMonday.AddDays((number - 1) * 7),
            FirstMonday.AddDays((number - 1) * 7 + 6),
            number == 2 ? DraftWeekValueKind.Missing :
                number == 3 ? DraftWeekValueKind.Unknown : DraftWeekValueKind.Known,
            number is 2 or 3 ? null : 0m));

        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [series]).Data);
        Assert.Equal(DraftWeekValueKind.Known, draft.Series[0].Weeks[0].ValueKind);
        Assert.Equal(0m, draft.Series[0].Weeks[0].Quantity);
        Assert.Equal(DraftWeekValueKind.Missing, draft.Series[0].Weeks[1].ValueKind);
        Assert.Null(draft.Series[0].Weeks[1].Quantity);
        Assert.Equal(DraftWeekValueKind.Unknown, draft.Series[0].Weeks[2].ValueKind);
        Assert.Null(draft.Series[0].Weeks[2].Quantity);
        Assert.False(draft.HasCompleteManualQuantities);
    }

    [Fact]
    public void Create_DifferentCalendarWeekOrIncompleteHorizon_IsRejected()
    {
        var wrongWeek = Series(SkuA, overrideWeek: number => new ManualDraftWeekInput(number,
            FirstMonday.AddDays((number - 1) * 7 + (number == 12 ? 1 : 0)),
            FirstMonday.AddDays((number - 1) * 7 + 6),
            DraftWeekValueKind.Known, 10m));
        var shortSeries = Series(SkuA) with { Weeks = Series(SkuA).Weeks.Take(51).ToArray() };

        Assert.Equal(422, Create(Cycle(), [wrongWeek]).StatusCode);
        Assert.Equal(422, Create(Cycle(), [shortSeries]).StatusCode);
        Assert.Equal(422, Create(Cycle(), [Series(SkuA), Series(SkuA)]).StatusCode);
    }

    [Fact]
    public void Create_ReasonlessOrInconsistentValue_IsRejected()
    {
        var falseZero = Series(SkuA, overrideWeek: number => new ManualDraftWeekInput(number,
            FirstMonday.AddDays((number - 1) * 7),
            FirstMonday.AddDays((number - 1) * 7 + 6),
            number == 5 ? DraftWeekValueKind.Unknown : DraftWeekValueKind.Known, 0m));

        Assert.Equal(422, Create(Cycle(), [Series(SkuA)], " ").StatusCode);
        Assert.Equal(422, Create(Cycle(), [falseZero]).StatusCode);
    }

    [Fact]
    public void Create_CrossTenantAndCrossLegalEntity_AreDenied()
    {
        Assert.Equal(404, Create(Cycle(), [Series(SkuA)],
            tenant: Guid.NewGuid()).StatusCode);
        Assert.Equal(404, Create(Cycle(), [Series(SkuA)],
            legalEntity: Guid.NewGuid()).StatusCode);
    }

    [Fact]
    public void EditManualWeek_RecordsOldNewActorTimeAndReason()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);

        var outcome = draft.EditManualWeek(Tenant, LegalEntity, OtherActor,
            SkuA, "WH-1", 1, DraftWeekValueKind.Known, 7m,
            "Planner adjustment", "edit-1", 0, Now.AddHours(1));

        Assert.Equal(DraftEditOutcome.Changed, outcome);
        Assert.Equal(1, draft.Version);
        Assert.Equal(OtherActor, draft.UpdatedBy);
        var change = Assert.Single(draft.Changes);
        Assert.Equal(0m, change.OldQuantity);
        Assert.Equal(7m, change.NewQuantity);
        Assert.Equal(OtherActor, change.ActorId);
        Assert.Equal(Now.AddHours(1), change.OccurredAt);
        Assert.Equal("Planner adjustment", change.Reason);
        Assert.Equal(DraftWeekSource.Manual, draft.Series[0].Weeks[0].Source);
        Assert.Equal("Planner adjustment", draft.Series[0].Weeks[0].ManualReason);
    }

    [Fact]
    public void EditManualWeek_RetryIsIdempotentAndChangedRequestConflicts()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        DraftEditOutcome Edit(decimal quantity) => draft.EditManualWeek(
            Tenant, LegalEntity, Actor, SkuA, "WH-1", 1,
            DraftWeekValueKind.Known, quantity, "Adjustment", "same-key", 0, Now);

        Assert.Equal(DraftEditOutcome.Changed, Edit(3m));
        Assert.Equal(DraftEditOutcome.Replayed, Edit(3m));
        Assert.Equal(DraftEditOutcome.Conflict, Edit(4m));
        Assert.Single(draft.Changes);
        Assert.Equal(1, draft.Version);
    }

    [Fact]
    public void EditManualWeek_ScopeReasonAndStaleVersionAreRejected()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        DraftEditOutcome Edit(Guid tenant, Guid legalEntity, string reason, string key,
            int version) => draft.EditManualWeek(tenant, legalEntity, Actor,
            SkuA, "WH-1", 1, DraftWeekValueKind.Known, 3m,
            reason, key, version, Now);

        Assert.Equal(DraftEditOutcome.ScopeDenied,
            Edit(Guid.NewGuid(), LegalEntity, "Adjustment", "a", 0));
        Assert.Equal(DraftEditOutcome.ScopeDenied,
            Edit(Tenant, Guid.NewGuid(), "Adjustment", "b", 0));
        Assert.Equal(DraftEditOutcome.Invalid,
            Edit(Tenant, LegalEntity, " ", "c", 0));
        Assert.Equal(DraftEditOutcome.Changed,
            Edit(Tenant, LegalEntity, "Adjustment", "d", 0));
        Assert.Equal(DraftEditOutcome.Conflict,
            Edit(Tenant, LegalEntity, "Adjustment", "e", 0));
        Assert.Single(draft.Changes);
    }

    [Fact]
    public async Task EditManualWeek_ConcurrentExpectedVersion_AllowsOneLocalChange()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            Task.Run(() => draft.EditManualWeek(Tenant, LegalEntity, Actor,
                SkuA, "WH-1", 1, DraftWeekValueKind.Known, index + 1,
                "Adjustment", $"race-{index}", 0, Now))));

        Assert.Single(outcomes, outcome => outcome == DraftEditOutcome.Changed);
        Assert.Equal(7, outcomes.Count(outcome => outcome == DraftEditOutcome.Conflict));
        Assert.Single(draft.Changes);
    }

    [Fact]
    public void ReturnedWeeks_CannotAlterLockedSnapshot()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        if (draft.Weeks is IList<PlanningWeek> returned)
        {
            try
            {
                returned[0].WeekStart = FirstMonday.AddDays(7);
                returned.Clear();
            }
            catch (NotSupportedException) { }
        }

        Assert.Equal(52, draft.Weeks.Count);
        Assert.Equal(FirstMonday, draft.Weeks[0].WeekStart);
        Assert.Equal(FirstMonday.AddDays(363), draft.Weeks[51].WeekEnd);
    }

    [Fact]
    public void ReturnedSeriesAndWeekReferences_CannotAlterDraftContent()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(),
            [Series(SkuA), Series(SkuB, "WH-2")]).Data);
        var returnedSeries = draft.Series[0];
        if (returnedSeries.Weeks is IList<DemandDraftWeek> returnedWeeks)
        {
            try
            {
                returnedWeeks[0] = new DemandDraftWeek(1, FirstMonday,
                    FirstMonday.AddDays(6), DraftWeekValueKind.Known, 999m,
                    "Injected", OtherActor, Now);
            }
            catch (NotSupportedException) { }
        }
        if (draft.Series is IList<DemandDraftSeries> returned)
        {
            returned[0] = new DemandDraftSeries { SkuId = SkuB,
                WarehouseId = "Injected", BaseUomId = "EA" };
            try { returned.Clear(); }
            catch (NotSupportedException) { }
        }

        Assert.Equal(2, draft.Series.Count);
        Assert.Equal(0m, draft.Series[0].Weeks[0].Quantity);
        Assert.Equal(Actor, draft.Series[0].Weeks[0].ManualActorId);
        Assert.True(draft.HasCompleteManualQuantities);
    }

    [Fact]
    public void ReturnedChanges_CannotEraseEditTrace()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(Tenant,
            LegalEntity, Actor, SkuA, "WH-1", 1, DraftWeekValueKind.Known,
            5m, "Adjustment", "edit-1", 0, Now.AddHours(1)));
        if (draft.Changes is IList<DraftWeekChange> returned)
        {
            returned[0] = returned[0] with { NewQuantity = 999m };
            try { returned.Clear(); }
            catch (NotSupportedException) { }
        }

        Assert.Single(draft.Changes);
        Assert.Equal(0m, draft.Changes[0].OldQuantity);
        Assert.Equal(5m, draft.Changes[0].NewQuantity);
    }

    [Fact]
    public void ReturnedWeekReference_RemainsDetachedAfterControlledEdit()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        var priorReference = draft.Series[0].Weeks[0];

        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(Tenant,
            LegalEntity, Actor, SkuA, "WH-1", 1, DraftWeekValueKind.Known,
            5m, "Adjustment", "edit-1", 0, Now.AddHours(1)));

        Assert.Equal(0m, priorReference.Quantity);
        Assert.Equal(5m, draft.Series[0].Weeks[0].Quantity);
        Assert.Equal(0m, draft.Changes[0].OldQuantity);
    }

    [Fact]
    public void EditManualWeek_WhitespaceEquivalentReason_ReplaysSameDecision()
    {
        var draft = Assert.IsType<DemandRevisionDraft>(Create(Cycle(), [Series(SkuA)]).Data);
        var first = draft.EditManualWeek(Tenant, LegalEntity, Actor, SkuA,
            "WH-1", 1, DraftWeekValueKind.Known, 5m,
            "Adjustment", "edit-1", 0, Now);
        var replay = draft.EditManualWeek(Tenant, LegalEntity, Actor, SkuA,
            "WH-1", 1, DraftWeekValueKind.Known, 5m,
            "  Adjustment  ", "edit-1", 0, Now.AddHours(1));
        var changed = draft.EditManualWeek(Tenant, LegalEntity, Actor, SkuA,
            "WH-1", 1, DraftWeekValueKind.Known, 5m,
            "Different adjustment", "edit-1", 0, Now.AddHours(2));

        Assert.Equal(DraftEditOutcome.Changed, first);
        Assert.Equal(DraftEditOutcome.Replayed, replay);
        Assert.Equal(DraftEditOutcome.Conflict, changed);
        Assert.Single(draft.Changes);
        Assert.Equal("Adjustment", draft.Changes[0].Reason);
        Assert.Equal(Now, draft.Changes[0].OccurredAt);
        Assert.Equal(1, draft.Version);
    }
}
