using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListObjectivesQuery : IRequest<Response<PagedResponseDto<ObjectiveDto>>>
{
    public PagedRequestDto Request { get; set; } = new();
}
