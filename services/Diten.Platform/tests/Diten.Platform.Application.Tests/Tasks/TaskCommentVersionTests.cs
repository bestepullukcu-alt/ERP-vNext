using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX4 (item 5) — a comment is rewritten only over the version that was READ. One author can
/// still race themselves (an edit in one tab, a withdrawal in another); an unconditional rewrite then brought the
/// withdrawn text back.
/// </summary>
public sealed class TaskCommentVersionTests
{
    private static readonly Guid Mentioned = Guid.Parse("5c000000-0000-4000-8000-000000000001");

    // ── the handlers answer a lost race, and write nothing ─────────────────────────────────────────────────────

    [Fact]
    public async Task An_edit_that_lost_to_a_withdrawal_is_refused_and_the_text_stays_withdrawn()
    {
        var (task, comment, store) = Seed();
        var racing = new RacingComments(store, beforeWrite: () => Withdraw(store, comment.Id));
        var notifications = new FakeTaskNotificationService();
        var handler = new UpdateTaskCommentHandler(
            new FakeTaskItemRepository(task), racing, new FakeCurrentUserContext(TaskTestData.Me),
            new AlwaysAdmitReadAccessPolicy(), notifications, NullLogger<UpdateTaskCommentHandler>.Instance,
            TaskWriteGateDoubles.Open);

        var answer = await handler.Handle(new UpdateTaskCommentCommand(task.Id, comment.Id,
            new UpdateTaskCommentRequest("yeni metin", [Mentioned]), "corr"), CancellationToken.None);

        Assert.Equal(409, answer.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, answer.ReasonCode);
        var stored = Assert.Single(store.Comments);
        Assert.Null(stored.Text);
        Assert.NotNull(stored.WithdrawnAt);
        Assert.Empty(notifications.Notifications);   // a mention that was not saved is not announced
    }

    [Fact]
    public async Task A_withdrawal_that_lost_to_an_edit_is_refused_and_the_edit_stands()
    {
        var (task, comment, store) = Seed();
        var racing = new RacingComments(store, beforeWrite: () => Edit(store, comment.Id, "başka sekmeden"));
        var handler = new WithdrawTaskCommentHandler(
            racing, new FakeCurrentUserContext(TaskTestData.Me), new FakeTaskItemRepository(task), TaskWriteGateDoubles.Open);

        var answer = await handler.Handle(new WithdrawTaskCommentCommand(task.Id, comment.Id, "corr"), CancellationToken.None);

        Assert.Equal(409, answer.StatusCode);
        Assert.Equal("başka sekmeden", Assert.Single(store.Comments).Text);
    }

    [Fact]
    public async Task An_edit_with_no_race_goes_through_and_moves_the_version()
    {
        // Non-vacuity for the two tests above.
        var (task, comment, store) = Seed();
        var handler = new UpdateTaskCommentHandler(
            new FakeTaskItemRepository(task), new RacingComments(store, beforeWrite: null),
            new FakeCurrentUserContext(TaskTestData.Me), new AlwaysAdmitReadAccessPolicy(), new FakeTaskNotificationService(),
            NullLogger<UpdateTaskCommentHandler>.Instance, TaskWriteGateDoubles.Open);

        var answer = await handler.Handle(new UpdateTaskCommentCommand(task.Id, comment.Id,
            new UpdateTaskCommentRequest("yeni metin"), "corr"), CancellationToken.None);

        Assert.Equal(204, answer.StatusCode);
        var stored = Assert.Single(store.Comments);
        Assert.Equal("yeni metin", stored.Text);
        Assert.Equal(2, stored.Version);
    }

