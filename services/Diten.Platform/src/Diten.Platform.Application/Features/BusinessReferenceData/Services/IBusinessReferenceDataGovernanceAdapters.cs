using Diten.Platform.Domain.Entities;
using Diten.Platform.Application.Contracts.Events;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public interface IBusinessReferenceDataWorkflowAdapter
{
    Task<BusinessReferenceDataWorkflowLaunchResult> LaunchApprovalWorkflowAsync(
        BusinessReferenceDataVersion version,
        string workflowTemplateCode,
        string actorId,
        string correlationId,
        CancellationToken ct = default);

    Task<BusinessReferenceDataWorkflowTransitionResult> TransitionApprovalTaskAsync(
        Guid workflowInstanceId,
        BusinessReferenceDataWorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string? targetStep,
        string correlationId,
        CancellationToken ct = default);
}

public interface IBusinessReferenceDataEvidenceAdapter
{
    Task<BusinessReferenceDataEvidenceCheckResult> CheckEvidenceAsync(
        BusinessReferenceDataVersion version,
        BusinessReferenceDataEvidenceInput evidence,
        string actionCode,
        string correlationId,
        CancellationToken ct = default);
}

public interface IBusinessReferenceDataGovernanceAuditAdapter
{
    Task EmitSubmitAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, bool success, string? reasonCode, CancellationToken ct = default);
    Task EmitWorkflowStartAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, Guid? workflowInstanceId, string? workflowState, bool success, string? reasonCode, CancellationToken ct = default);
    Task EmitWorkflowTransitionRequestedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid workflowInstanceId, string idempotencyKey, CancellationToken ct = default);
    Task EmitWorkflowTransitionSucceededAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default);
    Task EmitWorkflowTransitionFailedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid? workflowInstanceId, string? idempotencyKey, string reasonCode, CancellationToken ct = default);
    Task EmitWorkflowSyncAppliedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default);
    Task EmitEvidenceCheckAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default);
    Task EmitEvidenceRequirementEvaluatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default);
    Task EmitEvidenceArtifactValidatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default);
    Task EmitEvidenceUnsatisfiedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, string actionCode, CancellationToken ct = default);
    Task EmitApprovalDecisionAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool approved, string? rejectionReason, bool success, string? reasonCode, CancellationToken ct = default);
    Task EmitOverrideAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default);
    Task EmitPublishAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool success, string? reasonCode, string publishMode, CancellationToken ct = default);
}

public interface IBusinessReferenceDataEventPublisher
{
    Task PublishAsync(EventEnvelope envelope, string idempotencyKey, Guid versionId, CancellationToken ct = default);
}

public interface IBusinessReferenceDataPostPublicationReviewHook
{
    Task TriggerAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default);
}

public sealed record BusinessReferenceDataWorkflowLaunchResult(
    Guid WorkflowInstanceId,
    string WorkflowTemplateCode,
    string WorkflowState,
    bool IsMocked);

public enum BusinessReferenceDataWorkflowTransitionAction
{
    Approve,
    Reject,
    RequestInfo
}

public sealed record BusinessReferenceDataWorkflowTransitionResult(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    BusinessReferenceDataWorkflowTransitionAction Action,
    string TaskStatus,
    string InstanceStatus,
    bool IsIdempotent,
    string ReasonCode,
    string IdempotencyKey,
    DateTimeOffset TransitionedAt);

public sealed record BusinessReferenceDataEvidenceCheckResult(
    bool HasRequiredEvidence,
    string? ReasonCode,
    string? EffectiveEvidenceRef,
    string? EvidenceLinkId = null,
    Guid? EvaluationId = null,
    string? DocumentVersionId = null,
    string? RequirementCode = null,
    string? DecisionCode = null,
    string? ArtifactDecisionCode = null,
    string? ArtifactReasonCode = null);

public sealed record BusinessReferenceDataEvidenceInput(
    string? EvidenceRef,
    string? EvidenceLinkId,
    string? DocumentVersionId,
    string? RequirementCode)
{
    public static BusinessReferenceDataEvidenceInput FromLegacy(string? evidenceRef)
        => new(evidenceRef, null, null, null);

    public static BusinessReferenceDataEvidenceInput FromPersisted(BusinessReferenceDataVersion version)
        => new(
            version.LastEvidenceRef,
            version.EvidenceLinkId,
            version.EvidenceDocumentVersionId,
            version.EvidenceRequirementCode);
}
