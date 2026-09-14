using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Handlers;

internal static class ExitReferenceRecordHandlerMapper
{
    public static TepExitReferenceRecordMetadata ToEntity(Guid tenantId, ExitReferenceRecordRequest request) =>
        new()
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            CandidateProfileReference = request.CandidateProfileReference,
            OffboardingCaseReference = request.OffboardingCaseReference.Trim(),
            VerifiedParticipantReference = request.VerifiedParticipantReference,
            AssociationMembershipReference = request.AssociationMembershipReference,
            ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference,
            ReviewBoardCaseReference = request.ReviewBoardCaseReference,
            TrustLevelPolicyReference = request.TrustLevelPolicyReference,
            ReferenceRecordState = request.ReferenceRecordState,
            ReferenceSharingState = request.ReferenceSharingState,
            ConsentPreconditionState = request.ConsentPreconditionState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            EvidenceRetentionState = request.EvidenceRetentionState,
            ReviewDisputeBoundaryState = request.ReviewDisputeBoundaryState,
            DependencyStates = request.DependencyStates.Select(ExitReferenceRecordMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            ReferenceRecordVersion = request.ReferenceRecordVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };

    public static void Apply(TepExitReferenceRecordMetadata entity, ExitReferenceRecordRequest request)
    {
        entity.Code = request.Code.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.CandidateProfileReference = request.CandidateProfileReference;
        entity.OffboardingCaseReference = request.OffboardingCaseReference.Trim();
        entity.VerifiedParticipantReference = request.VerifiedParticipantReference;
        entity.AssociationMembershipReference = request.AssociationMembershipReference;
        entity.ConsentVisibilityPolicyReference = request.ConsentVisibilityPolicyReference;
        entity.ReviewBoardCaseReference = request.ReviewBoardCaseReference;
        entity.TrustLevelPolicyReference = request.TrustLevelPolicyReference;
        entity.ReferenceRecordState = request.ReferenceRecordState;
        entity.ReferenceSharingState = request.ReferenceSharingState;
        entity.ConsentPreconditionState = request.ConsentPreconditionState;
        entity.VisibilityApprovalState = request.VisibilityApprovalState;
        entity.DataScopeState = request.DataScopeState;
        entity.EvidenceRetentionState = request.EvidenceRetentionState;
        entity.ReviewDisputeBoundaryState = request.ReviewDisputeBoundaryState;
        entity.DependencyStates = request.DependencyStates.Select(ExitReferenceRecordMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.LastEvaluatedAt;
        entity.ReferenceRecordVersion = request.ReferenceRecordVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim();
    }
}
