using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-4A — the plan reads Faz 4 needs, on the PRODUCTION engine and handlers: the stored product pick on the detail,
/// an OLD whole-period (committed) plan shown as its written visits on legacy weeks (nothing generated, stages in the
/// written order), the list's counts and archive filter, and the detail's current / next-draft week.
/// <para>Period 2026-09-01 … 2026-09-28: weeks 31 Aug, 7, 14, 21, 28 Sep; "today" Wed 2 Sep (week 31 Aug).</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    // ── 1 · the detail carries the stored product pick ──────────────────────────────────────────────────────────

    [Fact]
    public async Task The_detail_carries_each_doctors_stored_product_pick_and_an_empty_list_without_one()
    {
        var env = Env.WithTwoDoctors();
        var product = Guid.NewGuid();
        env.Session.Selection.SelectedContacts.Single(c => c.ContactId == env.DoctorA).Products.Add(
            new PlanningSessionSelectedProduct { ProductId = product, ProductCode = "ALMIBA", Role = "non-promo" });

        var dto = (await Detail(env, Wed2Sep)).Data!;

        var row = Assert.Single(dto.SelectedContacts.Single(c => c.ContactId == env.DoctorA).Products!);
        Assert.Equal((product, "ALMIBA", "non-promo"), (row.ProductId, row.ProductCode, row.Role));
        Assert.Null(row.ProductName); // no MDM call on a plan read
        Assert.Empty(dto.SelectedContacts.Single(c => c.ContactId == env.DoctorB).Products!);
    }

    // ── 3 · an old committed plan: its written visits, frozen; nothing generated ─────────────────────────────────

    [Fact]
    public async Task An_old_committed_plan_shows_only_its_written_visits_on_legacy_weeks_in_their_stage_order()
    {
        var env = LegacyEnv(out var written);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        // Only the written, not cancelled visits; nothing generated.
        Assert.All(preview.Scheduled, s => Assert.True(s.IsFixed));
        Assert.Equal(written.Where(v => !v.IsCancelled()).Select(v => v.Id).OrderBy(id => id),
            preview.Scheduled.Select(s => s.VisitRef).OrderBy(id => id));
        // Doctor A's stages follow the written order (no re-projection, no double count).
        Assert.Equal(new int?[] { 0, 1 },
            preview.Scheduled.Where(s => s.ContactId == env.DoctorA).OrderBy(s => s.PlannedDate).Select(s => s.StageIndex));
        Assert.Empty(preview.Unscheduled);

        var weeks = preview.Weeks!.ToDictionary(w => w.WeekStart);
        foreach (var week in new[] { "2026-09-07", "2026-09-14" })
        {
            Assert.Equal((PlanningWeekDisplayStatus.Approved, PlanningWeekStatus.Legacy), (weeks[week].Status, weeks[week].StoredStatus));
            Assert.Empty(weeks[week].History!);
        }

        Assert.Equal(PlanningWeekDisplayStatus.Empty, weeks["2026-08-31"].Status);
        Assert.Equal(PlanningWeekDisplayStatus.Empty, weeks["2026-09-21"].Status); // only a cancelled visit there
        Assert.Equal(PlanningWeekDisplayStatus.Empty, weeks["2026-09-28"].Status);

        // The detail tells the same weeks, and has no next draft week (an old plan has no drafts).
        var dto = (await Detail(env, Wed2Sep)).Data!;
        var detailWeeks = dto.Weeks!.ToDictionary(w => w.WeekStart);
        Assert.Equal((PlanningWeekDisplayStatus.Approved, PlanningWeekStatus.Legacy, 2),
            (detailWeeks["2026-09-07"].Status, detailWeeks["2026-09-07"].StoredStatus, detailWeeks["2026-09-07"].VisitCount));
        Assert.Equal(PlanningWeekDisplayStatus.Empty, detailWeeks["2026-09-21"].Status);
        Assert.Null(dto.NextDraftWeekStart);
    }

    [Fact]
    public async Task An_old_generated_plan_still_plans_like_the_new_model()
    {
        var env = LegacyEnv(out _);
        env.Session.Status = PlanningSessionStatus.Generated;

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Contains(preview.Scheduled, s => !s.IsFixed);
        Assert.DoesNotContain(preview.Weeks!, w => w.StoredStatus == PlanningWeekStatus.Legacy);
    }

    // ── 4 · list counts + archive filter ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_counts_doctors_pharmacies_and_open_weeks_and_hides_archived_plans_unless_asked()
    {
        var store = new SessionList();
        var draft = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1", ResourceDisplayName = "Beste",
            Selection = new PlanningSessionSelection
            {
                SelectedContacts =
                {
                    new PlanningSessionSelectedContact { ContactId = Id(11), AccountId = Id(21) },
                    new PlanningSessionSelectedContact { ContactId = Id(12), AccountId = Id(21) },
                    new PlanningSessionSelectedContact { ContactId = Id(12), AccountId = Id(22) } // same doctor, 2nd place
                },
                SelectedPharmacyIds = { Id(70) }
            },
            Weeks = { new PlanningWeek { WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved } }
        };
        var committed = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1", Status = PlanningSessionStatus.Committed
        };
        var archived = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1", Status = PlanningSessionStatus.Archived
        };
        store.Items.AddRange(new[] { draft, committed, archived });
        var handler = new ListPlanningSessionsHandler(
            TenantOf(Tenant), store, new TestCallerScope("rep-1"), new ListedPeriod(Id(30)), new PinnedClock(Wed2Sep));

        var list = (await handler.Handle(new ListPlanningSessionsQuery(), default)).Data!.Items;

        Assert.DoesNotContain(list, i => i.PlanningSessionId == archived.Id);
        var row = list.Single(i => i.PlanningSessionId == draft.Id);
        Assert.Equal((2, 1, 1, 4, "Beste"), (row.DoctorCount, row.PharmacyCount, row.ApprovedWeekCount, row.DraftWeekCount, row.ResourceDisplayName));
        Assert.Equal(0, list.Single(i => i.PlanningSessionId == committed.Id).DraftWeekCount);

        var all = (await handler.Handle(new ListPlanningSessionsQuery(IncludeArchived: true), default)).Data!.Items;
        Assert.Contains(all, i => i.PlanningSessionId == archived.Id);
        var byStatus = (await handler.Handle(new ListPlanningSessionsQuery(Status: "archived"), default)).Data!.Items;
        Assert.Equal(archived.Id, Assert.Single(byStatus).PlanningSessionId);
    }

    // ── 5 · the detail's current / next-draft week ─────────────────────────────────────────────────────────────

    [Theory]
    // Inside the period: today's week; the next open week after it skips the approved 7 Sep.
    [InlineData("2026-09-02", "2026-08-31", "2026-09-14")]
    // Before the period: no current week; the first open week of the period is next.
    [InlineData("2026-08-20", null, "2026-08-31")]
    // In the last week: nothing after it.
    [InlineData("2026-09-28", "2026-09-28", null)]
    // After the period: neither.
    [InlineData("2026-10-05", null, null)]
    public async Task The_detail_names_the_current_week_and_the_next_week_to_open(string today, string? current, string? next)
    {
        var env = Env.WithTwoDoctors();
        env.Session.Weeks.Add(new PlanningWeek { WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved });
        var at = new DateTimeOffset(DateOnly.Parse(today).ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);

        var dto = (await Detail(env, at)).Data!;

        Assert.Equal((current, next), (dto.CurrentWeekStart, dto.NextDraftWeekStart));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private Task<Diten.CrmService.Application.Common.Models.Response<PlanningSessionDto>> Detail(Env env, DateTimeOffset now)
        => new GetPlanningSessionByIdHandler(
                TenantOf(Tenant), new FakePlanningSessionRepository(env.Session), TestCallerScope.Unrestricted("rep-1"),
                new Diten.CrmService.Application.Features.PlannedVisit.VisitTargetNameReader(env.Accounts, env.Contacts),
                env.Periods, new PinnedClock(now), env.PlannedVisits)
            .Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default);

    /// <summary>An old whole-period plan (committed): doctor A written on 8 Sep (stage 0) and 15 Sep (stage 1), doctor B
    /// on 9 Sep, and a CANCELLED doctor A visit on 22 Sep — every one in CommittedPlannedVisitIds. Weekly frequency, so a
    /// new-model plan WOULD generate visits for every week.</summary>
    private static Env LegacyEnv(out List<PlannedVisitEntity> written)
    {
        var env = WeeklyEnv();
        written = new List<PlannedVisitEntity>
        {
            Written(env, env.DoctorA, new DateOnly(2026, 9, 8), 0),
            Written(env, env.DoctorA, new DateOnly(2026, 9, 15), 1),
            Written(env, env.DoctorB, new DateOnly(2026, 9, 9), 0),
            Written(env, env.DoctorA, new DateOnly(2026, 9, 22), 2, PlannedVisitStatus.Cancelled)
        };
        env.Session.Status = PlanningSessionStatus.Committed;
        env.Session.CommittedPlannedVisitIds = written.Select(v => v.Id).ToList();
        return env;
    }

    private static PlannedVisitEntity Written(Env env, Guid doctor, DateOnly date, int stage, string status = PlannedVisitStatus.Planned)
    {
        var atom = env.SeedCommittedAtom(doctor);
        atom.PlannedDate = date;
        atom.PlanStatus = status;
        atom.Resource = new PlannedVisitResourceRef { ResourceId = "rep-1", ResourceType = "user" };
        atom.Content = new PlannedVisitContentRef { StageIndex = stage };
        return atom;
    }

    /// <summary>The period list read: the shared test period by id (GetByIdsAsync), nothing else.</summary>
    private sealed class ListedPeriod(Guid id) : ICyclePeriodReader
    {
        private readonly FakeCyclePeriodReader _inner = new(id);

        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => _inner.ResolveActiveAsync(at, country, legalEntityId, businessUnitId, ct);

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct) => _inner.GetByIdAsync(cyclePeriodId, ct);

        public async Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            var rows = new List<CyclePeriodSnapshot>();
            foreach (var periodId in ids)
            {
                if (await _inner.GetByIdAsync(periodId, ct) is { } p)
                {
                    rows.Add(p);
                }
            }

            return rows;
        }

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => _inner.ListByYearAsync(year, scopeType, scopeRef, ct);
    }
}
