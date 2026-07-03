using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 — a folder/branch dry-run or execute share operation record. The same flow
/// <see cref="CorrelationId"/> threads dry-run → execute.
/// </summary>
public sealed class FolderShareOperation : TenantScopedEntity
{
    public required Guid OperationId { get; set; }
    public required Guid SourceCompanyId { get; set; }
    public required Guid TargetCompanyId { get; set; }
    public required Guid SourceBranchCollectionInstanceId { get; set; }
    public bool IncludeTemplates { get; set; }
    public DocumentShareMode ShareMode { get; set; }
    public FolderShareOperationType OperationType { get; set; }
    public FolderShareStatus Status { get; set; }
    public int FoldersIncluded { get; set; }
    public int TemplatesIncluded { get; set; }
    public int TemplatesSkipped { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
    public required string CorrelationId { get; set; }
    public required string RequestedBy { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
