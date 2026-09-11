using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S1 — the ONE bridge, at the service level. AC1 (idempotent), AC2 (batched, both directions), AC3
/// (tenant isolation).
/// </summary>
public sealed class RecordLinkServiceTests
{
    private static readonly Guid MeetingId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid OtherTenantTaskId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    // ── AC1 — idempotent add ──────────────────────────────────────────────────

    [Fact]
    public async Task Adding_the_SAME_six_values_twice_writes_ONE_row()
    {
        var repository = new FakeRecordLinkRepository();
        var service = Service(repository);

        var first = await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.Agenda);
        var second = await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.Agenda);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task The_SAME_two_records_with_a_DIFFERENT_link_type_writes_a_SECOND_row()
    {
        // The pack's own example: a task can be `preparation` for a meeting and later `bornFromMeeting` from
        // it — two facts about the same pair, not one link overwritten.
        var repository = new FakeRecordLinkRepository();
        var service = Service(repository);

        await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.Preparation);
        await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.BornFromMeeting);

        Assert.Equal(2, repository.Items.Count);
    }

    [Fact]
    public async Task AddLink_recovers_when_a_concurrent_call_wins_the_race_and_raises_NO_error()
    {
        // K11's "hata da vermez": two callers who both saw nothing must both get back a link, never a fault —
        // whichever one the unique index actually accepted.
        var repository = new FakeRecordLinkRepository { SimulateConcurrentInsertOnNextCreate = true };
        var service = Service(repository);

        var result = await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.Agenda);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Single(repository.Items);
    }

    // ── AC2 — batched, both directions ────────────────────────────────────────

    [Fact]
    public async Task ListBySource_and_ListByTarget_both_find_the_SAME_link_from_their_own_side()
    {
        var repository = new FakeRecordLinkRepository();
        var service = Service(repository);
        await service.AddLinkAsync(Meeting(), Task_(), RecordLinkTypes.Agenda);

        var bySource = await service.ListBySourceAsync([MeetingId]);
        var byTarget = await service.ListByTargetAsync([TaskId]);

        Assert.Single(bySource);
        Assert.Single(byTarget);
        Assert.Equal(bySource[0].Id, byTarget[0].Id);
    }

    // Batching (N ids, ONE call, never one per id) is asserted at the resolver layer, where a per-call counter
    // actually exists — see RelatedRecordsProjectionTests.Ten_linked_tasks_resolve_through_ONE_batched_call.

    // ── AC3 — tenant isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task A_link_created_in_ANOTHER_tenant_is_invisible_from_either_direction()
    {
        var repository = new FakeRecordLinkRepository();
        var otherTenantService = Service(repository, tenant: TaskTestData.OtherTenant);
        await otherTenantService.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, MeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, OtherTenantTaskId),
            RecordLinkTypes.Agenda);

        var myService = Service(repository, tenant: TaskTestData.Tenant);

        Assert.Empty(await myService.ListBySourceAsync([MeetingId]));
        Assert.Empty(await myService.ListByTargetAsync([OtherTenantTaskId]));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static RecordLinkEndpoint Meeting() => new(RecordLinkModuleCodes.Meetings, MeetingId);

    private static RecordLinkEndpoint Task_() => new(RecordLinkModuleCodes.Tasks, TaskId);

    private static IRecordLinkService Service(FakeRecordLinkRepository repository, Guid? tenant = null)
        => new RecordLinkService(
            repository,
            new FakeTenantContext(tenant ?? TaskTestData.Tenant),
            new FakeCurrentUserContext(TaskTestData.Me));
}
