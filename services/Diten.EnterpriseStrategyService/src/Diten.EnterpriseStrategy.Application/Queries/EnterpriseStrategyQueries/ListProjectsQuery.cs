using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using MediatR;

namespace Diten.Application.Queries.EnterpriseStrategyQueries;

public sealed class ListProjectsQuery : IRequest<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>>
{
    public PagedRequestDto Request { get; set; } = new();
}
