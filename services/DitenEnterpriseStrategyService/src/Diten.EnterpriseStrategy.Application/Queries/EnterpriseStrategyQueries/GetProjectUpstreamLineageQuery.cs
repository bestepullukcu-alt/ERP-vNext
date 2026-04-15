using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetProjectUpstreamLineageQuery : IRequest<Response<string>>
{
    public string ProjectId { get; set; } = string.Empty;
}
