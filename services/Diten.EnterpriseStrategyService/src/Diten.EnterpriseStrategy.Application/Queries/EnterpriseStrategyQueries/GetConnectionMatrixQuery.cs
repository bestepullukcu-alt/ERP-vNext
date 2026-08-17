using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class GetConnectionMatrixQuery : IRequest<Response<IReadOnlyList<ConnectionMatrixCellDto>>>
{
    public string Mode { get; set; } = "goals-vs-objectives";
}
