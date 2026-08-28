using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// The comment feed's ORDER, against a REAL MongoDB.
///
/// <para><b>Why not the in-memory double.</b> A first attempt covered this with the fake repository, which does
/// its own ordering — so reversing the real repository's sort broke nothing. That is the same shape as the defect
/// this whole harness exists for: a fake at the end of the chain proving the chain works.</para>
///
/// <para><b>Why the sort is in memory (BL-030).</b> <c>CreatedAt</c> is a <c>DateTimeOffset</c>, which this driver
/// stores as a BSON ARRAY <c>[ticks, offsetMinutes]</c>. A server-side sort on two keys — which the stable
/// tie-break on Id requires — is rejected at runtime with "cannot sort with keys that are parallel arrays". A
/// task's conversation is bounded, so ordering it in the process costs nothing; the point of running this against
/// a real server is that the QUERY still has to survive, and any future move back to a server-side sort fails
/// here rather than in front of a user.</para>
/// </summary>
public sealed class TaskCommentOrderMongoTests : IAsyncLifetime
{
    private static readonly Guid TaskId = Guid.Parse("2c3896fc-1848-4539-8a99-774e72651b8a");
    private static readonly DateTimeOffset Base = new(2026, 7, 20, 9, 0, 0, TimeSpan.Zero);

    private MongoIntegrationHarness _harness = null!;
    private TaskCommentRepository _repository = null!;

    public async Task InitializeAsync()
    {
        /*
         * ⚠ ISOLATED, NOT TENANT-SHARED, AND THE REASON IS IN THE TEST BELOW: the tie-break case asserts on
         * FIXED ids (1111…, 2222…, 3333…) because the rule under test is "descending by id" and only constant,
         * ordered ids can prove it. Constant ids cannot live in a shared database — the second run collides
         * with the first on _id. The scope name is fixed, so this is one database reused, never one per run.
         */
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "task_comment_order",
            SchemaProfile.WorkflowWorkCenter);
        _repository = new TaskCommentRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_feed_comes_back_newest_first()
    {
        // Seeded out of order on purpose: insertion order must not be what the reader sees.
        await SeedAsync("ikinci", Base.AddHours(1));
        await SeedAsync("ilk", Base);
        await SeedAsync("üçüncü", Base.AddHours(2));

        var comments = await _repository.ListByTaskIdAsync(TaskId);

        Assert.Equal(["üçüncü", "ikinci", "ilk"], comments.Select(comment => comment.Text));
    }

    [Fact]
    public async Task Comments_written_in_the_same_instant_fall_back_to_a_declared_tie_break()
    {
        /*
         * Order is behaviour on this screen: a feed that rearranges itself between reads reads as data changing.
         *
         * This asserts the RULE (descending by id) rather than "the same twice", because "the same twice" cannot
         * tell the difference — the sort is stable, so with the tie-break deleted equal timestamps simply keep
         * whatever order they arrived in and two reads in one process still agree. They were SEEDED in ASCENDING
         * id order, so the expected descending result is only produced by the tie-break actually being there.
         */
        var ids = new[]
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333")
        }.OrderBy(id => id).ToList();

        foreach (var id in ids)
        {
            await SeedAsync($"same instant {id}", Base, id: id);
        }

        var comments = await _repository.ListByTaskIdAsync(TaskId);

        Assert.Equal(ids.OrderByDescending(id => id), comments.Select(comment => comment.Id));
    }

    [Fact]
    public async Task The_batched_read_orders_the_same_way_and_keeps_other_tasks_out()
    {
        var otherTask = Guid.Parse("11111111-2222-3333-4444-555555555555");
        await SeedAsync("bu görev", Base);
        await SeedAsync("daha yeni", Base.AddHours(1));
        await SeedAsync("başka görev", Base.AddHours(2), otherTask);

        var mine = await _repository.ListByTaskIdsAsync([TaskId]);

        Assert.Equal(["daha yeni", "bu görev"], mine.Select(comment => comment.Text));
    }

    [Fact]
    public async Task A_task_nobody_has_commented_on_reads_as_empty_rather_than_failing()
    {
        var comments = await _repository.ListByTaskIdAsync(Guid.NewGuid());

        Assert.Empty(comments);
    }

    private Task SeedAsync(string text, DateTimeOffset at, Guid? taskId = null, Guid? id = null)
        => _repository.CreateAsync(new TaskComment
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = _harness.TenantContext.TenantId,
            TaskItemId = taskId ?? TaskId,
            Text = text,
            AuthorUserId = Guid.NewGuid(),
            AuthorDisplayName = "CT",
            CreatedAt = at
        });
}
