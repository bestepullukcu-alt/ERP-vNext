using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — reusable template attached to a folder/company structure. <c>CollectionInstanceId</c> is
/// optional (a template may be company-scoped without a specific folder). Path/canonical are a read-only
/// snapshot from MOD-0028.
/// </summary>
public sealed class TemplateDocument : TenantScopedEntity
{
    public required string TemplateKey { get; set; }
    public required Guid CompanyId { get; set; }
    public required Guid OwnerCompanyId { get; set; }
    public Guid? CollectionInstanceId { get; set; }
    public string? CollectionPath { get; set; }
    public string? CanonicalId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public TemplateFlags TemplateFlags { get; set; } = new();
    public Guid? CurrentVersionId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public ControlledItemStatus Status { get; set; } = ControlledItemStatus.Active;
    public DocumentAccessPolicy AccessPolicy { get; set; } = new();

    // COPY_ON_ADOPT lineage: when this template is a copied target, points back to the source template.
    public Guid? CopiedFromTemplateId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
