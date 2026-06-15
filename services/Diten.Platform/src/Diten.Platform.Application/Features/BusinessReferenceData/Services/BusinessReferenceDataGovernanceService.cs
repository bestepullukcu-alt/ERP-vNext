using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class BusinessReferenceDataGovernanceService : IBusinessReferenceDataGovernanceService
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IBusinessReferenceDataValidationService _validationService;
    private readonly IBusinessReferenceDataWorkflowAdapter _workflowAdapter;
    private readonly IBusinessReferenceDataEvidenceAdapter _evidenceAdapter;
    private readonly IBusinessReferenceDataGovernanceAuditAdapter _auditAdapter;

    public BusinessReferenceDataGovernanceService(
        IBusinessReferenceDataStewardshipRepository repository,
        IBusinessReferenceDataValidationService validationService,
        IBusinessReferenceDataWorkflowAdapter workflowAdapter,
        IBusinessReferenceDataEvidenceAdapter evidenceAdapter,
        IBusinessReferenceDataGovernanceAuditAdapter auditAdapter)
    {
        _repository = repository;
        _validationService = validationService;
        _workflowAdapter = workflowAdapter;
        _evidenceAdapter = evidenceAdapter;
        _auditAdapter = auditAdapter;
    }

    public async Task<BusinessReferenceDataVersionDetailModel> SubmitAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string? expectedConcurrencyToken,
        BusinessReferenceDataEvidenceInput evidence,
        bool overrideAction,
        string? overrideReason,
        CancellationToken ct = default)
    {
        var version = await _repository.GetVersionByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");

        if (version.Status != BusinessReferenceDataVersionStatus.Draft)
        {
            await _auditAdapter.EmitSubmitAsync(version, actorId, correlationId, "N/A", false, "draft_required", ct);
            throw new InvalidOperationException("draft_required");
        }

        var validation = await _validationService.ValidateDraftVersionAsync(versionId, correlationId, ct);
        if (validation.BlockingErrorCount > 0)
        {
            await _auditAdapter.EmitSubmitAsync(version, actorId, correlationId, "N/A", false, "validation_blockers", ct);
            throw new InvalidOperationException("validation_blockers");
        }

        var evidenceCheck = await _evidenceAdapter.CheckEvidenceAsync(version, evidence, "submit", correlationId, ct);
        await EmitEvidenceAuditAsync(version, actorId, correlationId, evidenceCheck, "submit", ct);
        if (!evidenceCheck.HasRequiredEvidence)
        {
            throw new InvalidOperationException(evidenceCheck.ReasonCode ?? "evidence_required");
        }

        if (overrideAction)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new InvalidOperationException("override_reason_required");
            }

            version.IsOverrideAction = true;
            version.OverrideReason = overrideReason.Trim();
            await _auditAdapter.EmitOverrideAsync(version, actorId, correlationId, version.OverrideReason, ct);
        }

        var templateCode = SelectWorkflowTemplate(version);
        BusinessReferenceDataWorkflowLaunchResult workflowLaunch;
        try
        {
            workflowLaunch = await _workflowAdapter.LaunchApprovalWorkflowAsync(version, templateCode, actorId, correlationId, ct);
            await _auditAdapter.EmitWorkflowStartAsync(version, actorId, correlationId, templateCode, workflowLaunch.WorkflowInstanceId, workflowLaunch.WorkflowState, true, null, ct);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            await _auditAdapter.EmitWorkflowStartAsync(version, actorId, correlationId, templateCode, null, null, false, "workflow_start_failed", ct);
            await _auditAdapter.EmitSubmitAsync(version, actorId, correlationId, templateCode, false, "workflow_start_failed", ct);
            throw new InvalidOperationException("workflow_start_failed", ex);
        }

        version.BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Submitted;
        version.BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.Pending;
        version.IsEditable = false;
        version.SubmittedAt = DateTimeOffset.UtcNow;
        version.SubmittedBy = actorId;
        version.WorkflowTemplateCode = workflowLaunch.WorkflowTemplateCode;
        version.WorkflowInstanceId = workflowLaunch.WorkflowInstanceId;
        version.WorkflowState = workflowLaunch.WorkflowState;
        ApplyEvidenceDecision(version, evidenceCheck);

        var etag = string.IsNullOrWhiteSpace(expectedConcurrencyToken) ? version.ConcurrencyToken : expectedConcurrencyToken.Trim();
        var updated = await _repository.UpdateVersionAsync(version, etag, ct);
        if (!updated)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        await _auditAdapter.EmitSubmitAsync(version, actorId, correlationId, templateCode, true, null, ct);
        return BusinessReferenceDataModelMapper.ToVersionDetail(version);
    }

    public async Task<BusinessReferenceDataVersionDetailModel> ApproveAsync(
        Guid versionId,
        string actorId,
        string correlationId,
        string? expectedConcurrencyToken,
        BusinessReferenceDataWorkflowTransitionAction action,
        string? rejectionReason,
        bool overrideAction,
        string? overrideReason,
        BusinessReferenceDataEvidenceInput evidence,
        string? requestInfoComment,
        string? requestInfoTargetStep,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var version = await _repository.GetVersionByIdAsync(versionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");

        var transitionActorId = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId.Trim();
        var approved = action == BusinessReferenceDataWorkflowTransitionAction.Approve;
        if (approved
            && !string.IsNullOrWhiteSpace(version.SubmittedBy)
            && string.Equals(transitionActorId, version.SubmittedBy.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, version.WorkflowInstanceId, idempotencyKey, "sod_submitter_cannot_approve", ct);
            throw new InvalidOperationException("sod_submitter_cannot_approve");
        }

        if (action == BusinessReferenceDataWorkflowTransitionAction.Reject && string.IsNullOrWhiteSpace(rejectionReason))
        {
            await _auditAdapter.EmitApprovalDecisionAsync(version, transitionActorId, correlationId, false, rejectionReason, false, "rejection_reason_required", ct);
            throw new InvalidOperationException("rejection_reason_required");
        }

        if (action == BusinessReferenceDataWorkflowTransitionAction.RequestInfo)
        {
            if (string.IsNullOrWhiteSpace(requestInfoComment))
            {
                await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, version.WorkflowInstanceId, idempotencyKey, "request_info_comment_required", ct);
                throw new InvalidOperationException("request_info_comment_required");
            }

            if (string.IsNullOrWhiteSpace(requestInfoTargetStep))
            {
                await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, version.WorkflowInstanceId, idempotencyKey, "request_info_target_step_required", ct);
                throw new InvalidOperationException("request_info_target_step_required");
            }
        }

        BusinessReferenceDataEvidenceCheckResult? evidenceCheck = null;
        if (action is BusinessReferenceDataWorkflowTransitionAction.Approve or BusinessReferenceDataWorkflowTransitionAction.Reject)
        {
            evidenceCheck = await _evidenceAdapter.CheckEvidenceAsync(version, evidence, ResolveEvidenceActionCode(action), correlationId, ct);
            await EmitEvidenceAuditAsync(version, transitionActorId, correlationId, evidenceCheck, ResolveEvidenceActionCode(action), ct);
            if (!evidenceCheck.HasRequiredEvidence)
            {
                throw new InvalidOperationException(evidenceCheck.ReasonCode ?? "evidence_required");
            }
        }

        if (overrideAction)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new InvalidOperationException("override_reason_required");
            }

            version.IsOverrideAction = true;
            version.OverrideReason = overrideReason.Trim();
            await _auditAdapter.EmitOverrideAsync(version, transitionActorId, correlationId, version.OverrideReason, ct);
        }

        if (!version.WorkflowInstanceId.HasValue)
        {
            await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, null, idempotencyKey, "workflow_instance_missing", ct);
            throw new InvalidOperationException("workflow_instance_missing");
        }

        var workflowInstanceId = version.WorkflowInstanceId.Value;
        var effectiveIdempotencyKey = ResolveTransitionIdempotencyKey(idempotencyKey, version.BusinessReferenceDataVersionId, action, transitionActorId, correlationId);
        await _auditAdapter.EmitWorkflowTransitionRequestedAsync(version, transitionActorId, correlationId, action, workflowInstanceId, effectiveIdempotencyKey, ct);

        BusinessReferenceDataWorkflowTransitionResult transition;
        try
        {
            transition = await _workflowAdapter.TransitionApprovalTaskAsync(
                workflowInstanceId,
                action,
                transitionActorId,
                ResolveReasonCode(action),
                effectiveIdempotencyKey,
                action == BusinessReferenceDataWorkflowTransitionAction.RequestInfo ? requestInfoComment : rejectionReason,
                evidenceCheck?.EffectiveEvidenceRef,
                requestInfoTargetStep,
                correlationId,
                ct);

            await _auditAdapter.EmitWorkflowTransitionSucceededAsync(version, transitionActorId, correlationId, transition, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, workflowInstanceId, effectiveIdempotencyKey, "workflow_transition_forbidden", ct);
            throw new UnauthorizedAccessException("workflow_transition_forbidden", ex);
        }
        catch (InvalidOperationException ex)
        {
            var reason = string.IsNullOrWhiteSpace(ex.Message) ? "workflow_transition_failed" : ex.Message;
            await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, workflowInstanceId, effectiveIdempotencyKey, reason, ct);
            throw;
        }

        if (transition.IsIdempotent && IsAlreadySynced(version, action))
        {
            await _auditAdapter.EmitWorkflowSyncAppliedAsync(version, transitionActorId, correlationId, transition, ct);
            return BusinessReferenceDataModelMapper.ToVersionDetail(version);
        }

        if (action is BusinessReferenceDataWorkflowTransitionAction.Approve or BusinessReferenceDataWorkflowTransitionAction.Reject)
        {
            version.DecisionAt = DateTimeOffset.UtcNow;
            version.DecisionBy = transitionActorId;
            if (evidenceCheck is not null)
            {
                ApplyEvidenceDecision(version, evidenceCheck);
            }

            if (approved)
            {
                version.BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Approved;
                version.BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.Approved;
                version.ApprovedAt = DateTimeOffset.UtcNow;
                version.IsEditable = false;
                version.RejectionReason = null;
            }
            else
            {
                version.BusinessReferenceDataGovernanceState = BusinessReferenceDataGovernanceState.Rejected;
                version.BusinessReferenceDataApprovalState = BusinessReferenceDataApprovalState.Rejected;
                version.RejectionReason = rejectionReason?.Trim();
                version.IsEditable = true;
                version.Status = BusinessReferenceDataVersionStatus.Draft; // editable rework state
            }
        }

        version.WorkflowState = transition.InstanceStatus;

        var etag = string.IsNullOrWhiteSpace(expectedConcurrencyToken) ? version.ConcurrencyToken : expectedConcurrencyToken.Trim();
        var updated = await _repository.UpdateVersionAsync(version, etag, ct);
        if (!updated)
        {
            await _auditAdapter.EmitApprovalDecisionAsync(version, transitionActorId, correlationId, approved, rejectionReason, false, "concurrency_conflict", ct);
            await _auditAdapter.EmitWorkflowTransitionFailedAsync(version, transitionActorId, correlationId, action, workflowInstanceId, effectiveIdempotencyKey, "workflow_sync_concurrency_conflict", ct);
            throw new InvalidOperationException("concurrency_conflict");
        }

        await _auditAdapter.EmitWorkflowSyncAppliedAsync(version, transitionActorId, correlationId, transition, ct);
        if (action is BusinessReferenceDataWorkflowTransitionAction.Approve or BusinessReferenceDataWorkflowTransitionAction.Reject)
        {
            await _auditAdapter.EmitApprovalDecisionAsync(version, transitionActorId, correlationId, approved, rejectionReason, true, null, ct);
        }

        return BusinessReferenceDataModelMapper.ToVersionDetail(version);
    }

    private static string ResolveTransitionIdempotencyKey(
        string? idempotencyKey,
        Guid versionId,
        BusinessReferenceDataWorkflowTransitionAction action,
        string actorId,
        string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return idempotencyKey.Trim();
        }

        var correlationPart = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();
        return $"BusinessReferenceData:{versionId:D}:{action.ToString().ToLowerInvariant()}:{actorId}:{correlationPart}";
    }

    private static string ResolveReasonCode(BusinessReferenceDataWorkflowTransitionAction action)
        => action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "APPROVED",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "REJECTED",
            BusinessReferenceDataWorkflowTransitionAction.RequestInfo => "REQUEST_INFO",
            _ => "WORKFLOW_TRANSITION"
        };

    private static string ResolveEvidenceActionCode(BusinessReferenceDataWorkflowTransitionAction action)
        => action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "approve",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "reject",
            BusinessReferenceDataWorkflowTransitionAction.RequestInfo => "request_info",
            _ => "workflow_transition"
        };

    private async Task EmitEvidenceAuditAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        BusinessReferenceDataEvidenceCheckResult evidenceCheck,
        string actionCode,
        CancellationToken ct)
    {
        await _auditAdapter.EmitEvidenceCheckAsync(version, actorId, correlationId, evidenceCheck, ct);
        await _auditAdapter.EmitEvidenceRequirementEvaluatedAsync(version, actorId, correlationId, evidenceCheck, ct);
        if (!string.IsNullOrWhiteSpace(evidenceCheck.DocumentVersionId))
        {
            await _auditAdapter.EmitEvidenceArtifactValidatedAsync(version, actorId, correlationId, evidenceCheck, ct);
        }

        if (!evidenceCheck.HasRequiredEvidence)
        {
            await _auditAdapter.EmitEvidenceUnsatisfiedAsync(version, actorId, correlationId, evidenceCheck, actionCode, ct);
        }
    }

    internal static void ApplyEvidenceDecision(BusinessReferenceDataVersion version, BusinessReferenceDataEvidenceCheckResult evidenceCheck)
    {
        version.LastEvidenceRef = evidenceCheck.EffectiveEvidenceRef ?? version.LastEvidenceRef;
        version.EvidenceLinkId = evidenceCheck.EvidenceLinkId ?? version.EvidenceLinkId;
        version.EvidenceEvaluationId = evidenceCheck.EvaluationId ?? version.EvidenceEvaluationId;
        version.EvidenceDocumentVersionId = evidenceCheck.DocumentVersionId ?? version.EvidenceDocumentVersionId;
        version.EvidenceRequirementCode = evidenceCheck.RequirementCode ?? version.EvidenceRequirementCode;
        version.EvidenceDecisionCode = evidenceCheck.DecisionCode ?? version.EvidenceDecisionCode;
        version.EvidenceReasonCode = evidenceCheck.ReasonCode ?? evidenceCheck.ArtifactReasonCode ?? version.EvidenceReasonCode;
        version.EvidenceAttached = evidenceCheck.HasRequiredEvidence || version.EvidenceAttached;
    }

    private static bool IsAlreadySynced(BusinessReferenceDataVersion version, BusinessReferenceDataWorkflowTransitionAction action)
        => action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => version.BusinessReferenceDataGovernanceState == BusinessReferenceDataGovernanceState.Approved && version.BusinessReferenceDataApprovalState == BusinessReferenceDataApprovalState.Approved,
            BusinessReferenceDataWorkflowTransitionAction.Reject => version.BusinessReferenceDataGovernanceState == BusinessReferenceDataGovernanceState.Rejected && version.BusinessReferenceDataApprovalState == BusinessReferenceDataApprovalState.Rejected,
            BusinessReferenceDataWorkflowTransitionAction.RequestInfo => version.BusinessReferenceDataGovernanceState == BusinessReferenceDataGovernanceState.Submitted && version.BusinessReferenceDataApprovalState == BusinessReferenceDataApprovalState.Pending,
            _ => false
        };

    private static string SelectWorkflowTemplate(BusinessReferenceDataVersion version)
    {
        if (version.Status == BusinessReferenceDataVersionStatus.Retired)
        {
            return "REFDATA_RETIREMENT_APPROVAL";
        }

        if (version.RequiresEvidence || version.RequiresApproval)
        {
            return "REFDATA_COMPLIANCE_APPROVAL";
        }

        return "REFDATA_STANDARD_APPROVAL";
    }
}
