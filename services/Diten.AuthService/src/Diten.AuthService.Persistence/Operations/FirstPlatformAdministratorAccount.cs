using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Operations;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — Auth's half of a new environment's FIRST platform administrator, written
/// by the <c>Diten.AuthService.FirstPlatformAdministrator</c> command (Platform's half: <c>FirstPlatformAdministratorRecord</c>,
/// same id).
/// <para>The account is an INVITATION, exactly as the tenant administrator's invitation makes one
/// (<c>InternalEventsController</c>, tenant-admin-invited): a password nobody knows, inactive, the forced change pending,
/// and only the HASH of a one-time set-password link stored. Redeeming the link (Auth's platform set-password door) sets
/// the password, activates the account and confirms the address. No password is made, shown or stored.</para>
/// <para>Only for an environment with no platform account: <see cref="FindBlockerAsync"/> names what is there, and
/// <see cref="TryCreateAsync"/> writes nothing when anything is. An existing account is never changed.</para>
/// </summary>
public static class FirstPlatformAdministratorAccount
{
    public const string Via = "first-platform-administrator-command";

    /// <summary>How long the set-password link lives: the 24 hours of every link an administrator issues on the platform.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    /// <param name="UnusablePasswordHash">The hash of a random value nobody keeps (the invitation's placeholder).</param>
    /// <param name="SetupTokenHash">The one-time link's hash — the link itself is never handed to this class.</param>
    public sealed record Request(
        Guid Id,
        string Email,
        string UserName,
        string FirstName,
        string LastName,
        string UnusablePasswordHash,
        string SetupTokenHash,
        DateTime SetupExpiresAtUtc);

    /// <summary>A database as Auth's service opens its own (Standard Guids) — for the operator command, which has no host. The
    /// command opens Platform's database with it too, after Platform's own BSON setup (FirstPlatformAdministratorRecord).</summary>
    public static IMongoDatabase OpenDatabase(string connectionString, string databaseName)
    {
        Serialization.GuidSerializerRegistration.EnsureStandard();
        var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618 // the same setting Auth's own client carries (Persistence/DependencyInjection.cs)
        settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
        return new MongoClient(settings).GetDatabase(databaseName);
    }

    /// <summary>What prevents the bootstrap, or null when the platform tenant has no platform account and nothing under
    /// this id, address or user name.</summary>
    public static async Task<string?> FindBlockerAsync(
        IMongoDatabase database, Guid id, string email, string userName, CancellationToken ct = default)
    {
        var users = database.GetCollection<User>("users");
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedUserName = userName.Trim().ToLowerInvariant();
        var tenantId = PlatformSystemTenant.Id;
        if (await users.Find(u => u.Id == id
                                  || (u.TenantId == tenantId && (u.Email == normalizedEmail || u.NormalizedUserName == normalizedUserName)))
                .AnyAsync(ct))
        {
            return "an account with this id, address or user name exists";
        }

        return await users.Find(u => u.TenantId == tenantId && u.PlatformActorType != null && u.PlatformActorType != "" && !u.IsDeleted)
            .AnyAsync(ct)
            ? "the environment already has a platform account"
            : null;
    }

    /// <returns>True when the account (and its audit row) was written; false when a blocker was found or the insert met
    /// an existing account (nothing was changed either way).</returns>
    public static async Task<bool> TryCreateAsync(IMongoDatabase database, Request request, CancellationToken ct = default)
    {
        if (await FindBlockerAsync(database, request.Id, request.Email, request.UserName, ct) is not null)
        {
            return false;
        }

        var tenantId = PlatformSystemTenant.Id;
        var user = new User(request.Email.Trim().ToLowerInvariant(), request.UnusablePasswordHash, request.FirstName, request.LastName, tenantId)
        {
            Id = request.Id
        };
        user.SetUserName(request.UserName);
        user.SetPlatformActorType("platform_admin");
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — no automatic path classifies; classification is an explicit act.
        user.SetAccountKind(AccountKind.Unknown);
        // The tenant administrator's invitation, field for field (InternalEventsController.IssueNewAccountInvitation).
        user.SetPasswordResetToken(request.SetupTokenHash, request.SetupExpiresAtUtc);
        user.Deactivate();
        user.RequirePasswordChange(null);

        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        UserRepository accounts = new(database, tenant);
        try
        {
            await accounts.CreateAsync(user, ct);
        }
        catch (DuplicateUserEmailException)
        {
            return false;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }

        // Ids and facts only: no address, no link.
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            userId = user.Id,
            accountKind = user.AccountKind.ToString(),
            via = Via
        });
        var audit = database.GetCollection<AuthAuditLog>("authAuditLogs");
        await audit.InsertOneAsync(new AuthAuditLog(UserAuditEvents.Invited, user.Id, tenantId, metadata), cancellationToken: ct);
        return true;
    }
}
