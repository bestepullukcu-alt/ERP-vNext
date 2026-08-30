using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class GetBenefitCommitmentHandler(BenefitCommitmentService service) : IRequestHandler<GetBenefitCommitmentByIdQuery, Response<BenefitCommitmentDto>>
{
    public Task<Response<BenefitCommitmentDto>> Handle(GetBenefitCommitmentByIdQuery request, CancellationToken cancellationToken) => service.Get(request, cancellationToken);
}
