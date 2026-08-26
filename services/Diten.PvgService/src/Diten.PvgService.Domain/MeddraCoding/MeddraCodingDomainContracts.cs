namespace Diten.PvgService.Domain.MeddraCoding;

public sealed record Mod0231SourceTermReference(
    string SourceTermReference,
    string CaseProcessingReference,
    string LifecycleStateReference,
    bool IsApprovedForCoding);

public sealed record MeddraDictionaryVersionReference(
    string DictionaryVersionReference,
    string CodesetVersionReference,
    bool IsGovernanceApproved)
{
    public bool UsesOpaqueReferences =>
        IsOpaqueReference(DictionaryVersionReference) &&
        IsOpaqueReference(CodesetVersionReference);

    private static bool IsOpaqueReference(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => !char.IsWhiteSpace(character));
}

public sealed record MeddraCodedTermReference(
    MeddraDictionaryVersionReference DictionaryVersion,
    string CodeReferenceToken,
    string HierarchyReferenceToken)
{
    public bool UsesOpaqueReferences =>
        DictionaryVersion.UsesOpaqueReferences &&
        IsOpaqueReference(CodeReferenceToken) &&
        IsOpaqueReference(HierarchyReferenceToken);

    private static bool IsOpaqueReference(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.All(character => !char.IsWhiteSpace(character));
}

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
