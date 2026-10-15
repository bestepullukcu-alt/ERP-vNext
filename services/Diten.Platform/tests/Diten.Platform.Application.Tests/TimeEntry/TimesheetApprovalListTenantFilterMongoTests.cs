using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-597 (WP-CT-LANE-DEBT-01) — THE APPROVAL LIST STORE IS BOUNDED BY TENANT AND SOFT DELETE ITSELF, measured on the real
/// store. <c>TimesheetWeekRepository.ListSubmittedInApprovalAsync</c> applies the base <c>ExecutionFilter</c> (the server
/// context's TenantId + IsDeleted=false); the handler above it also narrows to the workflow instances MOD-0023 lets the
/// caller decide, so with the store filter removed every one of the 353 time-entry tests stayed green (CT sabotage M5). This
/// class pins the store's own bound: tenant A's call returns ONLY tenant A's live Submitted weeks — with no id list, and with
/// an id list that names tenant B's week and A's deleted week too.
/// <para>A week's <c>Id</c> is the document's <c>_id</c>, so two tenants cannot hold the same <c>Id</c>; the "same week" in
/// both tenants is the same person, week key and workflow instance id under a different <c>Id</c>.</para>
/// Disposable mongod (<see cref="TimeEntryMongoFixture"/>); fresh tenant ids per test.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetApprovalListTenantFilterMongoTests
{
    private readonly TimeEntryMongoFixture _fixture;
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _person = Guid.NewGuid();
    private readonly Guid _workflowInstance = Guid.NewGuid();

    public TimesheetApprovalListTenantFilterMongoTests(TimeEntryMongoFixture fixture) => _fixture = fixture;

    private IMongoCollection<TimesheetWeek> Weeks => _fixture.Database.GetCollection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks);

    private TimesheetWeekRepository StoreOf(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return new TimesheetWeekRepository(_fixture.DbContext, context);
    }

    private TimesheetWeek Week(Guid tenant, TimesheetWeekStatus status, long submittedTicks, bool deleted = false) => new()
    {
        TenantId = tenant,
        UserId = _person,
        WeekKey = "2026-W41",
        WeekStartDate = new DateOnly(2026, 10, 5),
        TimeZoneId = "Europe/Istanbul",
        RevisionNumber = 1,
        Status = status,
        WorkflowInstanceId = _workflowInstance,
        SubmittedAtUtcTicks = submittedTicks,
        IsDeleted = deleted
    };

    private async Task<(TimesheetWeek Mine, TimesheetWeek MineDeleted, TimesheetWeek Theirs)> SeedAsync()
    {
        var mine = Week(_tenantA, TimesheetWeekStatus.Submitted, 1_000);
        var mineDeleted = Week(_tenantA, TimesheetWeekStatus.Submitted, 2_000, deleted: true);
        var theirs = Week(_tenantB, TimesheetWeekStatus.Submitted, 500); // the same person / week / instance, tenant B
        await Weeks.InsertManyAsync([mine, mineDeleted, theirs]);
        return (mine, mineDeleted, theirs);
    }

    [Fact]
    public async Task Without_an_id_list_a_tenant_lists_only_its_own_live_submitted_weeks()
    {
        var (mine, _, theirs) = await SeedAsync();

        var listed = await StoreOf(_tenantA).ListSubmittedInApprovalAsync(null, CancellationToken.None);

        Assert.Equal([mine.Id], listed.Select(w => w.Id).ToArray());
        // the control: the other tenant's call sees its own week, so the seed is visible to the store at all
        Assert.Equal([theirs.Id], (await StoreOf(_tenantB).ListSubmittedInApprovalAsync(null, CancellationToken.None)).Select(w => w.Id).ToArray());
    }

    [Fact]
    public async Task With_an_id_list_naming_another_tenants_week_and_a_deleted_one_a_tenant_still_gets_only_its_own_live_week()
    {
        var (mine, mineDeleted, theirs) = await SeedAsync();

        var listed = await StoreOf(_tenantA).ListSubmittedInApprovalAsync([mine.Id, mineDeleted.Id, theirs.Id], CancellationToken.None);

        Assert.Equal([mine.Id], listed.Select(w => w.Id).ToArray());
        Assert.All(listed, w => Assert.Equal(_tenantA, w.TenantId));
    }
}
