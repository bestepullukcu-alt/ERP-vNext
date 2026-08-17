using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.HrisSources;

public sealed class HrisSourceHealthSnapshot : TenantScopedEntity
{
    public required Guid SourceProfileId { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public HrisHealthState HealthState { get; set; }
    public int? LatencyMs { get; set; }
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }
    public string? RedactedMessage { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
