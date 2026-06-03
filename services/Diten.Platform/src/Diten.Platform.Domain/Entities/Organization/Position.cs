using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class Position : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid OrganizationUnitId { get; set; }
    public Guid? ReportsToPositionId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
