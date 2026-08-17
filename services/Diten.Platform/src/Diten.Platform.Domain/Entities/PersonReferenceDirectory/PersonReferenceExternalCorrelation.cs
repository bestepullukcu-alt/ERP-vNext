using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PersonReferenceDirectory;

public sealed class PersonReferenceExternalCorrelation : TenantScopedEntity
{
    public Guid PersonReferenceProjectionId { get; set; }
    public Guid HrisSourceProfileId { get; set; }
    public PersonReferenceExternalObjectType ExternalObjectType { get; set; } = PersonReferenceExternalObjectType.Employee;
    public required string ExternalObjectReference { get; set; }
    public required string CorrelationKey { get; set; }
    public PersonReferenceCorrelationState CorrelationState { get; set; } = PersonReferenceCorrelationState.Deferred;
    public required string SourceContractVersion { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
