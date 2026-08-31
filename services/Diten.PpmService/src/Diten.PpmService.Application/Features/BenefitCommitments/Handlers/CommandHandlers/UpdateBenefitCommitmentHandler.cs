using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class UpdateBenefitCommitmentHandler(BenefitCommitmentService service) : IRequestHandler<UpdateBenefitCommitmentCommand, Response<BenefitCommitmentDto>>
{
    public Task<Response<BenefitCommitmentDto>> Handle(UpdateBenefitCommitmentCommand request, CancellationToken cancellationToken) => service.Update(request, cancellationToken);
}
