using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class CreateBenefitCommitmentHandler(BenefitCommitmentService service) : IRequestHandler<CreateBenefitCommitmentCommand, Response<BenefitCommitmentDto>>
{
    public Task<Response<BenefitCommitmentDto>> Handle(CreateBenefitCommitmentCommand request, CancellationToken cancellationToken) => service.Create(request, cancellationToken);
}
