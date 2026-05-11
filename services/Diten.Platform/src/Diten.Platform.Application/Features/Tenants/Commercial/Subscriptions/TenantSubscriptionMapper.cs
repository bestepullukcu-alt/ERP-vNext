using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;

internal static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(TenantSubscription subscription, SubscriptionPlan? plan)
    {
        return new TenantSubscriptionDto(
            subscription.Id,
            subscription.TenantId,
            subscription.PlanId,
            plan?.Code,
            plan?.Name,
            subscription.Status,
            subscription.TrialStartDateUtc,
            subscription.TrialEndDateUtc,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.ActivatedAtUtc,
            subscription.RenewedAtUtc,
            subscription.CancelledAtUtc,
            subscription.ExpiredAtUtc,
            subscription.SuspendedAtUtc,
            subscription.CancelAtPeriodEnd,
            subscription.CancellationReason,
            subscription.Source,
            subscription.RowVersion);
    }
}
