using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-025 — <b>does the in-app inbox actually run against MongoDB?</b> Real database, real repository, real
/// index.
///
/// <para><b>Why this file is not optional.</b> Every in-app notification assertion in
/// <c>UserNotificationTests</c> runs against an in-memory double, and a double answers LINQ — it will happily
/// order by two <c>DateTimeOffset</c> fields, which is a query MongoDB refuses. That is not hypothetical: the
/// first version of this repository sorted <c>{ReadAt, CreatedAt}</c>, every fake test was green, and
/// <c>DateTimeOffsetSortGuardTests</c> is what caught it. BL-030 is open, so each <c>DateTimeOffset</c> is
/// stored as a BSON array <c>[ticks, offsetMinutes]</c>, and two of them in one sort is "cannot sort with
/// keys that are parallel arrays" — a runtime failure with no compile-time and no fake-test signal.</para>
///
/// <para>So the ordering, the paging, the unread count and both mark-read paths are exercised where the
/// answer comes from mongod rather than from LINQ-to-objects.</para>
/// </summary>
public sealed class UserNotificationMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private UserNotificationRepository _repository = null!;

    /// <summary>The caller. Distinct from <see cref="_bob"/> so "only mine" means something.</summary>
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Notification);
        _repository = new UserNotificationRepository(_harness.DbContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_declared_index_is_the_index_mongod_actually_built()
    {
        /*
         * ⚠ THE INDEX HAS TO EXIST AS DECLARED, AND A MISSING ONE RAISES NOTHING. Mongo answers an unindexed
         * query perfectly well — slower, and completely silent about it. Worse for this collection: the
         * FIRST declared key set was {TenantId, UserId, ReadAt, CreatedAt}, two BSON-array fields in one
         * compound index, which is what "cannot index parallel arrays" is about. So the shape is read back
         * from listIndexes rather than trusted.
         */
        var indexes = await (await _harness.Database
            .GetCollection<UserNotification>(PlatformCollections.UserNotifications)
            .Indexes.ListAsync()).ToListAsync();

        var declared = indexes.SingleOrDefault(
            x => x["name"].AsString == "ix_user_notifications_tenant_user_read_created");

        Assert.True(declared is not null,
            "the in-app inbox index was not built; listIndexes returned: "
            + string.Join(", ", indexes.Select(x => x["name"].AsString)));

        Assert.Equal(
            new BsonDocument { { "TenantId", 1 }, { "UserId", 1 }, { "IsRead", 1 }, { "CreatedAt", -1 } },
            declared!["key"].AsBsonDocument);
    }

    [Fact]
    public async Task Mongod_itself_returns_unread_first_then_newest_first()
    {
        /*
         * THE assertion the fakes cannot make. The ordering here is produced by the server's sort stage, so
         * a sort MongoDB rejects fails HERE rather than in production. Reverting the repository to
         * .SortBy(x => x.ReadAt).ThenByDescending(x => x.CreatedAt) turns this red with
         * "cannot sort with keys that are parallel arrays" — that is the mutation this test exists for.
         */
        var oldUnread = await Write(_alice, "old unread", DateTimeOffset.UtcNow.AddHours(-3));
        var newUnread = await Write(_alice, "new unread", DateTimeOffset.UtcNow.AddHours(-1));
        // NEWEST of the three and still last, because read loses to unread whatever its age.
        var read = await Write(_alice, "already read", DateTimeOffset.UtcNow);

        Assert.True(await _repository.MarkReadAsync(
            _harness.TenantId, _alice, read.Id, DateTimeOffset.UtcNow));

        var page = await _repository.ListForUserAsync(_harness.TenantId, _alice, 0, 20);

        Assert.Equal(
            new[] { "new unread", "old unread", "already read" },
            page.Select(x => x.Title).ToArray());

        // Non-vacuity: three rows really were stored, so the order above is an ordering rather than a filter.
        Assert.Equal(3, page.Count);
        Assert.Contains(page, x => x.Id == oldUnread.Id);
        Assert.Contains(page, x => x.Id == newUnread.Id);
    }

    [Fact]
    public async Task Marking_read_writes_BOTH_shapes_of_the_read_state_in_one_update()
    {
        /*
         * ⚠ THE OTHER HALF OF THE PARALLEL-ARRAY PROBLEM, AND IT ONLY BITES ON A WRITE. An unread row has
         * ReadAt: null — one array field, fine. Marking it read stores a SECOND BSON array on the same
         * document. Reading the row back from mongod is what proves the update landed rather than throwing.
         */
        var row = await Write(_alice, "mine", DateTimeOffset.UtcNow);

        Assert.True(await _repository.MarkReadAsync(
            _harness.TenantId, _alice, row.Id, DateTimeOffset.UtcNow));

        var stored = Assert.Single(await _repository.ListForUserAsync(_harness.TenantId, _alice, 0, 20));
        Assert.NotNull(stored.ReadAt);
        Assert.True(stored.IsRead);
        Assert.Equal(0, await _repository.CountUnreadForUserAsync(_harness.TenantId, _alice));

        // Idempotent against the real server too: the second call matches nothing and moves no timestamp.
        Assert.False(await _repository.MarkReadAsync(
            _harness.TenantId, _alice, row.Id, DateTimeOffset.UtcNow.AddHours(1)));
        var again = Assert.Single(await _repository.ListForUserAsync(_harness.TenantId, _alice, 0, 20));
        Assert.Equal(stored.ReadAt, again.ReadAt);
    }

    [Fact]
    public async Task The_server_side_scope_withholds_another_person_s_rows()
    {
        // Both people have rows, so "A got one row" distinguishes a working filter from an empty collection.
        await Write(_alice, "A's own", DateTimeOffset.UtcNow);
        await Write(_bob, "B's own", DateTimeOffset.UtcNow);

        var mine = await _repository.ListForUserAsync(_harness.TenantId, _alice, 0, 20);
        var theirs = await _repository.ListForUserAsync(_harness.TenantId, _bob, 0, 20);

        Assert.Equal("A's own", Assert.Single(mine).Title);
        Assert.Equal("B's own", Assert.Single(theirs).Title);
        Assert.Equal(1, await _repository.CountUnreadForUserAsync(_harness.TenantId, _alice));

        // And read-all is scoped the same way: B's row survives A clearing their inbox.
        Assert.Equal(1, await _repository.MarkAllReadAsync(_harness.TenantId, _alice, DateTimeOffset.UtcNow));
        Assert.Equal(0, await _repository.CountUnreadForUserAsync(_harness.TenantId, _alice));
        Assert.Equal(1, await _repository.CountUnreadForUserAsync(_harness.TenantId, _bob));
    }

    [Fact]
    public async Task Paging_walks_the_same_order_the_server_sorted()
    {
        for (var i = 0; i < 5; i++)
        {
            await Write(_alice, $"row {i}", DateTimeOffset.UtcNow.AddMinutes(-i));
        }

        var first = await _repository.ListForUserAsync(_harness.TenantId, _alice, 0, 2);
        var second = await _repository.ListForUserAsync(_harness.TenantId, _alice, 2, 2);

        Assert.Equal(new[] { "row 0", "row 1" }, first.Select(x => x.Title).ToArray());
        Assert.Equal(new[] { "row 2", "row 3" }, second.Select(x => x.Title).ToArray());

        // The unread count is the whole inbox, not the page — the number a bell badge needs.
        Assert.Equal(5, await _repository.CountUnreadForUserAsync(_harness.TenantId, _alice));
    }

    private async Task<UserNotification> Write(Guid userId, string title, DateTimeOffset createdAt)
        => await _repository.CreateAsync(new UserNotification
        {
            TenantId = _harness.TenantId,
            UserId = userId,
            EventCode = "platform.tasks.assigned",
            Title = title,
            TargetUrl = "/Tasks/" + Guid.NewGuid(),
            Severity = UserNotificationSeverity.Info,
            CreatedAt = createdAt
        });
}
