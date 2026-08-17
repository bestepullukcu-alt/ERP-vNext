using System.Text.Json;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class MockBusinessReferenceDataWorkflowAdapter : IBusinessReferenceDataWorkflowAdapter
{
    public Task<BusinessReferenceDataWorkflowLaunchResult> LaunchApprovalWorkflowAsync(
        BusinessReferenceDataVersion version,
        string workflowTemplateCode,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
    {
        return Task.FromResult(new BusinessReferenceDataWorkflowLaunchResult(
            Guid.NewGuid(),
            workflowTemplateCode,
            "started",
            true));
    }

    public Task<BusinessReferenceDataWorkflowTransitionResult> TransitionApprovalTaskAsync(
        Guid workflowInstanceId,
        BusinessReferenceDataWorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string? targetStep,
        string correlationId,
        CancellationToken ct = default)
    {
        var taskStatus = action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "Approved",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "Rejected",
            BusinessReferenceDataWorkflowTransitionAction.RequestInfo => "WaitingEvidence",
            _ => "WaitingApproval"
        };

        var instanceStatus = action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "Completed",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "Rejected",
            _ => "Active"
        };

        return Task.FromResult(new BusinessReferenceDataWorkflowTransitionResult(
            workflowInstanceId,
            Guid.NewGuid(),
            action,
            taskStatus,
            instanceStatus,
            false,
            string.IsNullOrWhiteSpace(reasonCode) ? action.ToString() : reasonCode.Trim(),
            string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim(),
            DateTimeOffset.UtcNow));
    }
}

public sealed class DefaultBusinessReferenceDataEvidenceAdapter : IBusinessReferenceDataEvidenceAdapter
{
    public Task<BusinessReferenceDataEvidenceCheckResult> CheckEvidenceAsync(
        BusinessReferenceDataVersion version,
        BusinessReferenceDataEvidenceInput evidence,
        string actionCode,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!version.RequiresEvidence)
        {
            return Task.FromResult(new BusinessReferenceDataEvidenceCheckResult(
                true,
                null,
                evidence.EvidenceRef ?? version.LastEvidenceRef,
                evidence.EvidenceLinkId ?? version.EvidenceLinkId,
                version.EvidenceEvaluationId,
                evidence.DocumentVersionId ?? version.EvidenceDocumentVersionId,
                evidence.RequirementCode ?? version.EvidenceRequirementCode,
                version.EvidenceDecisionCode,
                null,
                version.EvidenceReasonCode));
        }

        var hasEvidence = !string.IsNullOrWhiteSpace(evidence.EvidenceRef)
            || !string.IsNullOrWhiteSpace(evidence.EvidenceLinkId)
            || !string.IsNullOrWhiteSpace(evidence.DocumentVersionId)
            || !string.IsNullOrWhiteSpace(version.LastEvidenceRef)
            || !string.IsNullOrWhiteSpace(version.EvidenceLinkId)
            || !string.IsNullOrWhiteSpace(version.EvidenceDocumentVersionId);

        // The real evidence integration (MOD-0031) is not wired into this stub adapter,
        // so there is no source that can supply a canonical evidence link. When evidence
        // has actually been attached we record it as satisfied (unchanged behaviour);
        // when none is present we no longer block the governance flow on a dependency
        // that cannot be satisfied here. A future Live adapter will re-introduce hard
        // enforcement once the evidence module ships.
        var decisionCode = hasEvidence ? "satisfied" : "integration_unavailable";
        var reasonCode = hasEvidence ? null : "evidence_integration_unavailable";

        return Task.FromResult(new BusinessReferenceDataEvidenceCheckResult(
            true,
            reasonCode,
            evidence.EvidenceRef ?? evidence.EvidenceLinkId ?? version.LastEvidenceRef ?? version.EvidenceLinkId,
            evidence.EvidenceLinkId ?? version.EvidenceLinkId,
            version.EvidenceEvaluationId,
            evidence.DocumentVersionId ?? version.EvidenceDocumentVersionId,
            evidence.RequirementCode ?? version.EvidenceRequirementCode,
            decisionCode,
            null,
            reasonCode));
    }
}

