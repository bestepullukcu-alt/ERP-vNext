using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Commands;

public sealed record CreateOfferReadinessCommand(OfferReadinessCreateRequest Request) : IRequest<Response<Guid>>;
