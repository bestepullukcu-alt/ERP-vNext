using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;

public sealed record ConsentVisibilityPolicyRequest(
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

public sealed record EvaluateConsentVisibilityPolicyRequest(
    bool AssociationActivationRequested);
