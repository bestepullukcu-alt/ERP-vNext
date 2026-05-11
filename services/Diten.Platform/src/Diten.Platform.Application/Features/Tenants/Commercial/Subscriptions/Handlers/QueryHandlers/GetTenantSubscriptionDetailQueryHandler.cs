using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.QueryHandlers;

public sealed class GetTenantSubscriptionDetailQueryHandler
    : IRequestHandler<GetTenantSubscriptionDetailQuery, Response<TenantSubscriptionDto>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantSubscriptionDetailQueryHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<TenantSubscriptionDto>> Handle(GetTenantSubscriptionDetailQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByTenantIdAsync(request.TenantId, request.SubscriptionId, ct);
        if (subscription == null)
        {
            return Response<TenantSubscriptionDto>.Fail("Tenant subscription not found.", 404);
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        return Response<TenantSubscriptionDto>.Success(TenantSubscriptionMapper.ToDto(subscription, plan));
    }
}
