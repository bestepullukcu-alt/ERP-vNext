using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.HrisSources;

public sealed class HrisExternalIdentifierMap : TenantScopedEntity
{
    public required Guid SourceProfileId { get; set; }
    public HrisExternalObjectType ExternalObjectType { get; set; }
    public required string ExternalObjectId { get; set; }
    public HrisInternalReferenceType InternalReferenceType { get; set; }
    public Guid? InternalReferenceId { get; set; }
    public HrisMappingState MappingState { get; set; } = HrisMappingState.Unmapped;
    public string? ProvenanceHash { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
