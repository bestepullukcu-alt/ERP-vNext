namespace Diten.PvgService.Domain.MeddraCoding;

public sealed record Mod0231SourceTermReference(
    string SourceTermReference,
    string CaseProcessingReference,
    string LifecycleStateReference,
    bool IsApprovedForCoding);

public sealed record MeddraDictionaryVersionReference(
    string DictionaryVersionReference,
    string CodesetVersionReference,
    bool IsGovernanceApproved);

public sealed record MeddraCodedTermReference(
    MeddraDictionaryVersionReference DictionaryVersion,
    string CodeReferenceToken,
    string HierarchyReferenceToken);

public sealed record MeddraCodingAssignmentDraft(
    string CodingWorkItemReference,
    Mod0231SourceTermReference SourceTermReference,
    MeddraCodedTermReference? ProposedTerm,
    MeddraCodingReviewStatus ReviewStatus);

public enum MeddraCodingReviewStatus
{
    Draft = 0,
    Proposed = 1,
    ReviewRequired = 2,
    Reviewed = 3,
    Blocked = 4
}
