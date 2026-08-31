using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class UpdateInvestmentCaseHandler(InvestmentCaseService service) : IRequestHandler<UpdateInvestmentCaseCommand, Response<InvestmentCaseDto>>
{
    public Task<Response<InvestmentCaseDto>> Handle(UpdateInvestmentCaseCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
