using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ManualDraftReviewTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static DemandRevisionDraft Draft(Guid tenant, Guid legalEntity, Guid creator,
        int? incompleteWeek = null, DraftWeekValueKind incompleteKind = DraftWeekValueKind.Missing,
        bool secondSeries = false, bool zero = false)
    {
        var cycle = new PlanningCycle
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = new DateOnly(2026, 10, 2), CalendarId = "ISO-8601",
            CalendarVersion = "1", TimeZoneId = "Asia/Baku",
            HorizonStart = Monday, HorizonEnd = Monday.AddDays(363),
            PlanningPeriodKey = "2026-10-05",
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1, WeekStart = Monday.AddDays(index * 7),
                WeekEnd = Monday.AddDays(index * 7 + 6)
            }).ToList()
        };
        var series = new VerifiedDraftSeries(Guid.NewGuid(), "WH-1", "EA",
            Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                number, Monday.AddDays((number - 1) * 7),
                Monday.AddDays((number - 1) * 7 + 6),
                DraftWeekValueKind.Known, zero ? 0m : 10m)).ToArray());
        var selected = new List<VerifiedDraftSeries> { series };
        if (secondSeries)
            selected.Add(new VerifiedDraftSeries(Guid.NewGuid(), "WH-2", "EA",
                Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                    number, Monday.AddDays((number - 1) * 7),
                    Monday.AddDays((number - 1) * 7 + 6),
                    number == incompleteWeek ? incompleteKind : DraftWeekValueKind.Known,
                    number == incompleteWeek ? null : 10m)).ToArray()));
        else if (incompleteWeek is not null)
            selected[0] = series with { Weeks = series.Weeks.Select(week =>
                week.Number == incompleteWeek
                    ? week with { ValueKind = incompleteKind, Quantity = null }
                    : week).ToArray() };
        return Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, legalEntity, creator, "Verified fixture", Now, selected).Data);
    }

    [Theory]
    [InlineData(DraftWeekValueKind.Missing, false)]
    [InlineData(DraftWeekValueKind.Unknown, false)]
    [InlineData(DraftWeekValueKind.Missing, true)]
    public void IncompleteManualDraft_CannotSubmit_AndCanBeCompleted(
        DraftWeekValueKind kind, bool secondSeries)
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, creator, 17, kind, secondSeries);
        Assert.False(draft.HasCompleteManualQuantities);
        Assert.Equal(DraftReviewOutcome.Invalid, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, creator, null, 0, 0, Now.AddMinutes(1)));
        Assert.Equal(DemandRevisionState.Draft, draft.State);
        Assert.Equal(0, draft.Version);
        Assert.Equal(0, draft.StateVersion);
        var incompleteSeries = draft.Series.Single(series =>
            series.Weeks[16].ValueKind == kind);
        Assert.Null(incompleteSeries.Weeks[16].Quantity);
        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(tenant,
            legalEntity, creator, incompleteSeries.SkuId, incompleteSeries.WarehouseId,
            17, DraftWeekValueKind.Known, 0m, "Completed manually", "complete-17",
            0, Now.AddMinutes(2)));
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, creator, null, 1, 0, Now.AddMinutes(3)));
        Assert.Equal(DemandRevisionState.InReview, draft.State);
    }

    [Fact]
    public void FiftyTwoExplicitZeros_CanSubmit()
    {
        var creator = Guid.NewGuid();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), creator, zero: true);
        Assert.True(draft.HasCompleteManualQuantities);
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, creator, null, 0, 0, Now.AddMinutes(1)));
        Assert.All(draft.Series[0].Weeks, week => Assert.Equal(0m, week.Quantity));
    }

    [Fact]
    public void ExcludedIncompleteSeries_RemainsVisibleAndDoesNotBlockSubmission()
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner, 17,
            DraftWeekValueKind.Unknown, secondSeries: true);
        var excluded = draft.Series.Single(x => x.WarehouseId == "WH-2");
        Assert.False(draft.HasCompleteManualQuantities);
        Assert.Equal(DraftEditOutcome.Changed, draft.ExcludeSeries(tenant,
            legalEntity, planner, excluded.SkuId, excluded.WarehouseId,
            "Unverified series", "exclude-1", 0, Now.AddMinutes(1),
            DemandRevisionState.Draft));
        Assert.True(draft.HasCompleteManualQuantities);
        Assert.Equal(DraftWeekValueKind.Unknown,
            draft.Series.Single(x => x.WarehouseId == "WH-2").Weeks[16].ValueKind);
        Assert.Equal("Unverified series", draft.Series.Single(x =>
            x.WarehouseId == "WH-2").Exclusion!.Reason);
        Assert.True(draft.HasSignificantContribution(planner));
        Assert.Equal(DraftEditOutcome.Replayed, draft.ExcludeSeries(tenant,
            legalEntity, planner, excluded.SkuId, excluded.WarehouseId,
            " Unverified series ", "exclude-1", 0, Now.AddMinutes(2),
            DemandRevisionState.Draft));
        Assert.Equal(DraftEditOutcome.Conflict, draft.ExcludeSeries(tenant,
            legalEntity, planner, excluded.SkuId, excluded.WarehouseId,
            "Different", "exclude-1", 0, Now.AddMinutes(2),
            DemandRevisionState.Draft));
        Assert.Equal(1, draft.Version);
        Assert.Single(draft.Exclusions);
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, planner, null, 1, 0, Now.AddMinutes(3)));
    }

    [Fact]
    public void Exclusion_CannotRemoveLastSelectedSeriesOrEditExcludedWeek()
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, planner);
        var series = draft.Series[0];
        Assert.Equal(DraftEditOutcome.Invalid, draft.ExcludeSeries(tenant,
            legalEntity, planner, series.SkuId, series.WarehouseId,
            "Exclude", "only", 0, Now.AddMinutes(1), DemandRevisionState.Draft));
        Assert.Equal(0, draft.Version);
        Assert.Empty(draft.Exclusions);

        var another = Draft(tenant, legalEntity, planner, secondSeries: true);
        var removed = another.Series.Single(x => x.WarehouseId == "WH-2");
        Assert.Equal(DraftEditOutcome.Changed, another.ExcludeSeries(tenant,
            legalEntity, planner, removed.SkuId, removed.WarehouseId,
            "Remove uncertain series", "remove-second", 0,
            Now.AddMinutes(2), DemandRevisionState.Draft));
        Assert.Equal(DraftEditOutcome.Invalid, another.EditManualWeek(tenant,
            legalEntity, planner, removed.SkuId, removed.WarehouseId,
            1, DraftWeekValueKind.Known, 12m, "Change excluded",
            "edit-excluded", 1, Now.AddMinutes(3)));
        Assert.Equal(1, another.Version);
    }

    [Fact]
    public void PersistedLegacyIncompleteInReview_CannotBeApproved()
    {
        var creator = Guid.NewGuid();
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), creator, 1);
        Assert.Equal(DraftReviewOutcome.Changed, draft.ReplayPersistedReviewTransition(
            DraftReviewAction.Submitted, creator, null, 0, 0, Now.AddMinutes(1)));
        Assert.Equal(DraftReviewOutcome.Invalid, draft.ApplyReviewTransition(
            DraftReviewAction.Approved, Guid.NewGuid(), "Reviewed", 0, 1,
            Now.AddMinutes(2)));
        Assert.Equal(DemandRevisionState.InReview, draft.State);
        Assert.Equal(1, draft.StateVersion);
        Assert.Null(draft.ReviewedBy);
    }

    [Fact]
    public void EveryManualEditorAndCreator_IsExcludedFromIndependentApproval()
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var firstEditor = Guid.NewGuid();
        var lastEditor = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, creator);
        var sku = draft.Series[0].SkuId;
        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(tenant,
            legalEntity, firstEditor, sku, "WH-1", 1, DraftWeekValueKind.Known,
            11m, "First edit", "edit-1", 0, Now.AddMinutes(1)));
        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(tenant,
            legalEntity, lastEditor, sku, "WH-1", 2, DraftWeekValueKind.Known,
            12m, "Last edit", "edit-2", 1, Now.AddMinutes(2)));
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, creator, null, 2, 0, Now.AddMinutes(3)));
        foreach (var actor in new[] { creator, firstEditor, lastEditor })
            Assert.Equal(DraftReviewOutcome.SeparationDenied, draft.ApplyReviewTransition(
                DraftReviewAction.Approved, actor, "Review", 2, 1, Now.AddMinutes(4)));
        Assert.Equal(DemandRevisionState.InReview, draft.State);
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Approved, reviewer, "Reviewed", 2, 1, Now.AddMinutes(5)));
        Assert.Equal(DemandRevisionState.Approved, draft.State);
        Assert.Equal(2, draft.StateVersion);
        Assert.Equal(2, draft.Version);
    }

    [Fact]
    public void ApprovedContent_IsLockedUntilExplicitReturnToDraft()
    {
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, creator);
        var sku = draft.Series[0].SkuId;
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Submitted, creator, null, 0, 0, Now.AddMinutes(1)));
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Approved, reviewer, "Approved", 0, 1, Now.AddMinutes(2)));
        Assert.Equal(DraftEditOutcome.Conflict, draft.EditManualWeek(tenant,
            legalEntity, creator, sku, "WH-1", 1, DraftWeekValueKind.Known,
            99m, "Would be silent", "blocked", 0, Now.AddMinutes(3)));
        Assert.Equal(DraftReviewOutcome.Conflict, draft.ApplyReviewTransition(
            DraftReviewAction.Reopened, creator, "Stale", 0, 1, Now.AddMinutes(4)));
        Assert.Equal(DraftReviewOutcome.Changed, draft.ApplyReviewTransition(
            DraftReviewAction.Reopened, creator, "Change needed", 0, 2, Now.AddMinutes(4)));
        Assert.Equal(DemandRevisionState.Draft, draft.State);
        Assert.Null(draft.ReviewedBy);
        Assert.Equal(DraftEditOutcome.Changed, draft.EditManualWeek(tenant,
            legalEntity, creator, sku, "WH-1", 1, DraftWeekValueKind.Known,
            99m, "Changed after review reset", "allowed", 0, Now.AddMinutes(5)));
    }
}
