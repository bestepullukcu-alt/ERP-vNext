using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed class ListBenefitCommitmentsHandler(BenefitCommitmentService service) : IRequestHandler<ListBenefitCommitmentsQuery, Response<IReadOnlyList<BenefitCommitmentDto>>>
{
    public Task<Response<IReadOnlyList<BenefitCommitmentDto>>> Handle(ListBenefitCommitmentsQuery request, CancellationToken cancellationToken) => service.List(cancellationToken);
}
