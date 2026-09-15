using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard;

public sealed record ReviewBoardCaseRequest(
    string Code,
    string DisplayName,
    TepReviewBoardCaseState ReviewBoardCaseState,
    TepReviewDecisionState ReviewDecisionState,
    Guid? AssociationMembershipRegistryId,
    Guid? ConsentVisibilityPolicyId,
    Guid? VerifiedParticipantAccessId,
    TepReviewerEligibilityState ReviewerEligibilityState,
    TepSegregationOfDutiesState SegregationOfDutiesState,
    TepLegalSecurityDecisionState LegalSecurityDecisionState,
    TepExternalReviewBoardState ExternalReviewBoardState,
    TepAuditEvidenceState AuditEvidenceState,
    TepRetentionState RetentionState,
    IReadOnlyList<ReviewBoardDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int ReviewBoardVersion,
    string? DeferredReason);

public sealed record EvaluateReviewBoardCaseRequest(bool ReviewRequested);

public sealed record ReviewBoardDecisionRequest(TepReviewDecisionState ReviewDecisionState);
