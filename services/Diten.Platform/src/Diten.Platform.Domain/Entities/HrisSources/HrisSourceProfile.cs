using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.HrisSources;

public sealed class HrisSourceProfile : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public HrisProviderKind ProviderKind { get; set; }
    public string? ExternalTenantKey { get; set; }
    public required string ConnectionProfileReference { get; set; }
    public HrisSourceLifecycleState LifecycleState { get; set; } = HrisSourceLifecycleState.Draft;
    public HrisSyncMode SyncMode { get; set; } = HrisSyncMode.Manual;
    public Guid? MappingProfileId { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public Guid? LastSyncCheckpointId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
