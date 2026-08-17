using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Commands;

public sealed record SeedDefaultSubscriptionPlansCommand() : IRequest<Response<NoContent>>;

