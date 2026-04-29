using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Commands;

public sealed record UpdateCapabilityGroupCommand(
    Guid Id,
    string Name,
    Guid DomainLandscapeId,
    Guid SuitePlatformId,
    string? Code = null,
    string? Description = null,
    bool IsActive = true) : IRequest<CapabilityGroupDto>;