    // ── the production repository, against a real mongod ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_repository_refuses_a_write_over_a_version_that_moved()
    {
        await using var mongo = await DisposableStandaloneMongo.StartAsync();
        var (repository, _, tenant) = await RepositoryAsync(mongo);
        var comment = await repository.CreateAsync(NewComment(tenant));

        var editing = (await repository.GetByIdAsync(comment.Id))!;
        var withdrawing = (await repository.GetByIdAsync(comment.Id))!;
        withdrawing.Text = null;
        withdrawing.WithdrawnAt = DateTimeOffset.UtcNow;
        Assert.True(await repository.UpdateAsync(withdrawing, 1));

        editing.Text = "geri gelmesin";
        Assert.False(await repository.UpdateAsync(editing, 1));

        var stored = (await repository.GetByIdAsync(comment.Id))!;
        Assert.Null(stored.Text);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_row_stored_without_a_version_field_is_version_1()
    {
        await using var mongo = await DisposableStandaloneMongo.StartAsync();
        var (repository, database, tenant) = await RepositoryAsync(mongo);
        var comment = await repository.CreateAsync(NewComment(tenant));
        await database.GetCollection<BsonDocument>(PlatformCollections.TaskComments).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(comment.Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset("Version"));

        var loaded = (await repository.GetByIdAsync(comment.Id))!;
        Assert.Equal(1, loaded.Version);
        loaded.Text = "düzeltildi";

        Assert.True(await repository.UpdateAsync(loaded, 1));
        Assert.Equal("düzeltildi", (await repository.GetByIdAsync(comment.Id))!.Text);
    }

    // ── harness ────────────────────────────────────────────────────────────────────────────────────────────────

    private static (TaskItem Task, TaskComment Comment, FakeTaskCommentRepository Store) Seed()
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant, Title = "Fix4 yorum", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me, CreatedByUserId = TaskTestData.Me, OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.InProgress, Version = 1
        };
        var comment = new TaskComment
        {
            TenantId = TaskTestData.Tenant, TaskItemId = task.Id, Text = "ilk metin", AuthorUserId = TaskTestData.Me
        };
        return (task, comment, new FakeTaskCommentRepository(comment));
    }

    private static TaskComment NewComment(Guid tenant) => new()
    {
        TenantId = tenant, TaskItemId = Guid.NewGuid(), Text = "ilk metin", AuthorUserId = TaskTestData.Me
    };

    /// <summary>The OTHER tab's write, landing between this handler's read and its write.</summary>
    private static async Task Withdraw(FakeTaskCommentRepository store, Guid id)
    {
        var other = Clone((await store.GetByIdAsync(id))!);
        var version = other.Version;
        other.Text = null;
        other.WithdrawnAt = DateTimeOffset.UtcNow;
        Assert.True(await store.UpdateAsync(other, version));
    }

    private static async Task Edit(FakeTaskCommentRepository store, Guid id, string text)
    {
        var other = Clone((await store.GetByIdAsync(id))!);
        var version = other.Version;
        other.Text = text;
        Assert.True(await store.UpdateAsync(other, version));
    }

    private static TaskComment Clone(TaskComment comment)
        => System.Text.Json.JsonSerializer.Deserialize<TaskComment>(System.Text.Json.JsonSerializer.Serialize(comment))!;

    private static async Task<(TaskCommentRepository, IMongoDatabase, Guid)> RepositoryAsync(DisposableStandaloneMongo mongo)
    {
        PlatformTestSerializers.Register();
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{mongo.Port}/?directConnection=true");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var database = client.GetDatabase("diten_platform_standalone_comment_version");
        var tenant = Guid.NewGuid();
        var context = new Diten.Platform.Common.Tenancy.TenantContext();
        context.SetTenant(tenant);
        return (new TaskCommentRepository(new PlatformDbContext(client, database), context), database, tenant);
    }

    /// <summary>Reads hand back a COPY (as a database does), and the other tab's write runs just before this one's.</summary>
    private sealed class RacingComments(FakeTaskCommentRepository inner, Func<Task>? beforeWrite) : ITaskCommentRepository
    {
        public Task<TaskComment> CreateAsync(TaskComment comment, CancellationToken ct = default) => inner.CreateAsync(comment, ct);
        public Task<IReadOnlyList<TaskComment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default) => inner.ListByTaskIdAsync(taskItemId, ct);
        public Task<IReadOnlyList<TaskComment>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskItemIds, CancellationToken ct = default) => inner.ListByTaskIdsAsync(taskItemIds, ct);

        public async Task<TaskComment?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await inner.GetByIdAsync(id, ct) is { } found ? Clone(found) : null;

        public async Task<bool> UpdateAsync(TaskComment comment, int expectedVersion, CancellationToken ct = default)
        {
            if (beforeWrite is not null)
            {
                await beforeWrite();
            }

            return await inner.UpdateAsync(comment, expectedVersion, ct);
        }
    }
}
