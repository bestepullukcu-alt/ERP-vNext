using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.QueryHandlers;

public sealed class HasTenantActiveSubscriptionQueryHandler
    : IRequestHandler<HasTenantActiveSubscriptionQuery, Response<bool>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;

    public HasTenantActiveSubscriptionQueryHandler(ITenantSubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<Response<bool>> Handle(HasTenantActiveSubscriptionQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(request.TenantId, ct);
        return Response<bool>.Success(subscription?.Status == TenantSubscriptionStatus.Active);
    }
}
