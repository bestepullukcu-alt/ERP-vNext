using Diten.Platform.Infrastructure.Persistence.Schema;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class PlatformAdministratorSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<PlatformAdministrator>(PlatformCollections.PlatformAdministrators);
        var rawCollection = database.GetCollection<BsonDocument>(PlatformCollections.PlatformAdministrators);

        var staticAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var email = "admin@diten.com";
        var normalizedEmail = "admin@diten.com";
        var userName = "admin";

        var existingAdministrators = await collection
            .Find(x => x.Email == email || x.NormalizedEmail == normalizedEmail || x.NormalizedEmail == "ADMIN@DITEN.COM")
            .ToListAsync(ct);

        var exists = existingAdministrators.FirstOrDefault(x => x.Id == staticAdminId) ?? existingAdministrators.FirstOrDefault();
        if (exists != null)
        {
            await NormalizeEnumStorageAsync(rawCollection, exists.Id, ct);
            return;
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
        await NormalizeEnumStorageAsync(rawCollection, staticAdminId, ct);
        Console.WriteLine("Created platform administrator seed user.");
    }

    private static async Task NormalizeEnumStorageAsync(IMongoCollection<BsonDocument> collection, Guid id, CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard));
        var document = await collection.Find(filter).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return;
        }

        var update = Builders<BsonDocument>.Update
            .Set("ActorType", (int)ActorType.PlatformAdmin)
            .Set("Status", (int)AdministratorStatus.Active)
            .Set("Roles", new BsonArray { (int)AdministratorRole.SuperAdmin })
            .Set("InvitationStatus", (int)AdministratorInvitationStatus.Accepted)
            .Set("IsDeleted", false);

        if (HasNumericValue(document, "ActorType", (int)ActorType.PlatformAdmin) &&
            HasNumericValue(document, "Status", (int)AdministratorStatus.Active) &&
            HasNumericArray(document, "Roles", (int)AdministratorRole.SuperAdmin) &&
            HasNumericValue(document, "InvitationStatus", (int)AdministratorInvitationStatus.Accepted) &&
            document.TryGetValue("IsDeleted", out var isDeleted) &&
            isDeleted.IsBoolean &&
            !isDeleted.AsBoolean)
        {
            return;
        }

        await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private static bool HasNumericValue(BsonDocument document, string fieldName, int expectedValue)
    {
        if (!document.TryGetValue(fieldName, out var value))
        {
            return false;
        }

        return value.IsInt32
            ? value.AsInt32 == expectedValue
            : value.IsInt64 && value.AsInt64 == expectedValue;
    }

    private static bool HasNumericArray(BsonDocument document, string fieldName, int expectedValue)
    {
        if (!document.TryGetValue(fieldName, out var value) || !value.IsBsonArray)
        {
            return false;
        }

        var array = value.AsBsonArray;
        return array.Count == 1 &&
               (array[0].IsInt32
                   ? array[0].AsInt32 == expectedValue
                   : array[0].IsInt64 && array[0].AsInt64 == expectedValue);
    }
}
