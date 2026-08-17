using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events;

public sealed record TenantActivatedV1(
    Guid TenantId,
    DateTimeOffset ActivatedAtUtc,
    Guid? ActivatedBy) : IInternalEvent
{
    public const string Name = "tenant.activated.v1";
    public const int Version = 1;

    public string EventName => Name;

    public int EventVersion => Version;
}
