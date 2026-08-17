using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollSources;

public sealed class PayrollEmployeeReferenceMap : TenantScopedEntity
{
    public required Guid PayrollExternalSystemProfileId { get; set; }
    public required string ExternalEmployeeReference { get; set; }
    public Guid? PersonReferenceId { get; set; }
    public Guid? OrganizationUnitReferenceId { get; set; }
    public Guid? PositionReferenceId { get; set; }
    public PayrollReferenceMappingState MappingState { get; set; } = PayrollReferenceMappingState.Unmapped;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
