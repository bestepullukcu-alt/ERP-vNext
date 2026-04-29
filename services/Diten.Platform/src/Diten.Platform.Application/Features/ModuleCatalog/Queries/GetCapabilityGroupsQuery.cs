using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetCapabilityGroupsQuery(
    Guid? DomainLandscapeId = null,
    Guid? SuitePlatformId = null) : IRequest<IReadOnlyList<CapabilityGroupDto>>;
