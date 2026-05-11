using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.QueryHandlers;

public sealed class GetTenantCommercialSubscriptionQueryHandler
    : IRequestHandler<GetTenantCommercialSubscriptionQuery, Response<TenantSubscriptionDto>>
{
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantCommercialSubscriptionQueryHandler(
        ITenantRegistryRepository tenantRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<TenantSubscriptionDto>> Handle(GetTenantCommercialSubscriptionQuery request, CancellationToken ct)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, ct);
        if (tenant == null)
        {
            return Response<TenantSubscriptionDto>.Fail("Tenant not found.", 404);
        }

        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(request.TenantId, ct);
        if (subscription == null)
        {
            return Response<TenantSubscriptionDto>.Fail("No active subscription found.", 404);
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        return Response<TenantSubscriptionDto>.Success(TenantSubscriptionMapper.ToDto(subscription, plan));
    }
}
