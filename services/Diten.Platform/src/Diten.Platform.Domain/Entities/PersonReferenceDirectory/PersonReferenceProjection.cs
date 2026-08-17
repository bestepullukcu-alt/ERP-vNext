using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PersonReferenceDirectory;

public sealed class PersonReferenceProjection : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string ReferenceDisplayName { get; set; }
    public Guid HrisSourceProfileId { get; set; }
    public Guid? PrimaryExternalCorrelationId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public PersonReferenceState ReferenceState { get; set; } = PersonReferenceState.Deferred;
    public required string SourceContractVersion { get; set; }
    public required string CorrelationKey { get; set; }
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? ValidationFailureReason { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
