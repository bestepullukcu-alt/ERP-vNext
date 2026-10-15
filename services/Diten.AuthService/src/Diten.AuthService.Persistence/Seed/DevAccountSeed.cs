using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — THE DEVELOPMENT-ONLY ACCOUNTS, and the only place in the service that
/// holds the dev password hash: the seeded platform administrator (<see cref="AdminAccountSeed.PlatformAdministratorEmail"/>),
/// the opt-in dev tenant administrator (<see cref="DevTenantAdminEmail"/>) and the opt-in mock users. Reached only through
/// <see cref="AdminAccountSeed.RunAsync"/>, and only when its settings say Development; nothing here is public but the
/// names of the switches and the dev tenant administrator's address.
/// <para>The dev password itself is never written in the repository (the local setup note has it).</para>
/// <para>Behaviour as before the split (BL-529 FIX8/FIX9, BL-521 follow-up), with one change: an account found under the
/// administrator's address with ANOTHER id is no longer deleted and recreated — it is left as it is, with a warning, in
/// every environment.</para>
/// </summary>
public static class DevAccountSeed
{
    /// <summary>
    /// Opt-in switch for the five mock users per dev tenant (john.doe, jane.smith, …). Absent means false: the
    /// mock users used to come back on EVERY Auth start whenever a tenant held only its admin — measured 2026-09-24,
    /// in the middle of the owner's control round, right after the test users had been deleted on purpose. Read in
    /// <c>DependencyInjection.AddPersistence</c>; it has an effect only in Development (<see cref="AdminAccountSeed"/>);
    /// set it only in a local, never-committed appsettings.Development.json.
    /// </summary>
    public const string MockUsersOptInConfigurationKey = "DevSeeds:MockUsers";

    /// <summary>
    /// Opt-in switch for the dev tenant administrator (<see cref="DevTenantAdminEmail"/>) — BL-521 follow-up, owner decision
    /// 2026-10-13. Since BL-521 the seeded admin@diten.com is a platform account and every tenant door refuses it, so a dev
    /// database had no account that could open Users / Roles. Same gate as the mock users.
    /// </summary>
    public const string TenantAdminOptInConfigurationKey = "DevSeeds:TenantAdmin";

    /// <summary>
    /// Opt-in switch for the dev platform administrator (<see cref="AdminAccountSeed.PlatformAdministratorEmail"/>) —
    /// WP-AUTH-ADMIN-BOOTSTRAP-01 v1.1, owner decision 2026-10-15. Absent means false: even in Development no administrator
    /// is created. Same gate as the mock users; Platform's <c>PlatformAdministratorSeed</c> reads the same key. Set it only in
    /// a local, never-committed appsettings.Development.json.
    /// </summary>
    public const string PlatformAdminOptInConfigurationKey = "DevSeeds:PlatformAdmin";

    /// <summary>The dev tenant administrator: tenant Admin role in the seed tenant, never a platform account.</summary>
    public const string DevTenantAdminEmail = "kiraci.admin@diten.com";

    private static readonly Guid DevTenantAdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Tenant97c5Id = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string SystemUser = "system";

    // The dev password's hash — the ONE copy in the service (guarded: AdminBootstrapGuardTests). Internal: AdminAccountSeed
    // compares stored hashes with it outside Development (read only, AUTH_SEED_ADMIN_DEFAULT_PASSWORD_ACTIVE).
    internal const string DevPasswordHash = "$2a$12$F1MdLL6aHDzrwl/790xPVuiSNW8xJM5/.5E8A/6M3DwAYYenixKwC";

