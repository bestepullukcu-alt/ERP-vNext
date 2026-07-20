using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Quotas;

public sealed class QuotaServiceTests
{
    [Fact]
    public async Task TryConsumeAsync_WhenSubscriptionSuspended_ReturnsSubscriptionInactive()
    {
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var fixture = CreateFixture();
        fixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscription
            {
                TenantId = tenantId,
                PlanId = planId,
                Status = TenantSubscriptionStatus.Suspended
            });

        var response = await fixture.Service.TryConsumeAsync(new TryConsumeQuotaRequest(
            tenantId,
            QuotaKeys.UsersMax,
            1,
            "UserCreate",
            "op-1",
            "user-1",
            "Create user.",
            "actor",
            "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Contains(QuotaErrorCodes.SubscriptionInactive, response.Errors);
        fixture.Usages.Verify(
            x => x.TryConsumeAtomicAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryConsumeAsync_WhenAtomicUpdateSucceeds_ReturnsUpdatedUsage()
    {
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var fixture = CreateEligibleFixture(tenantId, subscriptionId, planId);
        var usage = new QuotaUsage
        {
            TenantId = tenantId,
            QuotaKey = QuotaKeys.UsersMax,
            CurrentValue = 15,
            LimitValue = 15,
            PeriodStart = DateTimeOffset.UtcNow.AddDays(-1),
            PeriodEnd = DateTimeOffset.UtcNow.AddDays(29),
            SubscriptionId = subscriptionId,
            PlanId = planId
        };

        fixture.Usages
            .Setup(x => x.TryConsumeAtomicAsync(tenantId, QuotaKeys.UsersMax, 1, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaMutationResult(true, usage));
        fixture.Usages
            .Setup(x => x.MarkNotificationStateAsync(tenantId, QuotaKeys.UsersMax, true, true, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithNotificationFlags(usage));

        var response = await fixture.Service.TryConsumeAsync(new TryConsumeQuotaRequest(
            tenantId,
            QuotaKeys.UsersMax,
            1,
            "UserCreate",
            "op-2",
            "user-2",
            "Create user.",
            "actor",
            "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(15, response.Data!.CurrentValue);
        Assert.Equal(15, response.Data.LimitValue);
    }

    [Fact]
    public async Task TryConsumeAsync_WhenAtomicUpdateDoesNotMatch_ReturnsLimitExceeded()
    {
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var fixture = CreateEligibleFixture(tenantId, subscriptionId, planId);
        var usage = new QuotaUsage
        {
            TenantId = tenantId,
            QuotaKey = QuotaKeys.UsersMax,
            CurrentValue = 15,
            LimitValue = 15,
            PeriodStart = DateTimeOffset.UtcNow.AddDays(-1),
            PeriodEnd = DateTimeOffset.UtcNow.AddDays(29),
            SubscriptionId = subscriptionId,
            PlanId = planId
        };

        fixture.Usages
            .Setup(x => x.TryConsumeAtomicAsync(tenantId, QuotaKeys.UsersMax, 1, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaMutationResult(false, null));
        fixture.Usages
            .Setup(x => x.GetByTenantAndKeyAsync(tenantId, QuotaKeys.UsersMax, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        var response = await fixture.Service.TryConsumeAsync(new TryConsumeQuotaRequest(
            tenantId,
            QuotaKeys.UsersMax,
            1,
            "UserCreate",
            "op-3",
            "user-3",
            "Create user.",
            "actor",
            "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Contains(QuotaErrorCodes.LimitExceeded, response.Errors);
    }

    [Fact]
    public async Task RecalculateAsync_WhenApiCallCounterMissing_ReturnsNotSupported()
    {
        var tenantId = Guid.NewGuid();
        var fixture = CreateFixture();
        fixture.Usages
            .Setup(x => x.GetByTenantAndKeyAsync(tenantId, QuotaKeys.ApiCallsPerMonth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuotaUsage
            {
                TenantId = tenantId,
                QuotaKey = QuotaKeys.ApiCallsPerMonth,
                CurrentValue = 0,
                LimitValue = 100,
                PeriodStart = DateTimeOffset.UtcNow.AddDays(-1),
                PeriodEnd = DateTimeOffset.UtcNow.AddDays(29)
            });

        var response = await fixture.Service.RecalculateAsync(new RecalculateQuotaUsageRequest(
            tenantId,
            QuotaKeys.ApiCallsPerMonth,
            "ResetJob",
            "op-4",
            null,
            "Recalculate api calls.",
            "actor",
            "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Contains(QuotaErrorCodes.RecalculationNotSupported, response.Errors);
    }

    // FIX-QUOTA-PLAN-SYNC — assigning/changing a plan must re-sync stored quota LIMITS to the new plan (upsert
    // LimitValue) while PRESERVING CurrentValue. This is the path AssignPlanToTenantCommandHandler now calls
    // (previously it used initialize-once, which left an existing LimitValue frozen — Free=3 stayed 3 after upgrade).
    [Fact]
    public async Task SyncTenantQuotaLimitsAsync_ResyncsStoredLimitsToNewPlan_PreservingCurrentValue()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var fixture = CreateFixture();

        fixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscription { Id = subscriptionId, TenantId = tenantId, PlanId = planId, Status = TenantSubscriptionStatus.Active });

        // New Enterprise plan: modules.max jumps 3 → 100, users 5 → 50.
        fixture.Plans
            .Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan
            {
                Id = planId,
                Code = "ENT",
                Name = "Enterprise",
                DefaultQuotas = new Dictionary<string, decimal> { [QuotaKeys.ModulesMax] = 100, [QuotaKeys.UsersMax] = 50 }
            });

        // UpdateLimitAsync upserts the new limit and PRESERVES the existing usage (modules already at 2 used).
        var capturedLimits = new Dictionary<string, decimal>();
        fixture.Usages
            .Setup(x => x.UpdateLimitAsync(tenantId, It.IsAny<string>(), It.IsAny<decimal>(), subscriptionId, planId, It.IsAny<string>(), null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid t, string key, decimal limit, Guid sub, Guid plan, string src, string? ov, DateTimeOffset now, CancellationToken _) =>
            {
                capturedLimits[key] = limit;
                return new QuotaUsage
                {
                    TenantId = t,
                    QuotaKey = key,
                    CurrentValue = key == QuotaKeys.ModulesMax ? 2 : 0, // existing usage preserved, not reset
                    LimitValue = limit,
                    SubscriptionId = sub,
                    PlanId = plan,
                    PeriodStart = DateTimeOffset.UtcNow.AddDays(-1),
                    PeriodEnd = DateTimeOffset.UtcNow.AddDays(29)
                };
            });

        var response = await fixture.Service.SyncTenantQuotaLimitsAsync(
            tenantId, "SubscriptionActivation", "Plan assigned; limits synced.", "actor", "corr", CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(100, capturedLimits[QuotaKeys.ModulesMax]); // stored modules.max limit re-synced 3 → 100
        Assert.Equal(50, capturedLimits[QuotaKeys.UsersMax]);    // users.max limit updated to the new plan too
        var modules = Assert.Single(response.Data!, s => s.QuotaKey == QuotaKeys.ModulesMax);
        Assert.Equal(100, modules.LimitValue);
        Assert.Equal(2, modules.CurrentValue); // current usage preserved across the plan change
    }

    private static QuotaUsage WithNotificationFlags(QuotaUsage usage)
    {
        usage.WarningNotificationSentForPeriod = true;
        usage.LimitBreachNotificationSentForPeriod = true;
        usage.LastWarningNotifiedAtUtc = DateTimeOffset.UtcNow;
        usage.LastLimitBreachNotifiedAtUtc = DateTimeOffset.UtcNow;
        return usage;
    }

    private static QuotaFixture CreateEligibleFixture(Guid tenantId, Guid subscriptionId, Guid planId)
    {
        var fixture = CreateFixture();
        fixture.Subscriptions
            .Setup(x => x.GetCurrentByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscription
            {
                Id = subscriptionId,
                TenantId = tenantId,
                PlanId = planId,
                Status = TenantSubscriptionStatus.Active
            });
        fixture.Plans
            .Setup(x => x.GetByIdAsync(planId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPlan
            {
                Id = planId,
                Code = "PRO",
                Name = "Pro",
                DefaultQuotas = new Dictionary<string, decimal>
                {
                    [QuotaKeys.UsersMax] = 15
                }
            });
        return fixture;
    }

    private static QuotaFixture CreateFixture()
    {
        var usages = new Mock<IQuotaUsageRepository>(MockBehavior.Strict);
        var events = new Mock<IQuotaEventRepository>(MockBehavior.Strict);
        var subscriptions = new Mock<ITenantSubscriptionRepository>(MockBehavior.Strict);
        var plans = new Mock<ISubscriptionPlanRepository>(MockBehavior.Strict);
        var tenants = new Mock<ITenantRegistryRepository>(MockBehavior.Strict);
        var entitlements = new Mock<ITenantModuleEntitlementRepository>(MockBehavior.Strict);

        events
            .Setup(x => x.CreateAsync(It.IsAny<QuotaEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuotaEvent quotaEvent, CancellationToken _) => quotaEvent);
        events
            .Setup(x => x.ExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        return new QuotaFixture(
            usages,
            events,
            subscriptions,
            plans,
            tenants,
            entitlements,
            new QuotaService(
                usages.Object,
                events.Object,
                subscriptions.Object,
                plans.Object,
                tenants.Object,
                entitlements.Object,
                NullLogger<QuotaService>.Instance));
    }

    private sealed record QuotaFixture(
        Mock<IQuotaUsageRepository> Usages,
        Mock<IQuotaEventRepository> Events,
        Mock<ITenantSubscriptionRepository> Subscriptions,
        Mock<ISubscriptionPlanRepository> Plans,
        Mock<ITenantRegistryRepository> Tenants,
        Mock<ITenantModuleEntitlementRepository> Entitlements,
        QuotaService Service);
}
