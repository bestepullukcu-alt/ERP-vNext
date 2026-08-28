using System.Security.Cryptography;
using System.Text;

namespace Diten.Application.Common.Tenancy;

public static class EnterpriseStrategyCollectionInventoryContracts
{
    public const string MongoUriEnvironmentName = "ESBP_INVENTORY_MONGO_URI";
    public const string DatabaseEnvironmentName = "ESBP_INVENTORY_DATABASE";
    public const string TargetCopyEnvironmentName = "ESBP_INVENTORY_TARGET_COPY_ID";
    public const string RunEnvironmentName = "ESBP_INVENTORY_RUN_ID";
    public const string AcknowledgementEnvironmentName = "ESBP_INVENTORY_ACKNOWLEDGE_READ_ONLY_TARGET_COPY";
    public const string RequiredAcknowledgement = "I_ACKNOWLEDGE_READ_ONLY_TARGET_COPY";

    public static InventoryOptions ReadOptions(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        var mongoUri = Required(readEnvironment, MongoUriEnvironmentName);
        var database = Required(readEnvironment, DatabaseEnvironmentName);
        var targetCopyId = Required(readEnvironment, TargetCopyEnvironmentName);
        var runId = Required(readEnvironment, RunEnvironmentName);
        var acknowledgement = Required(readEnvironment, AcknowledgementEnvironmentName);

        ValidateMongoUri(mongoUri);
        ValidateIdentifier(database, DatabaseEnvironmentName);
        ValidateIdentifier(targetCopyId, TargetCopyEnvironmentName);
        ValidateIdentifier(runId, RunEnvironmentName);
        if (!string.Equals(database, targetCopyId, StringComparison.Ordinal))
            throw Invalid(TargetCopyEnvironmentName);
        if (!string.Equals(acknowledgement, RequiredAcknowledgement, StringComparison.Ordinal))
            throw Invalid(AcknowledgementEnvironmentName);

        return new InventoryOptions(mongoUri, database, targetCopyId, runId);
    }

    public static string ComputeMetadataDigest(IEnumerable<CollectionEvidence> collections)
    {
        var canonical = new StringBuilder();
        foreach (var collection in collections.OrderBy(item => item.Name, StringComparer.Ordinal))
            AppendCollection(canonical, collection);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    public static string SanitizeMarkdown(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safe = new string(value.Where(character => !char.IsControl(character)).ToArray());
        return safe.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal);
    }

    private static void AppendCollection(StringBuilder output, CollectionEvidence collection)
    {
        output.Append(collection.Name).Append('|').Append(collection.DocumentCount).Append('\n');
        foreach (var index in collection.Indexes.OrderBy(item => item.Name, StringComparer.Ordinal))
            output.Append("index|").Append(index.Name).Append('|').Append(index.IsUnique).Append('|')
                .Append(index.IsSparse).Append('|').Append(index.ExpireAfterSeconds).Append('|')
                .AppendJoin(',', index.Keys.Select(key => $"{key.Field}:{key.Direction}")).Append('\n');
        foreach (var field in collection.FieldTypes.OrderBy(item => item.Field, StringComparer.Ordinal)
                     .ThenBy(item => item.BsonType, StringComparer.Ordinal))
            output.Append("field|").Append(field.Field).Append('|').Append(field.BsonType).Append('|')
                .Append(field.Count).Append('\n');
    }

    private static string Required(Func<string, string?> readEnvironment, string name)
    {
        var value = readEnvironment(name);
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid(name);
        return value;
    }

    private static void ValidateMongoUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "mongodb" && uri.Scheme != "mongodb+srv"))
            throw Invalid(MongoUriEnvironmentName);
    }

    private static void ValidateIdentifier(string value, string name)
    {
        if (value.Length > 128 || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            throw Invalid(name);
    }

    private static InvalidOperationException Invalid(string name) =>
        new($"Inventory environment gate failed: {name}.");

    public sealed record InventoryOptions(string MongoUri, string Database, string TargetCopyId, string RunId);

    public sealed record InventoryReport(
        string TargetCopyId,
        string RunId,
        DateTimeOffset MeasuredAtUtc,
        IReadOnlyList<CollectionEvidence> Collections,
        string MetadataSha256);

    public sealed record CollectionEvidence(
        string Name,
        long DocumentCount,
        string Disposition,
        IReadOnlyList<IndexEvidence> Indexes,
        IReadOnlyList<FieldTypeEvidence> FieldTypes);

    public sealed record IndexEvidence(
        string Name,
        bool IsUnique,
        bool IsSparse,
        long? ExpireAfterSeconds,
        IReadOnlyList<IndexKeyEvidence> Keys);

    public sealed record IndexKeyEvidence(string Field, string Direction);

    public sealed record FieldTypeEvidence(string Field, string BsonType, long Count);
}
