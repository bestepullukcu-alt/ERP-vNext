using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.QueryHandlers;

public sealed class GetTenantSubscriptionEntitlementSnapshotQueryHandler
    : IRequestHandler<GetTenantSubscriptionEntitlementSnapshotQuery, Response<TenantSubscriptionEntitlementSnapshotDto>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantSubscriptionEntitlementSnapshotQueryHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<TenantSubscriptionEntitlementSnapshotDto>> Handle(GetTenantSubscriptionEntitlementSnapshotQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(request.TenantId, ct);
        if (subscription == null)
        {
            return Response<TenantSubscriptionEntitlementSnapshotDto>.Success(new TenantSubscriptionEntitlementSnapshotDto(
                request.TenantId,
                false,
                null,
                null,
                null,
                null,
                null,
                new Dictionary<string, decimal>(),
                [],
                []));
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        var snapshot = new TenantSubscriptionEntitlementSnapshotDto(
            request.TenantId,
            subscription.Status == TenantSubscriptionStatus.Active || subscription.Status == TenantSubscriptionStatus.Trialing,
            subscription.PlanId,
            plan?.Code,
            plan?.Name,
            subscription.Status,
            subscription.CurrentPeriodEndUtc,
            plan?.DefaultQuotas ?? new Dictionary<string, decimal>(),
            plan?.IncludedFeatures ?? [],
            plan?.IncludedModuleKeys ?? []);

        return Response<TenantSubscriptionEntitlementSnapshotDto>.Success(snapshot);
    }
}
