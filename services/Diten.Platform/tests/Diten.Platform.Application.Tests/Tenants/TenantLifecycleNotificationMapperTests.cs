using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class TenantLifecycleNotificationMapperTests
{
    [Fact]
    public void TenantLifecycleNotificationMappers_AreRegistered()
    {
        var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider();

        // BL-454 slice 2 stage D — TenantCreatedV1 has no mail mapper: the created event INVITES the initial administrator
        // (TenantLifecycleNotificationConsumer → IAdminUserInvitationService), it does not send a template by key.
        Assert.Null(provider.GetService<INotificationEventMapper<TenantCreatedV1>>());
        Assert.IsType<TenantSuspendedV1NotificationMapper>(
            provider.GetRequiredService<INotificationEventMapper<TenantSuspendedV1>>());
        Assert.IsType<TenantReactivatedV1NotificationMapper>(
            provider.GetRequiredService<INotificationEventMapper<TenantReactivatedV1>>());
    }

    [Fact]
    public void TenantSuspendedMapper_MapsResolvedTenantAdminRecipients()
    {
        var tenantId = Guid.NewGuid();
        var suspendedAt = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            TenantSuspendedV1.Name,
            new TenantSuspendedV1(tenantId, suspendedAt, "billing hold", Guid.NewGuid()),
            tenantId,
            Guid.NewGuid());

        var result = new TenantSuspendedV1NotificationMapper().Map(
            envelope,
            [new("owner@example.com", "Owner")],
            "tr-TR");

        Assert.NotNull(result);
        Assert.Equal("tenant.suspended.email", result!.TemplateKey);
        Assert.Equal("tr-TR", result.Locale);
        Assert.Equal(tenantId, result.Variables["TenantId"]);
        Assert.Equal("billing hold", result.Variables["Reason"]);
        Assert.Equal(suspendedAt, result.Variables["SuspendedAtUtc"]);
        Assert.Single(result.To);
        Assert.Equal("tenant.suspended.email", TenantSuspendedV1NotificationMapper.TemplateKey);
    }

    [Fact]
    public void TenantReactivatedMapper_MapsResolvedTenantAdminRecipients()
    {
        var tenantId = Guid.NewGuid();
        var reactivatedAt = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            TenantReactivatedV1.Name,
            new TenantReactivatedV1(tenantId, reactivatedAt, Guid.NewGuid()),
            tenantId,
            Guid.NewGuid());

        var result = new TenantReactivatedV1NotificationMapper().Map(
            envelope,
            [new("owner@example.com", "Owner")],
            null);

        Assert.NotNull(result);
        Assert.Equal("tenant.reactivated.email", result!.TemplateKey);
        Assert.Equal("en-US", result.Locale);
        Assert.Equal(tenantId, result.Variables["TenantId"]);
        Assert.Equal(reactivatedAt, result.Variables["ReactivatedAtUtc"]);
        Assert.Single(result.To);
        Assert.Equal("tenant.reactivated.email", TenantReactivatedV1NotificationMapper.TemplateKey);
    }

    private static EventEnvelope<TEvent> CreateEnvelope<TEvent>(
        string eventName,
        TEvent payload,
        Guid tenantId,
        Guid correlationId,
        Guid? causationId = null)
        where TEvent : IIntegrationEvent
    {
        return new EventEnvelope<TEvent>(
            new EventMetadata(
                Guid.NewGuid(),
                eventName,
                1,
                correlationId,
                causationId ?? Guid.NewGuid(),
                tenantId,
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow),
            payload);
    }
}
