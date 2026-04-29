using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record UpdateDomainLandscapeCommand(
    Guid Id,
    string Name,
    string? Code = null,
    string? Description = null,
    bool IsActive = true) : IRequest<DomainLandscapeDto>;
