using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — what the Auth start-up seeds, per environment, measured on the REAL host
/// (Program → AddPersistence → DataSeeder + AdminAccountSeed) over a test-owned mongod whose database starts empty. The state
/// is read right after the production start-up, before the test fixture adds anything (BeforeSeedHookForTesting).
/// </summary>
public static class AdminAccountSeedStartUp
{
    public static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public sealed record Snapshot(IReadOnlyList<User> Users, long Permissions, IReadOnlyList<string> PlatformTenantRoles, IReadOnlyList<string> AdminRoles);

    public static async Task<Snapshot> ReadAsync(IMongoDatabase database)
    {
        var users = await database.GetCollection<User>("users").Find(FilterDefinition<User>.Empty).ToListAsync();
        var permissions = await database.GetCollection<Permission>("permissions").CountDocumentsAsync(FilterDefinition<Permission>.Empty);
        var roles = await database.GetCollection<Role>("roles").Find(r => r.TenantId == PlatformTenantId).ToListAsync();
        var admin = users.FirstOrDefault(u => u.Email == AdminAccountSeed.PlatformAdministratorEmail);
        var adminRoleIds = admin is null
            ? []
            : (await database.GetCollection<UserRole>("userRoles").Find(ur => ur.UserId == admin.Id).ToListAsync()).Select(ur => ur.RoleId).ToList();
        return new Snapshot(
            users,
            permissions,
            roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(),
            roles.Where(r => adminRoleIds.Contains(r.Id)).Select(r => r.Name).ToList());
    }

    public sealed class ProductionHost : AccountKindAcceptance.AuthTestHost
    {
        public ProductionHost() => BeforeSeedHookForTesting = async host => AtStart = await ReadAsync(host.Database);

        protected override string HostEnvironmentName => "Production";

        public Snapshot? AtStart { get; private set; }
    }

    public sealed class DevelopmentHost : AccountKindAcceptance.AuthTestHost
    {
        public DevelopmentHost() => BeforeSeedHookForTesting = async host => AtStart = await ReadAsync(host.Database);

        public Snapshot? AtStart { get; private set; }
    }
}

public sealed class AdminAccountSeedProductionStartTests : IClassFixture<AdminAccountSeedStartUp.ProductionHost>
{
    private readonly AdminAccountSeedStartUp.ProductionHost _host;

    public AdminAccountSeedProductionStartTests(AdminAccountSeedStartUp.ProductionHost host) => _host = host;

    [Fact]
    public void A_production_start_on_an_empty_database_seeds_permissions_and_roles_and_no_account()
    {
        var atStart = Assert.IsType<AdminAccountSeedStartUp.Snapshot>(_host.AtStart);

        Assert.Empty(atStart.Users);
        Assert.True(atStart.Permissions > 0, "the permission catalog was not seeded");
        Assert.Equal(["Admin", "SuperAdmin", "Viewer"], atStart.PlatformTenantRoles);
    }
}

public sealed class AdminAccountSeedDevelopmentStartTests : IClassFixture<AdminAccountSeedStartUp.DevelopmentHost>
{
    private readonly AdminAccountSeedStartUp.DevelopmentHost _host;

    public AdminAccountSeedDevelopmentStartTests(AdminAccountSeedStartUp.DevelopmentHost host) => _host = host;

    // Today's development behaviour: the platform administrator, active, platform account, SuperAdmin; the opt-in accounts
    // stay off without their switches.
    [Fact]
    public void A_development_start_on_an_empty_database_seeds_the_dev_platform_administrator()
    {
        var atStart = Assert.IsType<AdminAccountSeedStartUp.Snapshot>(_host.AtStart);

        var admin = Assert.Single(atStart.Users);
        Assert.Equal(AdminAccountSeed.PlatformAdministratorEmail, admin.Email);
        Assert.Equal(AdminAccountSeed.PlatformAdministratorId, admin.Id);
        Assert.True(admin.IsActive);
        Assert.Equal("platform_admin", admin.PlatformActorType);
        Assert.Equal(["SuperAdmin"], atStart.AdminRoles);
        Assert.True(atStart.Permissions > 0);
        Assert.Equal(["Admin", "SuperAdmin", "Viewer"], atStart.PlatformTenantRoles);
    }
}

