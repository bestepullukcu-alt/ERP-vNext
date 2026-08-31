using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class TransitionBenefitCommitmentHandler(BenefitCommitmentService service) : IRequestHandler<TransitionBenefitCommitmentLifecycleCommand, Response<BenefitCommitmentDto>>
{
    public Task<Response<BenefitCommitmentDto>> Handle(TransitionBenefitCommitmentLifecycleCommand request, CancellationToken cancellationToken) => service.Transition(request, cancellationToken);
}
