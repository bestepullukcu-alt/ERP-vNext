using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed class TransitionInvestmentCaseHandler(InvestmentCaseService service) : IRequestHandler<TransitionInvestmentCaseLifecycleCommand, Response<InvestmentCaseDto>>
{
    public Task<Response<InvestmentCaseDto>> Handle(TransitionInvestmentCaseLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
