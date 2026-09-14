using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships;

public sealed record AssociationMembershipRegistryRequest(
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

public sealed record AssociationMemberCompanyRequest(
    TepMemberCompanyState MemberCompanyState,
    string MemberCompanyReference,
    string SourceContractVersion);

public sealed record EvaluateAssociationMembershipRequest(bool ActivationRequested);
