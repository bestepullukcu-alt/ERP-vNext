using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;

public sealed record GetCompensationBenefitsReadinessByIdQuery(Guid Id) : IRequest<Response<CompensationBenefitsReadinessDto>>;
