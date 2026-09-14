using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ReviewBoard;

public sealed record ReviewBoardCaseDto(
    Guid Id,
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

public sealed record ReviewBoardCaseListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepReviewBoardCaseState ReviewBoardCaseState,
    TepReviewDecisionState ReviewDecisionState,
    TepReviewerEligibilityState ReviewerEligibilityState,
    TepSegregationOfDutiesState SegregationOfDutiesState,
    TepLegalSecurityDecisionState LegalSecurityDecisionState,
    DateTimeOffset? LastEvaluatedAt,
    int ReviewBoardVersion);

public sealed record ReviewBoardDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record ReviewBoardEvaluationDto(
    Guid Id,
    bool ReviewAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record ReviewBoardAuditMetadataDto(
    Guid Id,
    TepAuditEvidenceState AuditEvidenceState,
    TepRetentionState RetentionState,
    IReadOnlyList<ReviewBoardDependencyStateDto> DependencyStates,
    string? DeferredReason);