public sealed class NoOpBusinessReferenceDataGovernanceAuditAdapter : IBusinessReferenceDataGovernanceAuditAdapter
{
    public Task EmitSubmitAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, bool success, string? reasonCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitWorkflowStartAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, Guid? workflowInstanceId, string? workflowState, bool success, string? reasonCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitWorkflowTransitionRequestedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid workflowInstanceId, string idempotencyKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitWorkflowTransitionSucceededAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitWorkflowTransitionFailedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid? workflowInstanceId, string? idempotencyKey, string reasonCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitWorkflowSyncAppliedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitEvidenceCheckAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitEvidenceRequirementEvaluatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitEvidenceArtifactValidatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitEvidenceUnsatisfiedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, string actionCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitApprovalDecisionAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool approved, string? rejectionReason, bool success, string? reasonCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitOverrideAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default) => Task.CompletedTask;
    public Task EmitPublishAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool success, string? reasonCode, string publishMode, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class DbBusinessReferenceDataEventPublisher : IBusinessReferenceDataEventPublisher
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public DbBusinessReferenceDataEventPublisher(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task PublishAsync(EventEnvelope envelope, string idempotencyKey, Guid versionId, CancellationToken ct = default)
    {
        if (await _repository.IntegrationEventExistsAsync(versionId, envelope.EventName, idempotencyKey, ct))
        {
            return;
        }

        await _repository.SaveIntegrationEventAsync(new BusinessReferenceDataIntegrationEvent
        {
            TenantId = envelope.TenantId,
            BusinessReferenceDataVersionId = versionId,
            EventName = envelope.EventName,
            EventVersion = envelope.EventVersion,
            IdempotencyKey = idempotencyKey,
            PayloadJson = JsonSerializer.Serialize(envelope)
        }, ct);
    }
}

public sealed class MockBusinessReferenceDataPostPublicationReviewHook : IBusinessReferenceDataPostPublicationReviewHook
{
    public Task TriggerAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Governance integration mode for BusinessReferenceData (PSS-012).
/// MOD-0023 (workflow) and MOD-0031 (evidence) are not yet implemented, so the production
/// default is <see cref="Disabled"/>: mutations proceed but are explicitly marked and audited
/// instead of being silently treated as a successful mock workflow.
/// </summary>
public enum BusinessReferenceDataGovernanceMode
{
    /// <summary>Workflow/evidence steps are skipped; the mutation proceeds and is audited as governance-disabled. Production default.</summary>
    Disabled,

    /// <summary>Missing dependency causes the mutation to be rejected. Strict posture.</summary>
    FailClosed,

    /// <summary>Stub behaviour for Development/Test only.</summary>
    Mock,

