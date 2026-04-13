using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetConnectionCoverageGapsQuery : IRequest<Response<IReadOnlyList<CoverageGapDto>>>
{
}
