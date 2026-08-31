using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Domain.Repositories;

namespace Diten.PpmService.Application.Events;

public sealed class PpmAuditIntentSubmittedV1 : IIntegrationEvent, ICanonicalIntegrationEvent
{
    public const string CanonicalEventName = "ppm.audit-intent.submitted.v1";
    public const int CanonicalEventVersion = 1;
    public const int MaximumPayloadBytes = 2048;

    private static readonly HashSet<string> EntityTypes =
        new(["Portfolio", "Initiative", "Program", "Project"], StringComparer.Ordinal);

    private static readonly HashSet<string> Mutations =
        new(["created", "updated", "lifecycle-changed", "soft-deleted"], StringComparer.Ordinal);

    public PpmAuditIntentSubmittedV1(AuditIntentDispatchCandidate intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.Id == Guid.Empty
            || intent.ActorId == Guid.Empty
            || intent.EntityId == Guid.Empty
            || !EntityTypes.Contains(intent.EntityType)
            || !Mutations.Contains(intent.Mutation)
            || intent.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new EventValidationException("PPM audit intent is invalid.");
        }

        AuditIntentId = intent.Id;
        ActorId = intent.ActorId;
        EntityId = intent.EntityId;
        EntityType = intent.EntityType;
        Mutation = intent.Mutation;
        OccurredAtUtc = intent.OccurredAtUtc;
        _canonicalPayloadUtf8 = CreateCanonicalPayload();
    }

    public string EventName => CanonicalEventName;
    public int EventVersion => CanonicalEventVersion;
    public Guid AuditIntentId { get; }
    public Guid ActorId { get; }
    public Guid EntityId { get; }
    public string EntityType { get; }
    public string Mutation { get; }
    public DateTime OccurredAtUtc { get; }
    private readonly byte[] _canonicalPayloadUtf8;
    ReadOnlyMemory<byte> ICanonicalIntegrationEvent.CanonicalPayloadUtf8 => _canonicalPayloadUtf8;

    private byte[] CreateCanonicalPayload()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false
               }))
        {
            writer.WriteStartObject();
            writer.WriteString("actorId", ActorId.ToString("D"));
            writer.WriteString("auditIntentId", AuditIntentId.ToString("D"));
            writer.WriteString("entityId", EntityId.ToString("D"));
            writer.WriteString("entityType", EntityType);
            writer.WriteString("mutation", Mutation);
            writer.WriteString(
                "occurredAtUtc",
                OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        var payload = buffer.WrittenSpan.ToArray();
        if (payload.Length == 0 || payload.Length > MaximumPayloadBytes)
        {
            throw new EventValidationException(
                $"PPM audit payload must contain 1..{MaximumPayloadBytes} UTF-8 bytes.");
        }

        _ = new UTF8Encoding(false, true).GetString(payload);
        return payload;
    }
}