/// <summary>The account step itself, on fixed-name databases of the shared test-owned mongod (the seed writes the platform
/// tenant: never the host's database), each emptied first.</summary>
[Collection("AccountKindAcceptance")]
public sealed class AdminAccountSeedRuleTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid PlatformTenantId = AdminAccountSeedStartUp.PlatformTenantId;
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public AdminAccountSeedRuleTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    private async Task<IMongoDatabase> EmptySeededDatabaseAsync(string name)
    {
        await _host.Database.Client.DropDatabaseAsync(name);
        var database = _host.Database.Client.GetDatabase(name);
        await DataSeeder.SeedAsync(database);
        return database;
    }

    // An account under the seeded address with ANOTHER id (an operator recreated it, or an older seed did): before, every
    // start deleted it and its roles and recreated the dev account. Now it is never touched, in any environment.
    [Theory]
    [InlineData(false, "admin_bootstrap_other_id_prod")]
    [InlineData(true, "admin_bootstrap_other_id_dev")]
    public async Task An_account_with_another_id_under_the_seeded_address_is_kept_and_reported(bool development, string databaseName)
    {
        var database = await EmptySeededDatabaseAsync(databaseName);
        var users = database.GetCollection<BsonDocument>("users");
        var userRoles = database.GetCollection<UserRole>("userRoles");
        var other = new User(AdminAccountSeed.PlatformAdministratorEmail, "not-a-real-hash", "Real", "Administrator", PlatformTenantId);
        other.SetUserName("real.admin");
        other.SetPlatformActorType("platform_admin");
        await database.GetCollection<User>("users").InsertOneAsync(other);
        var superAdmin = await database.GetCollection<Role>("roles").Find(r => r.Name == "SuperAdmin" && r.TenantId == PlatformTenantId).SingleAsync();
        await userRoles.InsertOneAsync(new UserRole(other.Id, superAdmin.Id, PlatformTenantId, "test"));
        var before = await users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        var logger = new WarningCapture();

        await AdminAccountSeed.RunAsync(database, new AdminAccountSeed.Settings(development), logger);

        Assert.Equal(before, await users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()); // kept, nothing added
        Assert.Single(await userRoles.Find(ur => ur.UserId == other.Id).ToListAsync());
        Assert.Contains(logger.Warnings, w => w.Contains(other.Id.ToString(), StringComparison.Ordinal));
    }

    // Outside Development the seeded administrator — passive, or marked by an administrator — stays exactly as it is; the
    // development seed would switch the unmarked one on (BL-529 FIX9, measured in AdminResetInvalidatesEndpointTests).
    [Theory]
    [InlineData(false, "admin_bootstrap_passive")]
    [InlineData(true, "admin_bootstrap_marked")]
    public async Task Outside_development_a_passive_seeded_administrator_stays_passive(bool markedByAdministrator, string databaseName)
    {
        var database = await EmptySeededDatabaseAsync(databaseName);
        await AdminAccountSeed.RunAsync(database, new AdminAccountSeed.Settings(Development: true)); // the account as dev made it
        var users = database.GetCollection<BsonDocument>("users");
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(AdminAccountSeed.PlatformAdministratorId, GuidRepresentation.Standard));
        await users.UpdateOneAsync(byId, Builders<BsonDocument>.Update
            .Set(nameof(User.IsActive), false)
            .Set(nameof(User.DeactivatedByAdministrator), markedByAdministrator)
            .Set(nameof(User.EmailConfirmed), false));
        var before = await users.Find(byId).SingleAsync();

        await AdminAccountSeed.RunAsync(database, new AdminAccountSeed.Settings(Development: false));

        Assert.Equal(before, await users.Find(byId).SingleAsync());
        Assert.False((await database.GetCollection<User>("users").Find(u => u.Id == AdminAccountSeed.PlatformAdministratorId).SingleAsync()).IsActive);
    }

    // The switches are Development's: outside it neither the mock users nor the dev tenant administrator appear.
    [Fact]
    public async Task Outside_development_the_dev_switches_create_no_account()
    {
        var database = await EmptySeededDatabaseAsync("admin_bootstrap_switches_prod");

        await AdminAccountSeed.RunAsync(database, new AdminAccountSeed.Settings(Development: false, MockUsers: true, TenantAdmin: true));

        Assert.Equal(0, await database.GetCollection<User>("users").CountDocumentsAsync(FilterDefinition<User>.Empty));
    }

    private sealed class WarningCapture : Microsoft.Extensions.Logging.ILogger
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Enqueue(formatter(state, exception));
            }
        }
    }
}
