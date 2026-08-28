using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Contract;

/// <summary>The in-domain vocabulary the runtime validates against (D-VOCAB = A). Published so a UI never hardcodes a
/// list and never has to wait for a MOD-0048 set to be published.</summary>
public sealed record SegmentVocabularyDto(
    IReadOnlyList<string> SegmentTypes,
    IReadOnlyList<string> SubjectTypes,
    IReadOnlyList<string> SegmentStatuses,
    IReadOnlyList<string> MatchModes,
    IReadOnlyList<string> CriteriaNodeKinds,
    IReadOnlyList<string> GroupOperators,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> ValueTypes,
    IReadOnlyList<string> MembershipModes,
    IReadOnlyList<string> MembershipVerdicts,
    IReadOnlyList<string> MembershipSources,
    IReadOnlyList<string> ConceptAffinityRelationshipTypes)
{
    public static SegmentVocabularyDto Current => new(
        Domain.Entities.SegmentTypes.All,
        SegmentSubjectTypes.All,
        Domain.Entities.SegmentStatuses.All,
        SegmentMatchModes.AllValues,
        SegmentCriteriaNodeKinds.All,
        SegmentGroupOperators.All,
        Domain.Entities.SegmentOperators.All,
        Domain.Entities.SegmentValueTypes.All,
        SegmentMembershipModes.All,
        SegmentMembershipVerdicts.All,
        SegmentMembershipSources.All,
        Domain.Entities.ConceptAffinityRelationshipTypes.All);
}