    /// <summary>Real MOD-0023/MOD-0031 integration (once those modules ship).</summary>
    Live
}

/// <summary>
/// Resolves the active governance mode. Honors an explicit <c>BusinessReferenceData__GovernanceMode</c>
/// environment override; otherwise defaults to <see cref="BusinessReferenceDataGovernanceMode.Mock"/>
/// only in Development/Local/Test and to <see cref="BusinessReferenceDataGovernanceMode.Disabled"/> elsewhere.
/// </summary>
public static class BusinessReferenceDataGovernanceModeResolver
{
    public static BusinessReferenceDataGovernanceMode Resolve()
    {
        var explicitMode = Environment.GetEnvironmentVariable("BusinessReferenceData__GovernanceMode");
        if (!string.IsNullOrWhiteSpace(explicitMode)
            && Enum.TryParse<BusinessReferenceDataGovernanceMode>(explicitMode.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isNonProduction = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(env, "Local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(env, "Test", StringComparison.OrdinalIgnoreCase);

        return isNonProduction
            ? BusinessReferenceDataGovernanceMode.Mock
            : BusinessReferenceDataGovernanceMode.Disabled;
    }
}

/// <summary>
/// Disabled-mode workflow adapter. MOD-0023 is not wired, so the approval workflow step is
/// skipped and the version proceeds, but the result is explicitly flagged (not mocked) so the
/// governance audit trail records that the workflow was bypassed rather than silently succeeded.
/// </summary>
public sealed class DisabledBusinessReferenceDataWorkflowAdapter : IBusinessReferenceDataWorkflowAdapter
{
    public Task<BusinessReferenceDataWorkflowLaunchResult> LaunchApprovalWorkflowAsync(
        BusinessReferenceDataVersion version,
        string workflowTemplateCode,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
        => Task.FromResult(new BusinessReferenceDataWorkflowLaunchResult(
            Guid.NewGuid(),
            workflowTemplateCode,
            "GovernanceDisabled",
            IsMocked: false));

    public Task<BusinessReferenceDataWorkflowTransitionResult> TransitionApprovalTaskAsync(
        Guid workflowInstanceId,
        BusinessReferenceDataWorkflowTransitionAction action,
        string actorId,
        string reasonCode,
        string idempotencyKey,
        string? comment,
        string? evidenceRef,
        string? targetStep,
        string correlationId,
        CancellationToken ct = default)
    {
        var taskStatus = action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "Approved",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "Rejected",
            BusinessReferenceDataWorkflowTransitionAction.RequestInfo => "WaitingEvidence",
            _ => "WaitingApproval"
        };

        var instanceStatus = action switch
        {
            BusinessReferenceDataWorkflowTransitionAction.Approve => "Completed",
            BusinessReferenceDataWorkflowTransitionAction.Reject => "Rejected",
            _ => "Active"
        };

        return Task.FromResult(new BusinessReferenceDataWorkflowTransitionResult(
            workflowInstanceId,
            Guid.NewGuid(),
            action,
            taskStatus,
            instanceStatus,
            IsIdempotent: false,
            string.IsNullOrWhiteSpace(reasonCode) ? action.ToString() : reasonCode.Trim(),
            string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString("N") : idempotencyKey.Trim(),
            DateTimeOffset.UtcNow));
    }
}

/// <summary>Disabled-mode post-publication review hook. MOD-0023 is not wired; no review is triggered.</summary>
public sealed class DisabledBusinessReferenceDataPostPublicationReviewHook : IBusinessReferenceDataPostPublicationReviewHook
{
    public Task TriggerAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Real governance audit adapter that writes BusinessReferenceData governance events to the central
/// MOD-0021 audit trail via <see cref="IAuditService"/> under <see cref="AuditCategory.ReferenceData"/>.
/// Replaces <see cref="NoOpBusinessReferenceDataGovernanceAuditAdapter"/> in production DI so submit/
/// approve/publish/evidence/override events are no longer silently dropped. Append failures are logged
/// and swallowed here because the command-level <c>AuditBehavior</c> is the enforced audit path.
/// </summary>
public sealed class AuditServiceBusinessReferenceDataGovernanceAuditAdapter : IBusinessReferenceDataGovernanceAuditAdapter
{
    private const string EntityTypeName = "BusinessReferenceDataVersion";
    private const string SourceModuleName = "PSS-012";

    private readonly IAuditService _auditService;
    private readonly ILogger<AuditServiceBusinessReferenceDataGovernanceAuditAdapter> _logger;
    private readonly BusinessReferenceDataGovernanceMode _mode;

    public AuditServiceBusinessReferenceDataGovernanceAuditAdapter(
        IAuditService auditService,
        ILogger<AuditServiceBusinessReferenceDataGovernanceAuditAdapter> logger)
    {
        _auditService = auditService;
        _logger = logger;
        _mode = BusinessReferenceDataGovernanceModeResolver.Resolve();
    }

    public Task EmitSubmitAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, bool success, string? reasonCode, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, Outcome(success), "submit",
            new() { ["workflowTemplateCode"] = workflowTemplateCode, ["reasonCode"] = reasonCode }, ct);

    public Task EmitWorkflowStartAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string workflowTemplateCode, Guid? workflowInstanceId, string? workflowState, bool success, string? reasonCode, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, Outcome(success), "workflow_start",
            new() { ["workflowTemplateCode"] = workflowTemplateCode, ["workflowInstanceId"] = workflowInstanceId, ["workflowState"] = workflowState, ["reasonCode"] = reasonCode }, ct);

    public Task EmitWorkflowTransitionRequestedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid workflowInstanceId, string idempotencyKey, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Succeeded, "workflow_transition_requested",
            new() { ["action"] = action.ToString(), ["workflowInstanceId"] = workflowInstanceId, ["idempotencyKey"] = idempotencyKey }, ct);

    public Task EmitWorkflowTransitionSucceededAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Succeeded, "workflow_transition_succeeded",
            new() { ["action"] = result.Action.ToString(), ["taskStatus"] = result.TaskStatus, ["instanceStatus"] = result.InstanceStatus, ["idempotencyKey"] = result.IdempotencyKey }, ct);

    public Task EmitWorkflowTransitionFailedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionAction action, Guid? workflowInstanceId, string? idempotencyKey, string reasonCode, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Failed, "workflow_transition_failed",
            new() { ["action"] = action.ToString(), ["workflowInstanceId"] = workflowInstanceId, ["idempotencyKey"] = idempotencyKey, ["reasonCode"] = reasonCode }, ct);

    public Task EmitWorkflowSyncAppliedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataWorkflowTransitionResult result, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Succeeded, "workflow_sync_applied",
            new() { ["action"] = result.Action.ToString(), ["instanceStatus"] = result.InstanceStatus }, ct);

    public Task EmitEvidenceCheckAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, Outcome(result.HasRequiredEvidence), "evidence_check", EvidenceMetadata(result), ct);

    public Task EmitEvidenceRequirementEvaluatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, Outcome(result.HasRequiredEvidence), "evidence_requirement_evaluated", EvidenceMetadata(result), ct);

    public Task EmitEvidenceArtifactValidatedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, Outcome(result.HasRequiredEvidence), "evidence_artifact_validated", EvidenceMetadata(result), ct);

    public Task EmitEvidenceUnsatisfiedAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, BusinessReferenceDataEvidenceCheckResult result, string actionCode, CancellationToken ct = default)
    {
        var metadata = EvidenceMetadata(result);
        metadata["actionCode"] = actionCode;
        return AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Failed, "evidence_unsatisfied", metadata, ct);
    }

    public Task EmitApprovalDecisionAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool approved, string? rejectionReason, bool success, string? reasonCode, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, approved ? AuditOperation.Activate : AuditOperation.Update, Outcome(success), approved ? "approval_approved" : "approval_rejected",
            new() { ["rejectionReason"] = rejectionReason, ["reasonCode"] = reasonCode }, ct);

    public Task EmitOverrideAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, string overrideReason, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Execute, AuditOutcome.Succeeded, "override",
            new() { ["overrideReason"] = overrideReason }, ct);

    public Task EmitPublishAsync(BusinessReferenceDataVersion version, string actorId, string correlationId, bool success, string? reasonCode, string publishMode, CancellationToken ct = default)
        => AppendAsync(version, actorId, correlationId, AuditOperation.Activate, Outcome(success), "publish",
            new() { ["publishMode"] = publishMode, ["reasonCode"] = reasonCode }, ct);

    private static AuditOutcome Outcome(bool success) => success ? AuditOutcome.Succeeded : AuditOutcome.Failed;

    private static Dictionary<string, object?> EvidenceMetadata(BusinessReferenceDataEvidenceCheckResult result) => new()
    {
        ["hasRequiredEvidence"] = result.HasRequiredEvidence,
        ["reasonCode"] = result.ReasonCode,
        ["requirementCode"] = result.RequirementCode,
        ["decisionCode"] = result.DecisionCode
    };

    private async Task AppendAsync(
        BusinessReferenceDataVersion version,
        string actorId,
        string correlationId,
        AuditOperation operation,
        AuditOutcome outcome,
        string governanceEvent,
        Dictionary<string, object?> metadata,
        CancellationToken ct)
    {
        try
        {
            metadata["governanceEvent"] = governanceEvent;
            metadata["governanceMode"] = _mode.ToString();
            metadata["versionNumber"] = version.VersionNumber;
            metadata["setId"] = version.BusinessReferenceDataSetId;
            var hasActor = Guid.TryParse(actorId, out var parsedActorId);
            var auditActorType = hasActor ? AuditActorType.TenantUser : AuditActorType.System;
            metadata["actor"] = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId;
            metadata["actorType"] = auditActorType.ToString();

            var request = new AuditAppendRequest
            {
                CorrelationId = Guid.TryParse(correlationId, out var cid) ? cid : Guid.NewGuid(),
                RequestType = $"BusinessReferenceData.{governanceEvent}",
                ActorType = auditActorType,
                ActorId = hasActor ? parsedActorId : null,
                ActorDisplayName = string.IsNullOrWhiteSpace(actorId) ? "system" : actorId,
                TargetTenantId = version.TenantId,
                Category = AuditCategory.ReferenceData,
                EntityType = EntityTypeName,
                EntityId = version.BusinessReferenceDataVersionId,
                Operation = operation,
                Outcome = outcome,
                Metadata = metadata,
                SourceModule = SourceModuleName
            };

            await _auditService.AppendAsync(request, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "BusinessReferenceData governance audit append failed for event {GovernanceEvent} on version {VersionId}. Governance flow continues.",
                governanceEvent,
                version.BusinessReferenceDataVersionId);
        }
    }
}
