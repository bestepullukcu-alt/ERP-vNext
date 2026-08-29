using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.Models;

public sealed record TerritoryBusinessScopeDto(string ScopeType, string ScopeCode);

public sealed record TerritoryModelListItemDto(
    Guid Id,
    string ModelCode,
    string Name,
    string Status,
    string StoredStatus,
    string ComputedStatus,
    bool IsExpired,
    int VersionNumber,
    string? CountryScope,
    string? DivisionScope,
    IReadOnlyList<TerritoryBusinessScopeDto> BusinessScopes,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt);

public sealed record TerritoryModelListDto(IReadOnlyList<TerritoryModelListItemDto> Items, long Total, int Page, int PageSize);

public sealed record TerritoryModelDetailDto(
    Guid Id,
    string ModelCode,
    string Name,
    string Status,
    string StoredStatus,
    string ComputedStatus,
    bool IsExpired,
    int VersionNumber,
    string? CountryScope,
    string? DivisionScope,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid? BasedOnModelId,
    string? ChangeReason,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TerritoryBusinessScopeDto> BusinessScopes);

public static class TerritoryModelMapper
{
    public static bool IsExpired(TerritoryModel model, DateTimeOffset? now = null)
        => model.EffectiveTo is { } end && end < (now ?? DateTimeOffset.UtcNow);

    public static string ComputedStatus(TerritoryModel model, DateTimeOffset? now = null)
        => IsExpired(model, now) ? "expired" : model.Status;

    public static TerritoryModelListItemDto ToListItem(TerritoryModel m) => new(
        m.Id, m.ModelCode, m.Name, m.Status, m.Status, ComputedStatus(m), IsExpired(m),
        m.VersionNumber, m.CountryScope, m.DivisionScope,
        (m.BusinessScopes ?? []).Select(s => new TerritoryBusinessScopeDto(s.ScopeType, s.ScopeCode)).ToList(),
        m.EffectiveFrom, m.EffectiveTo, m.CreatedAt);

    public static TerritoryModelDetailDto ToDetail(TerritoryModel m) => new(
        m.Id, m.ModelCode, m.Name, m.Status, m.Status, ComputedStatus(m), IsExpired(m),
        m.VersionNumber, m.CountryScope, m.DivisionScope,
        m.EffectiveFrom, m.EffectiveTo, m.BasedOnModelId, m.ChangeReason, m.CorrelationId, m.CreatedAt, m.UpdatedAt,
        (m.BusinessScopes ?? []).Select(s => new TerritoryBusinessScopeDto(s.ScopeType, s.ScopeCode)).ToList());
}
