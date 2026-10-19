using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556, owner decision 2026-10-15) — THE ACCOUNT STEP of the Auth start-up seed,
/// separated from the system seed (<see cref="DataSeeder"/>: permissions, roles, templates — every environment).
/// <list type="bullet">
/// <item><b>Development</b> — the dev accounts (<see cref="DevAccountSeed"/>), each under its own opt-in switch: the platform
/// administrator (<see cref="DevAccountSeed.PlatformAdminOptInConfigurationKey"/>, v1.1), the dev tenant administrator and the
/// mock users. As before, except that an account found under the
/// administrator's address with another id is no longer deleted.</item>
/// <item><b>Every other environment</b> — no account is created, deleted, activated or changed. The step only reads: an
/// account under <see cref="PlatformAdministratorEmail"/> with another id is reported (warning), and so is the absence of
/// any platform administrator. The seeded administrator (fixed id, or the seeded address) still holding the dev password's
/// hash is reported once at Critical (<see cref="DefaultPasswordActiveCode"/>, its id only; v1.1). A new environment's first platform administrator is created by the
/// <c>Diten.AuthService.FirstPlatformAdministrator</c> command (one-time set-password link, no password).</item>
/// </list>
/// The step never stops the service: a failure is logged by its exception TYPE and the start-up goes on.
/// </summary>
public static class AdminAccountSeed
{
    /// <summary>The development platform administrator's address (never created outside Development).</summary>
    public const string PlatformAdministratorEmail = "admin@diten.com";

    /// <summary>The development platform administrator's fixed id — the same id Platform's development record carries.</summary>
    public static readonly Guid PlatformAdministratorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    internal const string OtherIdWarning =
        "An account under the seeded administrator's address has another id ({UserId}); it was left as it is (never deleted or recreated).";

    /// <summary>The code of the Critical line: the seeded administrator still holds the repository's dev password hash.</summary>
    public const string DefaultPasswordActiveCode = "AUTH_SEED_ADMIN_DEFAULT_PASSWORD_ACTIVE";

    internal const string DefaultPasswordActiveMessage =
        DefaultPasswordActiveCode + " UserId={UserId}: the seeded administrator still has the repository's dev password. Change it now.";

    internal const string NoAccountOutsideDevelopment =
        "No account is seeded outside Development. Create the first platform administrator with the FirstPlatformAdministrator command.";

    /// <param name="Development">The host runs in the Development environment.</param>
    /// <param name="PlatformAdmin">The <see cref="DevAccountSeed.PlatformAdminOptInConfigurationKey"/> switch (Development only).</param>
    /// <param name="MockUsers">The <see cref="DevAccountSeed.MockUsersOptInConfigurationKey"/> switch (Development only).</param>
    /// <param name="TenantAdmin">The <see cref="DevAccountSeed.TenantAdminOptInConfigurationKey"/> switch (Development only).</param>
    public sealed record Settings(bool Development, bool MockUsers = false, bool TenantAdmin = false, bool PlatformAdmin = false);

    public static async Task RunAsync(IMongoDatabase database, Settings settings, ILogger? logger = null)
    {
        logger ??= SeedConsoleLogger.Instance;
        try
        {
            // THE ENVIRONMENT GATE — outside Development nothing below is reached (no dev hash, no write of any kind).
            if (!settings.Development)
            {
                await ReportAsync(database, logger);
                return;
            }

            await DevAccountSeed.SeedAsync(database, settings.PlatformAdmin, settings.MockUsers, settings.TenantAdmin, logger);
        }
        catch (Exception ex)
        {
            // The TYPE only: a driver's or a serializer's message quotes values from the documents it was reading.
            logger.LogError("The account seed could not run: {ExceptionType}. Nothing else is affected.", ex.GetType().FullName);
        }
    }

    // Read only. Outside Development the seeded address is not special any more; it is named so that an environment that
    // still holds the old seed's account under another id is visible in the log, as BL-534 asked.
    private static async Task ReportAsync(IMongoDatabase database, ILogger logger)
    {
        var users = database.GetCollection<User>("users");
        var other = await users
            .Find(u => u.Email == PlatformAdministratorEmail && u.Id != PlatformAdministratorId)
            .Project(u => u.Id)
            .FirstOrDefaultAsync();
        if (other != Guid.Empty)
        {
            logger.LogWarning(OtherIdWarning, other);
        }

        // v1.1 — the seeded administrator (fixed id, or the seeded address) still holding the dev password: compared as the hash
        // STRING in the filter (no verification call; the stored hash is never read into this process). One Critical line.
        var defaultPassword = await users
            .Find(u => (u.Id == PlatformAdministratorId || u.Email == PlatformAdministratorEmail) && u.PasswordHash == DevAccountSeed.DevPasswordHash)
            .Project(u => u.Id)
            .ToListAsync();
        if (defaultPassword.Count > 0)
        {
            logger.LogCritical(DefaultPasswordActiveMessage, string.Join(",", defaultPassword));
        }

        var anyPlatformAccount = await users
            .Find(u => u.PlatformActorType != null && u.PlatformActorType != "" && !u.IsDeleted)
            .AnyAsync();
        if (!anyPlatformAccount)
        {
            logger.LogInformation(NoAccountOutsideDevelopment);
        }
    }
}
