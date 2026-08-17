using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Queries;

public sealed record GetModuleCatalogStatsQuery() : IRequest<Response<ModuleCatalogStatsResponse>>;

public sealed record ModuleCatalogStatsResponse(
    int Total,
    int Active,
    int Beta,
    int Preview
);
