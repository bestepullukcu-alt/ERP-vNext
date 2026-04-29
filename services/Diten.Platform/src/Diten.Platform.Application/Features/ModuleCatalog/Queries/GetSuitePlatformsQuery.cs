using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetSuitePlatformsQuery(Guid? DomainLandscapeId = null) : IRequest<IReadOnlyList<SuitePlatformDto>>;
