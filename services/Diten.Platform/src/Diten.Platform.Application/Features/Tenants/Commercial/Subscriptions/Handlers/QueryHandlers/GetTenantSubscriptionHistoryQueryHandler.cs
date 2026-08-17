using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Handlers.QueryHandlers;

public sealed class GetTenantSubscriptionHistoryQueryHandler
    : IRequestHandler<GetTenantSubscriptionHistoryQuery, Response<IReadOnlyList<TenantSubscriptionHistoryDto>>>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantSubscriptionHistoryQueryHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<IReadOnlyList<TenantSubscriptionHistoryDto>>> Handle(GetTenantSubscriptionHistoryQuery request, CancellationToken ct)
    {
        var subscriptions = await _subscriptionRepository.GetHistoryByTenantIdAsync(request.TenantId, ct);
        var rows = new List<TenantSubscriptionHistoryDto>();

        foreach (var subscription in subscriptions)
        {
            var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
            if (subscription.History.Count == 0)
            {
                rows.Add(new TenantSubscriptionHistoryDto(
                    subscription.Id,
                    subscription.Status,
                    plan?.Code,
                    plan?.Name,
                    subscription.CurrentPeriodStartUtc,
                    subscription.CurrentPeriodEndUtc,
                    subscription.CreatedAt,
                    subscription.CreatedBy,
                    subscription.CancellationReason,
                    "created"));
                continue;
            }

            rows.AddRange(subscription.History.Select(entry => new TenantSubscriptionHistoryDto(
                subscription.Id,
                entry.Status,
                plan?.Code,
                plan?.Name,
                entry.CurrentPeriodStartUtc,
                entry.CurrentPeriodEndUtc,
                entry.ChangedAtUtc,
                entry.Actor ?? "system",
                entry.Reason,
                entry.Action)));
        }

        return Response<IReadOnlyList<TenantSubscriptionHistoryDto>>.Success(
            rows.OrderByDescending(x => x.ChangedAtUtc).ToList());
    }
}
