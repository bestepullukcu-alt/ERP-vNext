using MongoDB.Driver;
using MongoDB.Bson;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class SubscriptionPlanSeed
{
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