    internal static async Task SeedAsync(
        IMongoDatabase database, bool seedPlatformAdmin, bool seedMockUsers, bool seedTenantAdmin, ILogger logger)
    {
        Console.WriteLine("Seeding the development accounts...");
        // The dev platform administrator — OPT-IN only (PlatformAdminOptInConfigurationKey, v1.1). Default: none, even here.
        if (seedPlatformAdmin)
        {
            await SeedPlatformAdministratorAsync(database, logger);
        }

        // Five mock users per dev tenant — OPT-IN only (MockUsersOptInConfigurationKey). Default: nothing is seeded,
        // so a tenant that was emptied on purpose stays empty across restarts.
        if (seedMockUsers)
        {
            var userCol = database.GetCollection<User>("users");
            await SeedMockUsersForTenantAsync(userCol, DefaultTenantId);
            await SeedMockUsersForTenantAsync(userCol, Tenant97c5Id);
        }

        if (seedTenantAdmin)
        {
            Console.WriteLine("Seeding the dev tenant administrator...");
            await SeedDevTenantAdminAsync(database, logger);
        }
    }

    private static Application.Common.TenantContext SeedTenant()
    {
        var tenant = new Application.Common.TenantContext();
        tenant.SetTenant(DefaultTenantId);
        return tenant;
    }

    private static async Task SeedPlatformAdministratorAsync(IMongoDatabase database, ILogger logger)
    {
        var userCol = database.GetCollection<User>("users");
        var roleCol = database.GetCollection<Role>("roles");
        var urCol = database.GetCollection<UserRole>("userRoles");

        var email = AdminAccountSeed.PlatformAdministratorEmail;
        var staticAdminId = AdminAccountSeed.PlatformAdministratorId;
        var users = await userCol.Find(u => u.Email == email).ToListAsync();
        var user = users.FirstOrDefault(u => u.TenantId == DefaultTenantId);

        // WP-AUTH-ADMIN-BOOTSTRAP-01 — an account under this address with another id is never deleted (it used to be,
        // with its roles, and recreated with the dev hash): it is left exactly as it is, and the seed says so.
        if (user != null && user.Id != staticAdminId)
        {
            logger.LogWarning(AdminAccountSeed.OtherIdWarning, user.Id);
            return;
        }

        if (user == null)
        {
            user = new User(email, DevPasswordHash, "Diten", "Admin", DefaultTenantId)
            {
                Id = staticAdminId
            };
            user.SetUserName("admin");
            user.SetPlatformActorType("platform_admin");
            user.Activate();
            user.ConfirmEmail();
            // WP-INFRA-AUTH-ACCOUNT-KIND-01 — the seed is an automatic creation path: Unknown, never Human by structure.
            user.SetAccountKind(AccountKind.Unknown);
            try
            {
                await userCol.InsertOneAsync(user);
                Console.WriteLine("Created admin user with static Guid.");
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // The fixed id is taken by a document this seed did not find (its address was changed): left as it is.
                logger.LogWarning("The seeded admin's id is held by an account under another address; it was left as it is.");
                return;
            }
        }
        else
        {
            // BL-529 FIX8 item 2 — Development only since WP-AUTH-ADMIN-BOOTSTRAP-01. An administrator's deactivation of the
            // seeded admin is not undone by a restart ("active AND marked" is never written), and only the fields the seed
            // changes are written, only while the mark is the one read — never the whole document from this copy.
            var userRepository = new Repositories.UserRepository(database, SeedTenant());
            var state = userRepository.CaptureState(user);
            if (string.IsNullOrWhiteSpace(user.NormalizedUserName))
            {
                user.SetUserName("admin");
            }

            user.SetPlatformActorType("platform_admin");
            if (!user.DeactivatedByAdministrator)
            {
                user.Activate();
            }

            user.ConfirmEmail();
            if (!await userRepository.TryWriteChangesAsync(user, state, DefaultTenantId,
                    new UserWriteCondition(DeactivatedByAdministrator: user.DeactivatedByAdministrator), CancellationToken.None))
            {
                // FIX9 (M5) — the admin changed between the read and the write (an administrator marked or deleted it): the seed
                // leaves it as it is, and says so.
                logger.LogWarning("The seeded admin changed while the seed was updating it; it was left as it is.");
            }
        }

        var roles = await roleCol.Find(r => r.Name == "SuperAdmin").ToListAsync();
        var superAdminRole = roles.FirstOrDefault(r => r.TenantId == DefaultTenantId);

        if (superAdminRole != null)
        {
            var urPairs = await urCol.Find(ur => ur.UserId == user.Id).ToListAsync();
            var exists = urPairs.Any(ur => ur.RoleId == superAdminRole.Id);

            if (!exists)
            {
                await urCol.InsertOneAsync(new UserRole(user.Id, superAdminRole.Id, DefaultTenantId, SystemUser));
                Console.WriteLine("Assigned SuperAdmin role to admin user.");
            }
        }
    }

