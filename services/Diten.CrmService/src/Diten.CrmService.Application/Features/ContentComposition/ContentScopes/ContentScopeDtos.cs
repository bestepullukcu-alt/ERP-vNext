namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>SCMM-14 (CAND-CAP-0011) read model for a content scope. TenantId is never echoed (server-resolved).</summary>
public sealed record ContentScopeDto(
    Guid ContentScopeId,
    string ScopeCode,
    string ScopeName,
    string? Description,
    IReadOnlyList<string> ProductRefs,
    IReadOnlyList<string> MarketRefs,
    IReadOnlyList<string> AudienceRefs,
    string? Channel,
    string? LanguageCode,
    DateTimeOffset? PeriodFrom,
    DateTimeOffset? PeriodTo,
    string ScopeVersion,
    string Status,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ContentScopeListDto(IReadOnlyList<ContentScopeDto> Items, int Total);
