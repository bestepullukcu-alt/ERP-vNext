using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class PositionAssignment : TenantScopedEntity
{
    public required Guid PositionId { get; set; }
    public required Guid UserId { get; set; }
    public required DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // MOD-0288 v1 — enterprise fields (additive). Derived status (Planned|Active|Ended) is COMPUTED from
    // EffectiveFrom/To + IsCancelled — there is intentionally no stored mutable status.
    public AssignmentType AssignmentType { get; set; } = AssignmentType.Primary;
    public decimal? AllocationPercent { get; set; }
    public AssignmentReason Reason { get; set; } = AssignmentReason.Hire;
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
}
