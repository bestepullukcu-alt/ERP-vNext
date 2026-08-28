using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0028-FU05 company-scoped instance node created from a PUBLISHED baseline definition.
/// Metadata only: no physical folder, binary storage, or document lifecycle side effect.
/// </summary>
public sealed class CollectionInstance : TenantScopedEntity
{
    public required string InstanceKey { get; set; }
    [BsonIgnoreIfDefault]
    public Guid CompanyId { get; set; }
    public Guid ScopeOwnerId { get; set; }
    [BsonIgnoreIfDefault]
    public Guid CorporateOwnerId { get; set; }
    public required Guid BaselineReleaseId { get; set; }
    public required string CanonicalId { get; set; }
    public string? ParentCanonicalId { get; set; }
    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public int DisplayOrder { get; set; }
    public CollectionScopeType CollectionScopeType { get; set; } = CollectionScopeType.Company;
    public CollectionInstanceStatus InstanceStatus { get; set; } = CollectionInstanceStatus.Active;
    public List<ScopeBinding> ScopeBindings { get; set; } = [];
    public string? InstanceToken { get; set; }
    public Guid? ProvisionedFromBaselineReleaseId { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }
    public string? ProvisionedBy { get; set; }
    public Guid? ProvisioningOperationId { get; set; }
    public string? StoragePartition { get; set; }
    public required string SourceDefinitionHash { get; set; }
    public DateTimeOffset LastChangeAt { get; set; } = DateTimeOffset.UtcNow;
    public int VersionToken { get; set; } = 1;
    public DateTimeOffset? DeletedAt { get; set; }
}
