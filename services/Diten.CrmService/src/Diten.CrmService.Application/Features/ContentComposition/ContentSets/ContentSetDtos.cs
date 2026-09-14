namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>SCMM-14 read models for a content set. TenantId is never echoed (server-resolved). Every reference carries
/// its pinned version.</summary>
public sealed record ContentSetTemplateRefDto(Guid ConceptChainTemplateId, string ChainVersion);

public sealed record ContentSetScopeRefDto(Guid ContentScopeId, string ScopeVersion);

public sealed record ContentArrangementDto(Guid TemplateStepId, string? BranchId, int Position);

public sealed record ContentSetComponentDto(
    Guid SelectionId,
    Guid KnowledgeContentId,
    string ContentVersion,
    string LanguageCode,
    string? Role,
    ContentArrangementDto Arrangement);

public sealed record ContentSetClaimDto(
    Guid SelectionId,
    Guid ClaimId,
    string ClaimVersion,
    ContentArrangementDto Arrangement);

public sealed record ContentSetEligibilityItemDto(
    string ItemKind,
    Guid SelectionId,
    Guid ItemId,
    Guid? PolicyId,
    string State,
    string? BlockingLevel,
    string? Reason,
    string? PolicyVersion);

public sealed record ContentSetEligibilitySnapshotDto(
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<ContentSetEligibilityItemDto> Items);

public sealed record ContentSetDto(
    Guid ContentSetId,
    string SetCode,
    string SetName,
    string? Description,
    ContentSetTemplateRefDto Template,
    ContentSetScopeRefDto? Scope,
    IReadOnlyList<ContentSetComponentDto> SelectedComponents,
    IReadOnlyList<ContentSetClaimDto> SelectedClaims,
    int DraftSchemaVersion,
    string Status,
    ContentSetEligibilitySnapshotDto? EligibilitySnapshot,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ContentSetListDto(IReadOnlyList<ContentSetDto> Items, int Total);
