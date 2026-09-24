namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact;

public sealed record GoldenReferenceCompactListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? ReferenceType,
    string? Category,
    string? GroupKey,
    string? SourceSystem,
    string? Owner,
    string? Version,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    int Priority,
    bool IsActive);

public sealed record GoldenReferenceCompactDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? ReferenceType,
    string? Category,
    string? GroupKey,
    string? SourceSystem,
    string? Owner,
    string? Version,
    DateTime? EffectiveDate,
    DateTime? ExpirationDate,
    int Priority,
    bool IsActive);

/// <summary>
/// The server-mode list envelope's `data` (WP-UI-LIST-SERVER-01): `{ items, total, filteredTotal }`. The list factory
/// hands DataTables `recordsTotal = total`, `recordsFiltered = filteredTotal`, `data = items`.
/// </summary>
public sealed record GoldenReferenceCompactListPageDto(
    IReadOnlyList<GoldenReferenceCompactListItemDto> Items,
    long Total,
    long FilteredTotal);
