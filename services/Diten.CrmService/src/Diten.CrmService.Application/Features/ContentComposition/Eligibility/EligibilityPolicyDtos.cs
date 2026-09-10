namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>SCMM-11 read model for one policy condition.</summary>
public sealed record EligibilityConditionDto(
    string Dimension,
    string Match,
    IReadOnlyList<string> Values,
    bool Required);

/// <summary>SCMM-11 (CAND-CAP-0011) read model for an eligibility policy. <c>Conditions</c> is frozen once published.</summary>
public sealed record EligibilityPolicyDto(
    Guid EligibilityPolicyId,
    string PolicyCode,
    string PolicyName,
    string? Description,
    string PolicyVersion,
    string Status,
    IReadOnlyList<EligibilityConditionDto> Conditions,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record EligibilityPolicyListDto(IReadOnlyList<EligibilityPolicyDto> Items, int Total);
