using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class LegacySavedViewMigration
{
    public static async Task MigrateAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<BsonDocument>(PlatformCollections.SavedViews);

        var legacyFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("CreatedAt", true),
            Builders<BsonDocument>.Filter.Exists("UpdatedAt", true),
            Builders<BsonDocument>.Filter.Exists("IsDeleted", true),
            Builders<BsonDocument>.Filter.Exists("DeletedAt", true),
            Builders<BsonDocument>.Filter.Exists("Status", false),
            Builders<BsonDocument>.Filter.Exists("CreatedDate", false),
            Builders<BsonDocument>.Filter.Exists("ModifiedDate", false));

        var documents = await collection.Find(legacyFilter).ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            return;
        }

        foreach (var document in documents)
        {
            var id = document.GetValue("_id", BsonNull.Value);
            if (id == BsonNull.Value)
            {
                continue;
            }

            var isDeleted = document.TryGetValue("IsDeleted", out var isDeletedValue) && isDeletedValue.IsBoolean
                ? isDeletedValue.AsBoolean
                : false;

            var createdDate = ExtractLegacyDate(document, "CreatedDate")
                ?? ExtractLegacyDate(document, "CreatedAt")
                ?? DateTime.UtcNow;

            var modifiedDate = ExtractLegacyDate(document, "ModifiedDate")
                ?? ExtractLegacyDate(document, "UpdatedAt")
                ?? createdDate;

            var update = Builders<BsonDocument>.Update
                .Set("Status", !isDeleted)
                .Set("CreatedDate", createdDate)
                .Set("ModifiedDate", modifiedDate)
                .Set("CreatedBy", document.GetValue("CreatedBy", string.Empty).AsString)
                .Set("ModifiedBy", document.GetValue("ModifiedBy", string.Empty).AsString)
                .Unset("CreatedAt")
                .Unset("UpdatedAt")
                .Unset("IsDeleted")
                .Unset("DeletedAt");

            await collection.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                update,
                cancellationToken: cancellationToken);
        }
    }

    private static DateTime? ExtractLegacyDate(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value == BsonNull.Value)
        {
            return null;
        }

        return TryConvertToDateTime(value);
    }

    private static DateTime? TryConvertToDateTime(BsonValue value)
    {
        try
        {
            return value.BsonType switch
            {
                BsonType.DateTime => value.ToUniversalTime(),
                BsonType.String when DateTime.TryParse(value.AsString, out var parsedString) => parsedString.ToUniversalTime(),
                BsonType.Int64 => new DateTime(value.AsInt64, DateTimeKind.Utc),
                BsonType.Array => TryConvertLegacyArray(value.AsBsonArray),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? TryConvertLegacyArray(BsonArray array)
    {
        if (array.Count == 0)
        {
            return null;
        }

        var first = array[0];
        if (!first.IsInt64)
        {
            return null;
        }

        try
        {
            return new DateTime(first.AsInt64, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }
}
