using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetDomainLandscapesQuery : IRequest<IReadOnlyList<DomainLandscapeDto>>;
