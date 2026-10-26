using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-521 follow-up (owner decision 2026-10-13) — the opt-in dev tenant administrator, measured on the production
/// start-up seed steps (<see cref="DataSeeder"/>, then <see cref="AdminAccountSeed"/>; since WP-AUTH-ADMIN-BOOTSTRAP-01 the
/// account lives in <see cref="DevAccountSeed"/>) against the test-owned mongod. Since BL-521 admin@diten.com is a platform account that every
/// tenant door refuses; this account is the dev database's tenant-side administrator: tenant Admin role, never platform.
/// </summary>
[Trait("Category", "Mongo")]
public sealed class DevTenantAdminSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public DevTenantAdminSeedTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    private IMongoCollection<User> Users => _host.Database.GetCollection<User>("users");
    private IMongoCollection<Role> Roles => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<UserRole> UserRoles => _host.Database.GetCollection<UserRole>("userRoles");

    private async Task<List<User>> TenantAdminsAsync()
        => (await Users.Find(u => u.Email == DevAccountSeed.DevTenantAdminEmail).ToListAsync())
            .Where(u => u.TenantId == DefaultTenantId && !u.IsDeleted).ToList();

    private async Task RemoveTenantAdminAsync()
    {
        foreach (var user in await Users.Find(u => u.Email == DevAccountSeed.DevTenantAdminEmail).ToListAsync())
        {
            await UserRoles.DeleteManyAsync(ur => ur.UserId == user.Id);
            await Users.DeleteOneAsync(u => u.Id == user.Id);
        }
    }

    // The two start-up seed steps as AddPersistence runs them (system seed, then the account step).
    private async Task StartUpSeedAsync(bool development, bool tenantAdmin)
    {
        await DataSeeder.SeedAsync(_host.Database);
        await AdminAccountSeed.RunAsync(_host.Database, new AdminAccountSeed.Settings(development, TenantAdmin: tenantAdmin));
    }

    private async Task<List<string>> RoleNamesAsync(Guid userId)
    {
        var roleIds = (await UserRoles.Find(ur => ur.UserId == userId).ToListAsync()).Select(ur => ur.RoleId).ToList();
        return (await Roles.Find(r => roleIds.Contains(r.Id)).ToListAsync()).Select(r => r.Name).OrderBy(n => n).ToList();
    }

    [Fact]
    public async Task Without_the_switch_no_tenant_administrator_is_seeded()
    {
        await RemoveTenantAdminAsync();

        await StartUpSeedAsync(development: true, tenantAdmin: false);
        await StartUpSeedAsync(development: true, tenantAdmin: false);

        Assert.Empty(await TenantAdminsAsync());
    }

    // WP-AUTH-ADMIN-BOOTSTRAP-01 — the switch alone is not enough: outside Development no account step writes anything.
    [Fact]
    public async Task Outside_development_the_switch_seeds_no_tenant_administrator()
    {
        await RemoveTenantAdminAsync();

        await StartUpSeedAsync(development: false, tenantAdmin: true);

        Assert.Empty(await TenantAdminsAsync());
    }

    [Fact]
    public async Task With_the_switch_the_seed_tenant_gets_an_active_tenant_admin_that_is_not_a_platform_account()
    {
        await RemoveTenantAdminAsync();

        await StartUpSeedAsync(development: true, tenantAdmin: true);

        var admin = Assert.Single(await TenantAdminsAsync());
        Assert.True(admin.IsActive);
        Assert.False(admin.IsPlatformAccount);
        Assert.Equal(["Admin"], await RoleNamesAsync(admin.Id));
    }

    [Fact]
    public async Task A_second_start_adds_nothing_and_keeps_an_administrators_deactivation()
    {
        await RemoveTenantAdminAsync();
        await StartUpSeedAsync(development: true, tenantAdmin: true);
        var admin = Assert.Single(await TenantAdminsAsync());
        await Users.UpdateOneAsync(u => u.Id == admin.Id,
            Builders<User>.Update.Set(u => u.IsActive, false).Set(u => u.DeactivatedByAdministrator, true));

        await StartUpSeedAsync(development: true, tenantAdmin: true);

        var after = Assert.Single(await TenantAdminsAsync());
        Assert.False(after.IsActive);
        Assert.True(after.DeactivatedByAdministrator);
        Assert.Single(await UserRoles.Find(ur => ur.UserId == after.Id).ToListAsync());
    }
}
