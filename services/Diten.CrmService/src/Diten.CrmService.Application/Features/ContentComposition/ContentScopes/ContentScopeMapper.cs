using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentScopes;

/// <summary>SCMM-14 (CAND-CAP-0011) aggregate ↔ DTO projection. Reads never echo TenantId (server-resolved).</summary>
public static class ContentScopeMapper
{
    public static ContentScopeDto ToDto(ContentScope s) => new(
        s.Id,
        s.ScopeCode,
        s.ScopeName,
        s.Description,
        s.ProductRefs.ToList(),
        s.MarketRefs.ToList(),
        s.AudienceRefs.ToList(),
        s.Channel,
        s.LanguageCode,
        s.PeriodFrom,
        s.PeriodTo,
        s.ScopeVersion,
        s.Status,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy,
        s.ArchivedAt,
        s.ArchivedBy,
        s.IsArchived());

    /// <summary>Trim / drop-empty / dedupe an opaque reference list (order preserved).</summary>
    public static List<string> CleanRefs(IReadOnlyList<string>? refs)
        => (refs ?? Array.Empty<string>()).Select(r => r?.Trim() ?? string.Empty)
            .Where(r => r.Length > 0).Distinct().ToList();
}
