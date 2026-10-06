using System.Collections.Concurrent;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;
using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-USERS-LIST-QUERY-01 — the two measurements that do not go through HTTP: (1) the derived status of the Mongo
/// expression equals <see cref="UserLifecycle.StatusOf"/> on every combination of the four facts, and (2) how many
/// commands the list costs (the N+1 is gone). Both run against the same throwaway mongod as the endpoint tests.
/// </summary>
public sealed partial class UserListQueryTests
{
    // ── (1) the Mongo status expression IS UserLifecycle.StatusOf ────────────────────────────────────────

    [Fact]
    public async Task The_status_filter_the_status_order_and_the_summary_agree_with_UserLifecycle_StatusOf_on_every_combination_of_the_four_facts()
    {
        var tenantId = Guid.NewGuid();
        var raw = _host.Database.GetCollection<BsonDocument>("users");
        var users = _host.Database.GetCollection<User>("users");

        // 2⁴ combinations of MustChangePassword × EmailConfirmed × LastLoginAt(null/set) × IsActive …
        var n = 0;
        foreach (var mustChange in new[] { false, true })
        foreach (var confirmed in new[] { false, true })
        foreach (var loggedIn in new[] { false, true })
        foreach (var active in new[] { false, true })
        {
            var user = new User($"combo{n++}@parity.test", "hash:x", "Combo", "User", tenantId);
            if (mustChange) user.RequirePasswordChange(null);
            if (confirmed) user.ConfirmEmail();
            if (loggedIn) user.RecordLoginSuccess();
            if (!active) user.Deactivate();
            await users.InsertOneAsync(user);
        }

        // … and documents where the facts are simply ABSENT (older shapes): a missing field must read as its entity default.
        for (var i = 0; i < 2; i++)
        {
            var user = new User($"bare{i}@parity.test", "hash:x", "Bare", "Doc", tenantId);
            await users.InsertOneAsync(user);
            await raw.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(user.Id, GuidRepresentation.Standard)),
                Builders<BsonDocument>.Update.Unset("MustChangePassword").Unset("EmailConfirmed").Unset("LastLoginAt").Unset("IsActive"));
        }

        var stored = await users.Find(u => u.TenantId == tenantId).ToListAsync();
        Assert.Equal(18, stored.Count);
        // Every status is actually reachable in this data set, so the comparison below is not vacuous.
        Assert.Equal(3, stored.Select(UserLifecycle.StatusOf).Distinct().Count());

        var reader = new UserListReader(_host.Database);
        UserListCriteria Criteria(string[] statuses, UserListSortKey key = UserListSortKey.Email)
            => new(0, 500, null, new UserListSort(key, false), statuses, [], null);

        foreach (var status in new[] { UserLifecycle.StatusInvited, UserLifecycle.StatusActive, UserLifecycle.StatusInactive })
        {
            var expected = stored.Where(u => UserLifecycle.StatusOf(u) == status).Select(u => u.Id).Order().ToList();
            var page = await reader.SearchAsync(tenantId, Criteria([status]), CancellationToken.None);

            Assert.Equal(expected, page.Items.Select(u => u.Id).Order().ToList());
            Assert.Equal(expected.Count, page.FilteredTotal);
            Assert.All(page.Items, u => Assert.Equal(status, UserLifecycle.StatusOf(u)));
        }

        // The order: the sequence of production statuses is non-decreasing (Active < Inactive < Invited), descending mirrors it.
        var ascending = (await reader.SearchAsync(tenantId, Criteria([], UserListSortKey.Status), CancellationToken.None)).Items.Select(UserLifecycle.StatusOf).ToList();
        Assert.Equal(ascending.OrderBy(s => s, StringComparer.Ordinal), ascending);
        var descending = (await reader.SearchAsync(tenantId,
            Criteria([], UserListSortKey.Status) with { Sort = new UserListSort(UserListSortKey.Status, true) }, CancellationToken.None)).Items.Select(UserLifecycle.StatusOf).ToList();
        Assert.Equal(descending.OrderByDescending(s => s, StringComparer.Ordinal), descending);

        // The summary is the same expression counted.
        var summary = await reader.GetSummaryAsync(tenantId, CancellationToken.None);
        Assert.Equal(stored.Count, summary.Total);
        Assert.Equal(stored.Count(u => UserLifecycle.StatusOf(u) == UserLifecycle.StatusInvited), summary.Invited);
        Assert.Equal(stored.Count(u => UserLifecycle.StatusOf(u) == UserLifecycle.StatusActive), summary.Active);
        Assert.Equal(stored.Count(u => UserLifecycle.StatusOf(u) == UserLifecycle.StatusInactive), summary.Passive);
        Assert.Equal(stored.Count, summary.NoRole); // nobody here holds a role
    }

    // ── (2) the N+1 is gone: measured as commands the server actually received ───────────────────────────

    private sealed record Command(string Name, string Collection);

    private (IMongoDatabase Database, ConcurrentQueue<Command> Log) CountingDatabase()
    {
        var settings = MongoClientSettings.FromConnectionString(_host.ConnectionString);
#pragma warning disable CS0618 // the production client sets it the same way (Persistence/DependencyInjection.cs)
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var log = new ConcurrentQueue<Command>();
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e =>
        {
            if (e.CommandName is "find" or "aggregate" or "distinct" or "count" or "getMore")
            {
                var value = e.Command.GetValue(e.CommandName, BsonNull.Value);
                log.Enqueue(new Command(e.CommandName, value.IsString ? value.AsString : string.Empty));
            }
        });
        return (new MongoClient(settings).GetDatabase(AccountKindAcceptance.DatabaseName), log);
    }

    /// <summary>A disposable tenant with <paramref name="count"/> users, every one holding a live role.</summary>
    private async Task<Guid> TenantWithUsersAsync(int count)
    {
        var tenantId = Guid.NewGuid();
        var users = _host.Database.GetCollection<User>("users");
        var roles = _host.Database.GetCollection<Role>("roles");
        var userRoles = _host.Database.GetCollection<UserRole>("userRoles");
        var roleA = new Role("A", "A", null, tenantId);
        var roleB = new Role("B", "B", null, tenantId);
        await roles.InsertManyAsync([roleA, roleB]);
        var created = Enumerable.Range(0, count).Select(i => new User($"u{i}@count.test", "hash:x", $"First{i}", $"Last{i}", tenantId)).ToList();
        await users.InsertManyAsync(created);
        await userRoles.InsertManyAsync(created.Select((u, i) => new UserRole(u.Id, i % 2 == 0 ? roleA.Id : roleB.Id, tenantId, "count-fixture")));
        return tenantId;
    }

    private static GetAllUsersQueryHandler HandlerOn(IMongoDatabase database, Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        return new GetAllUsersQueryHandler(new UserListReader(database), tenant);
    }

    [Fact]
    public async Task The_roles_of_fifty_users_cost_one_query_on_userRoles_and_none_on_any_other_collection()
    {
        var tenantId = await TenantWithUsersAsync(50);
        var (database, log) = CountingDatabase();
        var reader = new UserListReader(database);
        var ids = (await _host.Database.GetCollection<User>("users").Find(u => u.TenantId == tenantId).ToListAsync()).Select(u => u.Id).ToList();
        Assert.Equal(50, ids.Count);

        var names = await reader.GetRoleNamesForUsersAsync(tenantId, ids, CancellationToken.None);

        Assert.Equal(50, names.Count);
        Assert.All(names.Values, roles => Assert.Single(roles));
        var command = Assert.Single(log); // ONE command for the whole page — roles are joined in the database
        Assert.Equal(new Command("aggregate", "userRoles"), command);
    }

    [Fact]
    public async Task A_list_call_costs_the_same_number_of_userRoles_queries_for_five_users_as_for_fifty()
    {
        var small = await TenantWithUsersAsync(5);
        var large = await TenantWithUsersAsync(50);
        var (database, log) = CountingDatabase();

        async Task<int> UserRoleQueriesAsync(Guid tenantId, int length, bool legacy)
        {
            while (log.TryDequeue(out _)) { }
            var query = legacy ? new GetAllUsersQuery(1, length) : new GetAllUsersQuery(List: new UserListRequest(0, length));
            var result = await HandlerOn(database, tenantId).Handle(query, CancellationToken.None);
            Assert.True(result.IsSuccessful);
            Assert.All(result.Data!.Items, u => Assert.Single(u.Roles)); // the roles really arrived
            return log.Count(c => c.Collection == "userRoles");
        }

        var listSmall = await UserRoleQueriesAsync(small, 5, legacy: false);
        var listLarge = await UserRoleQueriesAsync(large, 50, legacy: false);
        var legacySmall = await UserRoleQueriesAsync(small, 5, legacy: true);
        var legacyLarge = await UserRoleQueriesAsync(large, 50, legacy: true);

        Assert.Equal(listSmall, listLarge);
        Assert.Equal(legacySmall, legacyLarge);
        Assert.Equal(2, listLarge);    // the page's roles + the summary's role holders
        Assert.Equal(1, legacyLarge);  // the page's roles — the legacy call has no summary
    }

    [Fact]
    public async Task A_whole_list_call_for_fifty_users_stays_under_eight_commands()
    {
        var tenantId = await TenantWithUsersAsync(50);
        var (database, log) = CountingDatabase();

        var result = await HandlerOn(database, tenantId).Handle(new GetAllUsersQuery(List: new UserListRequest(0, 50, OrderBy: "email")), CancellationToken.None);

        Assert.Equal(50, result.Data!.Items.Count);
        Assert.InRange(log.Count, 1, 7); // count · page · roles · live roles · role holders · summary  (+ nothing per user)
    }

    [Fact]
    public async Task The_old_per_user_role_loop_cost_two_queries_per_user_measured_here_for_the_record()
    {
        // The pre-WP handler called IUserRoleRepository.GetRolesByUserAsync once per user (and each call scanned the whole
        // userRoles and roles collections). That method still exists for its other callers; measured with the same counter.
        var tenantId = await TenantWithUsersAsync(50);
        var (database, log) = CountingDatabase();
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var oldRepository = new UserRoleRepository(database, tenant);
        var ids = (await _host.Database.GetCollection<User>("users").Find(u => u.TenantId == tenantId).ToListAsync()).Select(u => u.Id).ToList();

        foreach (var id in ids)
        {
            await oldRepository.GetRolesByUserAsync(id, tenantId, CancellationToken.None);
        }

        Assert.Equal(50, log.Count(c => c.Collection == "userRoles"));
        Assert.Equal(50, log.Count(c => c.Collection == "roles"));
    }
}
