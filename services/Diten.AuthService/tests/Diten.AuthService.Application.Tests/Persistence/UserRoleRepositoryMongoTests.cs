using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// BL-521 FIX5 item 4 (c) and (d) — THE USER-ROLE STORE UNDER THE PLATFORM-ROLE CONVERGENCE, measured on the PRODUCTION
/// repositories over a test-owned mongod (the shared acceptance host).
///
/// <para>(c) A grant is an idempotent upsert on user + role + tenant (two grants leave one row — even when the collection
/// has no unique index, and when eight run at once the index still lets exactly one in); a revoke removes EVERY matching row
/// (a duplicate left behind kept the role alive after "revoking" it).</para>
///
/// <para>(d) The reads are bounded to the tenant and the user they were asked about. They used to load the WHOLE
/// collection on every sign-in and filter in memory. That is measured without a stopwatch: rows of other tenants and users
/// that the production serializer cannot read (a string where a Guid belongs) are put in the collections — a read that
/// touches any row but its own fails on them; a bounded one never sees them.</para>
///
/// <para>The unique index of the collection is dropped only inside this class's own throw-away database, to construct the
/// duplicates a missing index lets in; production indexes are not touched.</para>
/// </summary>
public sealed class UserRoleRepositoryMongoTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public UserRoleRepositoryMongoTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    // ── (c) idempotent grant ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Granting_the_same_role_twice_leaves_one_row_and_no_error()
    {
        await EnsureUniqueIndexAsync();
        var (userId, roleId) = await SeedAsync();

        await AssignAsync(userId, roleId);
        await AssignAsync(userId, roleId);

        Assert.Equal(1, await RowCountAsync(userId, roleId));
    }

    [Fact]
    public async Task Grants_running_at_the_same_moment_leave_exactly_one_row()
    {
        await EnsureUniqueIndexAsync();
        var (userId, roleId) = await SeedAsync();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AssignAsync(userId, roleId)));

        Assert.Equal(1, await RowCountAsync(userId, roleId));
    }

    /// <summary>The upsert is idempotent by itself, not by the index: with no unique index two sequential grants still leave
    /// one row (an insert would leave two).</summary>
    [Fact]
    public async Task Granting_twice_leaves_one_row_even_where_the_collection_has_no_unique_index()
    {
        var (userId, roleId) = await SeedAsync();
        await DropUniqueIndexAsync();

        await AssignAsync(userId, roleId);
        await AssignAsync(userId, roleId);

        Assert.Equal(1, await RowCountAsync(userId, roleId));
    }

    // ── (c) revoke removes every matching row ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoking_a_role_removes_every_matching_row_and_only_those()
    {
        var (userId, roleId) = await SeedAsync();
        var (otherUserId, _) = await SeedAsync(roleId);
        await DropUniqueIndexAsync();
        var rows = _host.Database.GetCollection<UserRole>("userRoles");
        await rows.InsertManyAsync(
        [
            new UserRole(userId, roleId, PlatformTenantId, "bl521-fix5"),
            new UserRole(userId, roleId, PlatformTenantId, "bl521-fix5"),
            new UserRole(userId, roleId, PlatformTenantId, "bl521-fix5"),
            new UserRole(otherUserId, roleId, PlatformTenantId, "bl521-fix5"),
            new UserRole(userId, roleId, Guid.NewGuid(), "bl521-fix5") // the same pair in another tenant
        ]);

        await RevokeAsync(userId, roleId);

        Assert.Equal(0, await RowCountAsync(userId, roleId));
        Assert.Equal(1, await RowCountAsync(otherUserId, roleId));
        Assert.Equal(1, await rows.CountDocumentsAsync(Builders<UserRole>.Filter.And(
            Builders<UserRole>.Filter.Eq(r => r.UserId, userId), Builders<UserRole>.Filter.Ne(r => r.TenantId, PlatformTenantId))));
    }

    // ── (d) reads bounded to the tenant and the user ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_reads_never_touch_rows_of_other_tenants_or_users()
    {
        var (userId, roleId) = await SeedAsync();
        await AssignAsync(userId, roleId);
        // rows the production serializer cannot read: only an unbounded read would ever load them
        await _host.Database.GetCollection<BsonDocument>("userRoles").InsertOneAsync(new BsonDocument
        {
            ["_id"] = "garbage-user-role", ["UserId"] = "not-a-guid", ["RoleId"] = "not-a-guid", ["TenantId"] = "not-a-guid", ["IsDeleted"] = false
        });
        await _host.Database.GetCollection<BsonDocument>("roles").InsertOneAsync(new BsonDocument
        {
            ["_id"] = "garbage-role", ["Name"] = "Garbage", ["TenantId"] = "not-a-guid", ["IsDeleted"] = false
        });

        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var userRoles = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();

        var names = (await userRoles.GetRolesByUserAsync(userId, PlatformTenantId, CancellationToken.None)).ToList();
        var exists = await userRoles.ExistsAsync(userId, roleId, PlatformTenantId, CancellationToken.None);
        await userRoles.RevokeAsync(userId, roleId, PlatformTenantId, CancellationToken.None);

        Assert.Single(names);
        Assert.True(exists);
        Assert.Equal(0, await RowCountAsync(userId, roleId));
    }

    // ── the convergence over the real stores ────────────────────────────────────────────────────────────

    /// <summary>(a) A role stored in another letter case ("superadmin") is recognised and revoked; before, the revoke looked
    /// the role up by the canonical spelling, found nothing and the row stayed — the token kept the right.</summary>
    [Fact]
    public async Task A_role_stored_in_another_letter_case_is_revoked_by_the_convergence()
    {
        var userId = Guid.NewGuid();
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var lower = await roles.UpsertSystemRoleAsync("superadmin", "superadmin", "bl521-fix5 mixed case", PlatformTenantId, CancellationToken.None);
        await userRoles.AssignAsync(new UserRole(userId, lower.Id, PlatformTenantId, "system"), CancellationToken.None);

        var changed = await PlatformAdministratorRoleConvergence.ConvergeAtMintAsync(userRoles, roles, userId, PlatformTenantId, ["ReadOnly"], CancellationToken.None);

        Assert.True(changed);
        Assert.Equal(["ReadOnly"], (await userRoles.GetRolesByUserAsync(userId, PlatformTenantId, CancellationToken.None)).ToList());
    }

    /// <summary>(c) Duplicate assignment rows of a role being revoked are all removed by the convergence.</summary>
    [Fact]
    public async Task Duplicate_rows_of_a_role_the_record_no_longer_names_are_all_removed_by_the_convergence()
    {
        var userId = Guid.NewGuid();
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var role = await roles.UpsertSystemRoleAsync("BillingAdmin", "BillingAdmin", PlatformAdministratorRoleConvergence.RoleDescription, PlatformTenantId, CancellationToken.None);
        await DropUniqueIndexAsync();
        await _host.Database.GetCollection<UserRole>("userRoles").InsertManyAsync(
        [
            new UserRole(userId, role.Id, PlatformTenantId, "system"),
            new UserRole(userId, role.Id, PlatformTenantId, "system")
        ]);

        await PlatformAdministratorRoleConvergence.ConvergeAtMintAsync(userRoles, roles, userId, PlatformTenantId, ["SupportAdmin"], CancellationToken.None);

        Assert.Equal(["SupportAdmin"], (await userRoles.GetRolesByUserAsync(userId, PlatformTenantId, CancellationToken.None)).ToList());
        Assert.Equal(0, await RowCountAsync(userId, role.Id));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task<(Guid UserId, Guid RoleId)> SeedAsync(Guid? existingRoleId = null)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var roleId = existingRoleId
            ?? (await sp.GetRequiredService<IRoleRepository>().UpsertSystemRoleAsync($"fix5-{Guid.NewGuid():N}"[..20], "fix5 probe", "bl521-fix5", PlatformTenantId, CancellationToken.None)).Id;
        return (Guid.NewGuid(), roleId);
    }

    private async Task AssignAsync(Guid userId, Guid roleId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>()
            .AssignAsync(new UserRole(userId, roleId, PlatformTenantId, "bl521-fix5"), CancellationToken.None);
    }

    private async Task RevokeAsync(Guid userId, Guid roleId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>().RevokeAsync(userId, roleId, PlatformTenantId, CancellationToken.None);
    }

    private async Task<long> RowCountAsync(Guid userId, Guid roleId) =>
        await _host.Database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(Builders<UserRole>.Filter.And(
            Builders<UserRole>.Filter.Eq(r => r.UserId, userId),
            Builders<UserRole>.Filter.Eq(r => r.RoleId, roleId),
            Builders<UserRole>.Filter.Eq(r => r.TenantId, PlatformTenantId)));

    /// <summary>The production shape again (another test of this class may have dropped the index): the same keys and the same
    /// uniqueness MongoDbIndexConfigurations creates.</summary>
    private async Task EnsureUniqueIndexAsync() =>
        await _host.Database.GetCollection<UserRole>("userRoles").Indexes.CreateOneAsync(new CreateIndexModel<UserRole>(
            Builders<UserRole>.IndexKeys.Ascending("UserId").Ascending("RoleId").Ascending("TenantId"),
            new CreateIndexOptions { Unique = true }));

    /// <summary>Only in this test database: lets the duplicates a missing index would let in be constructed.</summary>
    private async Task DropUniqueIndexAsync()
    {
        var indexes = _host.Database.GetCollection<UserRole>("userRoles").Indexes;
        var names = new List<string>();
        using (var cursor = await indexes.ListAsync())
        {
            foreach (var index in await cursor.ToListAsync())
            {
                if (index.TryGetValue("unique", out var unique) && unique.ToBoolean())
                {
                    names.Add(index["name"].AsString);
                }
            }
        }

        foreach (var name in names)
        {
            await indexes.DropOneAsync(name);
        }
    }
}
