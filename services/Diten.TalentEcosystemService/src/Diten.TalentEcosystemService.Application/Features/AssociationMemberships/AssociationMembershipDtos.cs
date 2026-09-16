using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships;

public sealed record AssociationMembershipRegistryDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepAssociationMembershipState AssociationMembershipState,
    TepMemberCompanyState MemberCompanyState,
    string MemberCompanyReference,
    string HcmFoundationReference,
    Guid? ConsentVisibilityPolicyId,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepAssociationActivationState AssociationActivationState,
    IReadOnlyList<TepAssociationDependencyStateDto> DependencyStates,
    string SourceContractVersion,
    DateTimeOffset? LastEvaluatedAt,
    int RegistryVersion);

public sealed record AssociationMembershipRegistryListItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    TepAssociationMembershipState AssociationMembershipState,
    TepMemberCompanyState MemberCompanyState,
    TepPolicyEvaluationState PolicyEvaluationState,
    TepVisibilityApprovalState VisibilityApprovalState,
    TepAssociationActivationState AssociationActivationState,
    DateTimeOffset? LastEvaluatedAt,
    int RegistryVersion);

public sealed record TepAssociationDependencyStateDto(
    string DependencyKey,
    TepShellDependencyStatus State,
    string? Reason);

public sealed record AssociationMembershipEvaluationDto(
    Guid Id,
    bool ActivationAllowed,
    bool EvaluationDeferred,
    string Decision,
    DateTimeOffset EvaluatedAt);
