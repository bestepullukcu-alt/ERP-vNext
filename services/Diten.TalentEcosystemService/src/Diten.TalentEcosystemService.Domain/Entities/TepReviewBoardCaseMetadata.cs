using Diten.TalentEcosystemService.Domain.Common;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Domain.Entities;

public sealed class TepReviewBoardCaseMetadata : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TepReviewBoardCaseState ReviewBoardCaseState { get; set; } = TepReviewBoardCaseState.Draft;
    public TepReviewDecisionState ReviewDecisionState { get; set; } = TepReviewDecisionState.NotReviewed;
    public Guid? AssociationMembershipRegistryId { get; set; }
    public Guid? ConsentVisibilityPolicyId { get; set; }
    public Guid? VerifiedParticipantAccessId { get; set; }
    public TepReviewerEligibilityState ReviewerEligibilityState { get; set; } = TepReviewerEligibilityState.NotEvaluated;
    public TepSegregationOfDutiesState SegregationOfDutiesState { get; set; } = TepSegregationOfDutiesState.NotEvaluated;
    public TepLegalSecurityDecisionState LegalSecurityDecisionState { get; set; } = TepLegalSecurityDecisionState.Draft;
    public TepExternalReviewBoardState ExternalReviewBoardState { get; set; } = TepExternalReviewBoardState.NotRequired;
    public TepAuditEvidenceState AuditEvidenceState { get; set; } = TepAuditEvidenceState.Deferred;
    public TepRetentionState RetentionState { get; set; } = TepRetentionState.Deferred;
    public List<TepReviewBoardDependencyState> DependencyStates { get; set; } = [];
    public string SourceContractVersion { get; set; } = string.Empty;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public int ReviewBoardVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class TepReviewBoardDependencyState
{
    public string DependencyKey { get; set; } = string.Empty;
    public TepShellDependencyStatus State { get; set; } = TepShellDependencyStatus.Deferred;
    public string? Reason { get; set; }
}
