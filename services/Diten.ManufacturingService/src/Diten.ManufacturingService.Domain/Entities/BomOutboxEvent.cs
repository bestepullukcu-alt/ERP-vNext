using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ManufacturingService.Domain.Entities;

/// <summary>
/// Outbox row for a BOM-version lifecycle event (<c>bom.openapi.yaml</c> v1.2.0, <c>BomVersionEvent</c>). Written by
/// <c>IBomHistoryJournal.CommitAsync</c> in the SAME Mongo transaction as the BOM change it announces, so an event
/// exists if and only if the change was committed. A row in the outbox is not a delivery: the background publisher
/// moves it to the broker and only then marks it <see cref="BomOutboxStatus.Published"/>.
/// </summary>
public sealed class BomOutboxEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary><c>manufacturing.bomversion.effective.v1</c> or <c>manufacturing.bomversion.superseded.v1</c>.</summary>
    public string EventName { get; set; } = string.Empty;

    public int EventVersion { get; set; } = 1;

    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid LegalEntityId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid BomVersionId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CorrelationId { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>The contract payload, serialized once at write time; the publisher sends these bytes unchanged.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public BomOutboxStatus Status { get; set; } = BomOutboxStatus.Pending;

    public int AttemptCount { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset NextAttemptAtUtc { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? ClaimedAtUtc { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public DateTimeOffset? PublishedAtUtc { get; set; }

    public string? LastError { get; set; }
}

public enum BomOutboxStatus
{
    Pending,
    Publishing,
    Published,
    DeadLettered
}
