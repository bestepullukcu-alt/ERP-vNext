using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class PositionAssignment : TenantScopedEntity
{
    public required Guid PositionId { get; set; }
    public required Guid UserId { get; set; }
    public required DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
