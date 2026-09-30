using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Features.EvidenceLinking.Events;

// MOD-0031 slice 1 — outbox events. Payload = identifiers and codes only: the quote, the supported spans and the
// removal reason (free text) never enter an event.

public sealed record EvidenceLinkCreatedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TenantId,
    Guid CorrelationId,
    Guid? ActorId,
    Guid LinkId,
    string ObjectModule,
    string ObjectType,
    string ObjectId,
    string? ObjectVersion,
    string DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string EvidenceTypeCode) : IIntegrationEvent
{
    public const string Name = "platform.evidence.link.created.v1";
    public const int Version = 1;

    public string EventName => Name;
    public int EventVersion => Version;
}

public sealed record EvidenceLinkRemovedV1(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid TenantId,
    Guid CorrelationId,
    Guid? ActorId,
    Guid LinkId,
    string ObjectModule,
    string ObjectType,
    string ObjectId,
    string? ObjectVersion,
    string DocumentKind,
    Guid DocumentId,
    Guid? DocumentVersionId,
    string EvidenceTypeCode) : IIntegrationEvent
{
    public const string Name = "platform.evidence.link.removed.v1";
    public const int Version = 1;

    public string EventName => Name;
    public int EventVersion => Version;
}
