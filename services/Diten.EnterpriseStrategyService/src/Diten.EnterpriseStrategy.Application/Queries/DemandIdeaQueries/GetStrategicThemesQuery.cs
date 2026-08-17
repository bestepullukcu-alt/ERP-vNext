using Diten.Application.Common.Models;
using Diten.Application.Dtos.DemandIdeas;
using MediatR;

namespace Diten.Application.Queries.DemandIdeaQueries;

public sealed class GetStrategicThemesQuery : IRequest<Response<IReadOnlyList<StrategicThemeDto>>>
{
}
