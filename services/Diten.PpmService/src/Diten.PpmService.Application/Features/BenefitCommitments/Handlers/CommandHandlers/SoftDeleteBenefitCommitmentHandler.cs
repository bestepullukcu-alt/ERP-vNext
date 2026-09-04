using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class SoftDeleteBenefitCommitmentHandler(BenefitCommitmentService service) : IRequestHandler<SoftDeleteBenefitCommitmentCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(SoftDeleteBenefitCommitmentCommand request, CancellationToken cancellationToken) => service.SoftDelete(request, cancellationToken);
}
