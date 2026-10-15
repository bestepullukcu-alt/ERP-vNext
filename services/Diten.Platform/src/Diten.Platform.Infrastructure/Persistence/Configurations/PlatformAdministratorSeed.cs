using Diten.Platform.Infrastructure.Persistence.Schema;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// The development platform administrator record (admin@diten.com, the same fixed id as Auth's development account).
/// <para>WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534, owner decision 2026-10-15): <b>Development only, and only "create when
/// missing".</b> Outside Development nothing is written. A record that exists — found by its fixed id OR by its address —
/// is never touched: a start-up no longer forces it back to PlatformAdmin + Active + [SuperAdmin] + not deleted, so an
/// administrator's suspension, demotion or deletion lasts. A record whose address was changed no longer makes the next
/// start fail on the fixed id. A new environment's first platform administrator is created by the
/// <c>Diten.AuthService.FirstPlatformAdministrator</c> command (<see cref="FirstPlatformAdministratorRecord"/>).</para>
/// </summary>
public static class PlatformAdministratorSeed
{
    public static readonly Guid DevelopmentAdministratorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const string DevelopmentAdministratorEmail = "admin@diten.com";

    /// <param name="isDevelopment">The host runs in the Development environment — THE GATE: false writes nothing.</param>
    public static async Task EnsureSeededAsync(IMongoDatabase database, bool isDevelopment, CancellationToken ct = default)
    {
        if (!isDevelopment)
        {
            return;
        }

        var collection = database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);
        var email = DevelopmentAdministratorEmail;
        var normalizedEmail = DevelopmentAdministratorEmail;
        var userName = "admin";

        var exists = await collection
            .Find(x => x.Id == DevelopmentAdministratorId
                       || x.Email == email || x.NormalizedEmail == normalizedEmail || x.NormalizedEmail == "ADMIN@DITEN.COM")
            .AnyAsync(ct);
        if (exists)
        {
            return;
        }

        var seed = new PlatformAdministrator
        {
            Id = DevelopmentAdministratorId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            NormalizedUserName = userName,
            DisplayName = "Diten Admin",
            ActorType = ActorType.PlatformAdmin,
            Status = AdministratorStatus.Active,
            Roles = new List<AdministratorRole> { AdministratorRole.SuperAdmin },
            InvitationStatus = AdministratorInvitationStatus.Accepted,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "system",
            Version = 1,
            IsDeleted = false
        };

        try
        {
            await collection.InsertOneAsync(seed, cancellationToken: ct);
            Console.WriteLine("Created platform administrator seed user.");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another start-up created it (or its address / user name is held) between the read and the write: left as it is.
            Console.WriteLine("Platform administrator seed skipped: the record exists.");
        }
    }
}
