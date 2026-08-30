using Diten.PpmService.Application.Common;
using MediatR;

namespace Diten.PpmService.Application.Features.BenefitCommitments;

public sealed record ListBenefitCommitmentsQuery : IRequest<Response<IReadOnlyList<BenefitCommitmentDto>>>;
