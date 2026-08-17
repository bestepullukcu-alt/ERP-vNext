using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record ActivateBillingPlanCommand(Guid PlanId) : IRequest<Response<BillingPlanDto>>;
