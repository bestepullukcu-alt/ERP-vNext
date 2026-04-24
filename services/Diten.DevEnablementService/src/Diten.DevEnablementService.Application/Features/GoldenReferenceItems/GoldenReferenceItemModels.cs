namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems;

public sealed record GoldenReferenceItemListItemDto(Guid Id, string Code, string Name, string? ReferenceType, int Priority, bool IsActive);
public sealed record GoldenReferenceItemDetailDto(Guid Id, string Code, string Name, string? Description, string? ReferenceType, int Priority, bool IsActive);
