using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed record GateIAuditIntent(
    Guid AuditIntentId,
    Guid ActorId,
    string EntityType,
    Guid EntityId,
    string Mutation,
    DateTimeOffset OccurredAtUtc,
    byte[] CanonicalPayload,
    string PayloadSha256,
    string VerifiedSigningIdentity);

internal static class GateIAuditIntentParser
{
    private const int MaxPayloadBytes = 2048;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] PropertyOrder =
        ["auditIntentId", "actorId", "entityType", "entityId", "mutation", "occurredAtUtc"];
    private static readonly HashSet<string> PropertyNames = new(PropertyOrder, StringComparer.Ordinal);

    public static GateIAuditIntent Parse(
        Diten.BuildingBlocks.Eventing.EventTransportMessage message,
        GateIAuditAdapterRegistry registry,
        string verifiedSigningIdentity)
    {
        if (string.IsNullOrEmpty(verifiedSigningIdentity)
            || !string.Equals(verifiedSigningIdentity, verifiedSigningIdentity.Trim(), StringComparison.Ordinal))
        {
            throw new EventSecurityException("The verified signing identity is invalid.", "gate-i.signing-identity.invalid");
        }
        if (message.EventId == Guid.Empty || message.CorrelationId == Guid.Empty
            || !message.TenantId.HasValue || message.TenantId.Value == Guid.Empty
            || message.OccurredAtUtc.Offset != TimeSpan.Zero
            || message.CanonicalPayloadUtf8.IsEmpty
            || message.CanonicalPayloadUtf8.Length > MaxPayloadBytes)
        {
            throw new EventContractException("The Gate I envelope is invalid.", "gate-i.envelope.invalid");
        }

        byte[] incoming = message.CanonicalPayloadUtf8.ToArray();
        try
        {
            _ = StrictUtf8.GetString(incoming);
            var reader = new Utf8JsonReader(incoming, new JsonReaderOptions
            {
                MaxDepth = 2,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.Read() || document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new EventContractException("The Gate I payload must be one JSON object.", "gate-i.payload.shape");
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var encountered = new List<string>(PropertyOrder.Length);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                encountered.Add(property.Name);
                if (!PropertyNames.Contains(property.Name) || !values.TryAdd(property.Name, property.Value)
                    || property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    throw new EventContractException("The Gate I payload has an unknown, duplicate or nested field.", "gate-i.payload.field");
                }
            }

            if (!encountered.SequenceEqual(PropertyOrder, StringComparer.Ordinal))
            {
                throw new EventContractException("The Gate I payload field order is not canonical.", "gate-i.payload.order");
            }

            var auditIntentId = GuidValue(values, "auditIntentId");
            var actorId = GuidValue(values, "actorId");
            var entityId = GuidValue(values, "entityId");
            var entityType = TextValue(values, "entityType");
            var mutation = TextValue(values, "mutation");
            var occurredAtUtc = UtcValue(values, "occurredAtUtc");
            if (auditIntentId != message.EventId || occurredAtUtc != message.OccurredAtUtc)
            {
                throw new EventSecurityException("The Gate I payload does not bind to its envelope.", "gate-i.envelope.binding");
            }

            _ = registry.Resolve(message.Producer, message.EventName, message.EventVersion, entityType, mutation);
            var canonical = Canonicalize(auditIntentId, actorId, entityType, entityId, mutation, occurredAtUtc);
            if (!incoming.AsSpan().SequenceEqual(canonical))
            {
                throw new EventContractException("The Gate I payload bytes are not canonical.", "gate-i.payload.canonical");
            }

            return new(auditIntentId, actorId, entityType, entityId, mutation, occurredAtUtc, canonical,
                Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant(), verifiedSigningIdentity);
        }
        catch (EventTransportTerminalExceptionBase)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or FormatException)
        {
            throw new EventContractException("The Gate I payload is malformed.", "gate-i.payload.malformed");
        }
    }

    private static byte[] Canonicalize(Guid auditIntentId, Guid actorId, string entityType, Guid entityId,
        string mutation, DateTimeOffset occurredAtUtc)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("auditIntentId", auditIntentId.ToString("D"));
        writer.WriteString("actorId", actorId.ToString("D"));
        writer.WriteString("entityType", entityType);
        writer.WriteString("entityId", entityId.ToString("D"));
        writer.WriteString("mutation", mutation);
        writer.WriteString("occurredAtUtc", occurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static Guid GuidValue(IReadOnlyDictionary<string, JsonElement> values, string name) =>
        values[name].ValueKind == JsonValueKind.String
        && Guid.TryParseExact(values[name].GetString(), "D", out var value)
        && value != Guid.Empty
            ? value
            : throw new EventContractException("A Gate I identifier is invalid.", "gate-i.payload.identifier");

    private static string TextValue(IReadOnlyDictionary<string, JsonElement> values, string name)
    {
        var value = values[name].ValueKind == JsonValueKind.String ? values[name].GetString() : null;
        return value is not null && value.Length is > 0 and <= 80
               && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            ? value
            : throw new EventContractException("A Gate I text field is invalid.", "gate-i.payload.text");
    }

    private static DateTimeOffset UtcValue(IReadOnlyDictionary<string, JsonElement> values, string name)
    {
        var text = values[name].ValueKind == JsonValueKind.String ? values[name].GetString() : null;
        return text is not null
               && DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
               && value.Offset == TimeSpan.Zero
            ? value
            : throw new EventContractException("The Gate I occurrence time is invalid.", "gate-i.payload.time");
    }
}
