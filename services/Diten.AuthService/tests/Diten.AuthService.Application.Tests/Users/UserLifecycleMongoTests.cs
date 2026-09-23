using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-INVITED-LIFECYCLE-01 — E4 evidence: the REAL Api (WebApplicationFactory) over a test-owned mongod
/// (EphemeralMongo, <see cref="AccountKindAcceptance"/>). Production startup built the indexes of this database; the
/// tests READ them back rather than trusting the configuration source. Every subject is a disposable account of the
/// fixture's disposable tenant; the shared 27017 server is never touched.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserLifecycleMongoTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private const string MigrationProbe = "users_email_index_migration_probe";

    private readonly AccountKindAcceptance.AuthTestHost _host;
    private readonly AccountKindAcceptance.Seed _seed;

    public UserLifecycleMongoTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
        _seed = host.Seeded;
    }

    // ── the index itself, as the server holds it ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Startup_built_the_users_email_index_unique_and_partial_on_live_users_and_the_legacy_one_is_gone()
    {
        var indexes = await (await _host.Database.GetCollection<BsonDocument>("users").Indexes.ListAsync()).ToListAsync();

        var email = Assert.Single(indexes, i => i["name"].AsString == MongoDbIndexConfigurations.UserEmailIndexName);
        Assert.True(email.GetValue("unique", false).ToBoolean());
        Assert.Equal(new BsonDocument { { "Email", 1 }, { "TenantId", 1 } }, email["key"].AsBsonDocument);
        Assert.Equal(new BsonDocument("IsDeleted", false), email["partialFilterExpression"].AsBsonDocument);
        Assert.DoesNotContain(indexes, i => i["name"].AsString == MongoDbIndexConfigurations.LegacyUserEmailIndexName);
        // Nothing else on users is unique over deleted accounts' e-mails.
        Assert.DoesNotContain(indexes, i => i["key"].AsBsonDocument.Contains("Email")
                                            && i.GetValue("unique", false).ToBoolean()
                                            && !i.Contains("partialFilterExpression"));
    }

    [Fact]
    public async Task The_startup_step_replaces_a_legacy_index_and_is_idempotent()
    {
        var database = _host.Database;
        await database.DropCollectionAsync(MigrationProbe);
        var raw = database.GetCollection<BsonDocument>(MigrationProbe);
        var tenant = Guid.NewGuid();

        // The world before: the driver-default legacy index, deleted users included.
        await raw.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument { { "Email", 1 }, { "TenantId", 1 } },
            new CreateIndexOptions { Unique = true, Name = MongoDbIndexConfigurations.LegacyUserEmailIndexName }));
        var deleted = new User("gone@acme.test", "hash:x", "G", "One", tenant);
        deleted.IsDeleted = true;
        await database.GetCollection<User>(MigrationProbe).InsertOneAsync(deleted);
        var burned = await Record.ExceptionAsync(() =>
            database.GetCollection<User>(MigrationProbe).InsertOneAsync(new User("gone@acme.test", "hash:x", "N", "Ew", tenant)));
        Assert.Equal(ServerErrorCategory.DuplicateKey, Assert.IsType<MongoWriteException>(burned).WriteError.Category); // the defect, measured

        // Run the production step twice: the second run must be a no-op, not an IndexOptionsConflict.
        await MongoDbIndexConfigurations.EnsureUserEmailIndexAsync(database.GetCollection<User>(MigrationProbe));
        await MongoDbIndexConfigurations.EnsureUserEmailIndexAsync(database.GetCollection<User>(MigrationProbe));

        var indexes = await (await raw.Indexes.ListAsync()).ToListAsync();
        Assert.DoesNotContain(indexes, i => i["name"].AsString == MongoDbIndexConfigurations.LegacyUserEmailIndexName);
        var email = Assert.Single(indexes, i => i["name"].AsString == MongoDbIndexConfigurations.UserEmailIndexName);
        Assert.Equal(new BsonDocument("IsDeleted", false), email["partialFilterExpression"].AsBsonDocument);

        // The world after: the deleted address opens a new live account; a second live one is still refused.
        await database.GetCollection<User>(MigrationProbe).InsertOneAsync(new User("gone@acme.test", "hash:x", "N", "Ew", tenant));
        var second = await Record.ExceptionAsync(() =>
            database.GetCollection<User>(MigrationProbe).InsertOneAsync(new User("gone@acme.test", "hash:x", "T", "Wo", tenant)));
        Assert.Equal(ServerErrorCategory.DuplicateKey, Assert.IsType<MongoWriteException>(second).WriteError.Category);
        // Tenant isolation is still part of the key: the same address in another tenant is a different account.
        await database.GetCollection<User>(MigrationProbe).InsertOneAsync(new User("gone@acme.test", "hash:x", "O", "Ther", Guid.NewGuid()));

        await database.DropCollectionAsync(MigrationProbe);
    }

    [Fact]
    public async Task The_repository_translates_the_real_E11000_into_the_typed_refusal()
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_seed.TenantId);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var address = $"race.{Guid.NewGuid():N}@acceptance.invalid";

        await users.CreateAsync(new User(address, "hash:x", "R", "One", _seed.TenantId), CancellationToken.None);
        var lost = await Record.ExceptionAsync(() =>
            users.CreateAsync(new User(address, "hash:x", "R", "Two", _seed.TenantId), CancellationToken.None));

        var typed = Assert.IsType<DuplicateUserEmailException>(lost);
        Assert.IsType<MongoWriteException>(typed.InnerException); // the server's refusal, not a simulation
    }

    // ── over HTTP: the owner's three findings ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_deleted_users_email_opens_a_NEW_account_the_old_record_stays_and_its_roles_do_not_follow()
    {
        using var admin = _host.Client(await TokenWithAsync("auth.users.create", "auth.users.delete", "auth.users.read"), _seed.TenantId);
        var address = $"reuse.{Guid.NewGuid():N}@acceptance.invalid";

        var oldId = await CreateAsync(admin, address);
        await GrantSomeRoleAsync(oldId);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"api/users/{oldId}")).StatusCode);

        var again = await admin.PostAsJsonAsync("api/users", new { email = address, firstName = "Re", lastName = "Used" });
        var body = await again.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, again.StatusCode); // before: 500 "An unexpected error occurred."
        using var doc = JsonDocument.Parse(body);
        var newId = doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        Assert.NotEqual(oldId, newId); // a NEW account — no resurrection
        Assert.Empty(doc.RootElement.GetProperty("data").GetProperty("roles").EnumerateArray());

        var stored = await _host.Database.GetCollection<User>("users").Find(u => u.Email == address && u.TenantId == _seed.TenantId).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.True(stored.Single(u => u.Id == oldId).IsDeleted); // the old record and its history stay
        Assert.False(stored.Single(u => u.Id == newId).IsDeleted);

        var roleRows = _host.Database.GetCollection<UserRole>("userRoles");
        Assert.Equal(1, await roleRows.CountDocumentsAsync(r => r.UserId == oldId));  // still the old id's
        Assert.Equal(0, await roleRows.CountDocumentsAsync(r => r.UserId == newId));  // nothing carried over
        using var read = JsonDocument.Parse(await (await admin.GetAsync($"api/users/{newId}")).Content.ReadAsStringAsync());
        Assert.Empty(read.RootElement.GetProperty("data").GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task A_live_email_is_still_refused_with_409_USER_EMAIL_TAKEN_not_an_unexpected_error()
    {
        using var admin = _host.Client(await TokenWithAsync("auth.users.create"), _seed.TenantId);
        var address = $"twice.{Guid.NewGuid():N}@acceptance.invalid";
        await CreateAsync(admin, address);

        var second = await admin.PostAsJsonAsync("api/users", new { email = address, firstName = "Tw", lastName = "Ice" });
        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.DoesNotContain("unexpected", body, StringComparison.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(UserLifecycle.EmailTakenCode, doc.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_new_invitation_lists_as_Invited_and_an_administrator_cannot_activate_it_by_either_door()
    {
        using var admin = _host.Client(await TokenWithAsync("auth.users.create", "auth.users.update", "auth.users.read"), _seed.TenantId);
        var id = await CreateAsync(admin, $"invite.{Guid.NewGuid():N}@acceptance.invalid");

        Assert.Equal(UserLifecycle.StatusInvited, await StatusFromListAsync(admin, id));

        var enable = await admin.PostAsync($"api/users/{id}/enable", content: null);
        var edit = await admin.PutAsJsonAsync($"api/users/{id}", new { firstName = "In", lastName = "Vited", isActive = true });

        foreach (var refused in new[] { enable, edit })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            using var doc = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
            Assert.Equal(UserLifecycle.InvitationPendingCode, doc.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
        }

        var stored = await _host.Database.GetCollection<User>("users").Find(u => u.Id == id).SingleAsync();
        Assert.False(stored.IsActive);
        Assert.Equal(UserLifecycle.StatusInvited, await StatusFromListAsync(admin, id));
    }

    [Fact]
    public async Task An_administrator_switching_off_a_normal_account_lists_it_as_Inactive_not_Invited()
    {
        using var admin = _host.Client(await TokenWithAsync("auth.users.update", "auth.users.read"), _seed.TenantId);
        var id = _seed.Pmo.Id; // a seeded, confirmed, active actor

        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"api/users/{id}/disable", content: null)).StatusCode);
            Assert.Equal(UserLifecycle.StatusInactive, await StatusFromListAsync(admin, id));
        }
        finally
        {
            await admin.PostAsync($"api/users/{id}/enable", content: null); // the shared seed stays as it was
        }

        Assert.Equal(UserLifecycle.StatusActive, await StatusFromListAsync(admin, id));
    }

    // ── wiring ──

    private static async Task<Guid> CreateAsync(HttpClient client, string address)
    {
        var response = await client.PostAsJsonAsync("api/users", new { email = address, firstName = "Sub", lastName = "Ject" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<string> StatusFromListAsync(HttpClient client, Guid id)
    {
        var body = await (await client.GetAsync("api/users?page=1&pageSize=500")).Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        // GET api/users answers the PaginatedResult itself ({ items, totalCount, … }), not the Response envelope.
        var root = doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object ? data : doc.RootElement;
        var items = root.GetProperty("items");
        return items.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == id).GetProperty("status").GetString()!;
    }

    private async Task GrantSomeRoleAsync(Guid userId)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_seed.TenantId);
        var role = (await scope.ServiceProvider.GetRequiredService<IRoleRepository>().GetAllByTenantAsync(_seed.TenantId, CancellationToken.None)).First();
        await scope.ServiceProvider.GetRequiredService<IUserRoleRepository>()
            .AssignAsync(new UserRole(userId, role.Id, _seed.TenantId, AccountKindAcceptance.SeedActor), CancellationToken.None);
    }

    /// <summary>A real host-signed token for the seeded Creator actor carrying exactly <paramref name="permissionKeys"/>.</summary>
    private async Task<string> TokenWithAsync(params string[] permissionKeys)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_seed.TenantId);
        var actor = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(_seed.Creator.Id, _seed.TenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("seeded actor vanished");
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(actor, ["ad-hoc-admin"], permissionKeys, expiresInMinutes: 60);
    }
}
