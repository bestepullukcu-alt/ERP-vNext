using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.TimeAttendanceProviders;

public sealed class TimeAttendanceEmployeeReferenceMap : TenantScopedEntity
{
    public required Guid ProviderProfileId { get; set; }
    public required string ExternalEmployeeReference { get; set; }
    public Guid? HrisReferenceId { get; set; }
    public Guid? PersonReferenceId { get; set; }
    public Guid? OrganizationUnitReferenceId { get; set; }
    public Guid? PositionReferenceId { get; set; }
    public TimeAttendanceReferenceMappingState MappingState { get; set; } = TimeAttendanceReferenceMappingState.Unmapped;
    public DateTimeOffset? LastValidatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
