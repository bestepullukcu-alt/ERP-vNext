using Diten.Platform.Domain.Entities;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class SubscriptionPlanSeed
{
    public static async Task EnsureSeededAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await RepairQuotaDataTypesAsync(database, ct);

        // Minimal, non-destructive seed called at startup (same pattern as LegacySavedViewMigration).
        var collection = database.GetCollection<SubscriptionPlan>("platform_subscription_plans");

        var seeds = new[]
        {
            new SubscriptionPlan
            {
                Code = "FREE",
                Name = "Free",
                Description = "Time-limited free access plan",
                IsActive = true,
                IsDefault = true,
                SortOrder = 0,
                PriceMonthly = 0,
                PriceYearly = 0,
                Currency = "USD",
                IsTrialPlan = true,
                TrialDurationDays = 14
            },
            new SubscriptionPlan
            {
                Code = "STARTER",
                Name = "Starter",
                Description = "Starter plan",
                IsActive = true,
                IsDefault = false,
                SortOrder = 10,
                PriceMonthly = 49,
                PriceYearly = 499,
                Currency = "USD",
                IsTrialPlan = false
            },
            new SubscriptionPlan
            {
                Code = "PROFESSIONAL",
                Name = "Professional",
                Description = "Professional plan",
                IsActive = true,
                IsDefault = false,
                SortOrder = 20,
                PriceMonthly = 99,
                PriceYearly = 999,
                Currency = "USD",
                IsTrialPlan = false
            },
            new SubscriptionPlan
            {
                Code = "ENTERPRISE",
                Name = "Enterprise",
                Description = "Enterprise plan (custom pricing)",
                IsActive = true,
                IsDefault = false,
                SortOrder = 30,
                PriceMonthly = null,
                PriceYearly = null,
                Currency = null,
                IsTrialPlan = false
            }
        };

        foreach (var seed in seeds)
        {
            if (seed.IsDefault && seed.IsActive)
            {
                var defaultExists = await collection.Find(x => x.IsDeleted == false && x.IsDefault == true && x.IsActive == true).AnyAsync(ct);
                if (defaultExists)
                {
                    // Respect "block conflicts" convention; seed should never break startup.
                    seed.IsDefault = false;
                }
            }

            var exists = await collection.Find(x => x.IsDeleted == false && x.Code == seed.Code).AnyAsync(ct);
            if (exists)
            {
                continue;
            }

            await collection.InsertOneAsync(seed, cancellationToken: ct);
        }
    }

    public static async Task RepairQuotaDataTypesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<BsonDocument>("quota_usages");

        // Find documents where CurrentValue or LimitValue is a string
        var filter = Builders<BsonDocument>.Filter.Or(
            new BsonDocument("CurrentValue", new BsonDocument("$type", "string")),
            new BsonDocument("LimitValue", new BsonDocument("$type", "string")),
            new BsonDocument("currentValue", new BsonDocument("$type", "string")),
            new BsonDocument("limitValue", new BsonDocument("$type", "string"))
        );

        var cursor = await collection.FindAsync(filter, cancellationToken: ct);
        var documents = await cursor.ToListAsync(ct);

        foreach (var doc in documents)
        {
            var id = doc["_id"];
            UpdateDefinition<BsonDocument>? update = null;

            foreach (var field in new[] { "CurrentValue", "LimitValue", "currentValue", "limitValue" })
            {
                if (doc.Contains(field) && doc[field].IsString)
                {
                    if (decimal.TryParse(doc[field].AsString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                    {
                        var setUpdate = Builders<BsonDocument>.Update.Set(field, new MongoDB.Bson.BsonDecimal128(val));
                        update = update == null ? setUpdate : Builders<BsonDocument>.Update.Combine(update, setUpdate);
                    }
                }
            }

            if (update != null)
            {
                await collection.UpdateOneAsync(new BsonDocument("_id", id), update, cancellationToken: ct);
            }
        }
    }
}
