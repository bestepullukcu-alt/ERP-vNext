using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Contracts.Events;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class EntitlementCacheInvalidationEventContractTests
{
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CorrelationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset OccurredAtUtc = DateTimeOffset.Parse("2026-05-20T10:15:00Z");

    public static TheoryData<IIntegrationEvent, string> EntitlementInvalidationEvents =>
        new()
        {
            { new TenantEntitlementAddedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, "hr"), TenantEntitlementAddedV1.Name },
            { new TenantEntitlementEnabledV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, "hr"), TenantEntitlementEnabledV1.Name },
            { new TenantEntitlementDisabledV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, "hr"), TenantEntitlementDisabledV1.Name },
            { new TenantEntitlementExpiryUpdatedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, "hr"), TenantEntitlementExpiryUpdatedV1.Name },
            { new TenantEntitlementOverrideRemovedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, "hr"), TenantEntitlementOverrideRemovedV1.Name },
            { new TenantSubscriptionChangedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, null, null, null, null), TenantSubscriptionChangedV1.Name }
        };

    public static TheoryData<Func<string, IIntegrationEvent>> EntitlementModuleEventFactories =>
        new()
        {
            moduleCode => new TenantEntitlementAddedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, moduleCode),
            moduleCode => new TenantEntitlementEnabledV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, moduleCode),
            moduleCode => new TenantEntitlementDisabledV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, moduleCode),
            moduleCode => new TenantEntitlementExpiryUpdatedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, moduleCode),
            moduleCode => new TenantEntitlementOverrideRemovedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, moduleCode)
        };

    [Theory]
    [MemberData(nameof(EntitlementInvalidationEvents))]
    public void EntitlementInvalidationEvents_expose_expected_name_and_version(IIntegrationEvent @event, string expectedName)
    {
        Assert.True(EventName.IsValid(@event.EventName));
        Assert.Equal(expectedName, @event.EventName);
        Assert.Equal(1, @event.EventVersion);
        EventName.EnsureMatchesVersion(@event.EventName, @event.EventVersion);
        Assert.True(@event is IInternalEvent);
    }

    [Fact]
    public void EntitlementInvalidationEvents_use_exact_event_names()
    {
        Assert.Equal("tenant.entitlement.added.v1", TenantEntitlementAddedV1.Name);
        Assert.Equal("tenant.entitlement.enabled.v1", TenantEntitlementEnabledV1.Name);
        Assert.Equal("tenant.entitlement.disabled.v1", TenantEntitlementDisabledV1.Name);
        Assert.Equal("tenant.entitlement.expiryupdated.v1", TenantEntitlementExpiryUpdatedV1.Name);
        Assert.Equal("tenant.entitlement.overrideremoved.v1", TenantEntitlementOverrideRemovedV1.Name);
        Assert.Equal("tenant.subscription.changed.v1", TenantSubscriptionChangedV1.Name);
    }

    [Fact]
    public void EntitlementInvalidationEvents_reject_empty_common_required_fields()
    {
        Assert.Throws<ArgumentException>(() => new TenantEntitlementAddedV1(Guid.Empty, OccurredAtUtc, TenantId, CorrelationId, ActorId, "HR"));
        Assert.Throws<ArgumentException>(() => new TenantEntitlementAddedV1(EventId, OccurredAtUtc, Guid.Empty, CorrelationId, ActorId, "HR"));
        Assert.Throws<ArgumentException>(() => new TenantEntitlementAddedV1(EventId, OccurredAtUtc, TenantId, Guid.Empty, ActorId, "HR"));
    }

    [Fact]
    public void EntitlementInvalidationEvents_reject_default_or_non_utc_occurred_at()
    {
        Assert.Throws<ArgumentException>(() => new TenantEntitlementAddedV1(EventId, default, TenantId, CorrelationId, ActorId, "HR"));
        Assert.Throws<ArgumentException>(() => new TenantEntitlementAddedV1(EventId, DateTimeOffset.Now, TenantId, CorrelationId, ActorId, "HR"));
    }

    [Theory]
    [MemberData(nameof(EntitlementModuleEventFactories))]
    public void EntitlementEvents_reject_empty_module_code(Func<string, IIntegrationEvent> factory)
    {
        Assert.Throws<ArgumentException>(() => factory(string.Empty));
        Assert.Throws<ArgumentException>(() => factory("   "));
    }

    [Fact]
    public void EntitlementEvents_normalize_module_code()
    {
        var @event = new TenantEntitlementAddedV1(EventId, OccurredAtUtc, TenantId, CorrelationId, ActorId, " hr ");

        Assert.Equal("HR", @event.ModuleCode);
    }

    [Fact]
    public void TenantSubscriptionChangedV1_allows_optional_plan_and_status_fields()
    {
        var @event = new TenantSubscriptionChangedV1(
            EventId,
            OccurredAtUtc,
            TenantId,
            CorrelationId,
            actorId: null,
            previousPlanId: null,
            newPlanId: null,
            previousStatus: null,
            newStatus: " Active ");

        Assert.Null(@event.ActorId);
        Assert.Null(@event.PreviousPlanId);
        Assert.Null(@event.NewPlanId);
        Assert.Null(@event.PreviousStatus);
        Assert.Equal("Active", @event.NewStatus);
    }

    [Fact]
    public void TenantSubscriptionChangedV1_rejects_empty_common_required_fields()
    {
        Assert.Throws<ArgumentException>(() => new TenantSubscriptionChangedV1(Guid.Empty, OccurredAtUtc, TenantId, CorrelationId, ActorId, null, null, null, null));
        Assert.Throws<ArgumentException>(() => new TenantSubscriptionChangedV1(EventId, default, TenantId, CorrelationId, ActorId, null, null, null, null));
        Assert.Throws<ArgumentException>(() => new TenantSubscriptionChangedV1(EventId, OccurredAtUtc, Guid.Empty, CorrelationId, ActorId, null, null, null, null));
        Assert.Throws<ArgumentException>(() => new TenantSubscriptionChangedV1(EventId, OccurredAtUtc, TenantId, Guid.Empty, ActorId, null, null, null, null));
    }
}
