using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.PayrollIntegrationGovernance;

public sealed class PayrollIntegrationRetryReplayRequest : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public PayrollIntegrationReplayRequestType RequestType { get; set; }
    public Guid? RequestedByActorId { get; set; }
    public required string PurposeCode { get; set; }
    public required string IdempotencyKey { get; set; }
    public Guid? ApprovalWorkflowId { get; set; }
    public PayrollIntegrationReplayRequestState RequestState { get; set; } = PayrollIntegrationReplayRequestState.Requested;
    public required string RedactedReason { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
