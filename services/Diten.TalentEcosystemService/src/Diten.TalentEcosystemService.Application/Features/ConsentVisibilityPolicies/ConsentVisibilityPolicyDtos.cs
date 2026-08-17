using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;

public sealed record ConsentVisibilityPolicyDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepPolicyState PolicyState,
    TepConsentRequirementState ConsentRequirementState,
    TepVisibilityScope VisibilityScope,
    TepDataScopeState DataScopeState,
    TepAccessPolicyState AccessPolicyState,
    TepAssociationConsumptionState AssociationConsumptionState,
    TepPolicyUnavailableBehavior PolicyUnavailableBehavior,
    string SourceContractVersion,
    IReadOnlyList<TepPolicyDependencyStateDto> DependencyStates,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    DateTimeOffset? LastEvaluatedAt,
    int PolicyVersion);

public sealed record ConsentVisibilityPolicyListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepPolicyState PolicyState,
    TepConsentRequirementState ConsentRequirementState,
    TepVisibilityScope VisibilityScope,
    TepDataScopeState DataScopeState,
    TepAssociationConsumptionState AssociationConsumptionState,
    DateTimeOffset? LastEvaluatedAt,
    int PolicyVersion);

public sealed record TepPolicyDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record ConsentVisibilityPolicyEvaluationDto(
    Guid Id,
    bool ActivationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);

public sealed record ConsentVisibilityPolicyAuditMetadataDto(
    Guid Id,
    TepLocalAuditEvidenceRetentionState LocalAuditEvidenceRetentionState,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int PolicyVersion);
