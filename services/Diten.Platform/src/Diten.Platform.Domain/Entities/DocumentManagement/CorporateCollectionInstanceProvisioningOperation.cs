using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

public sealed class CorporateCollectionInstanceProvisioningOperation : TenantScopedEntity
{
    public required string IdempotencyKey { get; set; }
    public required Guid BaselineReleaseId { get; set; }
    public required Guid CorporateOwnerId { get; set; }
    public CollectionScopeType ScopeType { get; set; } = CollectionScopeType.Corporate;
    public required Guid ScopeOwnerId { get; set; }
    public CorporateCollectionProvisioningStatus Status { get; set; } = CorporateCollectionProvisioningStatus.Pending;
    public Guid? CollectionInstanceId { get; set; }
    public string? FailureReasonCode { get; set; }
    public string? FailureDetail { get; set; }
    public int AttemptCount { get; set; } = 1;
    public DateTimeOffset LastAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public required string CorrelationId { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
