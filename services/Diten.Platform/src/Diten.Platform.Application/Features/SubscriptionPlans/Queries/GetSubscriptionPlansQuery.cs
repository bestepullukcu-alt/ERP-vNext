using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Queries;

public sealed record GetSubscriptionPlansQuery(SubscriptionPlanFilterRequest Filter)
    : IRequest<Response<PagedResult<SubscriptionPlanListItemDto>>>;

