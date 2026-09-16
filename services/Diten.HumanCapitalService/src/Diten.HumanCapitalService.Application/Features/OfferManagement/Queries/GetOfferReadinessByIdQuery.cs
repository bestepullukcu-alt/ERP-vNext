using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;

public sealed record GetOfferReadinessByIdQuery(Guid Id) : IRequest<Response<OfferReadinessDto>>;
