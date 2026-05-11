using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;

public sealed record GetTenantCommercialSubscriptionQuery(Guid TenantId)
    : IRequest<Response<TenantSubscriptionDto>>;
