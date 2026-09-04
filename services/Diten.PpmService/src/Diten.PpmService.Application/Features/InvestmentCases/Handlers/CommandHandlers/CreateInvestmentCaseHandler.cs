using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class CreateInvestmentCaseHandler(InvestmentCaseService service) : IRequestHandler<CreateInvestmentCaseCommand, Response<InvestmentCaseDto>>
{
    public Task<Response<InvestmentCaseDto>> Handle(CreateInvestmentCaseCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
