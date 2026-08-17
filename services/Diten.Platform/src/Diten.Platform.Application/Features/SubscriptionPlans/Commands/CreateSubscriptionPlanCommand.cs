using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record CreateSubscriptionPlanCommand(CreateSubscriptionPlanRequest Request) : IRequest<Response<Guid>>;

