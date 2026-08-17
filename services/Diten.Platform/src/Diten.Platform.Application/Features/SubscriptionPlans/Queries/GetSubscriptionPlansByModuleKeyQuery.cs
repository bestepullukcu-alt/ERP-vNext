using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Queries;

public sealed record GetSubscriptionPlansByModuleKeyQuery(string ModuleKey)
    : IRequest<Response<IReadOnlyList<SubscriptionPlanListItemDto>>>;