    // BL-521 follow-up — the dev tenant administrator. Created once (static id, the dev password hash), never marked
    // platform, and given the seed tenant's Admin role (not SuperAdmin). An existing account is never rewritten: an
    // administrator's deactivation or edits survive a restart; only a missing Admin link is added back.
    private static async Task SeedDevTenantAdminAsync(IMongoDatabase database, ILogger logger)
    {
        var userCol = database.GetCollection<User>("users");
        var roleCol = database.GetCollection<Role>("roles");
        var urCol = database.GetCollection<UserRole>("userRoles");

        var user = (await userCol.Find(u => u.Email == DevTenantAdminEmail).ToListAsync())
            .FirstOrDefault(u => u.TenantId == DefaultTenantId && !u.IsDeleted);
        if (user == null)
        {
            user = new User(DevTenantAdminEmail, DevPasswordHash, "Kiracı", "Admin", DefaultTenantId)
            {
                Id = DevTenantAdminId
            };
            user.SetUserName("kiraci.admin");
            user.Activate();
            user.ConfirmEmail();
            user.SetAccountKind(AccountKind.Unknown); // WP-INFRA-AUTH-ACCOUNT-KIND-01 — seed never classifies
            try
            {
                await userCol.InsertOneAsync(user);
                Console.WriteLine("Created the dev tenant administrator.");
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                logger.LogWarning("The dev tenant administrator was created by another start-up at the same moment; it was left as it is.");
                return;
            }
        }

        var adminRole = (await roleCol.Find(r => r.Name == "Admin").ToListAsync())
            .FirstOrDefault(r => r.TenantId == DefaultTenantId && !r.IsDeleted);
        if (adminRole == null)
        {
            logger.LogWarning("The seed tenant has no Admin role; the dev tenant administrator got no role.");
            return;
        }

        var linked = await urCol.Find(ur => ur.UserId == user.Id && ur.RoleId == adminRole.Id).AnyAsync();
        if (!linked)
        {
            await urCol.InsertOneAsync(new UserRole(user.Id, adminRole.Id, DefaultTenantId, SystemUser));
            Console.WriteLine("Assigned the Admin role to the dev tenant administrator.");
        }
    }

    private static async Task SeedMockUsersForTenantAsync(IMongoCollection<User> userCol, Guid tenantId)
    {
        var count = await userCol.CountDocumentsAsync(u => u.TenantId == tenantId && !u.IsDeleted);
        // If there's only 1 (the admin) or 0 users, let's seed 5 more.
        if (count <= 1)
        {
            var suffix = tenantId == DefaultTenantId ? "def" : "t97";
            var mockUsers = new List<User>
            {
                new User($"john.doe.{suffix}@diten.com", DevPasswordHash, "John", "Doe", tenantId),
                new User($"jane.smith.{suffix}@diten.com", DevPasswordHash, "Jane", "Smith", tenantId),
                new User($"bob.johnson.{suffix}@diten.com", DevPasswordHash, "Bob", "Johnson", tenantId),
                new User($"alice.williams.{suffix}@diten.com", DevPasswordHash, "Alice", "Williams", tenantId),
                new User($"charlie.brown.{suffix}@diten.com", DevPasswordHash, "Charlie", "Brown", tenantId)
            };

            for (int i = 0; i < mockUsers.Count; i++)
            {
                var mu = mockUsers[i];
                mu.SetUserName($"user-{suffix}-{i + 1}");
                mu.Activate();
                mu.ConfirmEmail();
                mu.SetAccountKind(AccountKind.Unknown); // WP-INFRA-AUTH-ACCOUNT-KIND-01 — seed never classifies
                await userCol.InsertOneAsync(mu);
            }
            Console.WriteLine($"Seeded 5 mock users for tenant {tenantId}.");
        }
    }
}
