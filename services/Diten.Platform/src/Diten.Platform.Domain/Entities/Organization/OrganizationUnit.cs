using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class OrganizationUnit : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid LegalEntityId { get; set; }
    public Guid? ParentOrganizationUnitId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
