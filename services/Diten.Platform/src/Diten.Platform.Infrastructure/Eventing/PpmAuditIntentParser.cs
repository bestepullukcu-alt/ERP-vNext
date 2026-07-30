using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Contracts.Eventing;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed record PpmAuditIntent(
    Guid AuditIntentId,
    Guid ActorId,
    string EntityType,
    Guid EntityId,
    string Mutation,
    DateTimeOffset OccurredAtUtc,
    byte[] CanonicalPayload,
    string PayloadSha256);

internal class PpmAuditContractException : Exception
{
    public PpmAuditContractException(string message) : base(message) { }
}

internal class PpmAuditTransientException : Exception
{
    public PpmAuditTransientException(Exception innerException)
        : base(
            $"PPM audit infrastructure acceptance is transient. ErrorType={innerException.GetType().Name}",
            innerException)
    {
    }
}

internal sealed class PpmAuditSecurityException : PpmAuditContractException
{
    public PpmAuditSecurityException(string message) : base(message) { }
}

internal sealed class PpmAuditRetriesExhaustedException : PpmAuditTransientException
{
    public PpmAuditRetriesExhaustedException(Exception innerException)
        : base(innerException)
    {
    }
}

internal static class PpmAuditIntentParser
{
    internal const string EventName = "ppm.audit-intent.submitted.v1";
    internal const int EventVersion = 1;
    internal const string Producer = "Diten.PpmService";
    internal const string SignatureScheme = "ppm-event-hmac-sha256.v1";
    internal const int MaxPayloadBytes = 2048;
    internal const string MissingCausationIdMarker = "-";
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly HashSet<string> EntityTypes =
        ["Portfolio", "Initiative", "Program", "Project"];
    private static readonly HashSet<string> Mutations =
        ["created", "updated", "lifecycle-changed", "soft-deleted"];
    private static readonly HashSet<string> PropertyNames =
        ["auditIntentId", "actorId", "entityType", "entityId", "mutation", "occurredAtUtc"];

