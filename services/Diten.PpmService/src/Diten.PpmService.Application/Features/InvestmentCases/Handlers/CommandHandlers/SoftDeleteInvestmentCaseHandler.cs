using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class SoftDeleteInvestmentCaseHandler(InvestmentCaseService service) : IRequestHandler<SoftDeleteInvestmentCaseCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeleteInvestmentCaseCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
