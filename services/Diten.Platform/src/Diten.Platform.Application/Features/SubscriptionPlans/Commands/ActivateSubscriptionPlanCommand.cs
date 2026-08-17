using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record ActivateSubscriptionPlanCommand(Guid Id) : IRequest<Response<NoContent>>;

