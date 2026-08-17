using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

internal static class BusinessReferenceDataModelMapper
{
    /// <summary>
    /// Normalizes the governance/approval state for read surfaces. A published version that still
    /// carries a <c>Draft</c> governance state or a <c>NotStarted</c> approval state is presented as
    /// <c>Approved</c>, matching the published-version contract used across the list and detail views.
    /// </summary>
    public static (BusinessReferenceDataGovernanceState Governance, BusinessReferenceDataApprovalState Approval) NormalizeGovernance(BusinessReferenceDataVersion version)
    {
        var isPublished = version.Status == BusinessReferenceDataVersionStatus.Published;
        var governanceState = isPublished && version.BusinessReferenceDataGovernanceState == BusinessReferenceDataGovernanceState.Draft
            ? BusinessReferenceDataGovernanceState.Approved
            : version.BusinessReferenceDataGovernanceState;
        var approvalState = isPublished && version.BusinessReferenceDataApprovalState == BusinessReferenceDataApprovalState.NotStarted
            ? BusinessReferenceDataApprovalState.Approved
            : version.BusinessReferenceDataApprovalState;
        return (governanceState, approvalState);
    }

    public static BusinessReferenceDataVersionDetailModel ToVersionDetail(BusinessReferenceDataVersion version)
    {
        var isPublished = version.Status == BusinessReferenceDataVersionStatus.Published;
        var isImmutable = isPublished || version.IsImmutable;
        var (governanceState, approvalState) = NormalizeGovernance(version);
        var isEditable = !isPublished
            && version.Status == BusinessReferenceDataVersionStatus.Draft
            && !isImmutable
            && version.IsEditable;

        return new BusinessReferenceDataVersionDetailModel(
            version.BusinessReferenceDataVersionId,
            version.BusinessReferenceDataSetId,
            version.VersionNumber,
            version.Status.ToString(),
            version.ConcurrencyToken,
            isImmutable,
            version.CreatedAt,
            version.UpdatedAt,
            version.PublishedAt,
            version.PublishedBy,
            version.SourceVersionId,
            version.TargetDraftVersionId,
            version.CopyActor,
            version.CopiedAt,
            governanceState.ToString(),
            approvalState.ToString(),
            isEditable,
            version.SubmittedAt,
            version.SubmittedBy,
            version.DecisionAt,
            version.DecisionBy,
            version.RejectionReason,
            version.WorkflowTemplateCode,
            version.WorkflowInstanceId,
            version.IsOverrideAction,
            version.OverrideReason,
            version.LastEvidenceRef,
            version.LastPublishIdempotencyKey,
            version.LastPublishMode,
            version.SupersededByVersionId,
            version.EvidenceLinkId,
            version.EvidenceEvaluationId,
            version.EvidenceDocumentVersionId,
            version.EvidenceRequirementCode,
            version.EvidenceDecisionCode,
            version.EvidenceReasonCode);
    }
}
