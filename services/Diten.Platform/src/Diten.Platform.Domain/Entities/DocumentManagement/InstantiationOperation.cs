using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

public sealed class InstantiationOperation : TenantScopedEntity
{
    public required Guid OperationId { get; set; }
    public required Guid CompanyId { get; set; }
    public required Guid BaselineReleaseId { get; set; }
    public string? InstanceToken { get; set; }
    public InstantiationOperationType OperationType { get; set; }
    public InstantiationOperationStatus Status { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
    public required string CorrelationId { get; set; }
    public required string RequestedBy { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
