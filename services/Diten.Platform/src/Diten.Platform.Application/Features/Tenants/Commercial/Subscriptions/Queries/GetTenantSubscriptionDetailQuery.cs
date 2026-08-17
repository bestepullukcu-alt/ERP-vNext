using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;

public sealed record GetTenantSubscriptionDetailQuery(Guid TenantId, Guid SubscriptionId)
    : IRequest<Response<TenantSubscriptionDto>>;
