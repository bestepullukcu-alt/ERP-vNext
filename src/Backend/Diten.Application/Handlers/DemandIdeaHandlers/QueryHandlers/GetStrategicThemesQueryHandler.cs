using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using MediatR;

namespace Diten.Application.Handlers.DemandIdeaHandlers.QueryHandlers;

public sealed class GetStrategicThemesQueryHandler : IRequestHandler<GetStrategicThemesQuery, Response<IReadOnlyList<StrategicThemeDto>>>
{
    public Task<Response<IReadOnlyList<StrategicThemeDto>>> Handle(GetStrategicThemesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Response<IReadOnlyList<StrategicThemeDto>>.Ok(DemandIdeaHandlerSupport.StrategicThemes()));
    }
}
