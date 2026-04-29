namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim;

public sealed record GoldenReferenceSlimListItemDto(Guid Id, string Code, string Name, string? Description, string? ReferenceType, int Priority, bool IsActive);
public sealed record GoldenReferenceSlimDetailDto(Guid Id, string Code, string Name, string? Description, string? ReferenceType, int Priority, bool IsActive);
