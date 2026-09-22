using Diten.CrmService.Application.Features.ContentComposition.ContentSets;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>SCMM-15 read models for a ContentSetRevision. TenantId is never echoed (server-resolved). The frozen
/// snapshot reuses the SCMM-14 content-set read models (same pinned-reference shape).</summary>
public sealed record ContentSetReviewDecisionDto(
    string? ReviewerId,
    string Decision,
    string? Reason,
    DateTimeOffset DecidedAt);

public sealed record ContentSetRevisionDto(
    Guid ContentSetRevisionId,
    string RevisionCode,
    int RevisionNumber,
    Guid ContentSetId,
    int ContentSetVersion,
    ContentSetTemplateRefDto Template,
    ContentSetScopeRefDto? Scope,
    IReadOnlyList<ContentSetComponentDto> SelectedComponents,
    IReadOnlyList<ContentSetClaimDto> SelectedClaims,
    ContentSetEligibilitySnapshotDto? EligibilitySnapshot,
    string ReviewStatus,
    ContentSetReviewDecisionDto? Decision,
    string? SubmittedBy,
    DateTimeOffset SubmittedAt,
    string? CorrelationId,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ContentSetRevisionListDto(IReadOnlyList<ContentSetRevisionDto> Items, int Total);
