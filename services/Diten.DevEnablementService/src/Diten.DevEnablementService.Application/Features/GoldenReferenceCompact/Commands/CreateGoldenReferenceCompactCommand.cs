using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;

public sealed record CreateGoldenReferenceCompactCommand(
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
    bool IsActive = true) : IRequest<Response<Guid>>;
