using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Commands;

public sealed record CreateBillingPlanCommand(CreateBillingPlanRequest Request) : IRequest<Response<BillingPlanDto>>;
