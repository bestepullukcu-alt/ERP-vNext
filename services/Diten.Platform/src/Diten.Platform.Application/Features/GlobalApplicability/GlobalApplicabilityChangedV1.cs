using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Features.GlobalApplicability;

public sealed record GlobalApplicabilityChangedV1(Guid EventId, DateTimeOffset OccurredAtUtc,
    Guid CorrelationId, string EntityType, Guid EntityId, string Operation,
    ulong GlobalApplicabilityVersion) : IIntegrationEvent
{
    public const string Name = "platform.globalapplicability.changed.v1";
    public const int Version = 1;
    public string EventName => Name;
    public int EventVersion => Version;
}
