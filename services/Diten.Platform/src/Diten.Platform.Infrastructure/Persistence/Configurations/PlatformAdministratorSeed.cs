using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class PlatformAdministratorSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<PlatformAdministrator>("platform_administrators");

        var staticAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var email = "admin@diten.com";
        var normalizedEmail = "admin@diten.com";
        var userName = "admin";

        var exists = await collection.Find(x => x.Email == email || x.NormalizedEmail == normalizedEmail || x.NormalizedEmail == "ADMIN@DITEN.COM").FirstOrDefaultAsync(ct);
        if (exists != null)
        {
            if (exists.Id != staticAdminId)
            {
                await collection.DeleteOneAsync(x => x.Id == exists.Id, cancellationToken: ct);
            }
            else
            {
                var update = Builders<PlatformAdministrator>.Update
                    .Set(x => x.Email, email)
                    .Set(x => x.NormalizedEmail, normalizedEmail)
                    .Set(x => x.UserName, userName)
                    .Set(x => x.NormalizedUserName, userName)
                    .Set(x => x.Status, AdministratorStatus.Active)
                    .Set(x => x.IsDeleted, false)
                    .Set(x => x.Roles, new List<AdministratorRole> { AdministratorRole.SuperAdmin })
                    .Set(x => x.InvitationStatus, AdministratorInvitationStatus.Accepted)
                    .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
                    .Set(x => x.UpdatedBy, "system");

                await collection.UpdateOneAsync(x => x.Id == staticAdminId, update, cancellationToken: ct);
                return;
            }
        }

        var seed = new PlatformAdministrator
        {
            Id = staticAdminId,
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

        await collection.InsertOneAsync(seed, cancellationToken: ct);
        Console.WriteLine("Created platform administrator seed user.");
    }
}