    public static PpmAuditIntent Parse(EventTransportMessage message)
    {
        if (message.EventId == Guid.Empty
            || message.CorrelationId == Guid.Empty
            || !message.TenantId.HasValue
            || message.TenantId.Value == Guid.Empty
            || message.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new PpmAuditContractException("PPM audit envelope identifiers are required.");
        }

        if (!string.Equals(message.EventName, EventName, StringComparison.Ordinal)
            || message.EventVersion != EventVersion
            || !string.Equals(message.Producer, Producer, StringComparison.Ordinal))
        {
            throw new PpmAuditContractException("PPM audit envelope identity is invalid.");
        }

        if (message.PayloadJson is null)
        {
            throw new PpmAuditContractException("PPM audit payload is required.");
        }

        // A UTF-16 code unit always produces at least one UTF-8 byte. Rejecting this bound first prevents
        // an attacker-controlled oversized string from reaching the encoder allocation.
        if (message.PayloadJson.Length > MaxPayloadBytes)
        {
            throw new PpmAuditContractException("PPM audit canonical payload exceeds 2048 UTF-8 bytes.");
        }

        byte[] incomingBytes;
        try
        {
            var byteCount = StrictUtf8.GetByteCount(message.PayloadJson);
            if (byteCount > MaxPayloadBytes)
            {
                throw new PpmAuditContractException("PPM audit canonical payload exceeds 2048 UTF-8 bytes.");
            }

            incomingBytes = new byte[byteCount];
            StrictUtf8.GetBytes(message.PayloadJson.AsSpan(), incomingBytes);
        }
        catch (EncoderFallbackException exception)
        {
            throw new PpmAuditContractException(
                $"PPM audit payload is not valid strict UTF-8 input: {exception.GetType().Name}.");
        }

        try
        {
            var reader = new Utf8JsonReader(incomingBytes, new JsonReaderOptions
            {
                MaxDepth = 2,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.Read() || document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new PpmAuditContractException("PPM audit payload must be exactly one JSON object.");
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!PropertyNames.Contains(property.Name) || !values.TryAdd(property.Name, property.Value))
                {
                    throw new PpmAuditContractException("PPM audit payload contains an unknown or duplicate property.");
                }

                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    throw new PpmAuditContractException("PPM audit payload does not permit arrays, dictionaries or nested objects.");
                }
            }

            if (values.Count != PropertyNames.Count)
            {
                throw new PpmAuditContractException("PPM audit payload must contain exactly six properties.");
            }

            var auditIntentId = ReadGuid(values, "auditIntentId");
            var actorId = ReadGuid(values, "actorId");
            var entityId = ReadGuid(values, "entityId");
            var entityType = ReadAscii(values, "entityType", EntityTypes);
            var mutation = ReadAscii(values, "mutation", Mutations);
            var occurredAtUtc = ReadUtc(values, "occurredAtUtc");

            if (auditIntentId != message.EventId || occurredAtUtc != message.OccurredAtUtc)
            {
                throw new PpmAuditContractException("PPM audit payload does not match its envelope.");
            }

            var canonical = Canonicalize(auditIntentId, actorId, entityType, entityId, mutation, occurredAtUtc);
            if (!incomingBytes.AsSpan().SequenceEqual(canonical))
            {
                throw new PpmAuditContractException(
                    "PPM audit payload bytes are not the exact canonical JSON representation.");
            }

            return new PpmAuditIntent(
                auditIntentId,
                actorId,
                entityType,
                entityId,
                mutation,
                occurredAtUtc,
                canonical,
                Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant());
        }
        catch (PpmAuditContractException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new PpmAuditContractException(
                $"PPM audit payload is malformed JSON: {exception.GetType().Name}.");
        }
    }

    public static byte[] BuildSigningInput(EventTransportMessage message, ReadOnlySpan<byte> canonicalPayload)
    {
        var prefix = string.Join('\n',
            SignatureScheme,
            message.EventId.ToString("D"),
            message.EventName,
            message.EventVersion.ToString(CultureInfo.InvariantCulture),
            message.TenantId!.Value.ToString("D"),
            message.CorrelationId.ToString("D"),
            message.Producer,
            message.CausationId?.ToString("D") ?? MissingCausationIdMarker,
            message.OccurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            canonicalPayload.Length.ToString(CultureInfo.InvariantCulture)) + "\n";
        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        var result = new byte[prefixBytes.Length + canonicalPayload.Length];
        prefixBytes.CopyTo(result, 0);
        canonicalPayload.CopyTo(result.AsSpan(prefixBytes.Length));
        return result;
    }

    private static byte[] Canonicalize(
        Guid auditIntentId,
        Guid actorId,
        string entityType,
        Guid entityId,
        string mutation,
        DateTimeOffset occurredAtUtc)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("actorId", actorId.ToString("D"));
        writer.WriteString("auditIntentId", auditIntentId.ToString("D"));
        writer.WriteString("entityId", entityId.ToString("D"));
        writer.WriteString("entityType", entityType);
        writer.WriteString("mutation", mutation);
        writer.WriteString("occurredAtUtc", occurredAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static Guid ReadGuid(IReadOnlyDictionary<string, JsonElement> values, string name)
    {
        if (values[name].ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(values[name].GetString(), "D", out var value)
            || value == Guid.Empty)
        {
            throw new PpmAuditContractException($"PPM audit field '{name}' must be a non-empty canonical Guid.");
        }

        return value;
    }

    private static string ReadAscii(
        IReadOnlyDictionary<string, JsonElement> values,
        string name,
        IReadOnlySet<string> allowed)
    {
        var value = values[name].ValueKind == JsonValueKind.String ? values[name].GetString() : null;
        if (value is null || Encoding.UTF8.GetByteCount(value) > 32 || value.Any(character => character > 0x7f)
            || !allowed.Contains(value))
        {
            throw new PpmAuditContractException($"PPM audit field '{name}' is invalid.");
        }

        return value;
    }

    private static DateTimeOffset ReadUtc(IReadOnlyDictionary<string, JsonElement> values, string name)
    {
        var text = values[name].ValueKind == JsonValueKind.String ? values[name].GetString() : null;
        if (text is null
            || !DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            || value.Offset != TimeSpan.Zero)
        {
            throw new PpmAuditContractException($"PPM audit field '{name}' must be an exact UTC round-trip timestamp.");
        }

        return value;
    }
}
