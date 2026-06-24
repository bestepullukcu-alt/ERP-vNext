using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

public sealed class InstantiationOutcome : TenantScopedEntity
{
    public required Guid OperationId { get; set; }
    public required string NodeKey { get; set; }
    public required string CanonicalId { get; set; }
    public InstantiationOutcomeStatus Status { get; set; }
    public required string ReasonCode { get; set; }
    public required string Message { get; set; }
    public bool Retryable { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
