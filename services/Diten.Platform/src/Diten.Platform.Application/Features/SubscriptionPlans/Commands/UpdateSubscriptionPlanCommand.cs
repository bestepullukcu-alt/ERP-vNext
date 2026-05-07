using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record UpdateSubscriptionPlanCommand(Guid Id, UpdateSubscriptionPlanRequest Request) : IRequest<Response<NoContent>>;

