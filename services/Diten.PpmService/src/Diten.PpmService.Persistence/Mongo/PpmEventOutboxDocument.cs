using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.BuildingBlocks.Eventing;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;


public sealed class PpmEventOutboxDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public int EventVersion { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public Guid TenantId { get; init; }
    public string Producer { get; init; } = string.Empty;
    public long OccurredAtUtcTicks { get; init; }
    public byte[] CanonicalPayloadUtf8 { get; init; } = [];
    public Dictionary<string, string> TransportHeaders { get; init; } =
        new(StringComparer.Ordinal);
    public EventOutboxDeliveryStatus Status { get; init; } = EventOutboxDeliveryStatus.Pending;
    public int AttemptCount { get; init; }
    public DateTime? NextAttemptAtUtc { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
