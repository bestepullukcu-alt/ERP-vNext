using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record CreateSuitePlatformCommand(
    string Name,
    Guid DomainLandscapeId,
    string? Code = null,
    string? Description = null,
    bool IsActive = true) : IRequest<SuitePlatformDto>;
