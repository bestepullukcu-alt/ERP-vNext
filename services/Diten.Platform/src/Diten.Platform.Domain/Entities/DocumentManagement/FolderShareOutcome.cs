using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — per-item outcome of a folder/branch share operation (a folder node or an associated
/// template). Counts are honest; a partial failure records FAILED + reason_code + retryable.
/// </summary>
public sealed class FolderShareOutcome : TenantScopedEntity
{
    public required Guid OperationId { get; set; }
    public FolderShareItemType ItemType { get; set; }
    public required string ItemKey { get; set; }
    public FolderShareOutcomeStatus Status { get; set; }
    public required string ReasonCode { get; set; }
    public required string Message { get; set; }
    public bool Retryable { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
