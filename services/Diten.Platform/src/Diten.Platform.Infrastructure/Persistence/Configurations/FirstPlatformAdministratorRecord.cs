using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — Platform's half of a new environment's FIRST platform administrator,
/// written by the <c>Diten.AuthService.FirstPlatformAdministrator</c> command under the same id as the Auth account it
/// writes. Platform owns this record's shape, so the command writes it through this class and never by hand.
/// <para>Only for an environment with NO platform administrator: <see cref="FindBlockerAsync"/> names what is already
/// there, and <see cref="TryCreateAsync"/> writes nothing when anything is (insert only; an existing record is never
/// changed). The record is a pending invitation: Active, PlatformAdmin, [SuperAdmin] — the invitation is accepted at the
/// administrator's first sign-in, after the one-time set-password link Auth holds.</para>
/// </summary>
public static class FirstPlatformAdministratorRecord
{
    public const string CreatedBy = "first-platform-administrator-command";

    public sealed record Request(Guid Id, string Email, string UserName, string DisplayName);

    /// <summary>Platform's process-wide BSON setup, in the order AddInfrastructure runs it — for the operator command, which
    /// has no host: call it before the record is read or written (the client itself needs only Standard Guids).</summary>
    public static void PrepareSerialization()
    {
        PlatformBsonConventions.Register();
        PlatformBsonSerializers.EnsureRegistered();
    }

    /// <summary>What prevents the bootstrap, or null when the collection holds no live administrator and nothing under
    /// this id, address or user name.</summary>
    public static async Task<string?> FindBlockerAsync(IMongoDatabase database, Request request, CancellationToken ct = default)
    {
        var collection = Collection(database);
        var email = Normalize(request.Email);
        var userName = Normalize(request.UserName);
        if (await collection.Find(x => x.Id == request.Id || x.NormalizedEmail == email || x.NormalizedUserName == userName).AnyAsync(ct))
        {
            return "a platform administrator record with this id, address or user name exists";
        }

        return await collection.Find(x => !x.IsDeleted).AnyAsync(ct)
            ? "the environment already has a platform administrator"
            : null;
    }

    /// <returns>True when the record was written; false when <see cref="FindBlockerAsync"/> found a blocker or the insert
    /// met an existing record (nothing was changed either way).</returns>
    public static async Task<bool> TryCreateAsync(IMongoDatabase database, Request request, DateTimeOffset now, CancellationToken ct = default)
    {
        if (await FindBlockerAsync(database, request, ct) is not null)
        {
            return false;
        }

        var email = Normalize(request.Email);
        var record = new PlatformAdministrator
        {
            Id = request.Id,
            Email = email,
            NormalizedEmail = email,
            UserName = request.UserName.Trim(),
            NormalizedUserName = Normalize(request.UserName),
            DisplayName = request.DisplayName.Trim(),
            ActorType = ActorType.PlatformAdmin,
            Status = AdministratorStatus.Active,
            Roles = [AdministratorRole.SuperAdmin],
            InvitationStatus = AdministratorInvitationStatus.PendingInvitation,
            InvitedAtUtc = now,
            CreatedAt = now,
            CreatedBy = CreatedBy,
            Version = 1,
            IsDeleted = false
        };

        try
        {
            await Collection(database).InsertOneAsync(record, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    private static IMongoCollection<PlatformAdministrator> Collection(IMongoDatabase database) =>
        database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
