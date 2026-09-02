using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

internal static class CandidateDisputeHandlerMapper
{
    public static TepCandidateDisputeReadinessMetadata ToEntity(Guid tenantId, CandidateDisputeReadinessRequest request) =>
        new()
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            DisputeReadinessState = request.DisputeReadinessState,
            CandidateProfileReference = request.CandidateProfileReference,
            ExitReferenceRecordReference = request.ExitReferenceRecordReference,
            ReferenceExchangeReference = request.ReferenceExchangeReference,
            RehireRecommendationReference = request.RehireRecommendationReference,
            ResponseBoundaryState = request.ResponseBoundaryState,
            DisputeIntakeState = request.DisputeIntakeState,
            DisputeReviewState = request.DisputeReviewState,
            ResolutionLifecycleState = request.ResolutionLifecycleState,
            ContestabilityState = request.ContestabilityState,
            HumanReviewState = request.HumanReviewState,
            ConsentPreconditionState = request.ConsentPreconditionState,
            VisibilityApprovalState = request.VisibilityApprovalState,
            DataScopeState = request.DataScopeState,
            EvidenceRetentionState = request.EvidenceRetentionState,
            AuditReadinessState = request.AuditReadinessState,
            LegalHoldState = request.LegalHoldState,
            DeletionPolicyState = request.DeletionPolicyState,
            SelfServiceBoundaryState = request.SelfServiceBoundaryState,
            NotificationDependencyState = request.NotificationDependencyState,
            DocumentDependencyState = request.DocumentDependencyState,
            AutomatedDecisionBoundaryState = request.AutomatedDecisionBoundaryState,
            MarketplaceBoundaryState = request.MarketplaceBoundaryState,
            DependencyStates = request.DependencyStates.Select(CandidateDisputeMapper.ToEntity).ToList(),
            SourceContractVersion = request.SourceContractVersion.Trim(),
            LastEvaluatedAt = request.LastEvaluatedAt,
            DisputeReadinessVersion = request.DisputeReadinessVersion,
            DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim()
        };

    public static void Apply(TepCandidateDisputeReadinessMetadata entity, CandidateDisputeReadinessRequest request)
    {
        entity.Code = request.Code.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.DisputeReadinessState = request.DisputeReadinessState;
        entity.CandidateProfileReference = request.CandidateProfileReference;
        entity.ExitReferenceRecordReference = request.ExitReferenceRecordReference;
        entity.ReferenceExchangeReference = request.ReferenceExchangeReference;
        entity.RehireRecommendationReference = request.RehireRecommendationReference;
        entity.ResponseBoundaryState = request.ResponseBoundaryState;
        entity.DisputeIntakeState = request.DisputeIntakeState;
        entity.DisputeReviewState = request.DisputeReviewState;
        entity.ResolutionLifecycleState = request.ResolutionLifecycleState;
        entity.ContestabilityState = request.ContestabilityState;
        entity.HumanReviewState = request.HumanReviewState;
        entity.ConsentPreconditionState = request.ConsentPreconditionState;
        entity.VisibilityApprovalState = request.VisibilityApprovalState;
        entity.DataScopeState = request.DataScopeState;
        entity.EvidenceRetentionState = request.EvidenceRetentionState;
        entity.AuditReadinessState = request.AuditReadinessState;
        entity.LegalHoldState = request.LegalHoldState;
        entity.DeletionPolicyState = request.DeletionPolicyState;
        entity.SelfServiceBoundaryState = request.SelfServiceBoundaryState;
        entity.NotificationDependencyState = request.NotificationDependencyState;
        entity.DocumentDependencyState = request.DocumentDependencyState;
        entity.AutomatedDecisionBoundaryState = request.AutomatedDecisionBoundaryState;
        entity.MarketplaceBoundaryState = request.MarketplaceBoundaryState;
        entity.DependencyStates = request.DependencyStates.Select(CandidateDisputeMapper.ToEntity).ToList();
        entity.SourceContractVersion = request.SourceContractVersion.Trim();
        entity.LastEvaluatedAt = request.LastEvaluatedAt;
        entity.DisputeReadinessVersion = request.DisputeReadinessVersion;
        entity.DeferredReason = string.IsNullOrWhiteSpace(request.DeferredReason) ? null : request.DeferredReason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
