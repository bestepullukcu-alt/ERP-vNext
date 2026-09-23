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
    bool IsArchived,
    ReleaseStateDto? ReleaseState = null);

public sealed record ContentSetRevisionListDto(IReadOnlyList<ContentSetRevisionDto> Items, int Total);

/// <summary>SCMM-16B — the rendered-artifact pointer returned by render / read. The internal storage object key is never
/// projected (FU01 non-leakage); the artifact is addressed by <see cref="ContentId"/>.</summary>
public sealed record RenderedArtifactDto(
    Guid ContentId,
    string Checksum,
    string MediaType,
    long ByteSize,
    string FileName,
    DateTimeOffset RenderedAtUtc,
    string? RenderedBy);

/// <summary>SCMM-17 — the release lifecycle state returned by release / withdraw and projected on the revision read. The
/// released artifact is manifest-bound by <see cref="ReleasedArtifactContentId"/> + <see cref="ReleasedArtifactChecksum"/>.
/// </summary>
public sealed record ReleaseStateDto(
    string ReleaseStatus,
    Guid ReleasedArtifactContentId,
    string ReleasedArtifactChecksum,
    DateTimeOffset ReleasedAtUtc,
    string? ReleasedBy,
    DateTimeOffset? WithdrawnAtUtc,
    string? WithdrawnBy,
    string? WithdrawalReason);
