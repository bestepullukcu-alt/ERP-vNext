using System.Collections;
using System.Text.Json;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using MongoDB.Bson;

namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class AuditOutboxPayloadMapper
{
    public const string OutboxIdempotencyMetadataKey = "AuditOutboxIdempotencyKey";
    public const string OutboxMessageIdMetadataKey = "AuditOutboxMessageId";

    public AuditEvent Map(AuditOutboxProcessingItem item, DateTimeOffset writtenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(item);

        var payload = NormalizeDictionary(item.Payload);
        var tenantId = ReadRequiredGuid(payload, "TenantId");
        if (tenantId != item.TenantId)
        {
            throw new AuditOutboxPayloadMappingException("Invalid audit outbox payload. Field=TenantId. Error=Tenant mismatch.");
        }

        var correlationId = ReadRequiredGuid(payload, "CorrelationId");
        if (correlationId != item.CorrelationId)
        {
            throw new AuditOutboxPayloadMappingException("Invalid audit outbox payload. Field=CorrelationId. Error=Correlation mismatch.");
        }

        var metadata = ReadOptionalDictionary(payload, "Metadata") ?? [];
        metadata[OutboxIdempotencyMetadataKey] = item.IdempotencyKey;
        metadata[OutboxMessageIdMetadataKey] = item.Id;

        var redactionStatus = ReadOptionalEnum<AuditRedactionStatus>(payload, "RedactionStatus")
            ?? InferRedactionStatus(payload, metadata);

        return new AuditEvent
        {
            TenantId = tenantId,
            CorrelationId = correlationId,
            RequestType = ReadOptionalString(payload, "RequestType") ?? item.RequestType,
            ActorType = ReadRequiredEnum<AuditActorType>(payload, "ActorType"),
            ActorId = ReadOptionalGuid(payload, "ActorId"),
            ActorEmailMasked = ReadOptionalString(payload, "ActorEmailMasked"),
            ActorDisplayNameMasked = ReadOptionalString(payload, "ActorDisplayNameMasked"),
            TargetTenantId = ReadOptionalGuid(payload, "TargetTenantId"),
            Category = ReadRequiredEnum<AuditCategory>(payload, "Category"),
            EntityType = ReadRequiredString(payload, "EntityType"),
            EntityId = ReadOptionalGuid(payload, "EntityId"),
            Operation = ReadRequiredEnum<AuditOperation>(payload, "Operation"),
            Outcome = ReadRequiredEnum<AuditOutcome>(payload, "Outcome"),
            BeforeState = ReadOptionalDictionary(payload, "BeforeState"),
            AfterState = ReadOptionalDictionary(payload, "AfterState"),
            Metadata = metadata,
            IpAddressMasked = ReadOptionalString(payload, "IpAddressMasked"),
            UserAgent = ReadOptionalString(payload, "UserAgent"),
            OccurredAtUtc = ReadOptionalDateTimeOffset(payload, "OccurredAtUtc") ?? item.CreatedAtUtc,
            WrittenAtUtc = writtenAtUtc,
            SourceService = ReadRequiredString(payload, "SourceService"),
            SourceModule = ReadOptionalString(payload, "SourceModule"),
            IsMetaAudit = ReadOptionalBool(payload, "IsMetaAudit") ?? false,
            RedactionStatus = redactionStatus
        };
    }

    private static AuditRedactionStatus InferRedactionStatus(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> metadata)
    {
        return ContainsRedactionMarker(ReadOptionalDictionary(payload, "BeforeState"))
               || ContainsRedactionMarker(ReadOptionalDictionary(payload, "AfterState"))
               || ContainsRedactionMarker(metadata)
            ? AuditRedactionStatus.SensitiveFieldsRedacted
            : AuditRedactionStatus.None;
    }

    private static Dictionary<string, object?> NormalizeDictionary(IReadOnlyDictionary<string, object?> value)
    {
        return value.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value), StringComparer.Ordinal);
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            BsonNull => null,
            BsonString bsonString => bsonString.Value,
            BsonBoolean bsonBoolean => bsonBoolean.Value,
            BsonInt32 bsonInt32 => bsonInt32.Value,
            BsonInt64 bsonInt64 => bsonInt64.Value,
            BsonDouble bsonDouble => bsonDouble.Value,
            BsonDecimal128 bsonDecimal => Decimal128.ToDecimal(bsonDecimal.Value),
            BsonBinaryData bsonBinary when bsonBinary.SubType is BsonBinarySubType.UuidStandard => bsonBinary.ToGuid(GuidRepresentation.Standard),
            BsonDateTime bsonDateTime => new DateTimeOffset(bsonDateTime.ToUniversalTime(), TimeSpan.Zero),
            BsonDocument bsonDocument => bsonDocument.ToDictionary(element => element.Name, element => NormalizeValue(element.Value), StringComparer.Ordinal),
            BsonArray bsonArray => bsonArray.Select(NormalizeValue).ToList(),
            JsonElement jsonElement => NormalizeJsonElement(jsonElement),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value), StringComparer.Ordinal),
            IDictionary<string, object?> dictionary => dictionary.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value), StringComparer.Ordinal),
            IDictionary nonGenericDictionary => NormalizeNonGenericDictionary(nonGenericDictionary),
            IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Select(NormalizeValue).ToList(),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value), StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> NormalizeNonGenericDictionary(IDictionary dictionary)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string key)
            {
                normalized[key] = NormalizeValue(entry.Value);
            }
        }

        return normalized;
    }

    private static string ReadRequiredString(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        var value = ReadOptionalString(payload, fieldName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Required string missing.");
        }

        return value.Trim();
    }

    private static string? ReadOptionalString(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => text,
            Guid guid => guid.ToString(),
            _ => value.ToString()
        };
    }

    private static Guid ReadRequiredGuid(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        var value = ReadOptionalGuid(payload, fieldName);
        if (!value.HasValue || value.Value == Guid.Empty)
        {
            throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Required guid missing.");
        }

        return value.Value;
    }

    private static Guid? ReadOptionalGuid(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            Guid guid => guid == Guid.Empty ? null : guid,
            string text when Guid.TryParse(text, out var parsed) && parsed != Guid.Empty => parsed,
            _ => throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Invalid guid.")
        };
    }

    private static TEnum ReadRequiredEnum<TEnum>(IReadOnlyDictionary<string, object?> payload, string fieldName)
        where TEnum : struct, Enum
    {
        var value = ReadOptionalEnum<TEnum>(payload, fieldName);
        if (!value.HasValue || EqualityComparer<TEnum>.Default.Equals(value.Value, default))
        {
            throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Required enum missing.");
        }

        return value.Value;
    }

    private static TEnum? ReadOptionalEnum<TEnum>(IReadOnlyDictionary<string, object?> payload, string fieldName)
        where TEnum : struct, Enum
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        if (value is TEnum typed)
        {
            return typed;
        }

        if (value is string text && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        if (value is int intValue && Enum.IsDefined(typeof(TEnum), intValue))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
        }

        if (value is long longValue && longValue <= int.MaxValue && Enum.IsDefined(typeof(TEnum), (int)longValue))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), (int)longValue);
        }

        throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Invalid enum.");
    }

    private static Dictionary<string, object?>? ReadOptionalDictionary(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        var normalized = NormalizeValue(value);
        return normalized switch
        {
            null => null,
            Dictionary<string, object?> dictionary => dictionary,
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            _ => throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Invalid dictionary.")
        };
    }

    private static DateTimeOffset? ReadOptionalDateTimeOffset(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            string text when DateTimeOffset.TryParse(text, out var parsed) => parsed,
            _ => throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Invalid timestamp.")
        };
    }

    private static bool? ReadOptionalBool(IReadOnlyDictionary<string, object?> payload, string fieldName)
    {
        if (!payload.TryGetValue(fieldName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool boolean => boolean,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => throw new AuditOutboxPayloadMappingException($"Invalid audit outbox payload. Field={fieldName}. Error=Invalid boolean.")
        };
    }

    private static bool ContainsRedactionMarker(object? value)
    {
        return value switch
        {
            null => false,
            string text => string.Equals(text, "[REDACTED]", StringComparison.Ordinal),
            IReadOnlyDictionary<string, object?> dictionary => dictionary.Values.Any(ContainsRedactionMarker),
            IEnumerable<object?> list => list.Any(ContainsRedactionMarker),
            _ => false
        };
    }
}
