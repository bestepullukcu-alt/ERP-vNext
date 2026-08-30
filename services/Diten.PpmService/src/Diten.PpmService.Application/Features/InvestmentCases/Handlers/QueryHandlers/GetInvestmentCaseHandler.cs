using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class GetInvestmentCaseHandler(InvestmentCaseService service) : IRequestHandler<GetInvestmentCaseByIdQuery, Response<InvestmentCaseDto>>
{
    public Task<Response<InvestmentCaseDto>> Handle(GetInvestmentCaseByIdQuery request, CancellationToken cancellationToken) => service.Get(request, cancellationToken);
}
