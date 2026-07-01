using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — persisted share of an individual controlled document or template to a target company/legal
/// entity in the same tenant. A <c>REFERENCE</c> share makes the source's active version visible to the
/// target; a <c>COPY_ON_ADOPT</c> share also records the <see cref="CopiedItemId"/> of the independent target
/// copy. This is the explicit-share record that allows controlled cross-company access.
/// </summary>
public sealed class DocumentShareRecord : TenantScopedEntity
{
    public required Guid ShareId { get; set; }
    public SharedItemKind ItemKind { get; set; }
    public required Guid ItemId { get; set; }
    public required Guid SourceCompanyId { get; set; }
    public required Guid TargetCompanyId { get; set; }
    public DocumentShareMode ShareMode { get; set; }
    public ShareVisibilityScope VisibilityScope { get; set; } = ShareVisibilityScope.Company;
    public bool CanUse { get; set; } = true;
    public bool CanCopy { get; set; }
    public bool SourceVisibleOnUpdate { get; set; } = true;

    /// <summary>For COPY_ON_ADOPT: the id of the independent target copy created in the target company.</summary>
    public Guid? CopiedItemId { get; set; }

    /// <summary>Set when this share was produced as part of a folder/branch share operation.</summary>
    public Guid? FolderShareOperationId { get; set; }

    public required string CorrelationId { get; set; }
    public required string SharedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
