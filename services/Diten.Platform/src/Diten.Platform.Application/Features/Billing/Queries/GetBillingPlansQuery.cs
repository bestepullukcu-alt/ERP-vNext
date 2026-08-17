using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Billing.Queries;

public sealed record GetBillingPlansQuery() : IRequest<Response<IReadOnlyList<BillingPlanDto>>>;
