using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;

public sealed record GetOfferReadinessListQuery : IRequest<Response<IReadOnlyList<OfferReadinessListItemDto>>>;
