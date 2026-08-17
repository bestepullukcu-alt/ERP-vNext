using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;

public sealed record CreateGoldenReferenceSlimCommand(
    string Code,
    string Name,
    string? Description,
    string? ReferenceType,
    int Priority,
    bool IsActive = true) : IRequest<Response<Guid>>;
