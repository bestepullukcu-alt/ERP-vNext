using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Contract;

/// <summary>Published ceilings, so a UI can warn before the runtime rejects and a caller is never surprised by a 400 or
/// a 422. Every one of them is enforced; none of them silently truncates.</summary>
public sealed record SegmentContractLimits(
    int MaxCriteriaDepth,
    int MaxCriteriaNodes,
    int MaxChildrenPerGroup,
    int MaxValuesPerInOperator,
    int MaxCandidateSet,
    int MaxSegmentsPerSubject,
    int DefaultConceptAffinityDepth,
    int MaxConceptAffinityDepth,
    bool CriteriaAreEmbeddedInSegmentDocument,
    bool MembershipIsPersisted)
{
    public static SegmentContractLimits Current => new(
        SegmentLimits.MaxCriteriaDepth,
        SegmentLimits.MaxCriteriaNodes,
        SegmentLimits.MaxChildrenPerGroup,
        SegmentLimits.MaxValuesPerInOperator,
        SegmentLimits.MaxCandidateSet,
        SegmentLimits.MaxSegmentsPerSubject,
        SegmentLimits.DefaultConceptAffinityDepth,
        SegmentLimits.MaxConceptAffinityDepth,
        CriteriaAreEmbeddedInSegmentDocument: true,
        MembershipIsPersisted: false);
}
