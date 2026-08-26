using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Features.Quotas;
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
        var collection = database.GetCollection<SubscriptionPlan>(PlatformCollections.SubscriptionPlans);

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
                TrialDurationDays = 14,
                DefaultQuotas = new Dictionary<string, decimal>
                {
                    [QuotaKeys.UsersMax] = 5,
                    [QuotaKeys.StorageGbMax] = 100,
                    [QuotaKeys.ApiCallsPerMonth] = 10000,
                    [QuotaKeys.ModulesMax] = 3
                }
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
                IsTrialPlan = false,
                DefaultQuotas = new Dictionary<string, decimal>
                {
                    [QuotaKeys.UsersMax] = 25,
                    [QuotaKeys.StorageGbMax] = 250,
                    [QuotaKeys.ApiCallsPerMonth] = 100000,
                    [QuotaKeys.ModulesMax] = 10
                }
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
                IsTrialPlan = false,
                DefaultQuotas = new Dictionary<string, decimal>
                {
                    [QuotaKeys.UsersMax] = 100,
                    [QuotaKeys.StorageGbMax] = 1000,
                    [QuotaKeys.ApiCallsPerMonth] = 1000000,
                    [QuotaKeys.ModulesMax] = 25
                }
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
                IsTrialPlan = false,
                DefaultQuotas = new Dictionary<string, decimal>
                {
                    [QuotaKeys.UsersMax] = 1000,
                    [QuotaKeys.StorageGbMax] = 10000,
                    [QuotaKeys.ApiCallsPerMonth] = 10000000,
                    [QuotaKeys.ModulesMax] = 100
                }
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

            var existing = await collection.Find(x => x.IsDeleted == false && x.Code == seed.Code).FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                // Idempotent backfill: only fill quotas for plans seeded before DefaultQuotas existed.
                // Once populated, the condition is false → never overwrites on subsequent runs or manual edits.
                if (existing.DefaultQuotas == null || existing.DefaultQuotas.Count == 0)
                {
                    var update = Builders<SubscriptionPlan>.Update.Set(x => x.DefaultQuotas, seed.DefaultQuotas);
                    await collection.UpdateOneAsync(x => x.Id == existing.Id, update, cancellationToken: ct);
                }

                continue;
            }

            await collection.InsertOneAsync(seed, cancellationToken: ct);
        }
    }

    public static async Task RepairQuotaDataTypesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var collection = database.GetCollection<BsonDocument>(PlatformCollections.QuotaUsages);

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
