using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionPlans.Queries;

public sealed record GetSubscriptionPlanSummaryQuery() : IRequest<Response<SubscriptionPlanSummaryDto>>;

