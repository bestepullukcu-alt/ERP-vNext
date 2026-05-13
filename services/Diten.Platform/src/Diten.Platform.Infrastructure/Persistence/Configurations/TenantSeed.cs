using System;
using System.Threading;
using System.Threading.Tasks;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class TenantSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<Tenant>("tenants");

        var platformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var exists = await collection.Find(x => x.Id == platformTenantId).FirstOrDefaultAsync(ct);
        if (exists != null)
        {
            return;
        }

        var seed = new Tenant
        {
            Id = platformTenantId,
            Code = "PLATFORM",
            Slug = "platform",
            Name = "Platform Admin Tenant",
            DisplayName = "Platform Admin Tenant",
            Domain = "platform.diten.tech",
            Status = TenantStatus.Active,
            TenantType = TenantType.Internal,
            SubscriptionStatus = TenantSubscriptionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "system",
            Version = 1,
            IsDeleted = false
        };

        await collection.InsertOneAsync(seed, cancellationToken: ct);
        Console.WriteLine("Created platform tenant seed.");
    }
}
