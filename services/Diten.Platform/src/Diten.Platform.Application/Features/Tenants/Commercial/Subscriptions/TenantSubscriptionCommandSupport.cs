using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;

internal static class TenantSubscriptionCommandSupport
{
    public static async Task<Response<Guid>> CreateAsync(
        Guid tenantId,
        Guid planId,
        bool isTrial,
        DateTimeOffset? trialEndDateUtc,
        DateTimeOffset? currentPeriodStartUtc,
        DateTimeOffset? currentPeriodEndUtc,
        string? source,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserContext currentUser,
        TenantSubscriptionTransactionWriter writer,
        Func<IPlatformTransactionSession, TenantSubscription, SubscriptionPlan, CancellationToken, Task<Response<NoContent>>>? participant,
        string mutation,
        AuditOperation operation,
        CancellationToken ct)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant == null)
        {
            return Response<Guid>.Fail("Tenant not found.", 404);
        }

        var plan = await planRepository.GetByIdAsync(planId, ct);
        if (plan == null || !plan.IsActive)
        {
            return Response<Guid>.Fail("Selected subscription plan was not found or is inactive.", 404);
        }

        if (await subscriptionRepository.HasCurrentAsync(tenantId, null, ct))
        {
            return Response<Guid>.Fail("Tenant already has a current subscription.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var subscription = new TenantSubscription
        {
            TenantId = tenantId,
            PlanId = plan.Id,
            Source = string.IsNullOrWhiteSpace(source) ? "platform-admin" : source.Trim(),
            Status = isTrial ? TenantSubscriptionStatus.Trialing : TenantSubscriptionStatus.Active,
            TrialStartDateUtc = isTrial ? now : null,
            TrialEndDateUtc = isTrial
                ? ResolveTrialEnd(plan, trialEndDateUtc, now)
                : null,
            CurrentPeriodStartUtc = isTrial ? null : currentPeriodStartUtc ?? now,
            CurrentPeriodEndUtc = isTrial ? null : currentPeriodEndUtc,
            ActivatedAtUtc = isTrial ? null : now,
            CreatedBy = currentUser.ActorName,
            UpdatedAt = now,
            UpdatedBy = currentUser.ActorName
        };

        if (subscription.Status == TenantSubscriptionStatus.Trialing && subscription.TrialEndDateUtc <= now)
        {
            return Response<Guid>.Fail("Trial end date must be in the future.", 400);
        }

        if (subscription.Status == TenantSubscriptionStatus.Active &&
            (!subscription.CurrentPeriodEndUtc.HasValue || subscription.CurrentPeriodEndUtc <= subscription.CurrentPeriodStartUtc))
        {
            return Response<Guid>.Fail("Current period end date must be after the start date.", 400);
        }

        TenantSubscriptionLifecycle.AddHistory(subscription, isTrial ? "trial_started" : "plan_assigned", null, currentUser.ActorName, now);
        return await writer.CreateAsync(subscription, tenant, plan, mutation, operation, participant, ct);
    }

    public static async Task<Response<NoContent>> UpdateTenantSnapshotAsync(
        TenantSubscription subscription,
        ITenantRegistryRepository tenantRepository,
        ISubscriptionPlanRepository planRepository,
        ICurrentUserContext currentUser,
        CancellationToken ct,
        bool markTenantActive = false)
    {
        var tenant = await tenantRepository.GetByIdAsync(subscription.TenantId, ct);
        if (tenant == null)
        {
            return Response<NoContent>.Fail("Tenant not found.", 404);
        }

        var plan = await planRepository.GetByIdAsync(subscription.PlanId, ct);
        var now = DateTimeOffset.UtcNow;
        tenant.PlanId = subscription.PlanId;
        tenant.PlanCode = plan?.Code;
        tenant.PlanName = plan?.Name;
        tenant.SubscriptionStatus = subscription.Status;
        tenant.TrialStartDateUtc = subscription.TrialStartDateUtc;
        tenant.TrialEndDateUtc = subscription.TrialEndDateUtc;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = currentUser.ActorName;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = $"tenant_subscription.{subscription.Status.ToString().ToLowerInvariant()}",
            Message = $"Tenant subscription moved to {subscription.Status}.",
            Actor = currentUser.ActorName,
            At = now
        });

        // Go-live: subscription activation is the operator's deliberate go-live action, so finalize tenant
        // provisioning here. Flag-gated — only ActivateTenantSubscription passes true; cancel/suspend/renew/
        // expire/reactivate snapshot updates leave the tenant lifecycle Status untouched. Idempotent: only
        // promotes a still-Provisioning tenant (never demotes an Active one nor resurrects a Suspended/Deactivated one).
        if (markTenantActive)
        {
            if (tenant.Status == TenantStatus.Provisioning)
            {
                tenant.Status = TenantStatus.Active;
                tenant.ActivatedAt ??= now;
                tenant.ActivityTimeline.Add(new TenantActivityEvent
                {
                    EventType = "tenant.activated",
                    Message = "Tenant activated on subscription activation.",
                    Actor = currentUser.ActorName,
                    At = now
                });
            }

            tenant.ProvisionedAt ??= now;
            foreach (var step in tenant.ProvisioningSteps)
            {
                if (string.Equals(step.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(step.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    step.Status = "Completed";
                    step.CompletedAt ??= now;
                }
            }

            tenant.ProvisioningStatus = "Completed";
        }

        await tenantRepository.UpdateAsync(tenant, ct);
        return Response<NoContent>.Success(204);
    }

    private static DateTimeOffset? ResolveTrialEnd(SubscriptionPlan plan, DateTimeOffset? requested, DateTimeOffset now)
    {
        if (requested.HasValue)
        {
            return requested.Value;
        }

        return plan.TrialDurationDays is > 0
            ? now.AddDays(plan.TrialDurationDays.Value)
            : null;
    }
}
