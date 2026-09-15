using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;

public sealed record GetCompensationBenefitsReadinessListQuery : IRequest<Response<IReadOnlyList<CompensationBenefitsReadinessListItemDto>>>;
