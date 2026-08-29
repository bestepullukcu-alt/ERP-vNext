using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — logical controlled document attached to a MOD-0028-FU05 <c>CollectionInstance</c> folder
/// node (consumed read-only). The <see cref="CollectionPath"/>/<see cref="CanonicalId"/> are a read-only
/// snapshot copied at attach time; FU01 never derives or edits folder hierarchy.
/// </summary>
public sealed class ControlledDocument : TenantScopedEntity
{
    public required string DocumentKey { get; set; }
    public DocumentScope DocumentScope { get; set; } = DocumentScope.Company;
    public Guid ScopeOwnerId { get; set; }
    [BsonIgnoreIfDefault]
    public Guid CorporateOwnerId { get; set; }
    [BsonIgnoreIfDefault]
    public Guid CompanyId { get; set; }
    [BsonIgnoreIfDefault]
    public Guid OwnerCompanyId { get; set; }
    public required Guid CollectionInstanceId { get; set; }
    public Guid FolderId { get; set; }
    public string? StoragePartition { get; set; }
    public string? GovernanceOwnerFunction { get; set; }
    public string? GovernanceOwnerRole { get; set; }
    public Guid? GovernanceOwnerUserId { get; set; }
    public required string CollectionPath { get; set; }
    public string? CanonicalId { get; set; }
    public required string Title { get; set; }
    public DocumentType DocumentType { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool Controlled { get; set; } = true;
    public DateTimeOffset? EffectiveDate { get; set; }
    public DateTimeOffset? ReviewDate { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public ControlledItemStatus Status { get; set; } = ControlledItemStatus.Active;
    public DocumentAccessPolicy AccessPolicy { get; set; } = new();

    // COPY_ON_ADOPT lineage: when this document is a copied target, points back to the source document.
    public Guid? CopiedFromDocumentId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
