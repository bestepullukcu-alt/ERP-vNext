using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record ReviseBillingPlanCommand(Guid PlanId, ReviseBillingPlanRequest Request) : IRequest<Response<BillingPlanDto>>;
