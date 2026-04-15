using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetObjectiveProjectsQuery : IRequest<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>>
{
    public string ObjectiveId { get; set; } = string.Empty;
}
