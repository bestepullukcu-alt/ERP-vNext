using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;

internal static class TenantSubscriptionLifecycle
{
    public static bool CanActivate(TenantSubscriptionStatus status) =>
        status is TenantSubscriptionStatus.PendingProvisioning or TenantSubscriptionStatus.Trialing;

    public static bool CanRenew(TenantSubscriptionStatus status) =>
        status == TenantSubscriptionStatus.Active;

    public static bool CanCancel(TenantSubscriptionStatus status) =>
        status is TenantSubscriptionStatus.Active or TenantSubscriptionStatus.Trialing;

    public static bool CanExpire(TenantSubscriptionStatus status) =>
        status is TenantSubscriptionStatus.Active or TenantSubscriptionStatus.Trialing or TenantSubscriptionStatus.PastDue;

    public static bool CanSuspend(TenantSubscriptionStatus status) =>
        status == TenantSubscriptionStatus.Active;

    public static bool CanReactivate(TenantSubscriptionStatus status) =>
        status == TenantSubscriptionStatus.Suspended;

    public static void AddHistory(TenantSubscription subscription, string action, string? reason, string? actor, DateTimeOffset now)
    {
        subscription.History.Add(new TenantSubscriptionHistoryEntry
        {
            Status = subscription.Status,
            Action = action,
            Reason = reason,
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
            ChangedAtUtc = now,
            CurrentPeriodStartUtc = subscription.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc = subscription.CurrentPeriodEndUtc
        });
    }
}
