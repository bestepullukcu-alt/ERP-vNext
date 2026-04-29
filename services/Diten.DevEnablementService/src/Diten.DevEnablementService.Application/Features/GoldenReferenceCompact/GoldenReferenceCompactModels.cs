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
