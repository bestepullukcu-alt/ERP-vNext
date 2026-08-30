using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class ListInvestmentCasesHandler(InvestmentCaseService service) : IRequestHandler<ListInvestmentCasesQuery, Response<IReadOnlyList<InvestmentCaseDto>>>
{
    public Task<Response<IReadOnlyList<InvestmentCaseDto>>> Handle(ListInvestmentCasesQuery request, CancellationToken cancellationToken) => service.List(cancellationToken);
}
